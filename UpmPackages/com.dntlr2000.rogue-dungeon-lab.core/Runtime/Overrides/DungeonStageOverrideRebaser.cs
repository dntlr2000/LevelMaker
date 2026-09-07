using System;
using System.Collections.Generic;

namespace RogueDungeonLab
{
    public enum DungeonStageOverrideOperationKind
    {
        Disable = 0,
        Content = 1,
        Transform = 2,
        AddedSpawn = 3
    }

    public enum DungeonStageOverrideRebindStatus
    {
        Exact = 0,
        ChangedExact = 1,
        UniqueSuggestion = 2,
        Missing = 3,
        Ambiguous = 4,
        Collision = 5,
        AddedIdCollision = 6
    }

    public static class DungeonStageOverrideRebindValidationCodes
    {
        public const string MissingCandidateBase = "RDL-OVR-REBIND-001";
        public const string MissingTarget = "RDL-OVR-REBIND-002";
        public const string AmbiguousTarget = "RDL-OVR-REBIND-003";
        public const string CandidateCollision = "RDL-OVR-REBIND-004";
        public const string AddedIdCollision = "RDL-OVR-REBIND-005";
    }

    public sealed class DungeonStageOverrideRebindEntry
    {
        public DungeonStageOverrideOperationKind OperationKind { get; internal set; }
        public string RecordId { get; internal set; }
        public string PreviousSpawnId { get; internal set; }
        public string ProposedSpawnId { get; internal set; }
        public DungeonSpawnBindingSnapshot ProposedBinding { get; internal set; }
        public DungeonStageOverrideRebindStatus Status { get; internal set; }
        public List<string> CandidateSpawnIds { get; internal set; }
    }

    public sealed class DungeonStageOverrideRebindPlan
    {
        public DungeonStageOverrides StageOverrides { get; internal set; }
        public DungeonBlueprintAsset CandidateBase { get; internal set; }
        public string CandidateBaseHash { get; internal set; }
        public List<DungeonStageOverrideRebindEntry> Entries { get; internal set; }
        public DungeonValidationReport ValidationReport { get; internal set; }

        public bool CanCommit
        {
            get
            {
                return CandidateBase != null &&
                       ValidationReport != null &&
                       ValidationReport.IsValid;
            }
        }
    }

    public static class DungeonStageOverrideRebaser
    {
        // 현재 Override와 새 원본을 변경하지 않고 exact·유일 후보·충돌 재결합 계획을 계산합니다.
        public static DungeonStageOverrideRebindPlan Analyze(
            DungeonStageOverrides stageOverrides,
            DungeonBlueprintAsset candidateBase)
        {
            DungeonValidationReport report = new DungeonValidationReport();
            DungeonStageOverrideRebindPlan plan =
                new DungeonStageOverrideRebindPlan
                {
                    StageOverrides = stageOverrides,
                    CandidateBase = candidateBase,
                    CandidateBaseHash =
                        candidateBase != null &&
                        candidateBase.blueprint != null
                            ? DungeonBlueprintHasher.Compute(
                                candidateBase.blueprint)
                            : string.Empty,
                    Entries =
                        new List<DungeonStageOverrideRebindEntry>(),
                    ValidationReport = report
                };
            if (stageOverrides == null ||
                candidateBase == null ||
                candidateBase.blueprint == null)
            {
                report.Add(
                    DungeonStageOverrideRebindValidationCodes
                        .MissingCandidateBase,
                    DungeonValidationSeverity.Error,
                    "Rebind analysis requires Stage Overrides and a valid candidate Blueprint.");
                return plan;
            }

            DungeonValidationReport blueprintReport =
                DungeonBlueprintValidator.Validate(
                    candidateBase.blueprint);
            Merge(report, blueprintReport);
            if (!blueprintReport.IsValid) return plan;

            List<DungeonSpawnRecord> candidates =
                candidateBase.blueprint.spawns != null
                    ? new List<DungeonSpawnRecord>(
                        candidateBase.blueprint.spawns)
                    : new List<DungeonSpawnRecord>();
            candidates.Sort(CompareSpawns);
            Dictionary<string, DungeonSpawnRecord> byId =
                BuildSpawnLookup(candidates);

            AnalyzeDisabled(
                stageOverrides.disabledSpawns,
                candidates,
                byId,
                plan.Entries);
            AnalyzeContent(
                stageOverrides.contentOverrides,
                candidates,
                byId,
                plan.Entries);
            AnalyzeTransforms(
                stageOverrides.transformOverrides,
                candidates,
                byId,
                plan.Entries);
            AnalyzeAdded(
                stageOverrides.addedSpawns,
                byId,
                plan.Entries);
            MarkCandidateCollisions(plan.Entries);
            plan.Entries.Sort(CompareEntries);
            AddPlanIssues(plan.Entries, report);
            return plan;
        }

