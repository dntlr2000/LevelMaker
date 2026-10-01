using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public sealed class FpsArenaWindow : EditorWindow
    {
        internal const int GeometryTab = 0, CoverTab = 1, EnemyTab = 2, GimmickTab = 3, ItemTab = 4, PrefabTab = 5;
        private static readonly string[] TabLabels = { "지형·계단", "엄폐", "적", "특수 기믹", "아이템", "프리팹" };
        private static readonly FpsArenaContentKind[] CatalogKinds = { FpsArenaContentKind.Cover, FpsArenaContentKind.Enemy, FpsArenaContentKind.Gimmick, FpsArenaContentKind.Item };
        private static readonly string[] CatalogKindLabels = { "엄폐", "적", "특수 기믹", "아이템" };
        [SerializeField] private FpsArenaGenerator target;
        [SerializeField] private int selectedTab;
        private Vector2 _scroll;
        private static bool showLegacyWallSettings;

        [MenuItem("Tools/Rogue Dungeon Lab/FPS 아레나 제작", priority = 2)]
        public static void Open() { GetWindow<FpsArenaWindow>("FPS 아레나").Show(); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("FPS 아레나 제작", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("지형·엄폐·적·기믹·아이템을 따로 조절하고 실제 게임 프리팹을 연결합니다. 기존 로그라이크 제작 경로는 그대로 유지됩니다.", MessageType.Info);
            if (GUILayout.Button("새 FPS 테스트 장면 만들기")) CreateTestScene();
            if (GUILayout.Button("방 구획 FPS 예제 장면 만들기")) CreateTestScene(false, true);
            target = (FpsArenaGenerator)EditorGUILayout.ObjectField("아레나 생성기", target, typeof(FpsArenaGenerator), true);
            if (target == null)
            {
                if (GUILayout.Button("현재 장면에 생성기 추가"))
                {
                    var go = new GameObject("FPS Arena"); Undo.RegisterCreatedObjectUndo(go, "FPS 생성기 추가");
                    target = Undo.AddComponent<FpsArenaGenerator>(go); Selection.activeGameObject = go;
                }
                return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            selectedTab = DrawTabs(selectedTab);
            DrawSettings(target, selectedTab);
            if (GUILayout.Button("시드로 생성 / 재생성", GUILayout.Height(35))) GenerateWithUndo(target);
            DrawResult(target.CurrentLayout, selectedTab);
            EditorGUILayout.EndScrollView();
        }

        internal static int DrawTabs(int tab)
        {
            return GUILayout.SelectionGrid(Mathf.Clamp(tab, 0, TabLabels.Length - 1), TabLabels, 3, EditorStyles.miniButton);
        }

        // The optional tab keeps the existing all-fields entry point available to other editors.
        internal static void DrawSettings(FpsArenaGenerator generator, int tab = -1)
        {
            var so = new SerializedObject(generator); so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("settings"), new GUIContent("공유 설정 자산", "연결하면 시드·레시피·카탈로그를 해당 자산에서 읽고 편집합니다."));
            EditorGUILayout.PropertyField(so.FindProperty("generateOnPlay"), new GUIContent("Play 시 생성"));
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(generator);
            UnityEngine.Object data = generator.settings != null ? (UnityEngine.Object)generator.settings : generator;
            var settings = new SerializedObject(data); settings.Update();
            EditorGUILayout.PropertyField(settings.FindProperty("seed"), new GUIContent("시드", "같은 설정과 시드는 같은 계단·콘텐츠·ID·배치 해시를 만듭니다."));
            DrawVersion(settings, data);
            DrawRecipe(settings.FindProperty("recipe"), tab);
            if (tab < 0 || tab == PrefabTab) DrawCatalogConnection(settings, IsFlexible(settings.FindProperty("recipe")));
            if (settings.ApplyModifiedProperties()) EditorUtility.SetDirty(data);
            if (tab < 0 || tab == PrefabTab) DrawCatalog(generator.ActiveCatalog, IsFlexible(settings.FindProperty("recipe")));
            DrawPresets(generator, IsFlexible(settings.FindProperty("recipe")));
            if (GUILayout.Button("현재 설정을 새 자산으로 저장")) SaveSettings(generator);
        }

        internal static void DrawSettingsAsset(FpsArenaSettings asset, SerializedObject settings, int tab)
        {
            settings.Update();
            EditorGUILayout.PropertyField(settings.FindProperty("seed"), new GUIContent("시드"));
            DrawVersion(settings, asset);
            DrawRecipe(settings.FindProperty("recipe"), tab);
            if (tab == PrefabTab) DrawCatalogConnection(settings, IsFlexible(settings.FindProperty("recipe")));
            if (settings.ApplyModifiedProperties()) EditorUtility.SetDirty(asset);
            if (tab == PrefabTab) DrawCatalog(asset.contentCatalog, IsFlexible(settings.FindProperty("recipe")));
            EditorGUILayout.HelpBox("설정 자산의 변경은 이 자산을 공유하는 생성기에 적용됩니다. 장면의 실제 배치는 생성기에서 재생성해야 갱신됩니다.", MessageType.Info);
        }

        private static bool IsFlexible(SerializedProperty recipe)
        {
            var version = recipe != null ? recipe.FindPropertyRelative("generatorVersion") : null;
            return version != null && version.intValue == (int)FpsArenaGeneratorVersion.FlexibleV2;
        }

        private static void DrawVersion(SerializedObject settings, UnityEngine.Object data)
        {
            if (IsFlexible(settings.FindProperty("recipe")))
            {
                EditorGUILayout.LabelField("생성 규칙", "V2 범용 생성");
                return;
            }
            EditorGUILayout.LabelField("생성 규칙", "V1 기존 결과 보존");
            EditorGUILayout.HelpBox("이 설정은 V1입니다. 같은 시드의 기존 계단·상자·장애물 결과를 유지합니다. V2 업그레이드를 누르면 시드 가변 계단, 크기·형태 범위, 적·기믹 밀도와 프리팹을 사용하며 다음 재생성 결과가 바뀝니다. V1 장애물은 적이 아닌 충돌 상자입니다.", MessageType.Warning);
            if (GUILayout.Button("V2 범용 생성으로 업그레이드 (Undo 가능)"))
            {
                if (settings.ApplyModifiedProperties()) EditorUtility.SetDirty(data);
                UpgradeRecipe(data); settings.Update();
            }
        }

        internal static void UpgradeRecipe(UnityEngine.Object data)
        {
            var generator = data as FpsArenaGenerator;
            var settings = data as FpsArenaSettings;
            if (generator == null && settings == null) throw new ArgumentException("FPS 생성기 또는 설정 자산이 필요합니다.", nameof(data));
            var source = generator != null ? generator.recipe : settings.recipe;
            Undo.RecordObject(data, "FPS V2 업그레이드");
            var upgraded = (source ?? new FpsArenaRecipe()).Upgraded();
            if (generator != null) generator.recipe = upgraded; else settings.recipe = upgraded;
            EditorUtility.SetDirty(data);
        }

        internal static void DrawRecipe(SerializedProperty recipe, int tab = -1)
        {
            if (recipe == null) return;
            bool flexible = IsFlexible(recipe);
            if (tab < 0 || tab == GeometryTab)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("지형과 이동", EditorStyles.boldLabel);
                DrawProperty(recipe, "width", "가로 (셀)"); DrawProperty(recipe, "depth", "세로 (셀)");
                var shape = recipe.FindPropertyRelative("shape");
                EditorGUI.BeginChangeCheck();
                int shapeIndex = EditorGUILayout.Popup("외곽 형태", Mathf.Clamp(shape.enumValueIndex, 0, 2), new[] { "직사각형", "타원", "팔각형" });
                if (EditorGUI.EndChangeCheck()) shape.enumValueIndex = shapeIndex;
                DrawProperty(recipe, "floors", "층 수"); DrawProperty(recipe, "cellSize", "셀 크기 (m)");
                DrawProperty(recipe, "floorHeight", "층 간 높이 (m)"); DrawProperty(recipe, "stairsPerFloor", "층 사이 계단 수");
                if(flexible) DrawProperty(recipe,"randomizeStairDirections","시드별 계단 방향 사용");
                EditorGUILayout.HelpBox(flexible ? "계단 위치와 방향을 유효한 후보에서 시드로 선택합니다. 이전 저장 설정은 방향 옵션을 켜고 재생성하세요. 외곽·스폰·양층 착지·다른 계단을 피하며 네 방향을 매번 강제하지 않습니다." : "V1 계단은 앞/뒤 고정 구간을 층마다 교대로 사용합니다. 계단 위치·방향의 시드 변형은 V2 업그레이드 후 적용됩니다.", MessageType.Info);
                EditorGUILayout.LabelField("공통 배치 제한", EditorStyles.boldLabel);
                DrawProperty(recipe, "spacingCells", "콘텐츠 간격 (셀)");
                if (flexible) DrawProperty(recipe, "maxContentPerCategoryPerFloor", "층별·범주별 최대 개수");
                if (flexible)
                {
                    EditorGUILayout.Space(); EditorGUILayout.LabelField("방 구획", EditorStyles.boldLabel);
                    DrawProperty(recipe, "partitionRooms", "방 구획 사용");
                    bool rooms=recipe.FindPropertyRelative("partitionRooms").boolValue;
                    using (new EditorGUI.DisabledScope(!rooms))
                    {
                        DrawProperty(recipe,"roomsPerFloor","층별 방 개수");
                        DrawProperty(recipe,"roomMinWidthCells","최소 방 폭 (셀)"); DrawProperty(recipe,"roomMinAreaCells","최소 방 면적 (셀)");
                        DrawProperty(recipe,"roomDoorWidthCells","방 사이 출입구 폭 (셀)");
                        DrawProperty(recipe,"roomWallHeight","구획벽 높이 (m)"); DrawProperty(recipe,"roomWallThickness","구획벽 두께 (m)");
                    }
                    EditorGUILayout.HelpBox("각 층을 지정한 개수의 방으로 나눕니다. 구획벽은 외곽 또는 기존 벽까지 이어지고 방 사이는 열린 출입구로 연결됩니다. 계단·스폰·최소 방 크기로 요청 개수를 만들 수 없으면 마지막 생성 결과에 실제 개수와 이유를 표시합니다. 문·벽·계단 접근 공간은 콘텐츠가 침범하지 않습니다.",MessageType.Info);
                    showLegacyWallSettings=EditorGUILayout.Foldout(showLegacyWallSettings,"이전 벽 조각 설정 (호환)",true);
                    if(showLegacyWallSettings)
                    {
                        using(new EditorGUI.DisabledScope(rooms))
                        {
                            DrawProperty(recipe,"internalWalls","이전 벽 조각 생성");
                            DrawProperty(recipe,"wallDensity","벽 점유 목표 (0–0.35)");
                            DrawIntRange(recipe,"wallMinLengthCells","wallMaxLengthCells","벽 조각 길이 (셀)",3,16);
                            DrawProperty(recipe,"wallHeight","벽 높이 (m)");DrawProperty(recipe,"wallThickness","벽 두께 (m)");
                            DrawProperty(recipe,"wallDoorWidthCells","출입구 폭 (셀)");DrawProperty(recipe,"maxWallRunsPerFloor","층별 벽 조각 상한");
                        }
                        EditorGUILayout.HelpBox("저장된 이전 설정과 결과를 보존하는 모드입니다. 방 개수를 만들려면 위의 방 구획 사용을 켜세요. 방 구획 중에는 이 값들을 보존하고 사용하지 않습니다.",MessageType.Info);
                    }
                    if(!rooms&&recipe.FindPropertyRelative("internalWalls").boolValue) EditorGUILayout.HelpBox("현재 이전 벽 조각 모드가 활성화되어 있습니다. 방 개수에 따른 구획은 방 구획 사용을 켜야 적용됩니다.",MessageType.Warning);
                }
            }
            if (tab < 0 || tab == CoverTab)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("엄폐", EditorStyles.boldLabel);
                if (flexible)
                {
                    DrawDensity(recipe, "coverDensity", "엄폐 밀도");
                    DrawIntRange(recipe, "coverMinWidthCells", "coverMaxWidthCells", "가로 범위 (셀)", 1, 4);
                    DrawIntRange(recipe, "coverMinDepthCells", "coverMaxDepthCells", "세로 범위 (셀)", 1, 4);
                    DrawFloatRange(recipe, "coverMinHeight", "coverMaxHeight", "높이 범위 (m)", .5f, 2.5f);
                    DrawCoverShapes(recipe);
                    EditorGUILayout.HelpBox("카탈로그에 엄폐 프리팹이 없으면 위 범위에서 가로·세로·높이·형태를 선택합니다. 엄폐 프리팹을 등록하면 해당 항목의 셀 크기·높이·크기 배율을 사용합니다. 모든 점유 셀과 주변 간격을 함께 검사합니다.", MessageType.Info);
                }
                else { DrawProperty(recipe, "coverPerFloor", "층별 엄폐 목표"); DrawProperty(recipe, "coverHeight", "엄폐 높이 (m)"); }
            }
            if (tab < 0 || tab == EnemyTab)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField(flexible ? "적" : "V1 장애물", EditorStyles.boldLabel);
                if (flexible)
                {
                    DrawDensity(recipe, "enemyDensity", "적 밀도");
                    EditorGUILayout.HelpBox("적은 독립 배치 범주입니다. 프리팹 탭에서 적 AI가 작성된 프리팹을 연결하세요. 카탈로그 항목이 없으면 위치 확인용 표식이 생깁니다. 생성기는 적 AI·공격·NavMesh를 자동으로 구현하지 않습니다.", MessageType.Info);
                }
                else
                {
                    DrawProperty(recipe, "obstaclesPerFloor", "층별 장애물 목표"); DrawProperty(recipe, "obstacleHeight", "장애물 높이 (m)");
                    EditorGUILayout.HelpBox("이 값은 기존 충돌 장애물 상자 수입니다. 적 배치와 적 프리팹은 V2 업그레이드 후 사용할 수 있습니다.", MessageType.Info);
                }
            }
            if (tab < 0 || tab == GimmickTab)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("특수 기믹", EditorStyles.boldLabel);
                if (flexible)
                {
                    DrawDensity(recipe, "gimmickDensity", "특수 기믹 밀도");
                    EditorGUILayout.HelpBox("기믹 밀도는 적·엄폐·아이템과 별도입니다. 프리팹 탭에서 함정·상호작용 장치 등 프로젝트의 기믹 프리팹을 연결하세요. 동작과 상태 저장은 제품 코드에서 구현합니다.", MessageType.Info);
                }
                else EditorGUILayout.HelpBox("V1에는 특수 기믹 범주가 없습니다. V2로 업그레이드하면 별도 밀도와 프리팹을 설정할 수 있습니다.", MessageType.Info);
            }
            if (tab < 0 || tab == ItemTab)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("아이템", EditorStyles.boldLabel);
                if (flexible) DrawDensity(recipe, "itemDensity", "아이템 밀도"); else DrawProperty(recipe, "itemsPerFloor", "층별 아이템 목표");
                EditorGUILayout.HelpBox(flexible ? "프리팹 탭에서 프로젝트의 픽업 프리팹을 등록합니다. 카탈로그 항목이 없으면 비차단 표식이 생깁니다. 지급·획득·리스폰 규칙은 제품 코드에서 연결합니다." : "V1 아이템은 비차단 trigger 표식입니다. 실제 픽업 프리팹과 밀도 설정은 V2에서 사용합니다.", MessageType.Info);
            }
            if (flexible && tab != PrefabTab)
                EditorGUILayout.HelpBox("밀도 0%는 해당 범주를 배치하지 않습니다. 층별 목표 = 예약 공간을 뺀 바닥 셀 수 × 밀도(올림), 공통 최대 개수 적용. 넓은 프리팹·엄폐, 간격과 안전 통로 때문에 실제 개수는 목표보다 적을 수 있습니다.", MessageType.Info);
        }

        private static void DrawProperty(SerializedProperty parent, string field, string label)
        {
            var property = parent.FindPropertyRelative(field);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private static void DrawDensity(SerializedProperty recipe, string field, string label)
        {
            var property = recipe.FindPropertyRelative(field);
            EditorGUI.BeginChangeCheck();
            float percentage = EditorGUILayout.Slider(new GUIContent(label + " (%)", "예약 공간을 제외한 층별 바닥 셀 수에 대한 배치 목표 비율입니다."), property.floatValue * 100f, 0f, 100f);
            if (EditorGUI.EndChangeCheck()) property.floatValue = percentage / 100f;
        }

        private static void DrawIntRange(SerializedProperty parent, string minField, string maxField, string label, int minimum, int maximum)
        {
            var min = parent.FindPropertyRelative(minField); var max = parent.FindPropertyRelative(maxField);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                int low = EditorGUILayout.IntField(min.intValue);
                GUILayout.Label("~", GUILayout.Width(12)); int high = EditorGUILayout.IntField(max.intValue);
                if (EditorGUI.EndChangeCheck()) { min.intValue = Mathf.Clamp(low, minimum, maximum); max.intValue = Mathf.Clamp(high, min.intValue, maximum); }
            }
        }

        private static void DrawFloatRange(SerializedProperty parent, string minField, string maxField, string label, float minimum, float maximum)
        {
            var min = parent.FindPropertyRelative(minField); var max = parent.FindPropertyRelative(maxField);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                float low = EditorGUILayout.FloatField(min.floatValue);
                GUILayout.Label("~", GUILayout.Width(12)); float high = EditorGUILayout.FloatField(max.floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    min.floatValue = float.IsNaN(low) || float.IsInfinity(low) ? minimum : Mathf.Clamp(low, minimum, maximum);
                    max.floatValue = float.IsNaN(high) || float.IsInfinity(high) ? min.floatValue : Mathf.Clamp(high, min.floatValue, maximum);
                }
            }
        }

        private static void DrawCoverShapes(SerializedProperty recipe)
        {
            var property = recipe.FindPropertyRelative("coverShapes"); var flags = (FpsArenaCoverShapes)property.intValue;
            EditorGUILayout.LabelField("기본 엄폐 형태", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool box = EditorGUILayout.Toggle("상자", (flags & FpsArenaCoverShapes.Box) != 0);
            bool cylinder = EditorGUILayout.Toggle("원기둥", (flags & FpsArenaCoverShapes.Cylinder) != 0);
            bool corner = EditorGUILayout.Toggle("모서리 (L자)", (flags & FpsArenaCoverShapes.Corner) != 0);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = (box ? (int)FpsArenaCoverShapes.Box : 0) | (cylinder ? (int)FpsArenaCoverShapes.Cylinder : 0) | (corner ? (int)FpsArenaCoverShapes.Corner : 0);
            }
            if (property.intValue == 0) EditorGUILayout.HelpBox("형태를 하나 이상 선택하세요. 아무 형태도 선택하지 않으면 정규화 시 상자를 사용합니다.", MessageType.Warning);
        }

        private static void DrawPresets(FpsArenaGenerator generator, bool flexible)
        {
            using (new EditorGUI.DisabledScope(!flexible))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("결투 프리셋")) ApplyPreset(generator, FpsArenaPreset.Duel);
                if (GUILayout.Button("팀전 프리셋")) ApplyPreset(generator, FpsArenaPreset.Team);
                if (GUILayout.Button("다층 프리셋")) ApplyPreset(generator, FpsArenaPreset.Vertical);
            }
        }

        private static void ApplyPreset(FpsArenaGenerator generator, FpsArenaPreset preset)
        {
            UnityEngine.Object data = generator.settings != null ? (UnityEngine.Object)generator.settings : generator;
            Undo.RecordObject(data, "FPS 프리셋");
            if (generator.settings != null) generator.settings.recipe = FpsArenaRecipe.Preset(preset); else generator.recipe = FpsArenaRecipe.Preset(preset);
            EditorUtility.SetDirty(data);
        }

        private static void SaveSettings(FpsArenaGenerator generator)
        {
            string path = EditorUtility.SaveFilePanelInProject("FPS 설정 저장", "FpsArenaSettings", "asset", "시드·정규화 레시피·콘텐츠 카탈로그 연결을 저장합니다.");
            if (string.IsNullOrEmpty(path)) return;
            var asset = ScriptableObject.CreateInstance<FpsArenaSettings>();
            asset.seed = generator.settings != null ? generator.settings.seed : generator.seed;
            asset.recipe = (generator.settings != null ? generator.settings.recipe : generator.recipe).Normalized();
            asset.contentCatalog = generator.ActiveCatalog;
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets();
            Undo.RecordObject(generator, "FPS 설정 연결"); generator.settings = asset; EditorUtility.SetDirty(generator);
        }

        private static void DrawCatalogConnection(SerializedObject source, bool flexible)
        {
            var property = source.FindProperty("contentCatalog");
            if (property == null) return;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("실제 게임 프리팹", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!flexible))
            {
                EditorGUILayout.PropertyField(property, new GUIContent("콘텐츠 카탈로그"));
                if (GUILayout.Button("새 콘텐츠 카탈로그 만들기"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("FPS 카탈로그 저장", "FpsArenaContentCatalog", "asset", "게임의 엄폐·적·기믹·아이템 프리팹을 등록할 자산입니다.");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var catalog = ScriptableObject.CreateInstance<FpsArenaContentCatalog>();
                        AssetDatabase.CreateAsset(catalog, path); AssetDatabase.SaveAssets(); property.objectReferenceValue = catalog;
                    }
                }
            }
            if (!flexible) EditorGUILayout.HelpBox("V1은 기존 상자·아이템 표식을 유지하며 카탈로그를 사용하지 않습니다. V2 업그레이드 후 연결할 수 있습니다.", MessageType.Info);
        }

        internal static void DrawCatalog(FpsArenaContentCatalog catalog, bool enabled = true)
        {
            if (catalog == null)
            {
                if (enabled) EditorGUILayout.HelpBox("카탈로그 없이도 기본 엄폐와 적·기믹·아이템 표식으로 생성됩니다. 실제 게임 오브젝트를 쓰려면 카탈로그를 만들고 프리팹을 등록하세요.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox("카탈로그 자산을 직접 편집합니다. 이 자산을 공유하는 다른 설정에도 적용되며 Undo를 지원합니다. 같은 범주에 여러 프리팹을 등록하면 가중치로 선택합니다. 프리팹에 작성된 게임 컴포넌트를 사용하고, 초기화가 필요하면 IFpsArenaContentInitializer를 구현합니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!enabled))
            {
                var so = new SerializedObject(catalog); so.Update(); var entries = so.FindProperty("entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i); var key = entry.FindPropertyRelative("contentKey");
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, string.IsNullOrEmpty(key.stringValue) ? "항목 " + (i + 1) : key.stringValue, true);
                            if (GUILayout.Button("삭제", GUILayout.Width(45))) { entries.DeleteArrayElementAtIndex(i); break; }
                        }
                        if (!entry.isExpanded) continue;
                        EditorGUILayout.PropertyField(key, new GUIContent("고유 콘텐츠 키", "앞뒤 공백 없이 입력합니다. 대소문자를 구분하며 카탈로그 전체에서 고유해야 합니다."));
                        var kind = entry.FindPropertyRelative("kind"); int selected = Array.IndexOf(CatalogKinds, (FpsArenaContentKind)kind.intValue);
                        EditorGUI.BeginChangeCheck(); int next = EditorGUILayout.Popup("범주", selected, CatalogKindLabels);
                        if (EditorGUI.EndChangeCheck() && next >= 0) kind.intValue = (int)CatalogKinds[next];
                        if (selected < 0) EditorGUILayout.HelpBox("이 범주는 V2에서 지원하지 않습니다. 엄폐·적·특수 기믹·아이템 중 하나를 선택하세요.", MessageType.Error);
                        var prefab = entry.FindPropertyRelative("prefab");
                        EditorGUI.BeginChangeCheck();
                        var value = (GameObject)EditorGUILayout.ObjectField("프리팹", prefab.objectReferenceValue, typeof(GameObject), false);
                        if (EditorGUI.EndChangeCheck()) prefab.objectReferenceValue = value;
                        DrawProperty(entry, "weight", "선택 가중치"); DrawProperty(entry, "widthCells", "가로 점유 (셀)"); DrawProperty(entry, "depthCells", "세로 점유 (셀)");
                        DrawProperty(entry, "height", "배치 높이 (m)");
                        DrawFloatRange(entry, "minSizeFactor", "maxSizeFactor", "크기 배율 범위", .25f, 1f);
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("authoredBounds"), new GUIContent("원본 Bounds 크기", "프리팹 루트 로컬 좌표 기준의 실제 가로·높이·세로 크기입니다. 비율을 보존하는 균일 스케일로 배치 공간 안에 맞춥니다."));
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("authoredBoundsCenter"), new GUIContent("원본 Bounds 중심", "프리팹 루트 로컬 좌표 기준 중심입니다. 루트 피벗이 중앙 또는 바닥 어디에 있든 배치 공간에 맞춰 보정합니다."));
                        using (new EditorGUI.DisabledScope(prefab.objectReferenceValue == null))
                        {
                            if (GUILayout.Button("프리팹에서 Bounds 읽기"))
                            {
                                Vector3 bounds, center;
                                if (TryMeasurePrefabBounds((GameObject)prefab.objectReferenceValue, out bounds, out center))
                                { entry.FindPropertyRelative("authoredBounds").vector3Value = bounds; entry.FindPropertyRelative("authoredBoundsCenter").vector3Value = center; }
                                else EditorUtility.DisplayDialog("Bounds를 읽을 수 없습니다", "Renderer 또는 지원되는 Collider가 없거나 크기가 0입니다. 원본 Bounds 크기와 중심을 직접 입력하세요.", "확인");
                            }
                        }
                    }
                }
                if (GUILayout.Button("프리팹 항목 추가"))
                {
                    int index = entries.arraySize; entries.InsertArrayElementAtIndex(index); var entry = entries.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("contentKey").stringValue = ""; entry.FindPropertyRelative("kind").intValue = (int)FpsArenaContentKind.Enemy;
                    entry.FindPropertyRelative("prefab").objectReferenceValue = null; entry.FindPropertyRelative("weight").floatValue = 1;
                    entry.FindPropertyRelative("widthCells").intValue = 1; entry.FindPropertyRelative("depthCells").intValue = 1;
                    entry.FindPropertyRelative("height").floatValue = 1.8f; entry.FindPropertyRelative("minSizeFactor").floatValue = .8f;
                    entry.FindPropertyRelative("maxSizeFactor").floatValue = 1; entry.FindPropertyRelative("authoredBounds").vector3Value = new Vector3(1, 2, 1);
                    entry.FindPropertyRelative("authoredBoundsCenter").vector3Value = new Vector3(0, 1, 0); entry.isExpanded = true;
                }
                if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(catalog);
            }
            string error = enabled ? CatalogValidationMessage(catalog) : "";
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        }

        internal static string CatalogValidationMessage(FpsArenaContentCatalog catalog)
        {
            if (catalog == null || catalog.entries == null) return "";
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                var entry = catalog.entries[i]; string label = "카탈로그 항목 " + (i + 1) + ": ";
                if (entry == null || string.IsNullOrWhiteSpace(entry.contentKey) || entry.contentKey != entry.contentKey.Trim()) return label + "앞뒤 공백 없는 고유 콘텐츠 키를 입력하세요.";
                if (!keys.Add(entry.contentKey)) return label + "콘텐츠 키 '" + entry.contentKey + "'가 중복됩니다.";
                if (Array.IndexOf(CatalogKinds, entry.kind) < 0) return label + "지원되는 범주를 선택하세요. V1 장애물 범주는 카탈로그에서 사용할 수 없습니다.";
                if (entry.prefab == null) return label + "실제 프리팹을 연결하세요.";
                if (!PrefabUtility.IsPartOfPrefabAsset(entry.prefab)) return label + "장면 오브젝트 대신 프로젝트의 프리팹 자산을 연결하세요.";
                if (!FinitePositive(entry.weight)) return label + "선택 가중치는 유한한 양수여야 합니다.";
                if (entry.widthCells < 1 || entry.widthCells > 4 || entry.depthCells < 1 || entry.depthCells > 4 || !FinitePositive(entry.height) || entry.height > 6) return label + "가로·세로는 1–4셀, 배치 높이는 0m 초과–6m여야 합니다.";
                if (!FinitePositive(entry.minSizeFactor) || !FinitePositive(entry.maxSizeFactor) || entry.minSizeFactor < .25f || entry.maxSizeFactor > 1 || entry.minSizeFactor > entry.maxSizeFactor) return label + "크기 배율은 0.25–1 범위에서 최소 ≤ 최대여야 합니다.";
                if (!FinitePositive(entry.authoredBounds.x) || !FinitePositive(entry.authoredBounds.y) || !FinitePositive(entry.authoredBounds.z)) return label + "원본 Bounds 크기의 모든 축은 유한한 양수여야 합니다.";
                if (!Finite(entry.authoredBoundsCenter.x) || !Finite(entry.authoredBoundsCenter.y) || !Finite(entry.authoredBoundsCenter.z)) return label + "원본 Bounds 중심의 모든 축은 유한한 값이어야 합니다.";
            }
            return "";
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool FinitePositive(float value) { return value > 0 && Finite(value); }

        internal static bool TryMeasurePrefabBounds(GameObject prefab, out Vector3 size, out Vector3 center)
        {
            size = Vector3.zero; center = Vector3.zero; if (prefab == null) return false;
            bool found = false; Bounds bounds = default(Bounds); var toRoot = prefab.transform.worldToLocalMatrix;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                EncapsulateBounds(renderer.localBounds, toRoot * renderer.transform.localToWorldMatrix, ref bounds, ref found);
            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                var matrix = toRoot * collider.transform.localToWorldMatrix; Bounds measured;
                if (TryGetRoundedColliderBounds(collider, matrix, out measured)) EncapsulateBounds(measured, Matrix4x4.identity, ref bounds, ref found);
                else if (TryGetColliderBounds(collider, out measured)) EncapsulateBounds(measured, matrix, ref bounds, ref found);
            }
            if (!found || !FinitePositive(bounds.size.x) || !FinitePositive(bounds.size.y) || !FinitePositive(bounds.size.z) || !Finite(bounds.center.x) || !Finite(bounds.center.y) || !Finite(bounds.center.z)) return false;
            size = bounds.size; center = bounds.center; return true;
        }

        private static bool TryGetRoundedColliderBounds(Collider collider, Matrix4x4 matrix, out Bounds bounds)
        {
            // Physics spheres keep a spherical radius using the largest axis. Capsules
            // scale radius by the largest perpendicular axis, then clamp total height.
            // This handles ordinary TRS hierarchies; arbitrary parent-induced shear is
            // an authoring caveat rather than an exact physics decomposition contract.
            Vector3 x = matrix.MultiplyVector(Vector3.right), y = matrix.MultiplyVector(Vector3.up), z = matrix.MultiplyVector(Vector3.forward);
            var scales = new Vector3(x.magnitude, y.magnitude, z.magnitude);
            var sphere = collider as SphereCollider;
            if (sphere != null)
            {
                float radius = sphere.radius * Mathf.Max(scales.x, Mathf.Max(scales.y, scales.z));
                bounds = new Bounds(matrix.MultiplyPoint3x4(sphere.center), Vector3.one * radius * 2); return true;
            }
            var capsule = collider as CapsuleCollider; var controller = collider as CharacterController;
            if (capsule != null || controller != null)
            {
                int direction = capsule != null ? capsule.direction : 1;
                Vector3 center = capsule != null ? capsule.center : controller.center;
                float radius = capsule != null ? capsule.radius : controller.radius;
                float height = capsule != null ? capsule.height : controller.height;
                radius *= Mathf.Max(scales[(direction + 1) % 3], scales[(direction + 2) % 3]);
                height = Mathf.Max(height * scales[direction], radius * 2);
                Vector3 axis = direction == 0 ? x : direction == 1 ? y : z; axis = axis.normalized;
                Vector3 halfSegment = axis * (height * .5f - radius);
                Vector3 extents = new Vector3(Mathf.Abs(halfSegment.x), Mathf.Abs(halfSegment.y), Mathf.Abs(halfSegment.z)) + Vector3.one * radius;
                bounds = new Bounds(matrix.MultiplyPoint3x4(center), extents * 2); return true;
            }
            bounds = default(Bounds); return false;
        }

        private static bool TryGetColliderBounds(Collider collider, out Bounds bounds)
        {
            var box = collider as BoxCollider; if (box != null) { bounds = new Bounds(box.center, box.size); return true; }
            var mesh = collider as MeshCollider; if (mesh != null && mesh.sharedMesh != null) { bounds = mesh.sharedMesh.bounds; return true; }
            bounds = default(Bounds); return false;
        }

        private static void EncapsulateBounds(Bounds local, Matrix4x4 matrix, ref Bounds total, ref bool found)
        {
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z)));
                if (!found) { total = new Bounds(point, Vector3.zero); found = true; } else total.Encapsulate(point);
            }
        }

        private static string StairDirectionLabel(FpsArenaStairDirection direction)
        { return direction==FpsArenaStairDirection.Right?"오른쪽 +X (90°)":direction==FpsArenaStairDirection.Backward?"후방 −Z (180°)":direction==FpsArenaStairDirection.Left?"왼쪽 −X (270°)":"전방 +Z (0°)"; }

        internal static void DrawResult(FpsArenaLayout layout, int tab = -1)
        {
            if (layout == null) return;
            EditorGUILayout.Space(); EditorGUILayout.LabelField("마지막 생성 결과", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("구조", "V" + layout.GeneratorVersion + " · " + layout.Recipe.floors + "층 · 계단 " + layout.Stairs.Count + " · 바닥 " + layout.FloorCount() + "셀");
            if (tab < 0 || tab == GeometryTab)
            {
                foreach(var stair in layout.Stairs)
                    EditorGUILayout.LabelField((stair.lowerFloor+1)+"→"+(stair.lowerFloor+2)+"층 계단",StairDirectionLabel(stair.direction)+" · 시작 "+stair.Bottom+" · 도착 "+stair.Top);
                foreach(var directionReport in layout.StairDirectionReports)
                {
                    string directions="";for(int q=0;q<4;q++)if((directionReport.validDirectionsMask&(1<<q))!=0)directions+=(directions.Length==0?"":" / ")+StairDirectionLabel((FpsArenaStairDirection)q);
                    EditorGUILayout.LabelField((directionReport.lowerFloor+1)+"→"+(directionReport.lowerFloor+2)+"층 유효 방향",directions+" · 후보 "+directionReport.candidates);
                    if(!string.IsNullOrEmpty(directionReport.reason))EditorGUILayout.HelpBox(directionReport.reason,MessageType.Info);
                }
                if(layout.RoomReports.Count>0)
                {
                    foreach(var report in layout.RoomReports)
                    {
                        EditorGUILayout.LabelField((report.floor+1)+"층 방 실제 / 요청",report.actual+" / "+report.requested);
                        if(!string.IsNullOrEmpty(report.reason)) EditorGUILayout.HelpBox(report.reason,MessageType.Warning);
                        int first=-1;foreach(var room in layout.Rooms) if(room.floor==report.floor) { if(first<0)first=room.index;EditorGUILayout.LabelField("방 "+(room.index-first+1),room.cellCount+"셀 · "+(room.cellCount*layout.Recipe.cellSize*layout.Recipe.cellSize).ToString("0")+"m²"); }
                        foreach(var edge in layout.RoomConnections) if(layout.Rooms[edge.roomA].floor==report.floor) EditorGUILayout.LabelField("출입구 연결","방 "+(edge.roomA-first+1)+" ↔ 방 "+(edge.roomB-first+1)+" · 폭 "+(layout.Recipe.roomDoorWidthCells*layout.Recipe.cellSize).ToString("0.#")+"m");
                    }
                }
                else if(layout.Recipe.internalWalls)
                {
                    EditorGUILayout.LabelField("이전 벽 조각 전체",layout.Walls.Count+"개");
                    for(int f=0;f<layout.Recipe.floors;f++)
                    { int runs=0,cells=0;foreach(var wall in layout.Walls)if(wall.cell.y==f){runs++;cells+=wall.lengthCells-wall.doorWidthCells;} EditorGUILayout.LabelField((f+1)+"층 벽 조각",runs+"개 · 점유 "+cells+" / 목표 "+layout.RequestedWallCells(f)+"셀"); }
                }
            }
            bool legacy = layout.GeneratorVersion == (int)FpsArenaGeneratorVersion.LegacyV1;
            var kinds = legacy ? new[] { FpsArenaContentKind.Cover, FpsArenaContentKind.Obstacle, FpsArenaContentKind.Item } : CatalogKinds;
            var relevant = tab == CoverTab ? FpsArenaContentKind.Cover : tab == EnemyTab ? (legacy ? FpsArenaContentKind.Obstacle : FpsArenaContentKind.Enemy) : tab == GimmickTab ? FpsArenaContentKind.Gimmick : tab == ItemTab ? FpsArenaContentKind.Item : (FpsArenaContentKind)(-1);
            foreach (var kind in kinds)
            {
                if ((int)relevant >= 0 && relevant != kind) continue;
                EditorGUILayout.LabelField(KindLabel(kind) + " 전체 실제 개수", CountContent(layout, -1, kind).ToString());
            }
            for (int floor = 0; floor < layout.Recipe.floors; floor++)
            {
                EditorGUILayout.LabelField((floor + 1) + "층 실제 / 목표", EditorStyles.boldLabel);
                foreach (var kind in kinds)
                {
                    if ((int)relevant >= 0 && relevant != kind) continue;
                    EditorGUILayout.LabelField(KindLabel(kind), CountContent(layout, floor, kind) + " / " + layout.RequestedCount(floor, kind));
                }
                if (legacy && tab == GimmickTab) EditorGUILayout.LabelField("특수 기믹", "V1에서는 지원하지 않음");
            }
            EditorGUILayout.LabelField("배치 해시"); EditorGUILayout.SelectableLabel(layout.Hash ?? "", GUILayout.Height(20));
            EditorGUILayout.HelpBox("실제/목표는 마지막 생성 결과 기준입니다. 설정 변경은 재생성 후 반영됩니다. 목표를 채우기 위해 안전 통로·계단·스폰·콘텐츠 간격을 침범하지 않습니다.", MessageType.Info);
        }

        private static int CountContent(FpsArenaLayout layout, int floor, FpsArenaContentKind kind)
        {
            int count = 0; foreach (var content in layout.Content) if (content.kind == kind && (floor < 0 || content.cell.y == floor)) count++; return count;
        }
        private static string KindLabel(FpsArenaContentKind kind)
        {
            switch (kind)
            {
                case FpsArenaContentKind.Cover: return "엄폐";
                case FpsArenaContentKind.Obstacle: return "V1 장애물";
                case FpsArenaContentKind.Enemy: return "적";
                case FpsArenaContentKind.Gimmick: return "특수 기믹";
                default: return "아이템";
            }
        }

        public static void GenerateWithUndo(FpsArenaGenerator generator)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            if (Application.isPlaying) { generator.Generate(); return; }
            FpsArenaLayout layout = generator.Plan();
            GameObject candidate = FpsArenaSceneBuilder.Build(layout, generator.transform, generator.ActiveCatalog);
            int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("FPS 아레나 생성");
            Undo.RegisterCreatedObjectUndo(candidate, "FPS 아레나 생성");
            for (int i = generator.transform.childCount - 1; i >= 0; i--)
            {
                var child = generator.transform.GetChild(i).gameObject;
                if (child != candidate && child.name == FpsArenaGenerator.GeneratedRootName) Undo.DestroyObjectImmediate(child);
            }
            Undo.RegisterCompleteObjectUndo(generator, "FPS 생성 메타데이터");
            generator.ReplaceGenerated(candidate, layout); EditorUtility.SetDirty(generator);
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene); Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = generator.gameObject;
        }

        [MenuItem("Tools/Rogue Dungeon Lab/내부 벽 FPS 예제 장면 만들기", priority = 3)]
        public static void CreateWallExample() { var window = GetWindow<FpsArenaWindow>("FPS 아레나"); window.CreateTestScene(true); window.Show(); }

        [MenuItem("Tools/Rogue Dungeon Lab/방 구획 FPS 예제 장면 만들기", priority = 3)]
        public static void CreateRoomExample() { var window=GetWindow<FpsArenaWindow>("FPS 아레나");window.CreateTestScene(false,true);window.Show(); }

        private void CreateTestScene(bool walls = false, bool rooms = false)
        {
            if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("FPS Arena"); target = root.AddComponent<FpsArenaGenerator>();
            if (walls) { target.recipe.internalWalls = true; target.recipe.width = target.recipe.depth = 32; target.recipe.spacingCells = 1; }
            if(rooms) { target.recipe.partitionRooms=true;target.recipe.width=target.recipe.depth=32;target.recipe.roomsPerFloor=4;target.recipe.spacingCells=1; }
            var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var player = new GameObject("FPS Test Player"); var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .35f; cc.center = new Vector3(0, .9f, 0); cc.stepOffset = .3f; cc.slopeLimit = 50; cc.skinWidth = .04f;
            var view = new GameObject("FPS Camera").AddComponent<Camera>(); view.tag = "MainCamera"; view.transform.SetParent(player.transform, false); view.transform.localPosition = new Vector3(0, 1.65f, 0); view.farClipPlane = 600;
            view.gameObject.AddComponent<AudioListener>(); var controller = player.AddComponent<FpsArenaPlayer>(); controller.arena = target; controller.view = view;
            GenerateWithUndo(target); player.transform.position = target.CurrentLayout.Position(target.CurrentLayout.Spawns[0]) + Vector3.up * .05f;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, new Vector3(90, 20, 90)), false);
        }
    }

    [CustomEditor(typeof(FpsArenaGenerator))]
    public sealed class FpsArenaGeneratorEditor : UnityEditor.Editor
    {
        private int selectedTab;
        private void OnSceneGUI()
        {
            var generator=(FpsArenaGenerator)target;var layout=generator.CurrentLayout;
            if(layout==null||layout.Rooms.Count==0)return;
            foreach(var room in layout.Rooms)
            {
                int number=0;foreach(var r in layout.Rooms)if(r.floor==room.floor&&r.index<=room.index)number++;
                Handles.Label(generator.transform.TransformPoint(layout.Position(room.anchor)+Vector3.up*.2f),(room.floor+1)+"층 · 방 "+number+" ("+room.cellCount+"셀)");
            }
            Handles.color=Color.cyan;
            foreach(var door in layout.RoomDoors)
            {
                Vector3 p=generator.transform.TransformPoint(door.Position(layout)+Vector3.up*.25f);
                Vector3 a=generator.transform.TransformPoint(layout.Position(layout.Rooms[door.roomA].anchor)+Vector3.up*.25f);
                Vector3 b=generator.transform.TransformPoint(layout.Position(layout.Rooms[door.roomB].anchor)+Vector3.up*.25f);
                Handles.DrawLine(a,p);Handles.DrawLine(p,b);Handles.Label(p,"출입구 "+(door.widthCells*layout.Recipe.cellSize).ToString("0.#")+"m");
            }
        }
        public override void OnInspectorGUI()
        {
            var generator = (FpsArenaGenerator)target;
            selectedTab = FpsArenaWindow.DrawTabs(selectedTab); FpsArenaWindow.DrawSettings(generator, selectedTab);
            if (GUILayout.Button("FPS 아레나 생성")) FpsArenaWindow.GenerateWithUndo(generator);
            FpsArenaWindow.DrawResult(generator.CurrentLayout, selectedTab);
        }
    }
    [CustomEditor(typeof(FpsArenaSettings))]
    public sealed class FpsArenaSettingsEditor : UnityEditor.Editor
    {
        private int selectedTab;
        public override void OnInspectorGUI()
        {
            selectedTab = FpsArenaWindow.DrawTabs(selectedTab);
            FpsArenaWindow.DrawSettingsAsset((FpsArenaSettings)target, serializedObject, selectedTab);
        }
    }
    [CustomEditor(typeof(FpsArenaContentCatalog))]
    public sealed class FpsArenaContentCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() { FpsArenaWindow.DrawCatalog((FpsArenaContentCatalog)target); }
    }
}
