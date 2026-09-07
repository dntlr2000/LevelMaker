using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public sealed class DungeonRunStateApplyResult
    {
        public DungeonRunStateTarget Target { get; private set; }
        public DungeonRunState AppliedState { get; private set; }
        public DungeonValidationReport ValidationReport
        {
            get;
            private set;
        }
        public bool WasApplied { get; private set; }
        public bool WasMigrated { get; private set; }
        public bool WasBestEffort { get; private set; }
        public int RemovedSpawnCount { get; private set; }
        public int RestoredGimmickStateCount { get; private set; }
        public string Message { get; private set; }

        // 상태 적용 결과와 실제 적용된 정규화 상태를 불변 metadata로 묶습니다.
        internal DungeonRunStateApplyResult(
            DungeonRunStateTarget target,
            DungeonRunState appliedState,
            DungeonValidationReport validationReport,
            bool wasApplied,
            bool wasMigrated,
            bool wasBestEffort,
            int removedSpawnCount,
            int restoredGimmickStateCount,
            string message)
        {
            Target = target;
            AppliedState = appliedState;
            ValidationReport =
                validationReport ?? new DungeonValidationReport();
            WasApplied = wasApplied;
            WasMigrated = wasMigrated;
            WasBestEffort = wasBestEffort;
            RemovedSpawnCount = removedSpawnCount;
            RestoredGimmickStateCount =
                restoredGimmickStateCount;
            Message = message ?? string.Empty;
        }

        // 상태가 없는 로드에도 동일한 대상 metadata를 제공하는 빈 성공 결과를 만듭니다.
        internal static DungeonRunStateApplyResult Skipped(
            DungeonRunStateTarget target)
        {
            return new DungeonRunStateApplyResult(
                target,
                null,
                new DungeonValidationReport(),
                false,
                false,
                false,
                0,
                0,
                "RunState was not supplied.");
        }
    }

    public static class DungeonRunStateApplier
    {
        private sealed class SceneSpawn
        {
            public DungeonSpawnIdentity Identity;
            public Dictionary<string, IDungeonRunStateParticipant>
                Participants;
        }

        // 비활성 후보 root를 검증한 뒤 제거 대상과 기믹 payload를 부작용 순서로 적용합니다.
        public static DungeonRunStateApplyResult Apply(
            GameObject candidateRoot,
            DungeonRunState source,
            DungeonRunStateTarget target,
            DungeonRunStateHashMismatchPolicy policy =
                DungeonRunStateHashMismatchPolicy.Reject,
            IDungeonRunStateMigrator migrator = null)
        {
            if (candidateRoot == null)
                throw new ArgumentNullException(
                    nameof(candidateRoot));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (source == null)
                return DungeonRunStateApplyResult.Skipped(target);

            string migrationMessage = string.Empty;
            bool wasMigrated = false;
            DungeonRunState state = source.DeepClone();
            if ((!DungeonRunStateValidator.IsExactTarget(
                     state,
                     target) ||
                 state.formatVersion !=
                     DungeonRunStateFormat.CurrentVersion) &&
                migrator != null)
            {
                DungeonRunState migrated;
                try
                {
                    if (!migrator.TryMigrate(
                            state.DeepClone(),
                            target,
                            out migrated,
                            out migrationMessage) ||
                        migrated == null)
                    {
                        return CreateMigrationFailure(
                            target,
                            migrationMessage,
                            null);
                    }
                }
                catch (Exception exception)
                {
                    return CreateMigrationFailure(
                        target,
                        "RunState migrator threw an exception.",
                        exception);
                }
                state = migrated.DeepClone();
                wasMigrated = true;
                policy = DungeonRunStateHashMismatchPolicy.Reject;
            }

            DungeonValidationReport validation =
                DungeonRunStateValidator.ValidateForTarget(
                    state,
                    target,
                    policy);
            if (!validation.IsValid)
            {
                return new DungeonRunStateApplyResult(
                    target,
                    null,
                    validation,
                    false,
                    wasMigrated,
                    false,
                    0,
                    0,
                    migrationMessage);
            }

            Dictionary<string, SceneSpawn> sceneSpawns =
                BuildSceneLookup(candidateRoot, validation);
            ValidateSceneTargets(
                state,
                sceneSpawns,
                policy,
                validation);
            if (!validation.IsValid)
            {
                return new DungeonRunStateApplyResult(
                    target,
                    null,
                    validation,
                    false,
                    wasMigrated,
                    false,
                    0,
                    0,
                    migrationMessage);
            }

            DungeonRunState applied =
                CreateAppliedState(
                    state,
                    target,
                    sceneSpawns);
            int restored = RestoreParticipants(
                applied,
                sceneSpawns,
                validation);
            if (!validation.IsValid)
            {
                return new DungeonRunStateApplyResult(
                    target,
                    null,
                    validation,
                    false,
                    wasMigrated,
                    false,
                    0,
                    restored,
                    migrationMessage);
            }
            int removed = SuppressRemovedSpawns(
                applied,
                sceneSpawns);
            bool bestEffort =
                policy ==
                    DungeonRunStateHashMismatchPolicy
                        .ApplyMatchingSpawnIds &&
                (!DungeonRunStateValidator.IsExactTarget(
                     state,
                     target) ||
                 validation.WarningCount > 0);
            return new DungeonRunStateApplyResult(
                target,
                applied,
                validation,
                true,
                wasMigrated,
                bestEffort,
                removed,
                restored,
                migrationMessage);
        }

        // migration 거부 또는 예외를 단일 안정 코드의 적용 실패로 변환합니다.
        private static DungeonRunStateApplyResult
            CreateMigrationFailure(
                DungeonRunStateTarget target,
                string message,
                Exception exception)
        {
            DungeonValidationReport report =
                new DungeonValidationReport();
            string detail = !string.IsNullOrWhiteSpace(message)
                ? message
                : exception != null
                    ? exception.Message
                    : "RunState migrator did not produce a state.";
            report.Add(
                DungeonRunStateValidationCodes.MigrationFailed,
                DungeonValidationSeverity.Error,
                detail);
            return new DungeonRunStateApplyResult(
                target,
                null,
                report,
                false,
                false,
                false,
                0,
                0,
                detail);
        }

        // 후보 hierarchy의 stable spawn과 기믹 participant를 중복 검사가 가능한 lookup으로 만듭니다.
        private static Dictionary<string, SceneSpawn>
            BuildSceneLookup(
                GameObject root,
                DungeonValidationReport report)
        {
            Dictionary<string, SceneSpawn> result =
                new Dictionary<string, SceneSpawn>(
                    StringComparer.Ordinal);
            DungeonSpawnIdentity[] identities =
                root.GetComponentsInChildren<DungeonSpawnIdentity>(
                    true);
            for (int i = 0; i < identities.Length; i++)
            {
                DungeonSpawnIdentity identity = identities[i];
                if (identity == null ||
                    string.IsNullOrWhiteSpace(identity.SpawnId))
                {
                    continue;
                }
                if (result.ContainsKey(identity.SpawnId))
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .DuplicateSceneSpawnId,
                        DungeonValidationSeverity.Error,
                        "Candidate scene contains a duplicate stable spawn ID.",
                        null,
                        identity.SpawnId);
                    continue;
                }
                SceneSpawn spawn = new SceneSpawn
                {
                    Identity = identity,
                    Participants =
                        BuildParticipantLookup(
                            identity,
                            report)
                };
                result.Add(identity.SpawnId, spawn);
            }
            return result;
        }

        // 한 spawn 아래의 participant key를 조회하고 같은 key의 중복 컴포넌트를 차단합니다.
        private static Dictionary<string, IDungeonRunStateParticipant>
            BuildParticipantLookup(
                DungeonSpawnIdentity identity,
                DungeonValidationReport report)
        {
            Dictionary<string, IDungeonRunStateParticipant> result =
                new Dictionary<string, IDungeonRunStateParticipant>(
                    StringComparer.Ordinal);
            if (identity.Category != DungeonSpawnCategory.Gimmick)
                return result;
            MonoBehaviour[] behaviours =
                identity.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                IDungeonRunStateParticipant participant =
                    behaviours[i] as IDungeonRunStateParticipant;
                if (participant == null) continue;
                string key;
                try
                {
                    key = participant.RunStateKey != null
                        ? participant.RunStateKey.Trim()
                        : string.Empty;
                }
                catch (Exception exception)
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .ParticipantRestoreFailed,
                        DungeonValidationSeverity.Error,
                        "Gimmick participant key read failed: " +
                        exception.Message,
                        null,
                        identity.SpawnId);
                    continue;
                }
                if (string.IsNullOrEmpty(key) ||
                    result.ContainsKey(key))
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .DuplicateParticipantKey,
                        DungeonValidationSeverity.Error,
                        "Gimmick participant key is empty or duplicated.",
                        null,
                        identity.SpawnId);
                    continue;
                }
                result.Add(key, participant);
            }
            return result;
        }

        // 상태가 참조한 scene spawn과 participant가 실제 후보에 존재하는지 mutation 전에 검사합니다.
        private static void ValidateSceneTargets(
            DungeonRunState state,
            Dictionary<string, SceneSpawn> sceneSpawns,
            DungeonRunStateHashMismatchPolicy policy,
            DungeonValidationReport report)
        {
            DungeonValidationSeverity missingSeverity =
                policy ==
                    DungeonRunStateHashMismatchPolicy
                        .ApplyMatchingSpawnIds
                    ? DungeonValidationSeverity.Warning
                    : DungeonValidationSeverity.Error;
            if (state.removedSpawnIds != null)
            {
                for (int i = 0;
                     i < state.removedSpawnIds.Count;
                     i++)
                {
                    string id = state.removedSpawnIds[i];
                    if (!string.IsNullOrWhiteSpace(id) &&
                        !sceneSpawns.ContainsKey(id))
                    {
                        report.Add(
                            DungeonRunStateValidationCodes
                                .MissingTargetSpawn,
                            missingSeverity,
                            "Removed spawn is missing from the candidate scene.",
                            null,
                            id);
                    }
                }
            }
            if (state.gimmickStates == null) return;
            for (int i = 0;
                 i < state.gimmickStates.Count;
                 i++)
            {
                DungeonGimmickRunState value =
                    state.gimmickStates[i];
                if (value == null) continue;
                SceneSpawn spawn;
                if (!sceneSpawns.TryGetValue(
                        value.spawnId,
                        out spawn))
                {
                    continue;
                }
                if (!spawn.Participants.ContainsKey(
                        value.stateKey))
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .MissingParticipant,
                        missingSeverity,
                        "Gimmick participant is missing from the candidate scene.",
                        null,
                        value.spawnId);
                }
            }
        }

        // 대상에서 실제로 다시 적용할 수 있는 레코드만 유지하고 현재 target fingerprint로 재결박합니다.
        private static DungeonRunState CreateAppliedState(
            DungeonRunState source,
            DungeonRunStateTarget target,
            Dictionary<string, SceneSpawn> sceneSpawns)
        {
            DungeonRunState result = new DungeonRunState
            {
                formatVersion =
                    DungeonRunStateFormat.CurrentVersion,
                stageId = target.StageId,
                sourceMode = target.SourceMode,
                runSeed = target.RunSeed,
                finalBlueprintHash =
                    target.FinalBlueprintHash,
                player = source.player != null
                    ? source.player.DeepClone()
                    : new DungeonRunPlayerState(),
                savedUtcTicks = source.savedUtcTicks
            };
            if (source.removedSpawnIds != null)
            {
                for (int i = 0;
                     i < source.removedSpawnIds.Count;
                     i++)
                {
                    string id = source.removedSpawnIds[i];
                    if (!string.IsNullOrWhiteSpace(id) &&
                        sceneSpawns.ContainsKey(id))
                    {
                        result.removedSpawnIds.Add(id);
                    }
                }
            }
            if (source.gimmickStates != null)
            {
                for (int i = 0;
                     i < source.gimmickStates.Count;
                     i++)
                {
                    DungeonGimmickRunState value =
                        source.gimmickStates[i];
                    SceneSpawn spawn;
                    if (value != null &&
                        sceneSpawns.TryGetValue(
                            value.spawnId,
                            out spawn) &&
                        spawn.Participants.ContainsKey(
                            value.stateKey))
                    {
                        result.gimmickStates.Add(
                            value.DeepClone());
                    }
                }
            }
            result.RefreshHash();
            return result;
        }

        // 정규화 상태의 payload를 각 기믹 participant에 복원하고 예외를 보고합니다.
        private static int RestoreParticipants(
            DungeonRunState state,
            Dictionary<string, SceneSpawn> sceneSpawns,
            DungeonValidationReport report)
        {
            int restored = 0;
            for (int i = 0;
                 i < state.gimmickStates.Count;
                 i++)
            {
                DungeonGimmickRunState value =
                    state.gimmickStates[i];
                SceneSpawn spawn;
                if (!sceneSpawns.TryGetValue(
                        value.spawnId,
                        out spawn))
                {
                    continue;
                }
                IDungeonRunStateParticipant participant;
                if (!spawn.Participants.TryGetValue(
                        value.stateKey,
                        out participant))
                {
                    continue;
                }
                try
                {
                    participant.RestoreRunState(
                        value.payload ?? string.Empty);
                    restored++;
                }
                catch (Exception exception)
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .ParticipantRestoreFailed,
                        DungeonValidationSeverity.Error,
                        "Gimmick participant restore failed: " +
                        exception.Message,
                        null,
                        value.spawnId);
                    break;
                }
            }
            return restored;
        }

        // 제거 상태의 Enemy·Destructible을 활성화 전에 비활성화하고 수명주기에 맞게 파괴합니다.
        private static int SuppressRemovedSpawns(
            DungeonRunState state,
            Dictionary<string, SceneSpawn> sceneSpawns)
        {
            int removed = 0;
            for (int i = 0;
                 i < state.removedSpawnIds.Count;
                 i++)
            {
                SceneSpawn spawn;
                if (!sceneSpawns.TryGetValue(
                        state.removedSpawnIds[i],
                        out spawn) ||
                    spawn.Identity == null)
                {
                    continue;
                }
                GameObject instance =
                    spawn.Identity.gameObject;
                instance.SetActive(false);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(instance);
                else
                    UnityEngine.Object.DestroyImmediate(instance);
                removed++;
            }
            return removed;
        }
    }
}
