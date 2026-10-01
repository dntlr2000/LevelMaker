// Copy into a dedicated validation project's Editor folder; never installs itself.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RogueDungeonLab;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArenaStairDirectionsUnityValidation
{
    private const string Scene="Assets/__ArenaStairValidation/StairDirections.unity",ReloadKey="ArenaStairValidation.Reload";
    private static int errors;
    private static string Evidence { get { var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-arenaEvidence");return i>=0&&i+1<a.Length?a[i+1]:Path.GetFullPath("Logs/ArenaStairs"); } }
    [Serializable] private sealed class SeedResult { public int seed;public string hash;public FpsArenaStair[] stairs;public FpsArenaRoomReport[] rooms;public FpsArenaStairDirectionReport[] directions; }
    [Serializable] private sealed class Results { public List<SeedResult> seeds=new List<SeedResult>(); }
    [Serializable] private sealed class Survey { public int seeds=64,stairs;public int[] directionHistogram=new int[12];public int limitedFloorReports;public bool allFourDirectionsOnEveryFloor; }
    private static FpsArenaRecipe Recipe(int floors=4)
    { var r=FpsArenaRecipe.CreateFlexible();r.width=r.depth=32;r.floors=floors;r.partitionRooms=true;r.roomsPerFloor=4;r.spacingCells=1;return r; }
    public static void BuildAndRender()
    {
        Directory.CreateDirectory(Evidence);ShaderUtil.allowAsyncCompilation=false;var results=new Results();
        foreach(int seed in new[]{0,1,71,73125})
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var g=new GameObject("시드 방향 계단 검증").AddComponent<FpsArenaGenerator>();g.recipe=Recipe(3);g.seed=seed;g.generateOnPlay=false;
            FpsArenaWindow.GenerateWithUndo(g);string hash=g.CurrentLayout.Hash;FpsArenaWindow.GenerateWithUndo(g);if(hash!=g.CurrentLayout.Hash||g.transform.childCount!=1)throw new InvalidOperationException("Seed repeat/root failed");
            var sun=new GameObject("Directional Light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
            var l=g.CurrentLayout;results.seeds.Add(new SeedResult{seed=seed,hash=l.Hash,stairs=l.Stairs.ToArray(),rooms=l.RoomReports.ToArray(),directions=l.StairDirectionReports.ToArray()});
            // Save full geometry before selectively hiding upper floors for captures.
            if(seed==73125){EditorSceneManager.SaveScene(g.gameObject.scene,Scene);File.WriteAllText(Evidence+"/SavedHash.txt",hash);}
            Render(g,"Seed"+seed+"_Overview",new Vector3(85,105,-95),Vector3.up*4,false);
            var first=l.Stairs.First(s=>s.lowerFloor==0);var start=l.Position(first.BottomLane(0));
            for(int floor=1;floor<3;floor++)g.GeneratedRoot.transform.Find("층_"+(floor+1)).gameObject.SetActive(false);
            foreach(var wall in l.RoomWalls.Where(w=>w.cell.y>0))g.GeneratedRoot.transform.Find(wall.id).gameObject.SetActive(false);
            foreach(var p in l.Content.Where(p=>p.cell.y>0))g.GeneratedRoot.transform.Find(p.id).gameObject.SetActive(false);
            foreach(var stair in l.Stairs.Where(s=>s.lowerFloor>0))g.GeneratedRoot.transform.Find("계단_"+stair.lowerFloor+"_"+stair.x+"_"+stair.z).gameObject.SetActive(false);
            var overlay=new GameObject("Validation direction arrows");
            foreach(var stair in l.Stairs.Where(s=>s.lowerFloor==0))
            {
                var line=new GameObject("Ascent axis").AddComponent<LineRenderer>();line.transform.SetParent(overlay.transform,false);line.material=new Material(Shader.Find("Sprites/Default"));line.widthMultiplier=.25f;line.startColor=line.endColor=Color.cyan;
                var a=l.Position(stair.BottomLane(0))+Vector3.up*8;var b=l.Position(stair.TopLane(0));b.y=a.y;var f=(Vector3)stair.Forward;var right=(Vector3)stair.LaneStep;
                line.positionCount=5;line.SetPositions(new[]{a,b,b-f*2+right,b,b-f*2-right});
            }
            Render(g,"Seed"+seed+"_LowerDirections",new Vector3(0,110,0),Vector3.zero,true);UnityEngine.Object.DestroyImmediate(overlay);
            Render(g,"Seed"+seed+"_Flight",start-(Vector3)first.Forward*7+(Vector3)first.LaneStep*2+Vector3.up*4.5f,l.Position(first.TopLane(0))+Vector3.up*.5f,false);
        }
        File.WriteAllText(Evidence+"/RenderedSeeds.json",JsonUtility.ToJson(results,true));
        var survey=new Survey();for(int seed=0;seed<64;seed++)
        {
            var l=FpsArenaPlanner.Generate(Recipe(),seed);foreach(var s in l.Stairs){survey.directionHistogram[s.lowerFloor*4+(int)s.direction]++;survey.stairs++;}
            survey.limitedFloorReports+=l.StairDirectionReports.Count(r=>r.validDirectionsMask!=15);
        }
        survey.allFourDirectionsOnEveryFloor=survey.directionHistogram.All(n=>n>0);if(!survey.allFourDirectionsOnEveryFloor)throw new InvalidOperationException("No full seed direction diversity");
        File.WriteAllText(Evidence+"/DirectionSurvey.json",JsonUtility.ToJson(survey,true));ReopenCheck();Debug.Log("STAIR_RENDER_COMPLETE");
    }
    public static void ReopenCheck()
    {
        EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);var g=AssertScene();var l=g.CurrentLayout;
        File.WriteAllText(Evidence+"/Reopen.json",JsonUtility.ToJson(new SeedResult{seed=l.Seed,hash=l.Hash,stairs=l.Stairs.ToArray(),rooms=l.RoomReports.ToArray(),directions=l.StairDirectionReports.ToArray()},true));
    }
    private static FpsArenaGenerator AssertScene()
    { var g=UnityEngine.Object.FindFirstObjectByType<FpsArenaGenerator>();if(g==null||g.CurrentLayout==null||g.CurrentLayout.Hash!=File.ReadAllText(Evidence+"/SavedHash.txt")||g.transform.childCount!=1||!g.CurrentLayout.Recipe.randomizeStairDirections)throw new InvalidOperationException("Directional built snapshot did not restore");g.CurrentLayout.Validate();Debug.Log("STAIR_RELOAD_STATE: "+SessionState.GetString(ReloadKey,""));return g; }
    private static void WatchErrors(){Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;};}
    [InitializeOnLoadMethod] private static void AfterReload(){if(SessionState.GetString(ReloadKey,"")=="")return;WatchErrors();EditorApplication.playModeStateChanged+=OnPlayState;EditorApplication.delayCall+=ContinueReload;}
    public static void CheckPlayDomainReload(){ReopenCheck();errors=0;WatchErrors();SessionState.SetString(ReloadKey,"entering");EditorApplication.playModeStateChanged+=OnPlayState;EditorApplication.EnterPlaymode();}
    private static void OnPlayState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetString(ReloadKey,"")=="entering"){AssertScene();SessionState.SetString(ReloadKey,"reloading");EditorApplication.delayCall+=()=>EditorUtility.RequestScriptReload();}
        if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetString(ReloadKey,"")=="exiting"){AssertScene();SessionState.EraseString(ReloadKey);File.WriteAllText(Evidence+"/PlayDomainReload.json","{\"playScriptReload\":true,\"returnedToEdit\":true,\"errors\":"+errors+"}");EditorApplication.Exit(errors==0?0:1);}
    }
    private static void ContinueReload(){if(SessionState.GetString(ReloadKey,"")=="reloading"&&EditorApplication.isPlaying){AssertScene();SessionState.SetString(ReloadKey,"exiting");EditorApplication.ExitPlaymode();}}
    public static void CheckEditorUi()
    {
        ReopenCheck();errors=0;WatchErrors();var window=EditorWindow.GetWindow<FpsArenaWindow>("FPS 아레나");
        typeof(FpsArenaWindow).GetField("target",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,UnityEngine.Object.FindFirstObjectByType<FpsArenaGenerator>());window.Show();int frames=0,tab=0;
        EditorApplication.CallbackFunction tick=null;tick=()=>{window.Repaint();if(++frames%45!=0)return;window.SendEvent(new Event{type=EventType.ExecuteCommand,commandName="ArenaStairValidation"});Debug.Log("STAIR_GUI_TAB: "+tab);
            if(++tab<6)typeof(FpsArenaWindow).GetField("selectedTab",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,tab);
            else{EditorApplication.update-=tick;window.Close();File.WriteAllText(Evidence+"/EditorUi.json","{\"tabs\":6,\"errors\":"+errors+",\"desktopScreenshot\":false}");EditorApplication.Exit(errors==0?0:1);}};EditorApplication.update+=tick;
    }
    private static void Render(FpsArenaGenerator g,string name,Vector3 position,Vector3 target,bool ortho)
    {
        var camera=new GameObject("Validation camera").AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target,ortho?Vector3.forward:Vector3.up);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.08f,.11f);camera.farClipPlane=600;camera.orthographic=ortho;camera.orthographicSize=g.CurrentLayout.Recipe.width*g.CurrentLayout.Recipe.cellSize*.54f;
        var rt=new RenderTexture(1600,1200,24);rt.Create();camera.targetTexture=rt;var previous=RenderTexture.active;
        try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();File.WriteAllBytes(Evidence+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
        finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
}
