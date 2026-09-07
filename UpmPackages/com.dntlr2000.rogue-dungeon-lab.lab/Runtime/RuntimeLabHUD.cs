using System;
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
            if (_generator == null)
            {
                GUILayout.Label("Generator를 먼저 연결하세요.", _warning);
                return;
            }
            if (!_generator.CanEditActiveRuntimeRecipe)
            {
                GUILayout.Label("Saved Blueprint 모드에서는 저장된 논리 맵을 보호하기 위해 구조 설정과 시드를 Play HUD에서 편집할 수 없습니다.", _warning);
                GUILayout.Label("구조를 바꾸려면 에디터의 스테이지 자산 탭에서 제작 레시피를 불러와 새 Blueprint로 저장하세요. 탐험 탭의 재생성은 같은 저장본만 다시 구축합니다.", _muted);
                return;
            }

            RogueDungeonSettings settings = _generator.ActiveRuntimeSettings;
            if (settings == null)
            {
                GUILayout.Label("편집할 절차 레시피가 없습니다. Generator settings 또는 Procedural StageDefinition recipe를 연결하세요.", _warning);
                return;
            }
            settings.ClampValues();
            GUILayout.Label("Play 전용 복제 설정을 편집합니다. 원본 settings와 StageDefinition recipe는 변경되지 않습니다. 슬라이더와 프리셋은 활성 시드를 유지해 자동 재생성되고, 시드 입력만 생성 버튼으로 확정합니다.", _muted);
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
            if (GUILayout.Button("활성 시드 재생성")) RegenerateActiveSeedImmediately();
            if (GUILayout.Button("새 시드로 생성")) GenerateNewSeedAndSync(settings);
            GUILayout.EndHorizontal();

            bool previousGuiChanged = GUI.changed;
            GUI.changed = false;
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
            bool liveSettingsChanged = GUI.changed;
            GUI.changed = previousGuiChanged || liveSettingsChanged;
            if (liveSettingsChanged) RequestLiveRegeneration();
            GUILayout.Label("입구→출구 진행도 곡선은 Unity 에디터의 분포 탭에서 계속 편집할 수 있습니다.", _muted);
        }

        // Compact, Balanced와 Chaos 프리셋을 현재 런타임 설정에 불러옵니다.
        private void DrawPresetButtons(RogueDungeonSettings settings)
        {
            GUILayout.Space(8f);
            GUILayout.Label("프리셋");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Compact"))
            {
                settings.ApplyPreset(DungeonPreset.Compact);
                RequestLiveRegeneration();
            }
            if (GUILayout.Button("Balanced"))
            {
                settings.ApplyPreset(DungeonPreset.Balanced);
                RequestLiveRegeneration();
            }
            if (GUILayout.Button("Chaos"))
            {
                settings.ApplyPreset(DungeonPreset.Chaos);
                RequestLiveRegeneration();
            }
            GUILayout.EndHorizontal();
        }

        // 현재 HUD 설정을 유효 범위로 보정하고 입력한 시드로 던전을 생성합니다.
        private void ApplySettingsAndGenerate(RogueDungeonSettings settings, int seed)
        {
            settings.seed = seed;
            settings.ClampValues();
            _seedText = settings.seed.ToString();
            _generator.GenerateActiveRecipeWithSeed(settings.seed);
            CompleteImmediateGeneration();
        }

        // 새 무작위 시드로 생성한 뒤 해당 값을 설정과 HUD 입력란에 동기화합니다.
        private void GenerateNewSeedAndSync(RogueDungeonSettings settings)
        {
            _generator.GenerateNewSeed();
            settings.seed = _generator.ActiveSeed;
            _seedText = settings.seed.ToString();
            CompleteImmediateGeneration();
        }

        // 수동 버튼으로 현재 활성 시드를 즉시 재생성하고 대기 중인 자동 요청을 정리합니다.
        private void RegenerateActiveSeedImmediately()
        {
            _generator.RegenerateActiveSeed();
            CompleteImmediateGeneration();
        }

        // 슬라이더 드래그에서 발생한 여러 변경을 다음 Update의 자동 재생성 요청 하나로 합칩니다.
        private void RequestLiveRegeneration()
        {
            if (_generator == null ||
                !_generator.CanEditActiveRuntimeRecipe ||
                _generator.ActiveRuntimeSettings == null)
            {
                return;
            }
            _liveRegenerationPending = true;
        }

        // 대기 중인 설정 변경을 짧은 제한 주기로 활성 시드에 적용해 드래그 중 결과를 갱신합니다.
        private void ProcessLiveRegeneration()
        {
            if (!_liveRegenerationPending) return;
            if (_generator == null ||
                !_generator.CanEditActiveRuntimeRecipe ||
                _generator.ActiveRuntimeSettings == null)
            {
                _liveRegenerationPending = false;
                return;
            }
            if (Time.unscaledTime < _nextLiveRegenerationTime) return;

            _liveRegenerationPending = false;
            _generator.ActiveRuntimeSettings.ClampValues();
            _generator.RegenerateActiveSeed();
            _nextLiveRegenerationTime = Time.unscaledTime + LiveRegenerationInterval;
        }

        // 즉시 생성 후 중복 자동 생성을 막고 다음 실시간 생성 가능 시점을 갱신합니다.
        private void CompleteImmediateGeneration()
        {
            _liveRegenerationPending = false;
            _nextLiveRegenerationTime = Time.unscaledTime + LiveRegenerationInterval;
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
            if (GUILayout.Button("현재 시드 재생성")) RegenerateActiveSeedImmediately();
            if (GUILayout.Button("새 시드"))
            {
                RogueDungeonSettings activeSettings = _generator.ActiveRuntimeSettings;
                if (_generator.CanEditActiveRuntimeRecipe && activeSettings != null)
                    GenerateNewSeedAndSync(activeSettings);
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

        // 현재 플레이 진행의 캡처·슬롯 저장·재구축 복원·삭제 UI를 그립니다.
        private void DrawRunStateTab()
        {
            GUILayout.Label("런 상태 저장과 복원", _header);
            if (_generator == null ||
                _generator.CurrentStageInstance == null)
            {
                GUILayout.Label(
                    "먼저 스테이지를 생성하거나 불러오세요.",
                    _warning);
                return;
            }

            DungeonRunState state =
                _generator.ActiveRunState;
            if (state != null)
            {
                GUILayout.Label(
                    "Stage ID: " +
                    Abbreviate(state.stageId, 42),
                    _muted);
                GUILayout.Label(
                    string.Format(
                        "출처 {0} · Seed {1} · 제거 {2} · 기믹 상태 {3}",
                        state.sourceMode,
                        state.runSeed,
                        state.removedSpawnIds != null
                            ? state.removedSpawnIds.Count
                            : 0,
                        state.gimmickStates != null
                            ? state.gimmickStates.Count
                            : 0),
                    _muted);
                GUILayout.Label(
                    "Final hash: " +
                    Abbreviate(
                        state.finalBlueprintHash,
                        20),
                    _muted);
            }

            DungeonRunStateApplyResult applyResult =
                _generator.CurrentStageInstance
                    .RunStateApplyResult;
            if (applyResult != null &&
                applyResult.WasApplied)
            {
                string mode = applyResult.WasMigrated
                    ? "migration"
                    : applyResult.WasBestEffort
                        ? "matching-ID"
                        : "정확 일치";
                GUILayout.Label(
                    string.Format(
                        "최근 복원: {0} · 제거 {1} · 기믹 {2}",
                        mode,
                        applyResult.RemovedSpawnCount,
                        applyResult
                            .RestoredGimmickStateCount),
                    _muted);
            }

            GUILayout.Space(8f);
            GUILayout.Label("저장 슬롯", _header);
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                "슬롯 ID",
                GUILayout.Width(80f));
            _runStateSlot = GUILayout.TextField(
                _runStateSlot ?? string.Empty);
            GUILayout.EndHorizontal();
            GUILayout.Label(
                "영문·숫자·-·_ 조합 1~64자",
                _muted);

            GUILayout.Space(6f);
            GUILayout.Label("Blueprint 불일치 정책");
            _runStatePolicyIndex = GUILayout.Toolbar(
                Mathf.Clamp(_runStatePolicyIndex, 0, 1),
                new[]
                {
                    "엄격 거부",
                    "일치 ID만"
                });
            GUILayout.Label(
                _runStatePolicyIndex == 0
                    ? "stage·출처·seed·final hash가 모두 같아야 복원합니다."
                    : "stage·출처·seed는 같아야 하며, final hash가 다르면 존재하는 stable ID만 재결합합니다.",
                _muted);

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("현재 상태 캡처"))
                CaptureRunStateFromHud();
            if (GUILayout.Button("슬롯 저장"))
                SaveRunStateFromHud();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("슬롯 불러오기"))
                LoadRunStateFromHud();
            if (GUILayout.Button("슬롯 삭제"))
                DeleteRunStateFromHud();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(
                    _runStateMessage))
            {
                GUILayout.Space(6f);
                GUILayout.Label(
                    _runStateMessage,
                    _runStateMessageIsError
                        ? _warning
                        : _muted);
            }
        }

        // 현재 participant와 임시 플레이어 pose를 메모리 RunState에 캡처합니다.
        private void CaptureRunStateFromHud()
        {
            try
            {
                DungeonRunState state =
                    _generator.CaptureCurrentRunState(
                        PrototypePlayerController.Active);
                SetRunStateMessage(
                    string.Format(
                        "캡처 완료: 제거 {0}, 기믹 {1}, 플레이어 {2}",
                        state.removedSpawnIds.Count,
                        state.gimmickStates.Count,
                        state.player != null &&
                        state.player.isPresent
                            ? "포함"
                            : "없음"),
                    false);
            }
            catch (Exception exception)
            {
                SetRunStateMessage(
                    "캡처 실패: " + exception.Message,
                    true);
            }
        }

        // 현재 진행을 캡처해 입력한 JSON 슬롯에 저장합니다.
        private void SaveRunStateFromHud()
        {
            try
            {
                DungeonRunState state =
                    _generator.SaveRunState(
                        _runStateSlot,
                        PrototypePlayerController.Active);
                SetRunStateMessage(
                    "저장 완료: " +
                    new DateTime(
                        state.savedUtcTicks,
                        DateTimeKind.Utc)
                        .ToLocalTime()
                        .ToString("yyyy-MM-dd HH:mm:ss"),
                    false);
            }
            catch (Exception exception)
            {
                SetRunStateMessage(
                    "저장 실패: " + exception.Message,
                    true);
            }
        }

        // 입력 슬롯의 seed와 상태로 stage를 재구축하고 검증된 진행을 적용합니다.
        private void LoadRunStateFromHud()
        {
            try
            {
                DungeonRunStateHashMismatchPolicy policy =
                    _runStatePolicyIndex == 0
                        ? DungeonRunStateHashMismatchPolicy
                            .Reject
                        : DungeonRunStateHashMismatchPolicy
                            .ApplyMatchingSpawnIds;
                bool loaded = _generator.LoadRunState(
                    _runStateSlot,
                    policy);
                if (!loaded)
                {
                    SetRunStateMessage(
                        "해당 슬롯이 없습니다.",
                        true);
                    return;
                }
                _seedText =
                    _generator.ActiveSeed.ToString();
                CompleteImmediateGeneration();
                SetRunStateMessage(
                    "불러오기 및 스테이지 복원을 완료했습니다.",
                    false);
            }
            catch (Exception exception)
            {
                SetRunStateMessage(
                    "불러오기 실패: " +
                    exception.Message,
                    true);
            }
        }

        // 입력 슬롯의 RunState 파일을 삭제합니다.
        private void DeleteRunStateFromHud()
        {
            try
            {
                bool deleted =
                    _generator.DeleteRunState(
                        _runStateSlot);
                SetRunStateMessage(
                    deleted
                        ? "슬롯을 삭제했습니다."
                        : "삭제할 슬롯이 없습니다.",
                    !deleted);
            }
            catch (Exception exception)
            {
                SetRunStateMessage(
                    "삭제 실패: " + exception.Message,
                    true);
            }
        }

        // HUD의 최근 RunState 작업 결과와 오류 표시 상태를 갱신합니다.
        private void SetRunStateMessage(
            string message,
            bool isError)
        {
            _runStateMessage = message ?? string.Empty;
            _runStateMessageIsError = isError;
        }

        // 긴 stage ID와 hash를 앞부분만 남긴 단일 줄 문자열로 줄입니다.
        private static string Abbreviate(
            string value,
            int maximumLength)
        {
            string text = value ?? string.Empty;
            return text.Length <= maximumLength
                ? text
                : text.Substring(
                    0,
                    Mathf.Max(1, maximumLength - 1)) +
                  "…";
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
