// Validation helper. Copy into a dedicated project's Assets/__ArenaRoomValidation/Editor.
// Unity -executeMethod ArenaRoomsUnityValidation.BuildAndRender -arenaEvidence <folder>
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RogueDungeonLab;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArenaRoomsUnityValidation
{
    private const string Folder="Assets/FpsArenaRoomsExample", ReloadKey="ArenaRoomValidation.ReloadStage";
    private static int errors;
    private static string Evidence { get { var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-arenaEvidence");return i>=0&&i+1<args.Length?args[i+1]:Path.GetFullPath("Logs/ArenaRooms"); } }
    [InitializeOnLoadMethod] private static void AfterReload()
    {
        if(SessionState.GetString(ReloadKey,"")=="")return;
        WatchErrors();EditorApplication.playModeStateChanged+=OnPlayState;EditorApplication.delayCall+=ContinueReload;
    }
    private static void WatchErrors(){Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;};}
    public static void CheckPlayDomainReload()
    { ReopenCheck();errors=0;SessionState.SetString(ReloadKey,"entering");WatchErrors();EditorApplication.playModeStateChanged+=OnPlayState;EditorApplication.EnterPlaymode(); }
    private static void OnPlayState(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetString(ReloadKey,"")=="entering")
        { AssertScene();SessionState.SetString(ReloadKey,"reloading");EditorApplication.delayCall+=()=>EditorUtility.RequestScriptReload(); }
        if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetString(ReloadKey,"")=="exiting")
        { AssertScene();SessionState.EraseString(ReloadKey);File.WriteAllText(Evidence+"/PlayDomainReload.json","{\"playScriptReload\":true,\"returnedToEdit\":true,\"errors\":"+errors+"}");EditorApplication.Exit(errors==0?0:1); }
    }
    private static void ContinueReload(){if(SessionState.GetString(ReloadKey,"")=="reloading"&&EditorApplication.isPlaying){AssertScene();SessionState.SetString(ReloadKey,"exiting");EditorApplication.ExitPlaymode();}}
    private static FpsArenaGenerator AssertScene()
    {
        var g=UnityEngine.Object.FindFirstObjectByType<FpsArenaGenerator>();
        if(g==null||g.CurrentLayout==null||g.CurrentLayout.Hash!=File.ReadAllText(Evidence+"/SavedHash.txt")||g.transform.childCount!=1||g.CurrentLayout.Rooms.Count!=8)throw new InvalidOperationException("Room layout/graph/root was not restored.");
        g.CurrentLayout.Validate();Debug.Log("ROOM_STATE: "+SessionState.GetString(ReloadKey,""));return g;
    }
    public static void BuildAndRender()
    {
        ShaderUtil.allowAsyncCompilation=false;Directory.CreateDirectory(Evidence);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets","FpsArenaRoomsExample");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var settings=AssetDatabase.LoadAssetAtPath<FpsArenaSettings>(Folder+"/RoomSettings.asset");
        if(settings==null){settings=ScriptableObject.CreateInstance<FpsArenaSettings>();AssetDatabase.CreateAsset(settings,Folder+"/RoomSettings.asset");}
        settings.recipe=FpsArenaRecipe.CreateFlexible();settings.recipe.width=settings.recipe.depth=32;settings.recipe.floors=2;
        settings.recipe.partitionRooms=true;settings.recipe.roomsPerFloor=4;settings.recipe.spacingCells=1;settings.seed=73125;EditorUtility.SetDirty(settings);
        var host=new GameObject("FPS Arena — 4개 방 구획 예제");var g=host.AddComponent<FpsArenaGenerator>();g.settings=settings;g.generateOnPlay=false;
        FpsArenaWindow.GenerateWithUndo(g);FpsArenaWindow.GenerateWithUndo(g);
        if(g.CurrentLayout.RoomReports.Any(r=>r.actual!=4)||host.transform.childCount!=1)throw new InvalidOperationException("Example did not create exactly four rooms per floor.");
        var sun=new GameObject("Directional Light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
        var player=new GameObject("FPS Test Player");var cc=player.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.35f;cc.center=Vector3.up*.9f;cc.stepOffset=.3f;cc.slopeLimit=50;cc.skinWidth=.04f;
        var camera=new GameObject("FPS Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.farClipPlane=600;camera.transform.SetParent(player.transform,false);camera.transform.localPosition=Vector3.up*1.65f;camera.gameObject.AddComponent<AudioListener>();
        var controller=player.AddComponent<FpsArenaPlayer>();controller.arena=g;controller.view=camera;player.transform.position=g.CurrentLayout.Position(g.CurrentLayout.Spawns[0])+Vector3.up*.05f;
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(host.scene,Folder+"/RoomPartitions.unity");File.WriteAllText(Evidence+"/SavedHash.txt",g.CurrentLayout.Hash);
        File.WriteAllText(Evidence+"/RoomGraph.json",JsonUtility.ToJson(new GraphReport { rooms=g.CurrentLayout.Rooms.ToArray(),doors=g.CurrentLayout.RoomDoors.ToArray(),connections=g.CurrentLayout.RoomConnections.ToArray(),floors=g.CurrentLayout.RoomReports.ToArray() },true));
        Render(g,"Overview",new Vector3(80,95,-90),Vector3.zero,false);
        Render(g,"UpperFloor",new Vector3(0,100,0),Vector3.up*4,true);
        // Floor visibility is changed only for captures, after the untouched scene was saved.
        g.GeneratedRoot.transform.Find("층_2").gameObject.SetActive(false);
        foreach(var wall in g.CurrentLayout.RoomWalls.Where(w=>w.cell.y==1))g.GeneratedRoot.transform.Find(wall.id).gameObject.SetActive(false);
        foreach(var p in g.CurrentLayout.Content.Where(p=>p.cell.y==1))g.GeneratedRoot.transform.Find(p.id).gameObject.SetActive(false);
        Render(g,"LowerFloor",new Vector3(0,80,0),Vector3.zero,true);
        var first=g.CurrentLayout.RoomDoors.First(d=>d.cell.y==0);Vector3 door=first.Position(g.CurrentLayout);
        Render(g,"Doorway",door-(Vector3)first.Normal*10+Vector3.up*1.65f,door+(Vector3)first.Normal*2+Vector3.up*1.65f,false);
        var overlay=new GameObject("Verification room graph");int number=0;
        foreach(var room in g.CurrentLayout.Rooms.Where(r=>r.floor==0))
        {
            var label=new GameObject("Room label").AddComponent<TextMesh>();label.transform.SetParent(overlay.transform,false);label.transform.position=g.CurrentLayout.Position(room.anchor)+Vector3.up*3.3f;
            label.transform.rotation=Quaternion.Euler(90,0,0);label.text="R"+(++number);label.fontSize=64;label.characterSize=2;label.anchor=TextAnchor.MiddleCenter;label.color=Color.white;
        }
        foreach(var d in g.CurrentLayout.RoomDoors.Where(d=>d.cell.y==0))
        {
            var line=new GameObject("Door connection").AddComponent<LineRenderer>();line.transform.SetParent(overlay.transform,false);line.positionCount=3;line.widthMultiplier=.25f;
            line.material=new Material(Shader.Find("Sprites/Default"));line.startColor=line.endColor=Color.cyan;
            line.SetPositions(new[]{g.CurrentLayout.Position(g.CurrentLayout.Rooms[d.roomA].anchor)+Vector3.up*3.1f,d.Position(g.CurrentLayout)+Vector3.up*3.1f,g.CurrentLayout.Position(g.CurrentLayout.Rooms[d.roomB].anchor)+Vector3.up*3.1f});
        }
        Render(g,"RoomGraph",new Vector3(0,90,0),Vector3.zero,true);
        UnityEngine.Object.DestroyImmediate(overlay);ReopenCheck();Debug.Log("ROOM_RENDER_COMPLETE");
    }
    [Serializable] private sealed class GraphReport { public FpsArenaRoom[] rooms;public FpsArenaRoomDoor[] doors;public FpsArenaRoomConnection[] connections;public FpsArenaRoomReport[] floors; }
    public static void ReopenCheck()
    {
        EditorSceneManager.OpenScene(Folder+"/RoomPartitions.unity",OpenSceneMode.Single);var g=AssertScene();
        File.WriteAllText(Evidence+"/Reopen.json","{\"hash\":\""+g.CurrentLayout.Hash+"\",\"rooms\":"+g.CurrentLayout.Rooms.Count+",\"doors\":"+g.CurrentLayout.RoomDoors.Count+",\"wallSegments\":"+g.CurrentLayout.RoomWalls.Count+",\"content\":"+g.CurrentLayout.Content.Count+",\"colliders\":"+g.GeneratedRoot.GetComponentsInChildren<Collider>().Length+"}");
    }
    private static void Render(FpsArenaGenerator g,string name,Vector3 position,Vector3 target,bool ortho)
    {
        var camera=new GameObject("Verification camera").AddComponent<Camera>();camera.transform.position=position;camera.transform.LookAt(target,ortho?Vector3.forward:Vector3.up);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.08f,.11f);camera.farClipPlane=600;camera.orthographic=ortho;camera.orthographicSize=g.CurrentLayout.Recipe.width*g.CurrentLayout.Recipe.cellSize*.54f;
        var rt=new RenderTexture(1600,1200,24);rt.Create();camera.targetTexture=rt;var previous=RenderTexture.active;
        try{camera.Render();RenderTexture.active=rt;var image=new Texture2D(1600,1200,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1200),0,0);image.Apply();File.WriteAllBytes(Evidence+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
        finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);}
    }
    public static void CheckEditorUi()
    {
        ReopenCheck();errors=0;WatchErrors();var g=UnityEngine.Object.FindFirstObjectByType<FpsArenaGenerator>();var window=EditorWindow.GetWindow<FpsArenaWindow>("FPS 아레나");
        typeof(FpsArenaWindow).GetField("target",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,g);window.position=new Rect(80,80,760,920);window.Show();window.Focus();
        int frames=0,tab=0;EditorApplication.CallbackFunction tick=null;
        tick=()=>{window.Repaint();if(++frames%45!=0)return;
            // Send an actual GUI event to this window. Screen pixels can belong to another
            // foreground app when Unity is hidden, so never capture the desktop here.
            window.SendEvent(new Event { type=EventType.ExecuteCommand,commandName="ArenaRoomValidation" });
            Debug.Log("ROOM_GUI_TAB: "+tab);
            if(++tab<6)typeof(FpsArenaWindow).GetField("selectedTab",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,tab);
            else if(tab==6){typeof(FpsArenaWindow).GetField("selectedTab",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,0);typeof(FpsArenaWindow).GetField("_scroll",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,new Vector2(0,700));}
            else{EditorApplication.update-=tick;window.Close();File.WriteAllText(Evidence+"/EditorUi.json","{\"tabs\":6,\"resultPanel\":true,\"errors\":"+errors+",\"desktopScreenshot\":false}");EditorApplication.Exit(errors==0?0:1);}};
        EditorApplication.update+=tick;
    }
}
