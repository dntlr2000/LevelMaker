// Copy this file into a validation project's Assets/__ArenaWallValidation/Editor.
// Unity -executeMethod ArenaWallsUnityValidation.BuildAndRender -arenaEvidence <absolute folder>
// Run with graphics enabled. This helper only writes its dedicated example folder and evidence.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RogueDungeonLab;
using RogueDungeonLab.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArenaWallsUnityValidation
{
    private const string Folder = "Assets/FpsArenaWallsExample";
    private const string ReloadKey = "ArenaWallValidation.ReloadStage";
    private static int errors;
    [InitializeOnLoadMethod]
    private static void AfterReload()
    {
        if (SessionState.GetString(ReloadKey, "") == "") return;
        Application.logMessageReceived += (message,stack,type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.delayCall += ContinueReloadCheck;
    }
    public static void CheckPlayDomainReload()
    {
        ReopenCheck(); errors = 0; SessionState.SetString(ReloadKey,"entering");
        Application.logMessageReceived += (message,stack,type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.EnterPlaymode();
    }
    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(ReloadKey, "") == "entering")
        {
            AssertCurrentScene(); SessionState.SetString(ReloadKey,"reloading");
            EditorApplication.delayCall += () => EditorUtility.RequestScriptReload();
        }
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetString(ReloadKey, "") == "exiting")
        {
            AssertCurrentScene(); SessionState.EraseString(ReloadKey);
            File.WriteAllText(Evidence + "/PlayDomainReload.json","{\"playScriptReload\":true,\"returnedToEdit\":true,\"errors\":" + errors + "}");
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }
    }
    private static void ContinueReloadCheck()
    {
        if (SessionState.GetString(ReloadKey, "") == "reloading" && EditorApplication.isPlaying)
        {
            AssertCurrentScene(); SessionState.SetString(ReloadKey,"exiting"); EditorApplication.ExitPlaymode();
        }
    }
    private static void AssertCurrentScene()
    {
        var g = UnityEngine.Object.FindFirstObjectByType<FpsArenaGenerator>();
        if (g == null || g.CurrentLayout == null || g.CurrentLayout.Hash != File.ReadAllText(Evidence + "/SavedHash.txt") || g.transform.childCount != 1 || g.CurrentLayout.Walls.Count == 0)
            throw new InvalidOperationException("Wall layout/root was not preserved through Play/domain reload.");
        g.CurrentLayout.Validate(); Debug.Log("WALL_DOMAIN_STATE: " + SessionState.GetString(ReloadKey, ""));
    }
    private static string Evidence
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args,"-arenaEvidence");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("Logs/ArenaWalls");
        }
    }
    public static void BuildAndRender()
    {
        ShaderUtil.allowAsyncCompilation = false;
        Directory.CreateDirectory(Evidence);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets","FpsArenaWallsExample");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var settings = AssetDatabase.LoadAssetAtPath<FpsArenaSettings>(Folder + "/WallSettings.asset");
        if (settings == null) { settings = ScriptableObject.CreateInstance<FpsArenaSettings>(); AssetDatabase.CreateAsset(settings,Folder + "/WallSettings.asset"); }
        settings.recipe = FpsArenaRecipe.CreateFlexible(); settings.recipe.width = settings.recipe.depth = 32;
        settings.recipe.floors = 2; settings.recipe.internalWalls = true; settings.recipe.wallDensity = .2f;
        settings.recipe.spacingCells = 1; settings.recipe.wallMinLengthCells = 6; settings.recipe.wallMaxLengthCells = 12;
        settings.seed = 73125; EditorUtility.SetDirty(settings);
        var host = new GameObject("FPS Arena — 내부 벽 예제"); var g = host.AddComponent<FpsArenaGenerator>(); g.settings = settings; g.generateOnPlay = false;
        FpsArenaWindow.GenerateWithUndo(g); FpsArenaWindow.GenerateWithUndo(g);
        if (host.transform.childCount != 1) throw new InvalidOperationException("Repeated setup duplicated generated roots.");
        var sun = new GameObject("Directional Light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.5f; sun.transform.rotation = Quaternion.Euler(45,-30,0);
        RenderSettings.ambientLight = new Color(.5f,.5f,.5f);
        var camera = new GameObject("FPS Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.farClipPlane = 600;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f,.08f,.11f);
        var player = new GameObject("FPS Test Player"); var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = .35f; cc.center = new Vector3(0,.9f,0); cc.stepOffset = .3f; cc.slopeLimit = 50; cc.skinWidth = .04f;
        camera.transform.SetParent(player.transform,false); camera.transform.localPosition = Vector3.up * 1.65f; camera.gameObject.AddComponent<AudioListener>();
        var controller = player.AddComponent<FpsArenaPlayer>(); controller.arena = g; controller.view = camera;
        player.transform.position = g.CurrentLayout.Position(g.CurrentLayout.Spawns[0]) + Vector3.up * .05f;
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(host.scene,Folder + "/InternalWalls.unity");
        File.WriteAllText(Evidence + "/SavedHash.txt",g.CurrentLayout.Hash);
        Render(g,"Overview",new Vector3(95,100,-105),new Vector3(0,0,0),false);
        Render(g,"UpperFloor",new Vector3(0,100,0),new Vector3(0,4,0),true);
        // Expose the lower floor by hiding only its upper slab; leave both storeys' walls visible.
        var upper = g.GeneratedRoot.transform.Find("층_2"); upper.gameObject.SetActive(false);
        foreach(var wall in g.CurrentLayout.Walls.Where(w => w.cell.y == 1)) g.GeneratedRoot.transform.Find(wall.id).gameObject.SetActive(false);
        foreach(var content in g.CurrentLayout.Content.Where(c => c.cell.y == 1)) g.GeneratedRoot.transform.Find(content.id).gameObject.SetActive(false);
        Render(g,"LowerFloor",new Vector3(0,80,-.01f),Vector3.zero,true);
        var w = g.CurrentLayout.Walls.First(wall => wall.cell.y == 0 && wall.doorWidthCells > 0);
        Vector3 door = g.CurrentLayout.Position(w.cell + w.Step * w.doorOffsetCells);
        Vector3 normal = w.alongX ? Vector3.forward : Vector3.right;
        Render(g,"Doorway",door - normal * 12 + Vector3.up * 1.65f,door + normal * 2 + Vector3.up * 1.65f,false);
        EditorSceneManager.OpenScene(Folder + "/InternalWalls.unity",OpenSceneMode.Single);
        ReopenCheck();
        File.WriteAllText(Evidence + "/Generation.json", JsonUtility.ToJson(new Report {
            hash = File.ReadAllText(Evidence + "/SavedHash.txt"),
            scene = Folder + "/InternalWalls.unity", seed = settings.seed
        },true));
        Debug.Log("ARENA_WALL_RENDER_COMPLETE");
    }
    [Serializable] private sealed class Report { public string hash,scene; public int seed; }
    public static void ReopenCheck()
    {
        EditorSceneManager.OpenScene(Folder + "/InternalWalls.unity",OpenSceneMode.Single);
        var g = UnityEngine.Object.FindFirstObjectByType<FpsArenaGenerator>();
        if (g == null || g.CurrentLayout == null || g.CurrentLayout.Hash != File.ReadAllText(Evidence + "/SavedHash.txt") || g.transform.childCount != 1) throw new InvalidOperationException("Wall scene metadata did not survive reopen.");
        g.CurrentLayout.Validate(); Physics.SyncTransforms();
        File.WriteAllText(Evidence + "/Reopen.json", "{\"hash\":\"" + g.CurrentLayout.Hash + "\",\"walls\":" + g.CurrentLayout.Walls.Count + ",\"content\":" + g.CurrentLayout.Content.Count + ",\"colliders\":" + g.GeneratedRoot.GetComponentsInChildren<Collider>().Length + "}");
        Debug.Log("ARENA_WALL_REOPEN_VERIFIED: " + g.CurrentLayout.Hash);
    }
    private static void Render(FpsArenaGenerator g,string name,Vector3 position,Vector3 target,bool ortho)
    {
        var camera = new GameObject("Verification " + name).AddComponent<Camera>(); camera.transform.position = position; camera.transform.LookAt(target,ortho ? Vector3.forward : Vector3.up);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f,.08f,.11f); camera.farClipPlane = 600;
        camera.orthographic = ortho; camera.orthographicSize = g.CurrentLayout.Recipe.width * g.CurrentLayout.Recipe.cellSize * .54f;
        var rt = new RenderTexture(1600,1200,24); rt.Create(); camera.targetTexture = rt;
        RenderTexture previous = RenderTexture.active;
        try { camera.Render(); RenderTexture.active = rt; var png = new Texture2D(1600,1200,TextureFormat.RGB24,false); png.ReadPixels(new Rect(0,0,1600,1200),0,0); png.Apply(); File.WriteAllBytes(Evidence + "/" + name + ".png",png.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(png); }
        finally { RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(camera.gameObject); }
    }
    // Run in a dedicated GUI Unity instance. Every tab is painted through real OnGUI events.
    public static void CaptureEditorUi()
    {
        ReopenCheck(); var g = UnityEngine.Object.FindFirstObjectByType<FpsArenaGenerator>();
        var window = EditorWindow.GetWindow<FpsArenaWindow>("FPS 아레나");
        typeof(FpsArenaWindow).GetField("target",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window,g);
        window.position = new Rect(80,80,760,920); window.Show(); window.Focus();
        int frames = 0, tab = 0; EditorApplication.CallbackFunction tick = null;
        tick = () => {
            window.Repaint(); frames++;
            if (frames % 45 != 0) return;
            Color[] pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(window.position.position,(int)window.position.width,(int)window.position.height);
            var image = new Texture2D((int)window.position.width,(int)window.position.height,TextureFormat.RGB24,false); image.SetPixels(pixels); image.Apply();
            File.WriteAllBytes(Evidence + "/EditorTab" + tab + ".png",image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            if (++tab < 6) typeof(FpsArenaWindow).GetField("selectedTab",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window,tab);
            else { EditorApplication.update -= tick; window.Close(); File.WriteAllText(Evidence + "/EditorUiComplete.txt","All six tabs rendered without GUI exceptions."); EditorApplication.Exit(0); }
        }; EditorApplication.update += tick;
    }
}