        // 비활성화 operation마다 새 원본의 재결합 상태를 계산합니다.
        private static void AnalyzeDisabled(
            List<DungeonSpawnDisableOverride> values,
            List<DungeonSpawnRecord> candidates,
            Dictionary<string, DungeonSpawnRecord> byId,
            List<DungeonStageOverrideRebindEntry> entries)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnDisableOverride value = values[i];
                entries.Add(AnalyzeBinding(
                    DungeonStageOverrideOperationKind.Disable,
                    value != null ? value.recordId : string.Empty,
                    value != null ? value.binding : null,
                    candidates,
                    byId));
            }
        }

        // 콘텐츠 교체 operation마다 새 원본의 재결합 상태를 계산합니다.
        private static void AnalyzeContent(
            List<DungeonSpawnContentOverride> values,
            List<DungeonSpawnRecord> candidates,
            Dictionary<string, DungeonSpawnRecord> byId,
            List<DungeonStageOverrideRebindEntry> entries)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnContentOverride value = values[i];
                entries.Add(AnalyzeBinding(
                    DungeonStageOverrideOperationKind.Content,
                    value != null ? value.recordId : string.Empty,
                    value != null ? value.binding : null,
                    candidates,
                    byId));
            }
        }

        // transform operation마다 새 원본의 재결합 상태를 계산합니다.
        private static void AnalyzeTransforms(
            List<DungeonSpawnTransformOverride> values,
            List<DungeonSpawnRecord> candidates,
            Dictionary<string, DungeonSpawnRecord> byId,
            List<DungeonStageOverrideRebindEntry> entries)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnTransformOverride value = values[i];
                entries.Add(AnalyzeBinding(
                    DungeonStageOverrideOperationKind.Transform,
                    value != null ? value.recordId : string.Empty,
                    value != null ? value.binding : null,
                    candidates,
                    byId));
            }
        }

        // 추가 spawn ID가 새 원본에 생겨난 경우 별도 충돌 항목으로 기록합니다.
        private static void AnalyzeAdded(
            List<DungeonSpawnRecord> values,
            Dictionary<string, DungeonSpawnRecord> byId,
            List<DungeonStageOverrideRebindEntry> entries)
        {
            if (values == null) return;
            for (int i = 0; i < values.Count; i++)
            {
                DungeonSpawnRecord value = values[i];
                if (value == null ||
                    !byId.ContainsKey(value.spawnId ?? string.Empty))
                {
                    continue;
                }
                entries.Add(new DungeonStageOverrideRebindEntry
                {
                    OperationKind =
                        DungeonStageOverrideOperationKind.AddedSpawn,
                    RecordId = value.spawnId ?? string.Empty,
                    PreviousSpawnId = value.spawnId ?? string.Empty,
                    ProposedSpawnId = string.Empty,
                    ProposedBinding = null,
                    Status =
                        DungeonStageOverrideRebindStatus
                            .AddedIdCollision,
                    CandidateSpawnIds =
                        new List<string> { value.spawnId ?? string.Empty }
                });
            }
        }

        // 하나의 binding을 exact ID 우선, 의미 anchor 유일 후보 순으로 분석합니다.
        private static DungeonStageOverrideRebindEntry AnalyzeBinding(
            DungeonStageOverrideOperationKind kind,
            string recordId,
            DungeonSpawnBindingSnapshot binding,
            List<DungeonSpawnRecord> candidates,
            Dictionary<string, DungeonSpawnRecord> byId)
        {
            DungeonStageOverrideRebindEntry result =
                new DungeonStageOverrideRebindEntry
                {
                    OperationKind = kind,
                    RecordId = recordId ?? string.Empty,
                    PreviousSpawnId =
                        binding != null
                            ? binding.spawnId ?? string.Empty
                            : string.Empty,
                    ProposedSpawnId = string.Empty,
                    ProposedBinding = null,
                    CandidateSpawnIds = new List<string>(),
                    Status = DungeonStageOverrideRebindStatus.Missing
                };
            if (binding == null) return result;

            DungeonSpawnRecord exact;
            if (byId.TryGetValue(
                    binding.spawnId ?? string.Empty,
                    out exact))
            {
                result.ProposedSpawnId = exact.spawnId;
                result.ProposedBinding =
                    DungeonSpawnBindingSnapshot.Capture(exact);
                result.CandidateSpawnIds.Add(exact.spawnId);
                result.Status = binding.Matches(exact)
                    ? DungeonStageOverrideRebindStatus.Exact
                    : DungeonStageOverrideRebindStatus.ChangedExact;
                return result;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                DungeonSpawnRecord candidate = candidates[i];
                if (binding.Matches(candidate))
                    result.CandidateSpawnIds.Add(candidate.spawnId);
            }
            if (result.CandidateSpawnIds.Count == 1)
            {
                result.ProposedSpawnId =
                    result.CandidateSpawnIds[0];
                result.ProposedBinding =
                    DungeonSpawnBindingSnapshot.Capture(
                        byId[result.ProposedSpawnId]);
                result.Status =
                    DungeonStageOverrideRebindStatus.UniqueSuggestion;
            }
            else if (result.CandidateSpawnIds.Count > 1)
            {
                result.Status =
                    DungeonStageOverrideRebindStatus.Ambiguous;
            }
            return result;
        }

        // 서로 다른 이전 target이 같은 새 candidate를 점유하면 두 항목을 충돌로 승격합니다.
        private static void MarkCandidateCollisions(
            List<DungeonStageOverrideRebindEntry> entries)
        {
            Dictionary<string, HashSet<string>> sourcesByCandidate =
                new Dictionary<string, HashSet<string>>(
                    StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                DungeonStageOverrideRebindEntry entry = entries[i];
                if (entry == null ||
                    string.IsNullOrEmpty(entry.ProposedSpawnId) ||
                    entry.OperationKind ==
                    DungeonStageOverrideOperationKind.AddedSpawn)
                {
                    continue;
                }
                HashSet<string> sources;
                if (!sourcesByCandidate.TryGetValue(
                        entry.ProposedSpawnId,
                        out sources))
                {
                    sources = new HashSet<string>(
                        StringComparer.Ordinal);
                    sourcesByCandidate.Add(
                        entry.ProposedSpawnId,
                        sources);
                }
                sources.Add(entry.PreviousSpawnId ?? string.Empty);
            }
            for (int i = 0; i < entries.Count; i++)
            {
                DungeonStageOverrideRebindEntry entry = entries[i];
                HashSet<string> sources;
                if (entry != null &&
                    !string.IsNullOrEmpty(entry.ProposedSpawnId) &&
                    sourcesByCandidate.TryGetValue(
                        entry.ProposedSpawnId,
                        out sources) &&
                    sources.Count > 1)
                {
                    entry.Status =
                        DungeonStageOverrideRebindStatus.Collision;
                }
            }
        }

        // 미해결 rebind 상태를 일반 검증 리포트의 안정적인 오류 코드로 변환합니다.
        private static void AddPlanIssues(
            List<DungeonStageOverrideRebindEntry> entries,
            DungeonValidationReport report)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                DungeonStageOverrideRebindEntry entry = entries[i];
                if (entry == null) continue;
                switch (entry.Status)
                {
                    case DungeonStageOverrideRebindStatus.Missing:
                        report.Add(
                            DungeonStageOverrideRebindValidationCodes
                                .MissingTarget,
                            DungeonValidationSeverity.Error,
                            "No exact or unique semantic target exists for this Override.",
                            null,
                            entry.PreviousSpawnId);
                        break;
                    case DungeonStageOverrideRebindStatus.Ambiguous:
                        report.Add(
                            DungeonStageOverrideRebindValidationCodes
                                .AmbiguousTarget,
                            DungeonValidationSeverity.Error,
                            "Multiple semantic candidates exist for this Override.",
                            null,
                            entry.PreviousSpawnId);
                        break;
                    case DungeonStageOverrideRebindStatus.Collision:
                        report.Add(
                            DungeonStageOverrideRebindValidationCodes
                                .CandidateCollision,
                            DungeonValidationSeverity.Error,
                            "Different previous targets resolve to the same new spawn.",
                            null,
                            entry.PreviousSpawnId);
                        break;
                    case DungeonStageOverrideRebindStatus
                        .AddedIdCollision:
                        report.Add(
                            DungeonStageOverrideRebindValidationCodes
                                .AddedIdCollision,
                            DungeonValidationSeverity.Error,
                            "An added spawn ID now collides with the new base Blueprint.",
                            null,
                            entry.PreviousSpawnId);
                        break;
                }
            }
        }

        // candidate spawn 목록을 ordinal stable ID lookup으로 변환합니다.
        private static Dictionary<string, DungeonSpawnRecord> BuildSpawnLookup(
            List<DungeonSpawnRecord> spawns)
        {
            Dictionary<string, DungeonSpawnRecord> result =
                new Dictionary<string, DungeonSpawnRecord>(
                    StringComparer.Ordinal);
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

        // spawn을 stable ID와 category·cell 순으로 비교합니다.
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

        // 계획 항목을 operation 종류·record ID·기존 target 순으로 정렬합니다.
        private static int CompareEntries(
            DungeonStageOverrideRebindEntry left,
            DungeonStageOverrideRebindEntry right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int result =
                ((int)left.OperationKind).CompareTo(
                    (int)right.OperationKind);
            if (result != 0) return result;
            result = string.CompareOrdinal(
                left.RecordId,
                right.RecordId);
            return result != 0
                ? result
                : string.CompareOrdinal(
                    left.PreviousSpawnId,
                    right.PreviousSpawnId);
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
