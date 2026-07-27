using System;
using System.Collections.Generic;

namespace RogueDungeonLab
{
    public sealed class DungeonStageOverrideApplyResult
    {
        public DungeonBlueprint FinalBlueprint { get; internal set; }
        public DungeonValidationReport ValidationReport { get; internal set; }
        public string SourceBlueprintHash { get; internal set; }
        public string OverrideHash { get; internal set; }
        public string FinalBlueprintHash { get; internal set; }

        public bool IsValid
        {
            get
            {
                return FinalBlueprint != null &&
                       ValidationReport != null &&
                       ValidationReport.IsValid;
            }
        }
    }

    public static class DungeonStageOverrideApplier
    {
        // 저장 Blueprint 자산과 선택적 Override를 검증해 원본과 분리된 최종 Blueprint를 만듭니다.
        public static DungeonStageOverrideApplyResult Apply(
            DungeonBlueprintAsset sourceAsset,
            DungeonStageOverrides stageOverrides,
            bool verifyStoredHash = true)
        {
            DungeonBlueprint source =
                sourceAsset != null ? sourceAsset.blueprint : null;
            DungeonStageOverrideApplyResult result = ApplyInternal(
                source,
                stageOverrides,
                verifyStoredHash,
                sourceAsset);
            return result;
        }

        // 메모리 Blueprint에 선택적 Override를 적용해 테스트와 순수 데이터 소비 경로를 지원합니다.
        public static DungeonStageOverrideApplyResult Apply(
            DungeonBlueprint source,
            DungeonStageOverrides stageOverrides,
            bool verifyStoredHash = true)
        {
            return ApplyInternal(
                source,
                stageOverrides,
                verifyStoredHash,
                null);
        }

