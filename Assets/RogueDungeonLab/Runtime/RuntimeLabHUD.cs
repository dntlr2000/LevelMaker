using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    // Unity가 RuntimeLabHUD 컴포넌트의 MonoScript 자산을 생성하도록 연결합니다.
    public sealed partial class RuntimeLabHUD : MonoBehaviour
    {
        // 현재 조작 모드에 맞는 키보드와 마우스 안내 문구를 반환합니다.
        private static string GetControlHint()
        {
            return PrototypePlayerController.Active == null
                ? "자유 시점: WASD 시선 기준 3D 이동 · Space 상승 · Ctrl 하강 · Shift 가속 · 우클릭 제자리 회전 · 휠 줌 · 중클릭 이동 · 좌클릭 파괴"
                : "캐릭터: WASD 이동 · Shift 달리기 · Space 점프 · R 입구 복귀 · 우클릭 시점 회전 · 좌클릭 파괴";
        }

        // Play 모드에서 시드, 스테이지 구조와 콘텐츠 밀도를 편집하는 탭을 그립니다.
        private void DrawStageSettingsTab()
        {
            GUILayout.Label("실시간 스테이지 설정", _header);
            if (_generator == null || _generator.settings == null)
            {
                GUILayout.Label("Generator와 RogueDungeonSettings를 먼저 연결하세요.", _warning);
                return;
            }

            RogueDungeonSettings settings = _generator.settings;
            settings.ClampValues();
            GUILayout.Label("값을 조절한 뒤 생성 버튼을 누르면 현재 Play 모드 던전에 한 번에 반영됩니다.", _muted);
            DrawPresetButtons(settings);

            GUILayout.Space(8f);
            GUILayout.Label("시드와 생성", _header);
            GUILayout.BeginHorizontal();
            GUILayout.Label("설정 시드", GUILayout.Width(105f));
            _seedText = GUILayout.TextField(_seedText ?? string.Empty);
            GUILayout.EndHorizontal();

            int requestedSeed;
            bool validSeed = int.TryParse(_seedText, out requestedSeed);
            if (!validSeed) GUILayout.Label("시드는 -2,147,483,648~2,147,483,647 범위의 정수여야 합니다.", _warning);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && validSeed;
            if (GUILayout.Button("설정 적용 및 입력 시드로 생성", GUILayout.Height(30f)))
                ApplySettingsAndGenerate(settings, requestedSeed);
            GUI.enabled = previousEnabled;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("활성 시드 재생성")) _generator.RegenerateActiveSeed();
            if (GUILayout.Button("새 시드로 생성")) GenerateNewSeedAndSync(settings);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("스테이지 크기", _header);
            settings.stageWidthCells = DrawIntSlider("가로 셀 수", settings.stageWidthCells, 12, 96);
            settings.stageDepthCells = DrawIntSlider("세로 셀 수", settings.stageDepthCells, 12, 96);
            settings.cellSize = DrawFloatSlider("셀 크기 (m)", settings.cellSize, 1.5f, 6f, "0.0");
            settings.wallHeight = DrawFloatSlider("벽 높이 (m)", settings.wallHeight, 1.5f, 8f, "0.0");

            GUILayout.Space(8f);
            GUILayout.Label("방과 복도", _header);
            settings.desiredRoomCount = DrawIntSlider("목표 방 개수", settings.desiredRoomCount, 2, 40);
            int maximumRoomWidth = Mathf.Max(3, settings.stageWidthCells - 4);
            int maximumRoomDepth = Mathf.Max(3, settings.stageDepthCells - 4);
            int minimumRoomWidth = DrawIntSlider("최소 방 너비", settings.roomSizeMin.x, 3, maximumRoomWidth);
            int minimumRoomDepth = DrawIntSlider("최소 방 깊이", settings.roomSizeMin.y, 3, maximumRoomDepth);
            int maximumSelectedWidth = DrawIntSlider("최대 방 너비", Mathf.Max(settings.roomSizeMax.x, minimumRoomWidth), minimumRoomWidth, maximumRoomWidth);
            int maximumSelectedDepth = DrawIntSlider("최대 방 깊이", Mathf.Max(settings.roomSizeMax.y, minimumRoomDepth), minimumRoomDepth, maximumRoomDepth);
            settings.roomSizeMin = new Vector2Int(minimumRoomWidth, minimumRoomDepth);
            settings.roomSizeMax = new Vector2Int(maximumSelectedWidth, maximumSelectedDepth);
            settings.roomPlacementAttempts = DrawIntSlider("방당 배치 시도", settings.roomPlacementAttempts, 5, 100);
            settings.corridorWidthCells = DrawIntSlider("복도 폭", settings.corridorWidthCells, 1, 4);
            settings.extraConnectionChance = DrawFloatSlider("추가 연결 확률", settings.extraConnectionChance, 0f, 1f, "P0");

            GUILayout.Space(8f);
            GUILayout.Label("콘텐츠 제약", _header);
            settings.specialGimmickCount = DrawIntSlider("특별 기믹 수", settings.specialGimmickCount, 0, 30);
            settings.contentSpacingCells = DrawIntSlider("콘텐츠 최소 간격", settings.contentSpacingCells, 0, 4);
            settings.reservedEntranceRadiusCells = DrawIntSlider("입구 비우기 반경", settings.reservedEntranceRadiusCells, 0, 8);

            GUILayout.Space(8f);
            GUILayout.Label("콘텐츠 밀도", _header);
            DrawDensityProfile("적 캐릭터", settings.enemyProfile);
            DrawDensityProfile("파괴 가능 오브젝트", settings.destructibleProfile);
            DrawDensityProfile("지형지물", settings.propProfile);
            GUILayout.Label("입구→출구 진행도 곡선은 Unity 에디터의 분포 탭에서 계속 편집할 수 있습니다.", _muted);
        }

        // Compact, Balanced와 Chaos 프리셋을 현재 런타임 설정에 불러옵니다.
        private static void DrawPresetButtons(RogueDungeonSettings settings)
        {
            GUILayout.Space(8f);
            GUILayout.Label("프리셋");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Compact")) settings.ApplyPreset(DungeonPreset.Compact);
            if (GUILayout.Button("Balanced")) settings.ApplyPreset(DungeonPreset.Balanced);
            if (GUILayout.Button("Chaos")) settings.ApplyPreset(DungeonPreset.Chaos);
            GUILayout.EndHorizontal();
        }

        // 현재 HUD 설정을 유효 범위로 보정하고 입력한 시드로 던전을 생성합니다.
        private void ApplySettingsAndGenerate(RogueDungeonSettings settings, int seed)
        {
            settings.seed = seed;
            settings.ClampValues();
            _seedText = settings.seed.ToString();
            _generator.GenerateWithSeed(settings.seed);
        }

        // 새 무작위 시드로 생성한 뒤 해당 값을 설정과 HUD 입력란에 동기화합니다.
        private void GenerateNewSeedAndSync(RogueDungeonSettings settings)
        {
            _generator.GenerateNewSeed();
            settings.seed = _generator.ActiveSeed;
            _seedText = settings.seed.ToString();
        }

        // 정수 설정을 현재 값 표시와 함께 반응형 가로 슬라이더로 편집합니다.
        private static int DrawIntSlider(string label, int value, int minimum, int maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(132f));
            float sliderValue = GUILayout.HorizontalSlider(Mathf.Clamp(value, minimum, maximum), minimum, maximum);
            int result = Mathf.RoundToInt(sliderValue);
            GUILayout.Label(result.ToString(), GUILayout.Width(48f));
            GUILayout.EndHorizontal();
            return result;
        }

        // 실수 설정을 지정한 표시 형식과 함께 반응형 가로 슬라이더로 편집합니다.
        private static float DrawFloatSlider(string label, float value, float minimum, float maximum, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(132f));
            float result = GUILayout.HorizontalSlider(Mathf.Clamp(value, minimum, maximum), minimum, maximum);
            GUILayout.Label(result.ToString(format), GUILayout.Width(48f));
            GUILayout.EndHorizontal();
            return result;
        }

        // 한 콘텐츠 종류의 기본 밀도, 방 선호도, 군집도와 최대 개수를 편집합니다.
        private static void DrawDensityProfile(string label, DensityProfile profile)
        {
            if (profile == null) return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(label);
            profile.baseDensity = DrawFloatSlider("기본 셀 밀도", profile.baseDensity, 0f, 0.5f, "P1");
            profile.roomBias = DrawFloatSlider("방 선호도", profile.roomBias, 0f, 1f, "P0");
            profile.clustering = DrawFloatSlider("군집도", profile.clustering, 0f, 1f, "P0");
            profile.maxCount = DrawIntSlider("최대 개수 (0=무제한)", profile.maxCount, 0, 500);
            GUILayout.EndVertical();
        }

        // 자유 카메라, 임시 플레이어와 최근 생성 결과를 관리하는 탐험 탭을 그립니다.
        private void DrawExplorationTab()
        {
            GUILayout.Label(GetControlHint(), _muted);
            GUILayout.Space(6f);
            if (_generator == null)
            {
                GUILayout.Label("Generator가 없습니다.", _warning);
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("현재 시드 재생성")) _generator.RegenerateActiveSeed();
            if (GUILayout.Button("새 시드"))
            {
                if (_generator.settings != null) GenerateNewSeedAndSync(_generator.settings);
                else _generator.GenerateNewSeed();
            }
            GUILayout.EndHorizontal();
            DrawExplorationControls();
            DrawGenerationReport();
        }

        // Play HUD에서 임시 캐릭터의 생성과 제거 버튼 및 현재 조작법을 표시합니다.
        private void DrawExplorationControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label("던전 탐험", _header);
            if (_generator == null || _generator.CurrentLayout == null)
            {
                GUILayout.Label("던전을 먼저 생성하세요.", _warning);
                return;
            }

            if (PrototypePlayerController.Active == null)
            {
                if (GUILayout.Button("임시 플레이어 생성 (WASD)", GUILayout.Height(30f)))
                    PrototypePlayerController.Spawn(_generator);
                GUILayout.Label("자유 시점: WASD 이동 · Space 상승 · Ctrl 하강 · Shift 가속", _muted);
                return;
            }

            if (GUILayout.Button("임시 플레이어 제거 / 자유 시점", GUILayout.Height(30f)))
                PrototypePlayerController.DestroyActive();
            GUILayout.Label("캐릭터: WASD 이동 · Shift 달리기 · Space 점프 · R 입구 복귀", _muted);
        }

        // 가장 최근 던전 생성 시간, 규모, 콘텐츠 개수와 경고를 표시합니다.
        private void DrawGenerationReport()
        {
            GUILayout.Space(8f);
            GUILayout.Label("생성 결과", _header);
            if (_generator == null || _generator.LastReport == null)
            {
                GUILayout.Label("아직 생성되지 않았습니다.", _muted);
                return;
            }

            GenerationReport report = _generator.LastReport;
            GUILayout.Label(string.Format(
                "Seed {0} · {1:0.0} ms · 방 {2} · 셀 {3} · 삼각형 {4:N0}",
                report.activeSeed,
                report.generationMilliseconds,
                report.roomCount,
                report.floorCellCount,
                report.meshTriangleCount));
            GUILayout.Label(string.Format(
                "적 {0} · 파괴물 {1} · 지형지물 {2} · 기믹 {3}",
                report.enemyCount,
                report.destructibleCount,
                report.propCount,
                report.gimmickCount));
            for (int i = 0; i < report.warnings.Count; i++)
                GUILayout.Label("⚠ " + report.warnings[i], _warning);
        }

        // 빠른 드랍 표본 생성, 통계 초기화와 관측 결과를 표시하는 탭을 그립니다.
        private void DrawDropStatisticsTab()
        {
            if (_generator == null || _service == null)
            {
                GUILayout.Label("Generator와 DropValidationService를 먼저 연결하세요.", _warning);
                return;
            }

            GUILayout.Label("빠른 몬테카를로", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("적 +100")) _service.Simulate(DropSourceKind.Enemy, _generator.GetEffectiveDropTable(DropSourceKind.Enemy), 100);
            if (GUILayout.Button("적 +1,000")) _service.Simulate(DropSourceKind.Enemy, _generator.GetEffectiveDropTable(DropSourceKind.Enemy), 1000);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("파괴물 +100")) _service.Simulate(DropSourceKind.Destructible, _generator.GetEffectiveDropTable(DropSourceKind.Destructible), 100);
            if (GUILayout.Button("파괴물 +1,000")) _service.Simulate(DropSourceKind.Destructible, _generator.GetEffectiveDropTable(DropSourceKind.Destructible), 1000);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("모든 드랍 통계 초기화")) _service.ResetStatistics();

            GUILayout.Space(8f);
            GUILayout.Label("드랍 통계", _header);
            List<DropSourceStatisticsSnapshot> snapshots = _service.GetSnapshots();
            if (snapshots.Count == 0)
                GUILayout.Label("대상을 클릭하거나 빠른 표본을 추가하세요.", _muted);
            for (int sourceIndex = 0; sourceIndex < snapshots.Count; sourceIndex++)
            {
                DropSourceStatisticsSnapshot source = snapshots[sourceIndex];
                GUILayout.Label(string.Format(
                    "{0} / {1} — {2:N0}회",
                    source.SourceKind == DropSourceKind.Enemy ? "적" : "파괴물",
                    source.TableName,
                    source.Attempts));
                for (int entryIndex = 0; entryIndex < source.Entries.Count; entryIndex++)
                {
                    DropEntryStatisticsSnapshot entry = source.Entries[entryIndex];
                    float delta = entry.ObservedProbability - entry.ExpectedProbability;
                    GUILayout.Label(string.Format(
                        "{0}: 기대 {1:P1} · 관측 {2:P1} · Δ {3:+0.0%;-0.0%;0.0%} · 95% [{4:P1}, {5:P1}]",
                        entry.ItemId,
                        entry.ExpectedProbability,
                        entry.ObservedProbability,
                        delta,
                        entry.WilsonLow95,
                        entry.WilsonHigh95), _muted);
                }
            }
        }
    }
}
