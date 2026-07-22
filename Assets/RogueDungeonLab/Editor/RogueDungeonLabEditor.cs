using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RogueDungeonLab;

namespace RogueDungeonLab.Editor
{
    internal static class DistributionGraphDrawer
    {
        public static void Draw(RogueDungeonSettings settings,float height=220f)
        {
            Rect rect=GUILayoutUtility.GetRect(120,height,GUILayout.ExpandWidth(true));EditorGUI.DrawRect(rect,new Color(0.09f,0.1f,0.12f));Rect graph=new Rect(rect.x+42,rect.y+18,rect.width-58,rect.height-48);Handles.BeginGUI();Color old=Handles.color;Handles.color=new Color(1,1,1,0.11f);for(int i=0;i<=4;i++){float t=i/4f;float x=Mathf.Lerp(graph.x,graph.xMax,t),y=Mathf.Lerp(graph.y,graph.yMax,t);Handles.DrawLine(new Vector3(x,graph.y),new Vector3(x,graph.yMax));Handles.DrawLine(new Vector3(graph.x,y),new Vector3(graph.xMax,y));}Handles.color=old;Handles.EndGUI();float max=Max(settings);Line(graph,settings.enemyProfile,max,new Color(0.95f,0.25f,0.25f));Line(graph,settings.destructibleProfile,max,new Color(1f,0.58f,0.18f));Line(graph,settings.propProfile,max,new Color(0.32f,0.82f,0.42f));GUIStyle axis=new GUIStyle(EditorStyles.miniLabel){alignment=TextAnchor.MiddleCenter};GUI.Label(new Rect(graph.x-36,graph.y-7,34,18),max.ToString("0.000"),axis);GUI.Label(new Rect(graph.x-34,graph.yMax-9,32,18),"0",axis);GUI.Label(new Rect(graph.x-10,graph.yMax+4,35,18),"입구",axis);GUI.Label(new Rect(graph.xMax-20,graph.yMax+4,40,18),"출구",axis);GUIStyle legend=new GUIStyle(EditorStyles.miniLabel){richText=true};GUI.Label(new Rect(rect.x+46,rect.y+1,rect.width-60,18),"<color=#F44343>● 적</color>    <color=#FF941F>● 파괴물</color>    <color=#52D16B>● 지형지물</color>",legend);
        }
        private static float Base(DensityProfile p,float t){return p==null||p.overProgression==null?0:Mathf.Max(0,p.baseDensity*p.overProgression.Evaluate(t));}
        private static float Max(RogueDungeonSettings s){float m=0.05f;for(int i=0;i<64;i++){float t=i/63f;m=Mathf.Max(m,Base(s.enemyProfile,t),Base(s.destructibleProfile,t),Base(s.propProfile,t));}return m*1.1f;}
        private static void Line(Rect r,DensityProfile p,float max,Color color){Vector3[] pts=new Vector3[64];for(int i=0;i<64;i++){float t=i/63f;pts[i]=new Vector3(Mathf.Lerp(r.x,r.xMax,t),Mathf.Lerp(r.yMax,r.y,Mathf.Clamp01(Base(p,t)/max)));}Handles.BeginGUI();Color old=Handles.color;Handles.color=color;Handles.DrawAAPolyLine(2.5f,pts);Handles.color=old;Handles.EndGUI();}
    }

