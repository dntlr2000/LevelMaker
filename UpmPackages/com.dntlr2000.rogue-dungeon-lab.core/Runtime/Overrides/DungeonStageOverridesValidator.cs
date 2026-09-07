using System;
using System.Collections.Generic;
using UnityEngine;

namespace RogueDungeonLab
{
    public static class DungeonStageOverrideValidationCodes
    {
        public const string NullOverrides = "RDL-OVR-001";
        public const string UnsupportedFormat = "RDL-OVR-002";
        public const string MissingBaseBlueprint = "RDL-OVR-003";
        public const string BaseBlueprintMismatch = "RDL-OVR-004";
        public const string BaseHashMismatch = "RDL-OVR-005";
        public const string MissingStoredHash = "RDL-OVR-006";
        public const string StoredHashMismatch = "RDL-OVR-007";
        public const string InvalidOperation = "RDL-OVR-008";
        public const string DuplicateRecordId = "RDL-OVR-009";
        public const string DuplicateTarget = "RDL-OVR-010";
        public const string MissingTarget = "RDL-OVR-011";
        public const string BindingMismatch = "RDL-OVR-012";
        public const string ProtectedMarker = "RDL-OVR-013";
        public const string DisabledTargetConflict = "RDL-OVR-014";
        public const string InvalidReplacement = "RDL-OVR-015";
        public const string InvalidTransform = "RDL-OVR-016";
        public const string InvalidAddedSpawn = "RDL-OVR-017";
        public const string DuplicateAddedSpawn = "RDL-OVR-018";
        public const string AddedSpawnIdCollision = "RDL-OVR-019";
        public const string InvalidFinalBlueprint = "RDL-OVR-020";
    }

    public static class DungeonStageOverridesValidator
    {
        // Override 자산을 자신이 참조하는 원본 Blueprint와 저장 hash 기준으로 검증합니다.
        public static DungeonValidationReport Validate(
            DungeonStageOverrides stageOverrides,
            DungeonBlueprintAsset expectedBase = null,
            bool verifyStoredHash = true)
        {
            DungeonBlueprint source =
                stageOverrides != null && stageOverrides.baseBlueprint != null
                    ? stageOverrides.baseBlueprint.blueprint
                    : null;
            return ValidateInternal(
                stageOverrides,
                source,
                expectedBase,
                verifyStoredHash);
        }

        // 메모리 Blueprint를 기준으로 Override 적용 계약을 검증합니다.
        public static DungeonValidationReport ValidateAgainstBlueprint(
            DungeonStageOverrides stageOverrides,
            DungeonBlueprint source,
            bool verifyStoredHash = true)
        {
            return ValidateInternal(
                stageOverrides,
                source,
                null,
                verifyStoredHash);
        }

