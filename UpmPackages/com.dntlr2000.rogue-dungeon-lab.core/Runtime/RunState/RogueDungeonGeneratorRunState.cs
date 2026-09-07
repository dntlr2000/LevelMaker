using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public sealed partial class RogueDungeonGenerator :
        MonoBehaviour
    {
        private IDungeonRunStateStore _runStateStore;
        private DungeonRunState _activeRunState;
        private IDungeonRunStatePlayer _runStatePlayer;

        public event Action<DungeonRunState> RunStateChanged;

        public DungeonRunState ActiveRunState
        {
            get
            {
                return _activeRunState != null
                    ? _activeRunState.DeepClone()
                    : null;
            }
        }

        public IDungeonRunStateStore RunStateStore
        {
            get
            {
                if (_runStateStore == null)
                    _runStateStore =
                        new JsonFileDungeonRunStateStore();
                return _runStateStore;
            }
        }

        // 테스트나 제품 저장 계층이 사용할 RunState 저장소 구현을 교체합니다.
        public void SetRunStateStore(
            IDungeonRunStateStore store)
        {
            _runStateStore = store ??
                throw new ArgumentNullException(nameof(store));
        }

        // 클릭 처치·파괴가 발생한 stable spawn을 현재 런 상태에 중복 없이 기록합니다.
        public bool RecordSpawnRemoved(
            DungeonSpawnIdentity identity)
        {
            if (identity == null ||
                _stageInstance == null ||
                _stageInstance.Root == null ||
                _activeRunState == null)
            {
                return false;
            }
            if (identity.Category != DungeonSpawnCategory.Enemy &&
                identity.Category !=
                    DungeonSpawnCategory.Destructible)
            {
                return false;
            }
            Transform identityTransform = identity.transform;
            Transform rootTransform =
                _stageInstance.Root.transform;
            if (identityTransform != rootTransform &&
                !identityTransform.IsChildOf(rootTransform))
            {
                return false;
            }
            bool changed =
                _activeRunState.AddRemovedSpawn(
                    identity.SpawnId);
            if (changed) RaiseRunStateChanged();
            return changed;
        }

        // 제품 또는 Sample 플레이어를 현재 Generator의 런 상태 pose 공급자로 등록합니다.
        public void RegisterRunStatePlayer(
            IDungeonRunStatePlayer player)
        {
            if (player == null)
                throw new ArgumentNullException(nameof(player));
            if (player.RunStateGenerator != this)
                throw new InvalidOperationException(
                    "A RunState player must belong to this Generator.");
            _runStatePlayer = player;
        }

        // 등록된 플레이어와 같은 인스턴스만 해제해 다른 시스템의 최신 등록을 보존합니다.
        public void UnregisterRunStatePlayer(
            IDungeonRunStatePlayer player)
        {
            if (ReferenceEquals(_runStatePlayer, player))
                _runStatePlayer = null;
        }

        // 현재 participant payload와 선택적 제품·Sample 플레이어 pose를 활성 RunState에 캡처합니다.
        public DungeonRunState CaptureCurrentRunState(
            IDungeonRunStatePlayer player = null)
        {
            if (_stageInstance == null ||
                _stageInstance.Root == null)
            {
                throw new InvalidOperationException(
                    "Generate or load a stage before capturing RunState.");
            }
            EnsureActiveRunState(_stageInstance);
            CaptureGimmickStates(
                _stageInstance.Root,
                _activeRunState);
            CapturePlayerState(player, _activeRunState);
            _activeRunState.RefreshHash();
            RaiseRunStateChanged();
            return _activeRunState.DeepClone();
        }

        // 현재 런 진행을 캡처하고 지정 슬롯에 canonical JSON 상태로 저장합니다.
        public DungeonRunState SaveRunState(
            string slotId,
            IDungeonRunStatePlayer player = null)
        {
            DungeonRunState state =
                CaptureCurrentRunState(player);
            state.savedUtcTicks = DateTime.UtcNow.Ticks;
            state.RefreshHash();
            RunStateStore.Save(slotId, state);
            _activeRunState = state.DeepClone();
            RaiseRunStateChanged();
            return state.DeepClone();
        }

        // 저장 슬롯을 읽어 원래 procedural seed 또는 saved stage에 transactional하게 다시 적용합니다.
        public bool LoadRunState(
            string slotId,
            DungeonRunStateHashMismatchPolicy policy =
                DungeonRunStateHashMismatchPolicy.Reject,
            IDungeonRunStateMigrator migrator = null)
        {
            DungeonRunState state;
            if (!RunStateStore.TryLoad(slotId, out state))
                return false;
            bool assignedDefinitionMatchesState =
                stageDefinition != null &&
                !string.IsNullOrWhiteSpace(
                    stageDefinition.stageId) &&
                string.Equals(
                    stageDefinition.stageId.Trim(),
                    state.stageId,
                    StringComparison.Ordinal);
            bool useStageDefinition =
                stageDefinition != null &&
                (assignedDefinitionMatchesState ||
                 (_stageInstance == null
                    ? ShouldUseAssignedStageDefinition()
                    : _stageInstance.Definition ==
                      stageDefinition));
            if (useStageDefinition)
            {
                int? explicitSeed =
                    state.sourceMode ==
                        DungeonStageSourceMode.Procedural
                        ? state.runSeed
                        : (int?)null;
                LoadStageDefinitionInternal(
                    explicitSeed,
                    state,
                    policy,
                    migrator);
                return true;
            }
            if (settings != null &&
                state.sourceMode ==
                    DungeonStageSourceMode.Procedural)
            {
                LoadLegacyProceduralRunState(
                    state,
                    policy,
                    migrator);
                return true;
            }
            throw new InvalidOperationException(
                "RunState restore requires the original StageDefinition, or a legacy procedural settings source.");
        }

        // 지정 저장 슬롯과 남은 백업을 삭제합니다.
        public bool DeleteRunState(string slotId)
        {
            return RunStateStore.Delete(slotId);
        }

        // 지정 저장 슬롯이 현재 저장소에 존재하는지 확인합니다.
        public bool HasRunState(string slotId)
        {
            return RunStateStore.Exists(slotId);
        }

        // 활성 RunState에 플레이어 pose가 있으면 등록 구현의 안전 절차로 복원합니다.
        public bool TryRestorePlayerRunState(
            IDungeonRunStatePlayer player)
        {
            if (player == null ||
                _activeRunState == null ||
                _activeRunState.player == null ||
                !_activeRunState.player.isPresent)
            {
                return false;
            }
            player.RestoreRunStatePose(
                transform,
                _activeRunState.player);
            return true;
        }

        // 새 StageInstance가 적용한 상태 또는 빈 target 상태를 Generator 활성 상태로 승격합니다.
        private void CommitRunStateInstance(
            DungeonStageInstance instance)
        {
            DungeonRunState applied =
                instance != null
                    ? instance.AppliedRunState
                    : null;
            if (applied != null)
            {
                _activeRunState = applied.DeepClone();
            }
            else
            {
                EnsureActiveRunState(instance, true);
            }
            RaiseRunStateChanged();
        }

        // StageInstance target fingerprint를 사용해 빈 런 상태를 필요할 때 생성합니다.
        private void EnsureActiveRunState(
            DungeonStageInstance instance,
            bool replace = false)
        {
            if (!replace && _activeRunState != null) return;
            if (instance == null ||
                instance.Blueprint == null)
            {
                _activeRunState = null;
                return;
            }
            DungeonRunStateTarget target =
                instance.RunStateApplyResult != null
                    ? instance.RunStateApplyResult.Target
                    : DungeonRunStateTargetFactory.Create(
                        instance.Definition,
                        instance.SourceMode,
                        instance.Blueprint,
                        instance.SourceBlueprintHash);
            _activeRunState = new DungeonRunState
            {
                stageId = target.StageId,
                sourceMode = target.SourceMode,
                runSeed = target.RunSeed,
                finalBlueprintHash =
                    target.FinalBlueprintHash
            };
            _activeRunState.RefreshHash();
        }

        // 현재 stage의 모든 기믹 participant payload를 stable spawn ID와 key로 다시 캡처합니다.
        private static void CaptureGimmickStates(
            GameObject root,
            DungeonRunState state)
        {
            List<DungeonGimmickRunState> captured =
                new List<DungeonGimmickRunState>();
            HashSet<string> keys =
                new HashSet<string>(StringComparer.Ordinal);
            DungeonSpawnIdentity[] identities =
                root.GetComponentsInChildren<DungeonSpawnIdentity>(
                    true);
            for (int identityIndex = 0;
                 identityIndex < identities.Length;
                 identityIndex++)
            {
                DungeonSpawnIdentity identity =
                    identities[identityIndex];
                if (identity == null ||
                    identity.Category !=
                        DungeonSpawnCategory.Gimmick)
                {
                    continue;
                }
                MonoBehaviour[] behaviours =
                    identity.GetComponentsInChildren<MonoBehaviour>(
                        true);
                for (int behaviourIndex = 0;
                     behaviourIndex < behaviours.Length;
                     behaviourIndex++)
                {
                    IDungeonRunStateParticipant participant =
                        behaviours[behaviourIndex] as
                            IDungeonRunStateParticipant;
                    if (participant == null) continue;
                    string participantKey =
                        participant.RunStateKey != null
                            ? participant.RunStateKey.Trim()
                            : string.Empty;
                    string composite =
                        identity.SpawnId + "\n" +
                        participantKey;
                    if (string.IsNullOrEmpty(
                            identity.SpawnId) ||
                        string.IsNullOrEmpty(
                            participantKey) ||
                        !keys.Add(composite))
                    {
                        throw new InvalidOperationException(
                            "Gimmick RunState participant key is empty or duplicated.");
                    }
                    captured.Add(
                        new DungeonGimmickRunState
                        {
                            spawnId = identity.SpawnId,
                            stateKey = participantKey,
                            payload =
                                participant.CaptureRunState() ??
                                string.Empty
                        });
                }
            }
            state.gimmickStates = captured;
        }

        // 연결된 제품·Sample 플레이어를 Generator 기준 위치·회전으로 상태에 기록합니다.
        private void CapturePlayerState(
            IDungeonRunStatePlayer requestedPlayer,
            DungeonRunState state)
        {
            IDungeonRunStatePlayer player =
                requestedPlayer != null
                    ? requestedPlayer
                    : _runStatePlayer;
            if (player == null ||
                player.RunStateGenerator != this ||
                player.RunStateTransform == null)
            {
                state.player =
                    new DungeonRunPlayerState();
                return;
            }
            Transform playerTransform =
                player.RunStateTransform;
            Quaternion localRotation =
                Quaternion.Inverse(transform.rotation) *
                playerTransform.rotation;
            state.player = new DungeonRunPlayerState
            {
                isPresent = true,
                localPosition =
                    transform.InverseTransformPoint(
                        playerTransform.position),
                localEulerAngles = localRotation.eulerAngles
            };
        }

        // StageDefinition 없는 Legacy settings facade를 같은 seed와 RunState로 다시 구축합니다.
        private void LoadLegacyProceduralRunState(
            DungeonRunState state,
            DungeonRunStateHashMismatchPolicy policy,
            IDungeonRunStateMigrator migrator)
        {
            RogueDungeonSettings runtimeSettings =
                GetOrCreateRuntimeSettings(settings, settings);
            if (IsGeneratorOwnedRuntimeSettings(
                    runtimeSettings))
            {
                runtimeSettings.seed = state.runSeed;
            }
            runtimeSettings.ClampValues();
            try
            {
                DungeonStageInstance instance =
                    DungeonStageLoader.LoadProcedural(
                        transform,
                        runtimeSettings,
                        state.runSeed,
                        DungeonGeneratorVersions.LegacyV1,
                        runtimeSettings,
                        null,
                        DungeonMissingContentPolicy
                            .BuiltInFallback,
                        null,
                        "run-state-restore",
                        state,
                        policy,
                        migrator);
                ApplyStageInstance(instance);
            }
            catch
            {
                DiscardPendingRuntimeSettings(
                    runtimeSettings);
                throw;
            }
        }

        // RunState 변경 구독자에게 외부 변경과 분리된 현재 상태 복사본을 전달합니다.
        private void RaiseRunStateChanged()
        {
            Action<DungeonRunState> handler =
                RunStateChanged;
            if (handler != null)
            {
                handler(
                    _activeRunState != null
                        ? _activeRunState.DeepClone()
                        : null);
            }
        }
    }
}