    public static class RogueDungeonLabSceneSetup
    {
        private const string Folder="Assets/RogueDungeonLab/Generated",SettingsPath=Folder+"/DungeonLabSettings.asset",EnemyPath=Folder+"/EnemyDropTable.asset",BreakablePath=Folder+"/DestructibleDropTable.asset";
        [MenuItem("Tools/Rogue Dungeon Lab/장면 자동 구성",priority=1)]public static void Menu(){CreateOrRepairScene(true);}
        // 실험 장면에 필요한 생성기, 입력 시스템, 카메라와 조명을 중복 없이 구성합니다.
        public static RogueDungeonGenerator CreateOrRepairScene(bool select)
        {
            RogueDungeonSettings settings=CreateOrLoadSettings();RogueDungeonGenerator generator=UnityEngine.Object.FindAnyObjectByType<RogueDungeonGenerator>();if(generator==null){GameObject go=new GameObject("Rogue Dungeon Generator");Undo.RegisterCreatedObjectUndo(go,"Create Rogue Dungeon Generator");generator=go.AddComponent<RogueDungeonGenerator>();}Undo.RecordObject(generator,"Configure Rogue Dungeon Generator");generator.settings=settings;EditorUtility.SetDirty(generator);
            GameObject systems=GameObject.Find("Rogue Dungeon Lab Systems");if(systems==null){systems=new GameObject("Rogue Dungeon Lab Systems");Undo.RegisterCreatedObjectUndo(systems,"Create Rogue Dungeon Lab Systems");}if(systems.GetComponent<DropValidationService>()==null)Undo.AddComponent<DropValidationService>(systems);RogueDungeonClickInteractor interactor=systems.GetComponent<RogueDungeonClickInteractor>();if(interactor==null)interactor=Undo.AddComponent<RogueDungeonClickInteractor>(systems);if(systems.GetComponent<RuntimeLabHUD>()==null)Undo.AddComponent<RuntimeLabHUD>(systems);
            Camera camera=EnsureCamera();interactor.targetCamera=camera;EditorUtility.SetDirty(interactor);LabOrbitCamera orbit=camera.GetComponent<LabOrbitCamera>();if(orbit==null)orbit=Undo.AddComponent<LabOrbitCamera>(camera.gameObject);EnsureLight();generator.GenerateFromSettings();orbit.FocusBounds(generator.GeneratedBounds);EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();if(select){Selection.activeObject=generator.gameObject;EditorGUIUtility.PingObject(generator.gameObject);}return generator;
        }
        public static RogueDungeonSettings CreateOrLoadSettings()
        {
            EnsureFolder(Folder);RogueDungeonSettings s=AssetDatabase.LoadAssetAtPath<RogueDungeonSettings>(SettingsPath);if(s==null){s=ScriptableObject.CreateInstance<RogueDungeonSettings>();s.ApplyPreset(DungeonPreset.Balanced);s.seed=12345;AssetDatabase.CreateAsset(s,SettingsPath);}WeightedDropTable enemy=DefaultTable(EnemyPath,true),breakable=DefaultTable(BreakablePath,false);if(s.enemyDropTable==null||s.destructibleDropTable==null){Undo.RecordObject(s,"Assign Default Drop Tables");if(s.enemyDropTable==null)s.enemyDropTable=enemy;if(s.destructibleDropTable==null)s.destructibleDropTable=breakable;EditorUtility.SetDirty(s);}return s;
        }
        private static WeightedDropTable DefaultTable(string path,bool enemy)
        {
            WeightedDropTable t=AssetDatabase.LoadAssetAtPath<WeightedDropTable>(path);if(t!=null)return t;t=ScriptableObject.CreateInstance<WeightedDropTable>();t.name=enemy?"Enemy Drop Table":"Destructible Drop Table";if(enemy){t.entries.Add(new DropEntry{itemId="Gold",weight=55,minQuantity=1,maxQuantity=3,markerColor=new Color(1,.82f,.15f)});t.entries.Add(new DropEntry{itemId="Potion",weight=15,markerColor=new Color(.9f,.2f,.35f)});t.entries.Add(new DropEntry{itemId="RareShard",weight=5,markerColor=new Color(.5f,.75f,1)});t.entries.Add(new DropEntry{itemId="Nothing",weight=25,representsNoDrop=true,markerColor=Color.clear});}else{t.entries.Add(new DropEntry{itemId="Gold",weight=45,minQuantity=1,maxQuantity=2,markerColor=new Color(1,.82f,.15f)});t.entries.Add(new DropEntry{itemId="CraftMaterial",weight=30,minQuantity=1,maxQuantity=4,markerColor=new Color(.45f,.9f,.55f)});t.entries.Add(new DropEntry{itemId="Potion",weight=5,markerColor=new Color(.9f,.2f,.35f)});t.entries.Add(new DropEntry{itemId="Nothing",weight=20,representsNoDrop=true,markerColor=Color.clear});}AssetDatabase.CreateAsset(t,path);return t;
        }
        // 기존 주 카메라를 재사용하거나 실험용 카메라를 생성해 기본 구도를 설정합니다.
        private static Camera EnsureCamera(){Camera c=Camera.main;if(c==null)c=UnityEngine.Object.FindAnyObjectByType<Camera>();if(c==null){GameObject go=new GameObject("Main Camera");Undo.RegisterCreatedObjectUndo(go,"Create Main Camera");c=go.AddComponent<Camera>();c.nearClipPlane=.1f;c.farClipPlane=500;}if(!c.CompareTag("MainCamera"))c.gameObject.tag="MainCamera";if(c.GetComponent<AudioListener>()==null&&UnityEngine.Object.FindAnyObjectByType<AudioListener>()==null)Undo.AddComponent<AudioListener>(c.gameObject);c.transform.position=new Vector3(-35,45,-35);c.transform.rotation=Quaternion.Euler(50,45,0);return c;}
        // 방향광이 없을 때만 실험용 방향광을 생성합니다.
        private static void EnsureLight(){Light[] lights=UnityEngine.Object.FindObjectsByType<Light>();for(int i=0;i<lights.Length;i++)if(lights[i].type==LightType.Directional)return;GameObject go=new GameObject("Directional Light");Undo.RegisterCreatedObjectUndo(go,"Create Directional Light");Light l=go.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.2f;l.transform.rotation=Quaternion.Euler(50,-30,0);}
        private static void EnsureFolder(string path){string[] parts=path.Split('/');string current=parts[0];for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}}
    }

    public sealed partial class RogueDungeonLabWindow : EditorWindow
    {
        private static readonly string[] Tabs={"생성","분포 그래프","드랍 검증","스테이지 자산","가이드"};private const string AutoKey="RogueDungeonLab.AutoRegenerate";
        private RogueDungeonGenerator _generator;private RogueDungeonSettings _settings;private SerializedObject _serialized;private Vector2 _scroll;private int _tab;private bool _auto,_pending,_enemyFold=true,_breakableFold=true;private double _regenAt;
        [MenuItem("Tools/Rogue Dungeon Lab/실험실 열기",priority=0)]public static void Open(){RogueDungeonLabWindow w=GetWindow<RogueDungeonLabWindow>();w.titleContent=new GUIContent("Dungeon Lab");w.minSize=new Vector2(520,620);w.Show();}
        private void OnEnable(){_auto=EditorPrefs.GetBool(AutoKey,true);FindBindings();EditorApplication.update+=EditorUpdate;EditorApplication.playModeStateChanged+=PlayChanged;}
        private void OnDisable(){EditorPrefs.SetBool(AutoKey,_auto);EditorApplication.update-=EditorUpdate;EditorApplication.playModeStateChanged-=PlayChanged;}
        private void OnHierarchyChange(){FindBindings();Repaint();}
        // 현재 탭이 설정 자산을 편집하는지 구분해 저장 제작 UI 변경이 자동 재생성을 유발하지 않게 그립니다.
        private void OnGUI()
        {
            if(_generator==null)FindBindings();
            Header();
            if(_generator==null)
            {
                EditorGUILayout.HelpBox("Generator가 없습니다.",MessageType.Info);
                if(GUILayout.Button("프로토타입 장면 자동 구성",GUILayout.Height(42)))
                {
                    _generator=RogueDungeonLabSceneSetup.CreateOrRepairScene(true);
                    Bind();
                }
                return;
            }

            if(_settings!=null&&(_serialized==null||_serialized.targetObject!=_settings))
                _serialized=new SerializedObject(_settings);
            _tab=GUILayout.Toolbar(_tab,Tabs,GUILayout.Height(25));
            _scroll=EditorGUILayout.BeginScrollView(_scroll);
            bool editsSettings=_tab<=2;
            if(editsSettings&&_settings==null)
            {
                EditorGUILayout.HelpBox("생성·분포·드랍 탭에는 설정 에셋이 필요합니다. 저장형 스테이지는 '스테이지 자산' 탭에서 계속 관리할 수 있습니다.",MessageType.Info);
            }
            else if(editsSettings)
            {
                _serialized.Update();
                EditorGUI.BeginChangeCheck();
                if(_tab==1)DistributionTab();else if(_tab==2)DropTab();else GenerationTab();
                bool changed=EditorGUI.EndChangeCheck();
                bool applied=_serialized.ApplyModifiedProperties();
                if(changed||applied){EditorUtility.SetDirty(_settings);Schedule();}
            }
            else if(_tab==3)
            {
                StageAssetsTab();
            }
            else
            {
                GuideTab();
            }
            EditorGUILayout.EndScrollView();
        }
        private void Header(){EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);GUILayout.Label("실시간 3D 로그라이크 던전 실험실",EditorStyles.boldLabel);GUILayout.FlexibleSpace();if(GUILayout.Button("장면 자동 구성",EditorStyles.toolbarButton)){_generator=RogueDungeonLabSceneSetup.CreateOrRepairScene(true);Bind();}if(_generator!=null&&GUILayout.Button("Generator 선택",EditorStyles.toolbarButton)){Selection.activeObject=_generator.gameObject;EditorGUIUtility.PingObject(_generator.gameObject);}EditorGUILayout.EndHorizontal();}
        private void GenerationTab()
        {
            EditorGUILayout.LabelField("프리셋",EditorStyles.boldLabel);EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Compact"))Preset(DungeonPreset.Compact);if(GUILayout.Button("Balanced"))Preset(DungeonPreset.Balanced);if(GUILayout.Button("Chaos"))Preset(DungeonPreset.Chaos);EditorGUILayout.EndHorizontal();EditorGUILayout.PropertyField(_serialized.FindProperty("seed"),new GUIContent("설정 시드"));EditorGUILayout.BeginHorizontal();if(GUILayout.Button("설정 시드로 생성",GUILayout.Height(30))){Apply();_generator.GenerateWithSeed(_settings.seed);RepaintAll();}if(GUILayout.Button("현재 시드 재생성",GUILayout.Height(30))){Apply();_generator.RegenerateActiveSeed();RepaintAll();}if(GUILayout.Button("새 랜덤 시드",GUILayout.Height(30))){Apply();Undo.RecordObject(_settings,"Set Random Seed");_settings.seed=unchecked(Environment.TickCount*397^Guid.NewGuid().GetHashCode());EditorUtility.SetDirty(_settings);_serialized.Update();_generator.GenerateWithSeed(_settings.seed);RepaintAll();}EditorGUILayout.EndHorizontal();_auto=EditorGUILayout.ToggleLeft("값 변경 후 자동 재생성(0.18초)",_auto);
            Section("스테이지 크기");IntSlider("stageWidthCells","가로 셀 수",12,96);IntSlider("stageDepthCells","세로 셀 수",12,96);Prop("cellSize","셀 크기(m)");Prop("wallHeight","벽 높이(m)");Section("방 및 연결");IntSlider("desiredRoomCount","목표 방 개수",2,40);Prop("roomSizeMin","최소 방 크기");Prop("roomSizeMax","최대 방 크기");IntSlider("roomPlacementAttempts","방당 배치 시도",5,100);IntSlider("corridorWidthCells","복도 폭",1,4);Prop("extraConnectionChance","추가 연결 확률");Section("콘텐츠 제약");IntSlider("specialGimmickCount","특별 기믹 개수",0,30);IntSlider("contentSpacingCells","콘텐츠 최소 간격",0,4);IntSlider("reservedEntranceRadiusCells","입구 비우기 반경",0,8);Prop("generateOnPlay","Play 진입 시 자동 생성");Report();
        }
        private void DistributionTab(){EditorGUILayout.HelpBox("곡선 X축은 입구에서 출구까지의 BFS 진행도, Y축은 기본 밀도 배율입니다.",MessageType.Info);DistributionGraphDrawer.Draw(_settings);Profile("enemyProfile","적 캐릭터",new Color(.9f,.2f,.2f));Profile("destructibleProfile","부술 수 있는 오브젝트",new Color(.95f,.5f,.12f));Profile("propProfile","지형지물",new Color(.25f,.7f,.35f));}
        private void Profile(string path,string title,Color accent){SerializedProperty p=_serialized.FindProperty(path);Rect r=EditorGUILayout.GetControlRect(false,22);EditorGUI.DrawRect(new Rect(r.x,r.y,5,r.height),accent);EditorGUI.LabelField(new Rect(r.x+10,r.y,r.width-10,r.height),title,EditorStyles.boldLabel);EditorGUI.indentLevel++;EditorGUILayout.PropertyField(p.FindPropertyRelative("baseDensity"),new GUIContent("기본 셀 밀도"));EditorGUILayout.PropertyField(p.FindPropertyRelative("overProgression"),new GUIContent("진행도 밀도 곡선"));EditorGUILayout.PropertyField(p.FindPropertyRelative("roomBias"),new GUIContent("방 선호도"));EditorGUILayout.PropertyField(p.FindPropertyRelative("clustering"),new GUIContent("군집도"));EditorGUILayout.PropertyField(p.FindPropertyRelative("maxCount"),new GUIContent("최대 개수(0=무제한)"));EditorGUI.indentLevel--;GUILayout.Space(7);}
        private void DropTab()
        {
            EditorGUILayout.HelpBox("Play 모드에서 빨간 적/주황 파괴물을 좌클릭하거나 빠른 표본을 추가하세요.",MessageType.Info);SerializedProperty enemy=_serialized.FindProperty("enemyDropTable"),breakable=_serialized.FindProperty("destructibleDropTable");EditorGUILayout.PropertyField(enemy,new GUIContent("적 드랍 테이블"));EditorGUILayout.PropertyField(breakable,new GUIContent("파괴물 드랍 테이블"));Prop("spawnDropMarkers","드랍 마커 표시");Prop("resetDropStatsOnGenerate","재생성 시 통계 초기화");InlineTable("적 테이블 항목",enemy.objectReferenceValue as WeightedDropTable,ref _enemyFold);InlineTable("파괴물 테이블 항목",breakable.objectReferenceValue as WeightedDropTable,ref _breakableFold);DropValidationService service=FindService();if(service==null){EditorGUILayout.HelpBox("DropValidationService가 없습니다. 장면 자동 구성을 실행하세요.",MessageType.Warning);return;}Section("빠른 샘플링");EditorGUILayout.BeginHorizontal();if(GUILayout.Button("적 +100"))Sim(service,DropSourceKind.Enemy,100);if(GUILayout.Button("적 +1,000"))Sim(service,DropSourceKind.Enemy,1000);if(GUILayout.Button("적 +10,000"))Sim(service,DropSourceKind.Enemy,10000);EditorGUILayout.EndHorizontal();EditorGUILayout.BeginHorizontal();if(GUILayout.Button("파괴물 +100"))Sim(service,DropSourceKind.Destructible,100);if(GUILayout.Button("파괴물 +1,000"))Sim(service,DropSourceKind.Destructible,1000);if(GUILayout.Button("파괴물 +10,000"))Sim(service,DropSourceKind.Destructible,10000);EditorGUILayout.EndHorizontal();if(GUILayout.Button("모든 통계 초기화"))service.ResetStatistics();Section("관측 결과");List<DropSourceStatisticsSnapshot> snaps=service.GetSnapshots();if(snaps.Count==0)EditorGUILayout.HelpBox("아직 표본이 없습니다.",MessageType.None);for(int s=0;s<snaps.Count;s++){DropSourceStatisticsSnapshot src=snaps[s];EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.LabelField(string.Format("{0} / {1} / {2:N0}회",src.SourceKind==DropSourceKind.Enemy?"적":"파괴물",src.TableName,src.Attempts),EditorStyles.boldLabel);for(int e=0;e<src.Entries.Count;e++){DropEntryStatisticsSnapshot x=src.Entries[e];EditorGUILayout.LabelField(string.Format("{0}: 기대 {1:P1}, 관측 {2:P1}, Δ {3:+0.0%;-0.0%;0.0%}, 95% [{4:P1}, {5:P1}]",x.ItemId,x.ExpectedProbability,x.ObservedProbability,x.ObservedProbability-x.ExpectedProbability,x.WilsonLow95,x.WilsonHigh95));}EditorGUILayout.EndVertical();}
        }
        private void InlineTable(string label,WeightedDropTable table,ref bool fold){fold=EditorGUILayout.Foldout(fold,label,true);if(!fold)return;EditorGUI.indentLevel++;if(table==null)EditorGUILayout.HelpBox("테이블 에셋을 지정하세요.",MessageType.None);else{SerializedObject so=new SerializedObject(table);so.Update();EditorGUI.BeginChangeCheck();EditorGUILayout.PropertyField(so.FindProperty("entries"),new GUIContent("가중치 항목"),true);if(EditorGUI.EndChangeCheck()){so.ApplyModifiedProperties();EditorUtility.SetDirty(table);DropValidationService s=FindService();if(s!=null)s.ResetStatistics();}else so.ApplyModifiedProperties();}EditorGUI.indentLevel--;}
        // 던전 생성·저장 제작, 자유 카메라와 임시 캐릭터의 사용법을 안내합니다.
        private void GuideTab(){EditorGUILayout.LabelField("사용 순서",EditorStyles.boldLabel);EditorGUILayout.HelpBox("1. 장면 자동 구성\n2. 생성/분포 탭에서 결과 확정\n3. 스테이지 자산 탭에서 Blueprint 저장·검증\n4. 저장본 미리보기 또는 저장 레시피 복원\n5. StageDefinition 생성 후 Play 탐험\n6. 기대/관측 확률 비교",MessageType.None);EditorGUILayout.LabelField("조작",EditorStyles.boldLabel);EditorGUILayout.HelpBox("자유 시점: WASD 시선 기준 3D 이동 · Space 상승 · Ctrl 하강 · Shift 가속 · 우클릭 드래그 -89°~89° 제자리 회전 · 휠 줌 · 중클릭 드래그 이동\n임시 캐릭터: WASD 이동 · Shift 달리기 · Space 점프 · R 입구 복귀",MessageType.None);EditorGUILayout.LabelField("저장 제작",EditorStyles.boldLabel);EditorGUILayout.HelpBox("스테이지 자산 탭은 현재 결과와 일치하는 레시피 설정을 함께 저장합니다. 저장본 맵 미리보기, 설정만 복원, 설정+시드 절차 재생성, hash 비교와 SavedBlueprint StageDefinition 생성을 제공합니다. 생성 계층은 미리보기이므로 직접 수정하지 않습니다.",MessageType.None);EditorGUILayout.LabelField("색상",EditorStyles.boldLabel);EditorGUILayout.HelpBox("파랑 입구 · 자홍 출구 · 빨강 적 · 주황 파괴물 · 초록 지형지물 · 청록 기믹",MessageType.None);EditorGUILayout.LabelField("프로토타입 범위",EditorStyles.boldLabel);EditorGUILayout.HelpBox("단일 층 직교 그리드를 사용합니다. 다음 단계는 저장 Blueprint의 Mesh·콘텐츠 Prefab Bake이며, NavMesh와 다층 연결은 후속 확장입니다.",MessageType.Info);}
        private void Report(){Section("최근 생성 리포트");GenerationReport r=_generator.LastReport;if(r==null){EditorGUILayout.HelpBox("아직 생성하지 않았습니다.",MessageType.None);return;}EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.LabelField("활성 시드",r.activeSeed.ToString());EditorGUILayout.LabelField("생성 시간",r.generationMilliseconds.ToString("0.00")+" ms");EditorGUILayout.LabelField("방 / 바닥 셀",r.roomCount+" / "+r.floorCellCount);EditorGUILayout.LabelField("적 / 파괴물 / 지형지물 / 기믹",string.Format("{0} / {1} / {2} / {3}",r.enemyCount,r.destructibleCount,r.propCount,r.gimmickCount));EditorGUILayout.LabelField("메시 삼각형",r.meshTriangleCount.ToString("N0"));for(int i=0;i<r.warnings.Count;i++)EditorGUILayout.HelpBox(r.warnings[i],MessageType.Warning);EditorGUILayout.EndVertical();}
        private void Preset(DungeonPreset p){Apply();Undo.RecordObject(_settings,"Apply Dungeon Preset");_settings.ApplyPreset(p);EditorUtility.SetDirty(_settings);_serialized.Update();_generator.GenerateWithSeed(_settings.seed);_pending=false;RepaintAll();}
        private void Sim(DropValidationService s,DropSourceKind k,int n){Apply();s.Simulate(k,_generator.GetEffectiveDropTable(k),n);Repaint();}
        private void Apply(){if(_serialized!=null){_serialized.ApplyModifiedProperties();EditorUtility.SetDirty(_settings);}}
        private void Schedule(){if(!_auto||_generator==null||_settings==null)return;_pending=true;_regenAt=EditorApplication.timeSinceStartup+.18;}
        private void EditorUpdate(){if(_pending&&EditorApplication.timeSinceStartup>=_regenAt&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&_generator!=null&&_settings!=null){_pending=false;_generator.GenerateWithSeed(_settings.seed);RepaintAll();}if(EditorApplication.isPlaying)Repaint();}
        // Play 모드 전환 후 장면 참조를 다시 연결하고 창을 갱신합니다.
        private void PlayChanged(PlayModeStateChange state){FindBindings();Repaint();}
        // 현재 장면의 던전 생성기를 찾아 에디터 창 참조를 갱신합니다.
        private void FindBindings(){_generator=UnityEngine.Object.FindAnyObjectByType<RogueDungeonGenerator>();Bind();}
        // 선택된 생성기 설정으로 SerializedObject 바인딩을 다시 만듭니다.
        private void Bind(){_settings=_generator!=null?_generator.settings:null;_serialized=_settings!=null?new SerializedObject(_settings):null;BindStageAssetDefaults();}
        // 활성 드랍 통계 서비스를 우선 사용하고 없으면 현재 장면에서 찾습니다.
        private static DropValidationService FindService(){DropValidationService s=DropValidationService.Active;return s!=null?s:UnityEngine.Object.FindAnyObjectByType<DropValidationService>();}
        private void IntSlider(string path,string label,int min,int max){SerializedProperty p=_serialized.FindProperty(path);p.intValue=EditorGUILayout.IntSlider(label,p.intValue,min,max);}private void Prop(string path,string label){EditorGUILayout.PropertyField(_serialized.FindProperty(path),new GUIContent(label));}private static void Section(string s){GUILayout.Space(8);EditorGUILayout.LabelField(s,EditorStyles.boldLabel);}private static void RepaintAll(){SceneView.RepaintAll();}
    }
}
