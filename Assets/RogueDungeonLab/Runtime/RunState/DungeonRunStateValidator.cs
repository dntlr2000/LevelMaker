using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonRunStateValidationCodes
    {
        public const string NullState = "RDL-RUN-001";
        public const string UnsupportedFormat = "RDL-RUN-002";
        public const string MissingStageId = "RDL-RUN-003";
        public const string InvalidSourceMode = "RDL-RUN-004";
        public const string MissingFinalBlueprintHash = "RDL-RUN-005";
        public const string MissingStateHash = "RDL-RUN-006";
        public const string StateHashMismatch = "RDL-RUN-007";
        public const string InvalidRemovedSpawnId = "RDL-RUN-008";
        public const string DuplicateRemovedSpawnId = "RDL-RUN-009";
        public const string InvalidGimmickState = "RDL-RUN-010";
        public const string DuplicateGimmickState = "RDL-RUN-011";
        public const string InvalidPlayerPose = "RDL-RUN-012";
        public const string StageIdMismatch = "RDL-RUN-013";
        public const string SourceModeMismatch = "RDL-RUN-014";
        public const string RunSeedMismatch = "RDL-RUN-015";
        public const string FinalBlueprintHashMismatch = "RDL-RUN-016";
        public const string MissingTargetSpawn = "RDL-RUN-017";
        public const string InvalidRemovedSpawnCategory = "RDL-RUN-018";
        public const string InvalidGimmickCategory = "RDL-RUN-019";
        public const string MigrationFailed = "RDL-RUN-020";
        public const string DuplicateSceneSpawnId = "RDL-RUN-021";
        public const string MissingParticipant = "RDL-RUN-022";
        public const string DuplicateParticipantKey = "RDL-RUN-023";
        public const string ParticipantRestoreFailed = "RDL-RUN-024";
        public const string InvalidSlotId = "RDL-RUN-025";
        public const string StoreFailure = "RDL-RUN-026";
        public const string InvalidMismatchPolicy = "RDL-RUN-027";
    }

    public static class DungeonRunStateTargetFactory
    {
        // StageDefinition 또는 생성 provenance에서 seed와 분리된 안정적인 stage ID를 계산합니다.
        public static string ResolveStageId(
            DungeonStageDefinition definition,
            DungeonStageSourceMode sourceMode,
            DungeonBlueprint blueprint,
            string sourceBlueprintHash = "")
        {
            if (definition != null &&
                !string.IsNullOrWhiteSpace(definition.stageId))
            {
                return definition.stageId.Trim();
            }
            if (sourceMode == DungeonStageSourceMode.SavedBlueprint)
            {
                string sourceHash =
                    !string.IsNullOrWhiteSpace(sourceBlueprintHash)
                        ? sourceBlueprintHash
                        : definition != null &&
                          definition.savedBlueprint != null &&
                          definition.savedBlueprint.blueprint != null
                            ? definition.savedBlueprint.blueprint
                                .blueprintHash
                            : blueprint != null
                                ? blueprint.blueprintHash
                                : string.Empty;
                return "blueprint:" + (sourceHash ?? string.Empty);
            }
            if (blueprint == null) return string.Empty;
            return string.Concat(
                "procedural:v",
                blueprint.generatorVersion,
                ":",
                blueprint.recipeHash ?? string.Empty,
                ":",
                blueprint.catalogPlanningHash ?? string.Empty);
        }

        // 현재 StageInstance의 최종 Blueprint와 stable spawn 목록에서 상태 호환 대상을 만듭니다.
        public static DungeonRunStateTarget Create(
            DungeonStageDefinition definition,
            DungeonStageSourceMode sourceMode,
            DungeonBlueprint blueprint,
            string sourceBlueprintHash = "")
        {
            if (blueprint == null)
                throw new ArgumentNullException(nameof(blueprint));
            List<DungeonRunStateSpawnDescriptor> spawns =
                new List<DungeonRunStateSpawnDescriptor>();
            if (blueprint.spawns != null)
            {
                for (int i = 0; i < blueprint.spawns.Count; i++)
                {
                    DungeonSpawnRecord spawn = blueprint.spawns[i];
                    if (spawn == null) continue;
                    spawns.Add(new DungeonRunStateSpawnDescriptor(
                        spawn.spawnId,
                        spawn.category));
                }
            }
            spawns.Sort(CompareSpawns);
            return new DungeonRunStateTarget(
                ResolveStageId(
                    definition,
                    sourceMode,
                    blueprint,
                    sourceBlueprintHash),
                sourceMode,
                blueprint.seed,
                blueprint.blueprintHash,
                spawns);
        }

        // stable ID와 범주 순으로 migration 대상 spawn 목록을 정렬합니다.
        private static int CompareSpawns(
            DungeonRunStateSpawnDescriptor left,
            DungeonRunStateSpawnDescriptor right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(
                left.SpawnId,
                right.SpawnId);
            return result != 0
                ? result
                : ((int)left.Category).CompareTo(
                    (int)right.Category);
        }
    }

    public static class DungeonRunStateValidator
    {
        // RunState의 버전·식별자·중복·수치와 저장 hash를 대상 없이 검증합니다.
        public static DungeonValidationReport Validate(
            DungeonRunState state,
            bool verifyStoredHash = true)
        {
            DungeonValidationReport report =
                new DungeonValidationReport();
            if (state == null)
            {
                report.Add(
                    DungeonRunStateValidationCodes.NullState,
                    DungeonValidationSeverity.Error,
                    "RunState is null.");
                return report;
            }
            if (state.formatVersion !=
                DungeonRunStateFormat.CurrentVersion)
            {
                report.Add(
                    DungeonRunStateValidationCodes.UnsupportedFormat,
                    DungeonValidationSeverity.Error,
                    "RunState format version is not supported.");
            }
            if (string.IsNullOrWhiteSpace(state.stageId))
            {
                report.Add(
                    DungeonRunStateValidationCodes.MissingStageId,
                    DungeonValidationSeverity.Error,
                    "RunState stage ID is missing.");
            }
            if (!Enum.IsDefined(
                    typeof(DungeonStageSourceMode),
                    state.sourceMode))
            {
                report.Add(
                    DungeonRunStateValidationCodes.InvalidSourceMode,
                    DungeonValidationSeverity.Error,
                    "RunState source mode is invalid.");
            }
            if (string.IsNullOrWhiteSpace(
                    state.finalBlueprintHash))
            {
                report.Add(
                    DungeonRunStateValidationCodes
                        .MissingFinalBlueprintHash,
                    DungeonValidationSeverity.Error,
                    "RunState final Blueprint hash is missing.");
            }
            ValidateRemovedSpawns(report, state.removedSpawnIds);
            ValidateGimmickStates(report, state.gimmickStates);
            ValidatePlayer(report, state.player);
            if (verifyStoredHash)
                ValidateStoredHash(report, state);
            return report;
        }

        // RunState가 지정 stage·source·seed·최종 Blueprint와 적용 정책에 맞는지 검증합니다.
        public static DungeonValidationReport ValidateForTarget(
            DungeonRunState state,
            DungeonRunStateTarget target,
            DungeonRunStateHashMismatchPolicy policy,
            bool verifyStoredHash = true)
        {
            DungeonValidationReport report =
                Validate(state, verifyStoredHash);
            if (target == null)
            {
                report.Add(
                    DungeonRunStateValidationCodes.MissingStageId,
                    DungeonValidationSeverity.Error,
                    "RunState target is missing.");
                return report;
            }
            if (!Enum.IsDefined(
                    typeof(DungeonRunStateHashMismatchPolicy),
                    policy))
            {
                report.Add(
                    DungeonRunStateValidationCodes
                        .InvalidMismatchPolicy,
                    DungeonValidationSeverity.Error,
                    "RunState mismatch policy is invalid.");
                return report;
            }
            if (state == null) return report;

            if (!string.Equals(
                    state.stageId,
                    target.StageId,
                    StringComparison.Ordinal))
            {
                report.Add(
                    DungeonRunStateValidationCodes.StageIdMismatch,
                    DungeonValidationSeverity.Error,
                    "RunState stage ID does not match the load target.");
            }
            if (state.sourceMode != target.SourceMode)
            {
                report.Add(
                    DungeonRunStateValidationCodes.SourceModeMismatch,
                    DungeonValidationSeverity.Error,
                    "RunState source mode does not match the load target.");
            }
            if (state.runSeed != target.RunSeed)
            {
                report.Add(
                    DungeonRunStateValidationCodes.RunSeedMismatch,
                    DungeonValidationSeverity.Error,
                    "RunState seed does not match the load target.");
            }

            bool exactBlueprint = string.Equals(
                state.finalBlueprintHash,
                target.FinalBlueprintHash,
                StringComparison.Ordinal);
            if (!exactBlueprint)
            {
                report.Add(
                    DungeonRunStateValidationCodes
                        .FinalBlueprintHashMismatch,
                    policy ==
                    DungeonRunStateHashMismatchPolicy
                        .ApplyMatchingSpawnIds
                        ? DungeonValidationSeverity.Warning
                        : DungeonValidationSeverity.Error,
                    "RunState final Blueprint hash does not match the load target.");
            }

            Dictionary<string, DungeonSpawnCategory> spawns =
                BuildSpawnLookup(target);
            ValidateRemovedTargets(
                report,
                state.removedSpawnIds,
                spawns,
                policy);
            ValidateGimmickTargets(
                report,
                state.gimmickStates,
                spawns,
                policy);
            return report;
        }

        // stage·source·seed·최종 hash가 migration 없이 모두 같은지 판정합니다.
        public static bool IsExactTarget(
            DungeonRunState state,
            DungeonRunStateTarget target)
        {
            return state != null &&
                   target != null &&
                   string.Equals(
                       state.stageId,
                       target.StageId,
                       StringComparison.Ordinal) &&
                   state.sourceMode == target.SourceMode &&
                   state.runSeed == target.RunSeed &&
                   string.Equals(
                       state.finalBlueprintHash,
                       target.FinalBlueprintHash,
                       StringComparison.Ordinal);
        }

        // 제거 목록의 빈 ID와 ordinal 중복을 검사합니다.
        private static void ValidateRemovedSpawns(
            DungeonValidationReport report,
            List<string> values)
        {
            if (values == null) return;
            HashSet<string> ids =
                new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                string value = values[i];
                if (string.IsNullOrWhiteSpace(value))
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .InvalidRemovedSpawnId,
                        DungeonValidationSeverity.Error,
                        "Removed spawn ID is empty.");
                    continue;
                }
                if (!ids.Add(value))
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .DuplicateRemovedSpawnId,
                        DungeonValidationSeverity.Error,
                        "Removed spawn ID is duplicated.",
                        null,
                        value);
                }
            }
        }

        // 기믹 목록의 필수 값과 spawn ID·key 복합 중복을 검사합니다.
        private static void ValidateGimmickStates(
            DungeonValidationReport report,
            List<DungeonGimmickRunState> values)
        {
            if (values == null) return;
            HashSet<string> keys =
                new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                DungeonGimmickRunState value = values[i];
                if (value == null ||
                    string.IsNullOrWhiteSpace(value.spawnId) ||
                    string.IsNullOrWhiteSpace(value.stateKey))
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .InvalidGimmickState,
                        DungeonValidationSeverity.Error,
                        "Gimmick state requires a spawn ID and participant key.");
                    continue;
                }
                string composite =
                    value.spawnId + "\n" + value.stateKey;
                if (!keys.Add(composite))
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .DuplicateGimmickState,
                        DungeonValidationSeverity.Error,
                        "Gimmick state key is duplicated.",
                        null,
                        value.spawnId);
                }
            }
        }

        // 플레이어 stage-local 위치와 회전이 유한한 수치인지 검사합니다.
        private static void ValidatePlayer(
            DungeonValidationReport report,
            DungeonRunPlayerState player)
        {
            if (player == null || !player.isPresent) return;
            if (!IsFinite(player.localPosition) ||
                !IsFinite(player.localEulerAngles))
            {
                report.Add(
                    DungeonRunStateValidationCodes.InvalidPlayerPose,
                    DungeonValidationSeverity.Error,
                    "Player pose contains a non-finite value.");
            }
        }

        // 저장된 state hash가 존재하고 현재 canonical 본문과 같은지 검사합니다.
        private static void ValidateStoredHash(
            DungeonValidationReport report,
            DungeonRunState state)
        {
            if (string.IsNullOrWhiteSpace(state.stateHash))
            {
                report.Add(
                    DungeonRunStateValidationCodes.MissingStateHash,
                    DungeonValidationSeverity.Error,
                    "RunState hash is missing.");
                return;
            }
            string actual = DungeonRunStateHasher.Compute(state);
            if (!string.Equals(
                    state.stateHash,
                    actual,
                    StringComparison.Ordinal))
            {
                report.Add(
                    DungeonRunStateValidationCodes.StateHashMismatch,
                    DungeonValidationSeverity.Error,
                    "RunState hash does not match its canonical data.");
            }
        }

        // 대상 spawn descriptor를 ordinal stable ID 조회로 변환합니다.
        private static Dictionary<string, DungeonSpawnCategory>
            BuildSpawnLookup(DungeonRunStateTarget target)
        {
            Dictionary<string, DungeonSpawnCategory> result =
                new Dictionary<string, DungeonSpawnCategory>(
                    StringComparer.Ordinal);
            IReadOnlyList<DungeonRunStateSpawnDescriptor> spawns =
                target.Spawns;
            for (int i = 0; i < spawns.Count; i++)
            {
                DungeonRunStateSpawnDescriptor spawn = spawns[i];
                if (spawn != null &&
                    !string.IsNullOrWhiteSpace(spawn.SpawnId) &&
                    !result.ContainsKey(spawn.SpawnId))
                {
                    result.Add(spawn.SpawnId, spawn.Category);
                }
            }
            return result;
        }

        // 제거 대상이 존재하며 Enemy 또는 Destructible인지 검사합니다.
        private static void ValidateRemovedTargets(
            DungeonValidationReport report,
            List<string> values,
            Dictionary<string, DungeonSpawnCategory> spawns,
            DungeonRunStateHashMismatchPolicy policy)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                string id = values[i];
                if (string.IsNullOrWhiteSpace(id)) continue;
                DungeonSpawnCategory category;
                if (!spawns.TryGetValue(id, out category))
                {
                    report.Add(
                        DungeonRunStateValidationCodes.MissingTargetSpawn,
                        policy ==
                        DungeonRunStateHashMismatchPolicy
                            .ApplyMatchingSpawnIds
                            ? DungeonValidationSeverity.Warning
                            : DungeonValidationSeverity.Error,
                        "Removed spawn ID does not exist in the load target.",
                        null,
                        id);
                    continue;
                }
                if (category != DungeonSpawnCategory.Enemy &&
                    category != DungeonSpawnCategory.Destructible)
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .InvalidRemovedSpawnCategory,
                        DungeonValidationSeverity.Error,
                        "Only Enemy and Destructible spawns can be restored as removed.",
                        null,
                        id);
                }
            }
        }

        // 기믹 payload 대상이 현재 Blueprint의 Gimmick spawn인지 검사합니다.
        private static void ValidateGimmickTargets(
            DungeonValidationReport report,
            List<DungeonGimmickRunState> values,
            Dictionary<string, DungeonSpawnCategory> spawns,
            DungeonRunStateHashMismatchPolicy policy)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonGimmickRunState value = values[i];
                if (value == null ||
                    string.IsNullOrWhiteSpace(value.spawnId))
                {
                    continue;
                }
                DungeonSpawnCategory category;
                if (!spawns.TryGetValue(value.spawnId, out category))
                {
                    report.Add(
                        DungeonRunStateValidationCodes.MissingTargetSpawn,
                        policy ==
                        DungeonRunStateHashMismatchPolicy
                            .ApplyMatchingSpawnIds
                            ? DungeonValidationSeverity.Warning
                            : DungeonValidationSeverity.Error,
                        "Gimmick state spawn ID does not exist in the load target.",
                        null,
                        value.spawnId);
                    continue;
                }
                if (category != DungeonSpawnCategory.Gimmick)
                {
                    report.Add(
                        DungeonRunStateValidationCodes
                            .InvalidGimmickCategory,
                        DungeonValidationSeverity.Error,
                        "Gimmick state can target only a Gimmick spawn.",
                        null,
                        value.spawnId);
                }
            }
        }

        // Vector3의 모든 축이 NaN이나 무한대가 아닌지 확인합니다.
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        // 단일 float가 NaN이나 무한대가 아닌지 확인합니다.
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