        // 형식·원본·operation target·충돌과 transform 값을 한 리포트로 검사합니다.
        private static DungeonValidationReport ValidateInternal(
            DungeonStageOverrides stageOverrides,
            DungeonBlueprint source,
            DungeonBlueprintAsset expectedBase,
            bool verifyStoredHash)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            if (stageOverrides == null)
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.NullOverrides,
                    DungeonValidationSeverity.Error,
                    "Stage Overrides is null.");
                return report;
            }
            if (stageOverrides.formatVersion !=
                DungeonStageOverrideFormat.CurrentVersion)
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.UnsupportedFormat,
                    DungeonValidationSeverity.Error,
                    "Stage Overrides format version is unsupported.");
            }
            if (stageOverrides.baseBlueprint == null ||
                stageOverrides.baseBlueprint.blueprint == null ||
                source == null)
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.MissingBaseBlueprint,
                    DungeonValidationSeverity.Error,
                    "Stage Overrides requires a saved base Blueprint.");
                return report;
            }
            if (expectedBase != null &&
                stageOverrides.baseBlueprint != expectedBase)
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.BaseBlueprintMismatch,
                    DungeonValidationSeverity.Error,
                    "Stage Overrides belongs to a different base Blueprint.");
            }

            DungeonValidationReport sourceReport =
                DungeonBlueprintValidator.Validate(source);
            Merge(report, sourceReport);
            string actualBaseHash = DungeonBlueprintHasher.Compute(source);
            if (!string.Equals(
                    stageOverrides.baseBlueprintHash,
                    actualBaseHash,
                    StringComparison.Ordinal))
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.BaseHashMismatch,
                    DungeonValidationSeverity.Error,
                    "Stage Overrides base hash differs from the current Blueprint. Explicit rebind approval is required.");
            }

            if (verifyStoredHash)
            {
                string computedHash =
                    DungeonStageOverridesHasher.Compute(stageOverrides);
                if (string.IsNullOrWhiteSpace(stageOverrides.overrideHash))
                {
                    report.Add(
                        DungeonStageOverrideValidationCodes.MissingStoredHash,
                        DungeonValidationSeverity.Error,
                        "Stage Overrides canonical hash is missing.");
                }
                else if (!string.Equals(
                             stageOverrides.overrideHash,
                             computedHash,
                             StringComparison.Ordinal))
                {
                    report.Add(
                        DungeonStageOverrideValidationCodes.StoredHashMismatch,
                        DungeonValidationSeverity.Error,
                        "Stored Stage Overrides hash does not match its logical data.");
                }
            }

            Dictionary<string, DungeonSpawnRecord> baseSpawns =
                BuildSpawnLookup(source.spawns);
            HashSet<string> recordIds =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> disabledTargets =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> contentTargets =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> transformTargets =
                new HashSet<string>(StringComparer.Ordinal);

            ValidateDisabled(
                stageOverrides.disabledSpawns,
                baseSpawns,
                recordIds,
                disabledTargets,
                report);
            ValidateContent(
                stageOverrides.contentOverrides,
                baseSpawns,
                recordIds,
                contentTargets,
                report);
            ValidateTransforms(
                stageOverrides.transformOverrides,
                baseSpawns,
                recordIds,
                transformTargets,
                report);
            ValidateDisabledConflicts(
                disabledTargets,
                contentTargets,
                transformTargets,
                report);
            ValidateAdded(
                stageOverrides.addedSpawns,
                baseSpawns,
                report);
            return report;
        }

        // 비활성화 operation의 레코드 ID와 base target binding을 검사합니다.
        private static void ValidateDisabled(
            List<DungeonSpawnDisableOverride> values,
            Dictionary<string, DungeonSpawnRecord> baseSpawns,
            HashSet<string> recordIds,
            HashSet<string> targets,
            DungeonValidationReport report)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnDisableOverride value = values[i];
                if (value == null)
                {
                    AddInvalidOperation(report, "Disable Override is null.");
                    continue;
                }
                ValidateRecordId(value.recordId, recordIds, report);
                ValidateTargetBinding(
                    value.binding,
                    baseSpawns,
                    targets,
                    report);
            }
        }

        // 콘텐츠 교체 operation의 target과 대체 key를 검사합니다.
        private static void ValidateContent(
            List<DungeonSpawnContentOverride> values,
            Dictionary<string, DungeonSpawnRecord> baseSpawns,
            HashSet<string> recordIds,
            HashSet<string> targets,
            DungeonValidationReport report)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnContentOverride value = values[i];
                if (value == null)
                {
                    AddInvalidOperation(report, "Content Override is null.");
                    continue;
                }
                ValidateRecordId(value.recordId, recordIds, report);
                ValidateTargetBinding(
                    value.binding,
                    baseSpawns,
                    targets,
                    report);
                if (string.IsNullOrWhiteSpace(value.replacementContentKey))
                {
                    report.Add(
                        DungeonStageOverrideValidationCodes.InvalidReplacement,
                        DungeonValidationSeverity.Error,
                        "Replacement content key is empty.",
                        value.binding != null
                            ? value.binding.cell
                            : (Vector2Int?)null,
                        value.binding != null
                            ? value.binding.spawnId
                            : string.Empty);
                }
            }
        }

        // 절대 transform operation의 target과 유한한 양수 scale을 검사합니다.
        private static void ValidateTransforms(
            List<DungeonSpawnTransformOverride> values,
            Dictionary<string, DungeonSpawnRecord> baseSpawns,
            HashSet<string> recordIds,
            HashSet<string> targets,
            DungeonValidationReport report)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnTransformOverride value = values[i];
                if (value == null)
                {
                    AddInvalidOperation(report, "Transform Override is null.");
                    continue;
                }
                ValidateRecordId(value.recordId, recordIds, report);
                ValidateTargetBinding(
                    value.binding,
                    baseSpawns,
                    targets,
                    report);
                if (!IsFinite(value.localPosition) ||
                    !IsFinite(value.pitchDegrees) ||
                    !IsFinite(value.yawDegrees) ||
                    !IsFinite(value.rollDegrees) ||
                    !IsFinite(value.localScale) ||
                    value.localScale.x <= 0f ||
                    value.localScale.y <= 0f ||
                    value.localScale.z <= 0f)
                {
                    report.Add(
                        DungeonStageOverrideValidationCodes.InvalidTransform,
                        DungeonValidationSeverity.Error,
                        "Transform Override requires finite position and rotation values with positive scale.",
                        value.binding != null
                            ? value.binding.cell
                            : (Vector2Int?)null,
                        value.binding != null
                            ? value.binding.spawnId
                            : string.Empty);
                }
            }
        }

        // Disable과 다른 변경이 같은 target에 동시에 적용되는 모호한 상태를 차단합니다.
        private static void ValidateDisabledConflicts(
            HashSet<string> disabled,
            HashSet<string> content,
            HashSet<string> transforms,
            DungeonValidationReport report)
        {
            foreach (string spawnId in disabled)
            {
                if (!content.Contains(spawnId) &&
                    !transforms.Contains(spawnId))
                {
                    continue;
                }
                report.Add(
                    DungeonStageOverrideValidationCodes.DisabledTargetConflict,
                    DungeonValidationSeverity.Error,
                    "A disabled spawn cannot also have content or transform Overrides.",
                    null,
                    spawnId);
            }
        }

        // 추가 spawn의 stable ID·Marker 금지·base 및 추가 목록 중복을 검사합니다.
        private static void ValidateAdded(
            List<DungeonSpawnRecord> values,
            Dictionary<string, DungeonSpawnRecord> baseSpawns,
            DungeonValidationReport report)
        {
            if (values == null) return;
            HashSet<string> addedIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnRecord value = values[i];
                if (value == null ||
                    string.IsNullOrWhiteSpace(value.spawnId) ||
                    string.IsNullOrWhiteSpace(value.contentKey) ||
                    !Enum.IsDefined(
                        typeof(DungeonSpawnCategory),
                        value.category))
                {
                    report.Add(
                        DungeonStageOverrideValidationCodes.InvalidAddedSpawn,
                        DungeonValidationSeverity.Error,
                        "Added spawn is incomplete.");
                    continue;
                }
                if (value.category == DungeonSpawnCategory.Marker)
                {
                    report.Add(
                        DungeonStageOverrideValidationCodes.ProtectedMarker,
                        DungeonValidationSeverity.Error,
                        "R7 does not allow adding entrance or exit Marker spawns.",
                        value.cell,
                        value.spawnId);
                }
                if (!addedIds.Add(value.spawnId))
                {
                    report.Add(
                        DungeonStageOverrideValidationCodes.DuplicateAddedSpawn,
                        DungeonValidationSeverity.Error,
                        "Added spawn IDs must be unique.",
                        value.cell,
                        value.spawnId);
                }
                if (baseSpawns.ContainsKey(value.spawnId))
                {
                    report.Add(
                        DungeonStageOverrideValidationCodes.AddedSpawnIdCollision,
                        DungeonValidationSeverity.Error,
                        "Added spawn ID collides with the base Blueprint.",
                        value.cell,
                        value.spawnId);
                }
            }
        }

        // operation의 제작용 record ID가 비어 있거나 전체 목록에서 중복인지 검사합니다.
        private static void ValidateRecordId(
            string recordId,
            HashSet<string> recordIds,
            DungeonValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(recordId))
            {
                AddInvalidOperation(
                    report,
                    "Override operation record ID is empty.");
                return;
            }
            if (!recordIds.Add(recordId))
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.DuplicateRecordId,
                    DungeonValidationSeverity.Error,
                    "Override operation record IDs must be unique.");
            }
        }

        // target stable ID, Marker 보호, 중복 operation과 binding snapshot 일치를 검사합니다.
        private static void ValidateTargetBinding(
            DungeonSpawnBindingSnapshot binding,
            Dictionary<string, DungeonSpawnRecord> baseSpawns,
            HashSet<string> targets,
            DungeonValidationReport report)
        {
            if (binding == null ||
                string.IsNullOrWhiteSpace(binding.spawnId))
            {
                AddInvalidOperation(
                    report,
                    "Override operation target binding is missing.");
                return;
            }
            if (!targets.Add(binding.spawnId))
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.DuplicateTarget,
                    DungeonValidationSeverity.Error,
                    "The same Override operation type targets a spawn more than once.",
                    binding.cell,
                    binding.spawnId);
            }
            DungeonSpawnRecord target;
            if (!baseSpawns.TryGetValue(binding.spawnId, out target))
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.MissingTarget,
                    DungeonValidationSeverity.Error,
                    "Override target spawn is missing from the base Blueprint.",
                    binding.cell,
                    binding.spawnId);
                return;
            }
            if (target.category == DungeonSpawnCategory.Marker)
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.ProtectedMarker,
                    DungeonValidationSeverity.Error,
                    "R7 does not allow editing entrance or exit Marker spawns.",
                    target.cell,
                    target.spawnId);
            }
            if (!binding.Matches(target))
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.BindingMismatch,
                    DungeonValidationSeverity.Error,
                    "Override binding snapshot no longer matches its base spawn. Explicit rebind approval is required.",
                    target.cell,
                    target.spawnId);
            }
        }

        // base spawn 목록을 ordinal stable ID lookup으로 변환합니다.
        private static Dictionary<string, DungeonSpawnRecord> BuildSpawnLookup(
            List<DungeonSpawnRecord> spawns)
        {
            Dictionary<string, DungeonSpawnRecord> result =
                new Dictionary<string, DungeonSpawnRecord>(
                    StringComparer.Ordinal);
            if (spawns == null) return result;
            for (int i = 0; i < spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = spawns[i];
                if (spawn == null ||
                    string.IsNullOrWhiteSpace(spawn.spawnId) ||
                    result.ContainsKey(spawn.spawnId))
                {
                    continue;
                }
                result.Add(spawn.spawnId, spawn);
            }
            return result;
        }

        // 잘못된 operation 공통 오류를 안정적인 코드로 추가합니다.
        private static void AddInvalidOperation(
            DungeonValidationReport report,
            string message)
        {
            report.Add(
                DungeonStageOverrideValidationCodes.InvalidOperation,
                DungeonValidationSeverity.Error,
                message);
        }

        // 하위 검증 리포트의 이슈를 원래 순서대로 병합합니다.
        private static void Merge(
            DungeonValidationReport destination,
            DungeonValidationReport source)
        {
            if (destination == null ||
                source == null ||
                source.issues == null)
            {
                return;
            }
            for (int i = 0; i < source.issues.Count; i++)
            {
                DungeonValidationIssue issue = source.issues[i];
                if (issue != null) destination.issues.Add(issue);
            }
        }

        // 단정밀도 값이 NaN이나 Infinity가 아닌지 확인합니다.
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // Vector3의 모든 축이 유한한지 확인합니다.
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }
    }
}