        // source 검증 뒤 operation을 고정 순서로 깊은 복사본에만 적용합니다.
        private static DungeonStageOverrideApplyResult ApplyInternal(
            DungeonBlueprint source,
            DungeonStageOverrides stageOverrides,
            bool verifyStoredHash,
            DungeonBlueprintAsset expectedBase)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            DungeonStageOverrideApplyResult result =
                new DungeonStageOverrideApplyResult
                {
                    ValidationReport = report,
                    SourceBlueprintHash = source != null
                        ? DungeonBlueprintHasher.Compute(source)
                        : string.Empty,
                    OverrideHash = stageOverrides != null
                        ? DungeonStageOverridesHasher.Compute(stageOverrides)
                        : string.Empty,
                    FinalBlueprintHash = string.Empty
                };
            if (source == null)
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.MissingBaseBlueprint,
                    DungeonValidationSeverity.Error,
                    "A source Blueprint is required before applying Stage Overrides.");
                return result;
            }

            Merge(report, DungeonBlueprintValidator.Validate(source));
            if (stageOverrides != null)
            {
                DungeonValidationReport overrideReport =
                    expectedBase != null
                        ? DungeonStageOverridesValidator.Validate(
                            stageOverrides,
                            expectedBase,
                            verifyStoredHash)
                        : DungeonStageOverridesValidator.ValidateAgainstBlueprint(
                            stageOverrides,
                            source,
                            verifyStoredHash);
                Merge(report, overrideReport);
            }
            if (!report.IsValid) return result;

            DungeonBlueprint finalBlueprint = source.DeepClone();
            if (stageOverrides != null)
            {
                ApplyDisabled(
                    finalBlueprint,
                    stageOverrides.disabledSpawns);
                ApplyContent(
                    finalBlueprint,
                    stageOverrides.contentOverrides);
                ApplyTransforms(
                    finalBlueprint,
                    stageOverrides.transformOverrides);
                ApplyAdded(
                    finalBlueprint,
                    stageOverrides.addedSpawns);
            }
            if (finalBlueprint.spawns != null)
                finalBlueprint.spawns.Sort(CompareSpawns);
            finalBlueprint.RefreshHash();

            DungeonValidationReport finalReport =
                DungeonBlueprintValidator.Validate(finalBlueprint);
            Merge(report, finalReport);
            if (!finalReport.IsValid)
            {
                report.Add(
                    DungeonStageOverrideValidationCodes.InvalidFinalBlueprint,
                    DungeonValidationSeverity.Error,
                    "Stage Overrides produced an invalid final Blueprint.");
                return result;
            }

            result.FinalBlueprint = finalBlueprint;
            result.FinalBlueprintHash = finalBlueprint.blueprintHash;
            return result;
        }

        // 비활성화 대상 stable ID를 최종 spawn 목록에서 제거합니다.
        private static void ApplyDisabled(
            DungeonBlueprint blueprint,
            List<DungeonSpawnDisableOverride> values)
        {
            if (blueprint.spawns == null || values == null) return;
            HashSet<string> disabled =
                new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnBindingSnapshot binding =
                    values[i] != null ? values[i].binding : null;
                if (binding != null) disabled.Add(binding.spawnId);
            }
            blueprint.spawns.RemoveAll(
                spawn => spawn != null &&
                         disabled.Contains(spawn.spawnId));
        }

        // 콘텐츠 교체 key를 같은 stable ID의 최종 spawn에 적용합니다.
        private static void ApplyContent(
            DungeonBlueprint blueprint,
            List<DungeonSpawnContentOverride> values)
        {
            Dictionary<string, DungeonSpawnRecord> lookup =
                BuildSpawnLookup(blueprint.spawns);
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnContentOverride value = values[i];
                DungeonSpawnRecord target;
                if (value == null ||
                    value.binding == null ||
                    !lookup.TryGetValue(
                        value.binding.spawnId,
                        out target))
                {
                    continue;
                }
                target.contentKey =
                    value.replacementContentKey ?? string.Empty;
            }
        }

        // 위치·회전·scale의 절대값을 같은 stable ID의 최종 spawn에 적용합니다.
        private static void ApplyTransforms(
            DungeonBlueprint blueprint,
            List<DungeonSpawnTransformOverride> values)
        {
            Dictionary<string, DungeonSpawnRecord> lookup =
                BuildSpawnLookup(blueprint.spawns);
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnTransformOverride value = values[i];
                DungeonSpawnRecord target;
                if (value == null ||
                    value.binding == null ||
                    !lookup.TryGetValue(
                        value.binding.spawnId,
                        out target))
                {
                    continue;
                }
                target.localPosition = value.localPosition;
                target.pitchDegrees = value.pitchDegrees;
                target.yawDegrees = value.yawDegrees;
                target.rollDegrees = value.rollDegrees;
                target.localScale = value.localScale;
            }
        }

        // 사용자가 만든 spawn 레코드를 컬렉션까지 분리된 복사본으로 최종 목록에 추가합니다.
        private static void ApplyAdded(
            DungeonBlueprint blueprint,
            List<DungeonSpawnRecord> values)
        {
            if (values == null) return;
            if (blueprint.spawns == null)
                blueprint.spawns = new List<DungeonSpawnRecord>();
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnRecord value = values[i];
                if (value != null)
                    blueprint.spawns.Add(CloneSpawn(value));
            }
        }

        // 하나의 spawn 레코드와 tag 목록을 수동 깊은 복사합니다.
        internal static DungeonSpawnRecord CloneSpawn(
            DungeonSpawnRecord source)
        {
            if (source == null) return null;
            return new DungeonSpawnRecord
            {
                spawnId = source.spawnId ?? string.Empty,
                category = source.category,
                contentKey = source.contentKey ?? string.Empty,
                instanceName = source.instanceName ?? string.Empty,
                cell = source.cell,
                localPosition = source.localPosition,
                pitchDegrees = source.pitchDegrees,
                yawDegrees = source.yawDegrees,
                rollDegrees = source.rollDegrees,
                localScale = source.localScale,
                roomId = source.roomId ?? string.Empty,
                progression = source.progression,
                tags = source.tags != null
                    ? new List<string>(source.tags)
                    : new List<string>(),
                variantSeed = source.variantSeed
            };
        }

        // spawn 목록을 ordinal stable ID와 category·cell 순으로 비교합니다.
        private static int CompareSpawns(
            DungeonSpawnRecord left,
            DungeonSpawnRecord right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result = string.CompareOrdinal(left.spawnId, right.spawnId);
            if (result != 0) return result;
            result = ((int)left.category).CompareTo((int)right.category);
            if (result != 0) return result;
            result = left.cell.y.CompareTo(right.cell.y);
            return result != 0 ? result : left.cell.x.CompareTo(right.cell.x);
        }

        // Blueprint spawn 목록을 stable ID lookup으로 변환합니다.
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
                if (spawn != null &&
                    !string.IsNullOrWhiteSpace(spawn.spawnId) &&
                    !result.ContainsKey(spawn.spawnId))
                {
                    result.Add(spawn.spawnId, spawn);
                }
            }
            return result;
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
    }
}
