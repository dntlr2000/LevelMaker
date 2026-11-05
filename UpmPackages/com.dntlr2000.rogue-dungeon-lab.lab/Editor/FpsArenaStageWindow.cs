using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    public sealed partial class FpsArenaStageWindow : EditorWindow
    {
        private static readonly string[] Tools = { "선택·이동", "바닥 칠하기", "바닥 지우기", "벽 그리기", "문 뚫기", "시작 스폰", "콘텐츠 배치", "계단 배치", "오브젝트 삭제" };
        private static readonly FpsArenaContentKind[] Kinds = { FpsArenaContentKind.Cover, FpsArenaContentKind.Enemy, FpsArenaContentKind.Gimmick, FpsArenaContentKind.Item };
        private static readonly string[] KindNames = { "구조물·엄폐", "적", "기믹", "아이템" };
        [SerializeField] private FpsArenaStageAsset stage;
        [SerializeField] private FpsArenaGenerator generator;
        [SerializeField] private int floor, tool, kindIndex, direction;
        [SerializeField] private string paletteKey = "", selectedId = "";
        [SerializeField] private int selectedStair = -1;
        [SerializeField] private int newWidth = 16, newDepth = 16, newFloors = 1;
        [SerializeField] private float zoom = 28;
        private Vector2 gridScroll, sideScroll;
        private string validation = "", connectivityWarning = "", message = "", prefabKey = "";
        private GameObject prefab;
        private bool needsValidation = true, catalogExpanded;
        private HashSet<Vector3Int> floorCells = new HashSet<Vector3Int>();
        private Vector3Int lastPaint = new Vector3Int(-1, -1, -1), dragStart, dragEnd;
        private bool dragging, dragMove, lastPaintAlongX;
        private int strokeGroup;
        private GUIStyle cellLabel;

        [MenuItem("Tools/Rogue Dungeon Lab/스테이지 직접 설계", priority = 1)]
        public static void Open() { Open(null); }
        public static void Open(FpsArenaStageAsset asset)
        {
            var window = GetWindow<FpsArenaStageWindow>("스테이지 직접 설계");
            window.minSize = new Vector2(850, 600);
            if (asset != null) { window.stage = asset; window.Invalidate(); }
            window.Show();
        }
        [OnOpenAsset]
#pragma warning disable CS0618 // Unity 6.5 still invokes the legacy int callback.
        private static bool OpenAsset(int id, int line)
        { var asset = EditorUtility.EntityIdToObject(id) as FpsArenaStageAsset; if (asset == null) return false; Open(asset); return true; }
#pragma warning restore CS0618
        private void OnEnable() { Undo.undoRedoPerformed += Invalidate; Invalidate(); }
        private void OnDisable() { Undo.undoRedoPerformed -= Invalidate; }
        private void OnFocus() { Invalidate(); }
        private void OnProjectChange() { Invalidate(); }
        // 자산/Undo 변경 후 검사와 규격 후보만 무효화하며 미적용 입력은 유지한다.
        private void Invalidate() { needsValidation = true; resizePreview = null; Repaint(); }
        private void Run(Action action)
        { try { action(); message = ""; Invalidate(); } catch (Exception e) { message = e.Message; Repaint(); } }
        private void Change(string label, Action<FpsArenaStageData> edit) { Run(() => FpsArenaStageAuthoring.Edit(stage, label, edit)); }

        // 구형 자산은 명시적으로 변환한 뒤 편집하며 Play 중 원본 자산 변경을 막는다.
        private void OnGUI()
        {
            if (cellLabel == null) cellLabel = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            EditorGUILayout.LabelField("스테이지 직접 설계", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. 빈 판/생성 결과에서 시작   →   2. 층별로 설계   →   3. 저장·검사·장면 적용", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                DrawHeader();
                if (stage == null)
                {
                    EditorGUILayout.HelpBox("새 빈 스테이지를 만들거나 저장된 자산을 위 칸에 넣으세요. 수동 제작은 시드와 생성 설정 없이 사용할 수 있습니다.", MessageType.Info);
                    DrawNewSize(); return;
                }
                if (stage.data == null) { EditorGUILayout.HelpBox("스테이지 데이터가 없습니다.", MessageType.Error); return; }
                if (stage.data.version == 1)
                {
                    EditorGUILayout.HelpBox("구형 v1 자산입니다. 기존 외곽 가드를 편집 가능한 벽으로 변환하면 격자 편집을 계속할 수 있습니다. 기존 외형은 유지되지만 수동 배치 해시는 v2로 바뀝니다. 변환은 Undo 가능하며 자동 저장하지 않습니다.", MessageType.Info);
                    if (GUILayout.Button("편집형 벽으로 변환 (Undo 가능)")) Run(() => FpsArenaStageAuthoring.UpgradeWalls(stage));
                    if (stage.data.version == 1) { if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Error); return; }
                }
                SyncSizingState(); Check();
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
                    {
                        sideScroll = EditorGUILayout.BeginScrollView(sideScroll);
                        float previousLabelWidth = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 115;
                        DrawSidebar(); EditorGUIUtility.labelWidth = previousLabelWidth;
                        EditorGUILayout.EndScrollView();
                    }
                    using (new EditorGUILayout.VerticalScope()) { DrawCanvas(); }
                }
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorGUILayout.HelpBox("Play 실행 중에는 수동 자산 편집을 잠급니다. 종료하면 편집을 계속할 수 있습니다.", MessageType.Info);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        private void DrawHeader()
        {
            EditorGUI.BeginChangeCheck();
            var next = (FpsArenaStageAsset)EditorGUILayout.ObjectField("수동 스테이지", stage, typeof(FpsArenaStageAsset), false);
            if (EditorGUI.EndChangeCheck()) { stage = next; floor = 0; selectedId = ""; selectedStair = -1; Invalidate(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("새 빈 스테이지")) Create(false);
                using (new EditorGUI.DisabledScope(stage == null))
                {
                    if (GUILayout.Button("저장")) Run(() => { AssetDatabase.SaveAssetIfDirty(stage); if (stage.contentCatalog != null) AssetDatabase.SaveAssetIfDirty(stage.contentCatalog); ShowNotification(new GUIContent("스테이지 저장 완료")); });
                    if (GUILayout.Button("다른 이름으로 저장")) Create(true);
                }
                if (GUILayout.Button("실행 취소")) Undo.PerformUndo();
                if (GUILayout.Button("다시 실행")) Undo.PerformRedo();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                generator = (FpsArenaGenerator)EditorGUILayout.ObjectField("생성 결과 가져오기", generator, typeof(FpsArenaGenerator), true);
                if (GUILayout.Button("선택 연결", GUILayout.Width(75))) generator = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<FpsArenaGenerator>() : null;
                using (new EditorGUI.DisabledScope(generator == null || generator.CurrentLayout == null))
                    if (GUILayout.Button("새 수동 자산으로 복사", GUILayout.Width(150))) Capture();
            }
        }
        private void Capture()
        {
            string path = EditorUtility.SaveFilePanelInProject("생성 결과 복사", "ManualStage", "asset", "원본 생성 결과를 보존하고 복사합니다.");
            if (!string.IsNullOrEmpty(path)) Run(() => { stage = FpsArenaStageAuthoring.CreateAsset(path, FpsArenaStageData.Capture(generator.CurrentLayout), generator.ActiveCatalog); floor = 0; selectedId = ""; selectedStair = -1; });
        }
        // 새 자산의 셀/층간 실제 규격을 현재 맵 설정과 구분하여 입력한다.
        private void DrawNewSize()
        {
            if (stage != null) { newSizeExpanded = EditorGUILayout.Foldout(newSizeExpanded, "새 스테이지 설정", true); if (!newSizeExpanded) return; }
            else EditorGUILayout.LabelField("새 스테이지 설정", EditorStyles.boldLabel);
            newWidth = EditorGUILayout.IntSlider("가로 (셀)", newWidth, 8, 64); newDepth = EditorGUILayout.IntSlider("세로 (셀)", newDepth, 8, 64); newFloors = EditorGUILayout.IntSlider("층 수", newFloors, 1, 4);
            newCellSize = EditorGUILayout.Slider("셀 크기 (m)", newCellSize, 2, 5);
            newFloorHeight = EditorGUILayout.Slider("층간 높이 (m)", newFloorHeight, 3, 6);
            EditorGUILayout.LabelField($"실제 면적: {newWidth * newCellSize:0.##} × {newDepth * newCellSize:0.##}m", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("이 설정으로 새 빈 스테이지")) Create(false);
        }
        // 새 규격 또는 기존 데이터/제작 기본값 사본으로 별도 자산을 만들며 기존 맵은 덮어쓰지 않는다.
        private void Create(bool copy)
        {
            string path = EditorUtility.SaveFilePanelInProject(copy ? "스테이지 사본 저장" : "새 수동 스테이지", "ManualStage", "asset", "스테이지 원본을 저장할 위치를 선택하세요.");
            if (string.IsNullOrEmpty(path)) return;
            Run(() => { var source = stage; stage = FpsArenaStageAuthoring.CreateAsset(path, copy ? source.data : FpsArenaStageData.Create(newWidth, newDepth, newFloors, newCellSize, newFloorHeight), copy ? source.contentCatalog : null, copy ? source.wallDefaults : null); floor = 0; selectedId = ""; selectedStair = -1; Selection.activeObject = stage; });
        }
        // 필수 오류와 비차단 연결성 경고를 분리하고 재검사 시 이전 결과를 비운다.
        private void Check()
        {
            if (!needsValidation) return; needsValidation = false;
            floorCells = new HashSet<Vector3Int>(stage.data.floorCells);
            validation = ""; connectivityWarning = "";
            try { connectivityWarning = FpsArenaManualValidation.GetConnectivityWarning(stage.CreateLayout()); }
            catch (Exception e) { validation = e.Message; }
        }
        // 필수 오류만 장면 적용을 잠그고 연결성 부족은 별도 경고로 안내한다.
        private void DrawSidebar()
        {
            DrawCurrentSize();
            tool = GUILayout.SelectionGrid(tool, Tools, 2, GUILayout.Height(125));
            EditorGUILayout.Space();
            if (tool == 6)
            {
                int previousKind = kindIndex; kindIndex = EditorGUILayout.Popup("범주", kindIndex, KindNames);
                if (kindIndex != previousKind) paletteKey = "";
                paletteKey = DrawPalette("배치할 콘텐츠", Kinds[kindIndex], paletteKey);
                EditorGUILayout.HelpBox("격자를 클릭해 배치합니다. 선택·이동 도구로 드래그하거나 속성에서 위치와 크기를 바꿉니다.", MessageType.None);
            }
            if (tool == 7)
            {
                direction = EditorGUILayout.Popup("올라가는 방향", direction, new[] { "위쪽 (+Z)", "오른쪽 (+X)", "아래쪽 (−Z)", "왼쪽 (−X)" });
                EditorGUILayout.HelpBox("폭 2셀·길이 4셀 계단입니다. 상층 바닥을 자동으로 뚫고, 계단 삭제 시 복원합니다.", MessageType.None);
            }
            if (tool == 3 || tool == 4) EditorGUILayout.HelpBox("셀의 상·하·좌·우 경계 가까이를 클릭·드래그하세요. 외곽에도 벽을 놓거나 문 뚫기로 열린 구간을 만들 수 있습니다.", MessageType.None);
            if (GUILayout.Button("벽 자동 배치 (전체 층)")) Run(() => FpsArenaStageAuthoring.AutoPlaceBoundaryWalls(stage));
            EditorGUILayout.HelpBox("전체 층의 외곽과 내부 구멍에서 빈 경계만 채웁니다. 계단은 제외하며 기존 벽은 유지합니다. 삭제한 벽은 이 버튼을 다시 누를 때만 채워집니다.", MessageType.None);
            DrawSelection();
            DrawWallDefaults(); DrawWallBatch();
            EditorGUILayout.Space(); EditorGUILayout.LabelField("검사와 장면", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(validation) ? "계단·겹침·콘텐츠 연결 필수 검사 통과" : validation, string.IsNullOrEmpty(validation) ? MessageType.Info : MessageType.Error);
            if (!string.IsNullOrEmpty(connectivityWarning)) EditorGUILayout.HelpBox(connectivityWarning, MessageType.Warning);
            if (GUILayout.Button("다시 검사")) Invalidate();
            generator = (FpsArenaGenerator)EditorGUILayout.ObjectField("적용할 생성기", generator, typeof(FpsArenaGenerator), true);
            if (GUILayout.Button("선택한 생성기 연결")) { generator = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<FpsArenaGenerator>() : null; }
            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(validation)))
            {
                if (GUILayout.Button("검사 후 장면에 적용", GUILayout.Height(30))) Run(() => { generator = FpsArenaStageAuthoring.Apply(stage, generator); SceneView.lastActiveSceneView?.Frame(new Bounds(generator.transform.position, new Vector3(stage.data.width * stage.data.cellSize, stage.data.floors * stage.data.floorHeight, stage.data.depth * stage.data.cellSize)), false); });
            }
            using (new EditorGUI.DisabledScope(generator == null || generator.CurrentLayout == null))
            {
                if (GUILayout.Button("테스트 플레이어 연결")) Run(AddTestPlayer);
            }
            EditorGUILayout.HelpBox("장면 적용 후 Unity의 Play 버튼으로 확인하세요. 장면 저장은 Ctrl+S입니다. 스테이지 자산은 위의 저장 버튼을 사용하세요. 검사 전 초안도 저장할 수 있습니다.", MessageType.None);
            DrawCatalog();
            DrawNewSize();
        }
        private string DrawPalette(string label, FpsArenaContentKind kind, string key)
        {
            var keys = new List<string> { "" }; var names = new List<string> { "기본 표식" };
            if (stage.contentCatalog != null) foreach (var entry in stage.contentCatalog.entries) if (entry != null && entry.kind == kind) { keys.Add(entry.contentKey); names.Add(entry.contentKey); }
            int index = keys.IndexOf(key);
            if (index < 0) { keys.Add(key); names.Add("연결 필요: " + key); index = keys.Count - 1; }
            return keys[EditorGUILayout.Popup(label, index, names.ToArray())];
        }
        private void DrawCatalog()
        {
            catalogExpanded = EditorGUILayout.Foldout(catalogExpanded, "프로젝트 프리팹 등록·교체", true);
            if (!catalogExpanded) return;
            var so = new SerializedObject(stage); so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("contentCatalog"), new GUIContent("콘텐츠 카탈로그"));
            EditorGUILayout.PropertyField(so.FindProperty("authoringNote"), new GUIContent("제작 메모"));
            if (so.ApplyModifiedProperties()) { EditorUtility.SetDirty(stage); Invalidate(); }
            if (stage.contentCatalog == null && GUILayout.Button("새 카탈로그 만들기"))
            {
                string path = EditorUtility.SaveFilePanelInProject("콘텐츠 카탈로그", "StageCatalog", "asset", "프리팹 매핑을 저장합니다.");
                if (!string.IsNullOrEmpty(path)) Run(() => { var catalog = ScriptableObject.CreateInstance<FpsArenaContentCatalog>(); AssetDatabase.CreateAsset(catalog, path); var assetSo = new SerializedObject(stage); assetSo.FindProperty("contentCatalog").objectReferenceValue = catalog; assetSo.ApplyModifiedProperties(); EditorUtility.SetDirty(stage); });
            }
            using (new EditorGUI.DisabledScope(stage.contentCatalog == null))
            {
                kindIndex = EditorGUILayout.Popup("등록 범주", kindIndex, KindNames);
                prefabKey = EditorGUILayout.TextField("콘텐츠 키", prefabKey);
                prefab = (GameObject)EditorGUILayout.ObjectField("프로젝트 프리팹", prefab, typeof(GameObject), false);
                if (GUILayout.Button("이 키의 프리팹 등록·교체")) Run(() => { FpsArenaStageAuthoring.RegisterPrefab(stage.contentCatalog, prefabKey, Kinds[kindIndex], prefab); paletteKey = prefabKey; });
                if (GUILayout.Button("카탈로그 상세 설정")) Selection.activeObject = stage.contentCatalog;
            }
            EditorGUILayout.HelpBox("같은 키의 프리팹을 바꾸면 그 키의 모든 배치가 다음 장면 적용에서 교체됩니다. Bounds는 자동 측정하며 게임 스크립트는 프리팹에 그대로 보존됩니다.", MessageType.None);
        }
        private void DrawSelection()
        {
            var p = stage.data.content.Find(c => c.id == selectedId);
            if (p != null)
            {
                EditorGUILayout.LabelField("선택한 콘텐츠", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("범주", p.kind == FpsArenaContentKind.Obstacle ? "기존 장애물" : KindNames[Mathf.Max(0, Array.IndexOf(Kinds, p.kind))]);
                EditorGUI.BeginChangeCheck();
                var cell = p.cell; cell.x = EditorGUILayout.IntField("가로 X", cell.x); cell.z = EditorGUILayout.IntField("세로 Z", cell.z); cell.y = EditorGUILayout.IntField("층 (1부터)", cell.y + 1) - 1;
                int width = EditorGUILayout.IntSlider("점유 가로", p.widthCells, 1, 4), depth = EditorGUILayout.IntSlider("점유 세로", p.depthCells, 1, 4);
                Vector3 size = EditorGUILayout.Vector3Field("크기 (m: 가로·높이·세로)", p.size);
                float yaw = EditorGUILayout.Slider("회전 (도)", p.yaw, -180, 180);
                string key = DrawPalette("콘텐츠 교체", p.kind, p.contentKey);
                if (EditorGUI.EndChangeCheck()) Change("콘텐츠 설정", d => { var c = d.content.Find(v => v.id == selectedId); c.cell = cell; c.widthCells = width; c.depthCells = depth; c.size = size; c.yaw = yaw; c.contentKey = key; });
                if (GUILayout.Button("선택 삭제 (Delete)")) DeleteSelection();
            }
            else if (selectedStair >= 0 && selectedStair < stage.data.stairs.Count)
            {
                EditorGUILayout.LabelField("선택한 계단", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("이동·방향 변경은 계단을 지우고 원하는 위치/방향으로 다시 놓으세요. 삭제 시 위층 바닥도 복원됩니다.", MessageType.None);
                if (GUILayout.Button("계단 삭제·개구부 복원")) DeleteSelection();
            }
            else if (stage.data.partitions.Exists(w => w.id == selectedId) || stage.data.walls.Exists(w => w.id == selectedId))
            {
                EditorGUILayout.LabelField("벽 선택됨", EditorStyles.boldLabel);
                DrawSelectedWallDimensions();
                var wall = stage.data.walls.Find(w => w.id == selectedId);
                if (wall != null)
                {
                    EditorGUI.BeginChangeCheck();
                    int offset = EditorGUILayout.IntSlider("문 시작 (셀)", wall.doorOffsetCells, 0, wall.lengthCells - 1);
                    int width = EditorGUILayout.IntSlider("문 폭 (0=닫힘)", wall.doorWidthCells, 0, wall.lengthCells - offset);
                    if (EditorGUI.EndChangeCheck()) Change("벽 출입구 편집", d => { var w = d.walls.Find(v => v.id == selectedId); w.doorOffsetCells = offset; w.doorWidthCells = width; });
                }
                if (GUILayout.Button("선택한 벽 삭제")) DeleteSelection();
            }
        }

        // 네 외곽선이 잘리거나 클릭 불가능해지지 않도록 격자에 편집 여백을 둔다.
        private void DrawCanvas()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string[] floors = new string[stage.data.floors]; for (int i = 0; i < floors.Length; i++) floors[i] = (i + 1) + "층";
                floor = EditorGUILayout.Popup(Mathf.Clamp(floor, 0, floors.Length - 1), floors, GUILayout.Width(80));
                GUILayout.Label("확대", GUILayout.Width(30)); zoom = GUILayout.HorizontalSlider(zoom, 14, 42);
                if (GUILayout.Button("맞춤", GUILayout.Width(48))) zoom = Mathf.Clamp((position.width - 350) / stage.data.width, 14, 42);
            }
            EditorGUILayout.LabelField("드래그: 칠하기/콘텐츠 이동 · Ctrl+Z: 취소 · Delete: 선택 삭제 · 색상: 녹색 구조물 / 빨강 적 / 보라 기믹 / 청록 아이템", EditorStyles.wordWrappedMiniLabel);
            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.ExpandHeight(true));
            Rect padded = GUILayoutUtility.GetRect(stage.data.width * zoom + 16, stage.data.depth * zoom + 16, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            Rect area = new Rect(padded.x + 8, padded.y + 8, stage.data.width * zoom, stage.data.depth * zoom);
            EditorGUI.DrawRect(area, new Color(.1f, .12f, .15f));
            for (int z = 0; z < stage.data.depth; z++) for (int x = 0; x < stage.data.width; x++)
            {
                var c = new Vector3Int(x, floor, z); Rect rect = CellRect(area, c);
                if (floorCells.Contains(c)) EditorGUI.DrawRect(Inset(rect, 1), new Color(.28f, .34f, .39f));
                if (zoom >= 24 && x % 4 == 0 && z % 4 == 0) GUI.Label(rect, x + "," + z, EditorStyles.centeredGreyMiniLabel);
            }
            foreach (var p in stage.data.content) if (p.cell.y == floor)
            {
                Rect rect = CellRect(area, p.cell); rect.y -= (p.depthCells - 1) * zoom; rect.width = p.widthCells * zoom; rect.height = p.depthCells * zoom;
                EditorGUI.DrawRect(Inset(rect, 3), p.id == selectedId ? new Color(.95f, .7f, .12f) : p.kind == FpsArenaContentKind.Enemy ? new Color(.7f, .22f, .22f) : p.kind == FpsArenaContentKind.Gimmick ? new Color(.5f, .3f, .65f) : p.kind == FpsArenaContentKind.Item ? new Color(.1f, .6f, .65f) : new Color(.2f, .5f, .3f));
                GUI.Label(rect, p.kind == FpsArenaContentKind.Enemy ? "적" : p.kind == FpsArenaContentKind.Gimmick ? "기" : p.kind == FpsArenaContentKind.Item ? "템" : "구", cellLabel);
            }
            for (int i = 0; i < stage.data.stairs.Count; i++)
            {
                var s = stage.data.stairs[i]; if (s.lowerFloor != floor && s.lowerFloor + 1 != floor) continue;
                Rect rect = CellRect(area, new Vector3Int(s.x, floor, s.z)); rect.y -= (s.FootprintDepth - 1) * zoom; rect.width = s.FootprintWidth * zoom; rect.height = s.FootprintDepth * zoom;
                EditorGUI.DrawRect(Inset(rect, 3), i == selectedStair ? new Color(.95f, .7f, .12f) : new Color(.3f, .5f, .7f));
                GUI.Label(rect, new[] { "↑", "→", "↓", "←" }[(int)s.direction] + (floor == s.lowerFloor ? " 계단" : " 개구부"), cellLabel);
            }
            foreach (var w in stage.data.partitions) if (w.cell.y == floor)
            {
                Rect rect = CellRect(area, w.cell); if (w.alongX) rect = new Rect(rect.x, rect.y - 2, zoom * w.lengthCells, 4); else rect = new Rect(rect.xMax - 2, rect.y - zoom * (w.lengthCells - 1), 4, zoom * w.lengthCells);
                EditorGUI.DrawRect(rect, w.id == selectedId ? Color.yellow : new Color(.9f, .85f, .7f));
            }
            foreach (var w in stage.data.walls) if (w.cell.y == floor) foreach (var c in w.Footprint()) EditorGUI.DrawRect(Inset(CellRect(area, c), 4), w.id == selectedId ? Color.yellow : new Color(.8f, .7f, .5f));
            for (int i = 0; i < stage.data.spawns.Count; i++) if (stage.data.spawns[i].y == floor) GUI.Label(CellRect(area, stage.data.spawns[i]), i == 0 ? "시작" : "S", cellLabel);
            HandleInput(area);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField($"{stage.data.width} × {stage.data.depth}셀 · {stage.data.floors}층 · 콘텐츠 {stage.data.content.Count}개 · 바닥 {stage.data.floorCells.Count}셀", EditorStyles.miniLabel);
        }
        private Rect CellRect(Rect area, Vector3Int cell) { return new Rect(area.x + cell.x * zoom, area.y + (stage.data.depth - 1 - cell.z) * zoom, zoom, zoom); }
        private static Rect Inset(Rect r, float inset) { return new Rect(r.x + inset, r.y + inset, r.width - 2 * inset, r.height - 2 * inset); }
        // 외곽의 여백에서도 벽 경계를 선택/칠할 수 있으며 콘텐츠 이동은 기존 격자 안으로 제한한다.
        private void HandleInput(Rect area)
        {
            var e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete && !EditorGUIUtility.editingTextField) { DeleteSelection(); e.Use(); return; }
            Vector3Int cell = new Vector3Int(Mathf.FloorToInt((e.mousePosition.x - area.x) / zoom), floor, stage.data.depth - 1 - Mathf.FloorToInt((e.mousePosition.y - area.y) / zoom));
            Rect interaction = (tool == 0 || tool == 3 || tool == 4 || tool == 8) ? Inset(area, -8) : area;
            if (interaction.Contains(e.mousePosition)) { cell.x = Mathf.Clamp(cell.x, 0, stage.data.width - 1); cell.z = Mathf.Clamp(cell.z, 0, stage.data.depth - 1); }
            if (e.type == EventType.MouseDown && e.button == 0 && interaction.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = control; GUIUtility.keyboardControl = 0; dragging = true; lastPaint = new Vector3Int(-1, -1, -1); dragStart = dragEnd = cell;
                Undo.IncrementCurrentGroup(); strokeGroup = Undo.GetCurrentGroup();
                if (tool == 0) { Select(cell, area, e.mousePosition); dragMove = stage.data.content.Exists(p => p.id == selectedId); }
                else { dragMove = false; Paint(cell, area, e.mousePosition); }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseDrag && dragging)
            {
                if (interaction.Contains(e.mousePosition)) { if (area.Contains(e.mousePosition)) dragEnd = cell; if (tool != 0 && tool != 6 && tool != 7) Paint(cell, area, e.mousePosition); }
                e.Use(); Repaint();
            }
            else if (e.type == EventType.MouseUp && dragging)
            {
                if (dragMove && dragEnd != dragStart) Change("콘텐츠 이동", d => { var p = d.content.Find(c => c.id == selectedId); if (p != null) p.cell += dragEnd - dragStart; });
                dragging = false; GUIUtility.hotControl = 0; Undo.CollapseUndoOperations(strokeGroup); e.Use();
            }
        }
        // 그리기와 같은 셀 사각형으로 네 경계를 정규화한다.
        private void WallEdge(Rect area, Vector3Int cell, Vector2 point, out Vector3Int anchor, out bool alongX)
        { var r = CellRect(area, cell); FpsArenaStageAuthoring.ResolveWallEdge(cell, new Vector2((point.x - r.x) / r.width, (point.y - r.y) / r.height), out anchor, out alongX); }

        // 경계 가까이에서는 벽 선택을 우선하여 콘텐츠 옆의 외곽벽도 삭제할 수 있게 한다.
        private void Select(Vector3Int cell, Rect area, Vector2 point)
        {
            selectedStair = -1; selectedId = "";
            WallEdge(area, cell, point, out var anchor, out bool alongX);
            var wall = stage.data.partitions.Find(w => FpsArenaStageAuthoring.IsWallOnEdge(w, anchor, alongX));
            var rect = CellRect(area, cell);
            float edgeDistance = Mathf.Min(Mathf.Abs(point.x - rect.x), Mathf.Abs(point.x - rect.xMax), Mathf.Abs(point.y - rect.y), Mathf.Abs(point.y - rect.yMax));
            if (wall != null && edgeDistance <= Mathf.Min(7, zoom * .25f)) { selectedId = wall.id; return; }
            var content = stage.data.content.Find(p => FpsArenaStageAuthoring.Covers(p, cell));
            if (content != null) { selectedId = content.id; return; }
            for (int i = 0; i < stage.data.stairs.Count; i++)
            { var s = stage.data.stairs[i]; if ((floor == s.lowerFloor || floor == s.lowerFloor + 1) && cell.x >= s.x && cell.x < s.x + s.FootprintWidth && cell.z >= s.z && cell.z < s.z + s.FootprintDepth) { selectedStair = i; return; } }
            if (wall != null) { selectedId = wall.id; return; }
            foreach (var w in stage.data.walls) foreach (var c in w.Footprint()) if (c == cell) { selectedId = w.id; return; }
        }
        // 벽 드래그는 셀이 아닌 정규 경계별로 중복을 막아 같은 셀의 다른 면도 편집한다.
        private void Paint(Vector3Int cell, Rect area, Vector2 point)
        {
            WallEdge(area, cell, point, out var anchor, out bool alongX);
            bool wallTool = tool == 3 || tool == 4 || tool == 8;
            Vector3Int paintCell = wallTool ? anchor : cell;
            if ((lastPaint == paintCell && (!wallTool || lastPaintAlongX == alongX)) || !stage.data.Contains(cell)) return;
            lastPaint = paintCell; lastPaintAlongX = alongX;
            if (tool == 8) { Select(cell, area, point); DeleteSelection(); return; }
            Change("스테이지 " + Tools[tool], d =>
            {
                if (tool == 1 && !d.floorCells.Contains(cell)) d.floorCells.Add(cell);
                if (tool == 2) d.floorCells.Remove(cell);
                if (tool == 3 || tool == 4) FpsArenaStageAuthoring.SetWall(d, anchor, alongX, tool == 3, stage.GetWallDefaults());
                if (tool == 5) { if (d.spawns.Count == 0) d.spawns.Add(cell); else d.spawns[0] = cell; }
                if (tool == 6)
                {
                    if (!d.floorCells.Contains(cell) || d.content.Exists(p => FpsArenaStageAuthoring.Covers(p, cell))) throw new InvalidOperationException("비어 있는 바닥 셀을 선택하세요.");
                    var entry = stage.contentCatalog != null ? stage.contentCatalog.Find(paletteKey) : null;
                    var content = FpsArenaStageAuthoring.MakeContent(d, Kinds[kindIndex], cell, entry); d.content.Add(content); selectedId = content.id; selectedStair = -1;
                }
                if (tool == 7) FpsArenaStageAuthoring.AddStair(d, cell, (FpsArenaStairDirection)direction);
            });
        }
        private void DeleteSelection()
        {
            Change("선택 삭제", d => { d.content.RemoveAll(p => p.id == selectedId); d.partitions.RemoveAll(w => w.id == selectedId); d.walls.RemoveAll(w => w.id == selectedId); if (selectedStair >= 0 && selectedStair < d.stairs.Count) FpsArenaStageAuthoring.RemoveStair(d, selectedStair); });
            selectedId = ""; selectedStair = -1;
        }
        private void AddTestPlayer()
        {
            foreach (var p in FindObjectsByType<FpsArenaPlayer>(FindObjectsInactive.Include)) if (p.arena == generator) { Selection.activeGameObject = p.gameObject; return; }
            var root = new GameObject("FPS Test Player"); Undo.RegisterCreatedObjectUndo(root, "테스트 플레이어 추가");
            var cc = Undo.AddComponent<CharacterController>(root); cc.height = 1.8f; cc.radius = .35f; cc.center = Vector3.up * .9f; cc.stepOffset = .3f;
            var viewObject = new GameObject("FPS Camera"); Undo.RegisterCreatedObjectUndo(viewObject, "테스트 카메라 추가"); viewObject.transform.SetParent(root.transform, false); viewObject.transform.localPosition = Vector3.up * 1.65f;
            var view = Undo.AddComponent<Camera>(viewObject); view.farClipPlane = 600; view.depth = 100;
            if (FindAnyObjectByType<AudioListener>() == null) Undo.AddComponent<AudioListener>(viewObject);
            var player = Undo.AddComponent<FpsArenaPlayer>(root); player.arena = generator; player.view = view;
            root.transform.position = generator.transform.TransformPoint(generator.CurrentLayout.Position(generator.CurrentLayout.Spawns[0])) + Vector3.up * .05f;
            if (FindAnyObjectByType<Light>() == null) { var sunObject = new GameObject("Directional Light"); Undo.RegisterCreatedObjectUndo(sunObject, "테스트 조명 추가"); var sun = Undo.AddComponent<Light>(sunObject); sun.type = LightType.Directional; sun.intensity = 1.5f; sun.transform.rotation = Quaternion.Euler(50, -30, 0); }
            Selection.activeGameObject = root;
        }
    }

    [CustomEditor(typeof(FpsArenaStageAsset))]
    public sealed class FpsArenaStageAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("스테이지 직접 설계 열기", GUILayout.Height(32))) FpsArenaStageWindow.Open((FpsArenaStageAsset)target);
            serializedObject.Update(); EditorGUILayout.PropertyField(serializedObject.FindProperty("contentCatalog"), new GUIContent("콘텐츠 카탈로그")); EditorGUILayout.PropertyField(serializedObject.FindProperty("authoringNote"), new GUIContent("제작 메모"));
            if (serializedObject.ApplyModifiedProperties()) EditorUtility.SetDirty(target);
            var stage = (FpsArenaStageAsset)target;
            if (stage.data != null) EditorGUILayout.LabelField($"{stage.data.width} × {stage.data.depth}셀 / {stage.data.floors}층 / {stage.data.content.Count}개 콘텐츠");
            EditorGUILayout.HelpBox("배치는 직접 설계 창에서 편집합니다. 이 자산을 FPS 생성기의 수동 스테이지에 연결하면 시드 없이 동일한 맵을 로드합니다.", MessageType.Info);
        }
    }
}
