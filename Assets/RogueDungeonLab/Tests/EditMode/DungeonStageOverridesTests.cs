using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDungeonLab.Tests.EditMode
{
    public sealed class DungeonStageOverridesTests
    {
        private const string RoomId = "room-r7-fixture";
        private const string EntranceId = "marker:entrance";
        private const string ExitId = "marker:exit";
        private const string DisabledEnemyId = "base:enemy:disable";
        private const string KeptEnemyId = "base:enemy:keep";
        private const string ReplacedCrateId = "base:crate:replace";
        private const string TransformedPropId = "base:prop:transform";
        private const string SecondPropId = "base:prop:second";
        private const string GimmickId = "base:gimmick:keep";
        private const string AddedCrateId = "override:r7-fixture:add:crate";

        // Override 목록 순서와 제작 메모가 canonical hash를 바꾸지 않는지 검사합니다.
        [Test]
        public void Hasher_IsStableAcrossListOrderAndAuthoringMetadata()
        {
            DungeonBlueprintAsset baseAsset = CreateBlueprintAsset();
            DungeonStageOverrides stageOverrides = CreateValidOverrides(baseAsset);
            try
            {
                stageOverrides.disabledSpawns.Add(
                    CreateDisable(
                        "disable-gimmick",
                        FindSpawn(baseAsset.blueprint, GimmickId)));
                stageOverrides.contentOverrides.Add(
                    CreateContent(
                        "replace-kept-enemy",
                        FindSpawn(baseAsset.blueprint, KeptEnemyId),
                        "custom/enemy/replacement"));
                stageOverrides.transformOverrides.Add(
                    CreateTransform(
                        "transform-second-prop",
                        FindSpawn(baseAsset.blueprint, SecondPropId),
                        new Vector3(9f, 0.75f, 3f),
                        5f,
                        80f,
                        -5f,
                        new Vector3(0.8f, 1.1f, 0.9f)));
                stageOverrides.addedSpawns.Add(
                    CreateSpawn(
                        "override:r7-fixture:add:prop",
                        DungeonSpawnCategory.Prop,
                        DungeonBuiltInContentKeys.PropCylinder,
                        new Vector2Int(0, 2),
                        809));
                stageOverrides.addedSpawns[0].tags.Add("zeta");
                stageOverrides.addedSpawns[0].tags.Add("alpha");

                string first = DungeonStageOverridesHasher.Compute(stageOverrides);
                stageOverrides.disabledSpawns.Reverse();
                stageOverrides.addedSpawns.Reverse();
                stageOverrides.contentOverrides.Reverse();
                stageOverrides.transformOverrides.Reverse();
                for (int i = 0; i < stageOverrides.addedSpawns.Count; i++)
                    stageOverrides.addedSpawns[i].tags.Reverse();
                stageOverrides.authoringNote = "hash에서 제외되는 제작 메모";
                string reordered = DungeonStageOverridesHasher.Compute(stageOverrides);

                Assert.That(first, Has.Length.EqualTo(64));
                Assert.That(reordered, Is.EqualTo(first));
            }
            finally
            {
                Object.DestroyImmediate(stageOverrides);
                Object.DestroyImmediate(baseAsset);
            }
        }

        // 네 가지 Spawn Override가 원본을 바꾸지 않고 깊은 복사된 최종 Blueprint에 절대값으로 적용되는지 검사합니다.
        [Test]
        public void Applier_DisablesAddsReplacesAndAppliesAbsoluteTransformWithoutMutatingSource()
        {
            DungeonBlueprintAsset baseAsset = CreateBlueprintAsset();
            DungeonStageOverrides stageOverrides = CreateValidOverrides(baseAsset);
            try
            {
                string sourceJson =
                    DungeonBlueprintSerialization.ToJson(baseAsset.blueprint);
                string sourceHash = baseAsset.blueprint.blueprintHash;

                DungeonStageOverrideApplyResult first =
                    DungeonStageOverrideApplier.Apply(
                        baseAsset,
                        stageOverrides);
                DungeonStageOverrideApplyResult second =
                    DungeonStageOverrideApplier.Apply(
                        baseAsset,
                        stageOverrides);

                Assert.That(first.IsValid, Is.True);
                Assert.That(second.IsValid, Is.True);
                Assert.That(first.SourceBlueprintHash, Is.EqualTo(sourceHash));
                Assert.That(
                    first.OverrideHash,
                    Is.EqualTo(stageOverrides.overrideHash));
                Assert.That(
                    first.FinalBlueprintHash,
                    Is.EqualTo(second.FinalBlueprintHash));
                Assert.That(
                    first.FinalBlueprintHash,
                    Is.Not.EqualTo(sourceHash));
                Assert.That(
                    DungeonBlueprintSerialization.ToJson(baseAsset.blueprint),
                    Is.EqualTo(sourceJson));
                Assert.That(
                    baseAsset.blueprint.blueprintHash,
                    Is.EqualTo(sourceHash));

                DungeonBlueprint finalBlueprint = first.FinalBlueprint;
                Assert.That(
                    FindSpawnOrNull(finalBlueprint, DisabledEnemyId),
                    Is.Null);
                DungeonSpawnRecord added =
                    FindSpawn(finalBlueprint, AddedCrateId);
                Assert.That(added.category, Is.EqualTo(DungeonSpawnCategory.Destructible));
                Assert.That(
                    added,
                    Is.Not.SameAs(stageOverrides.addedSpawns[0]));
                Assert.That(
                    added.tags,
                    Is.Not.SameAs(stageOverrides.addedSpawns[0].tags));
                Assert.That(
                    FindSpawn(finalBlueprint, ReplacedCrateId).contentKey,
                    Is.EqualTo("custom/destructible/replacement"));

                DungeonSpawnRecord transformed =
                    FindSpawn(finalBlueprint, TransformedPropId);
                Assert.That(
                    transformed.localPosition,
                    Is.EqualTo(new Vector3(6.5f, 0.75f, 3.25f)));
                Assert.That(transformed.pitchDegrees, Is.EqualTo(10f));
                Assert.That(transformed.yawDegrees, Is.EqualTo(35f));
                Assert.That(transformed.rollDegrees, Is.EqualTo(-4f));
                Assert.That(
                    transformed.localScale,
                    Is.EqualTo(new Vector3(1.25f, 1.5f, 0.8f)));

                DungeonSpawnRecord sourceProp =
                    FindSpawn(baseAsset.blueprint, TransformedPropId);
                Assert.That(
                    sourceProp.localPosition,
                    Is.EqualTo(new Vector3(6f, 0.5f, 3f)));
                Assert.That(sourceProp.yawDegrees, Is.EqualTo(0f));
                Assert.That(sourceProp.localScale, Is.EqualTo(Vector3.one));
                Assert.That(
                    DungeonBlueprintValidator.Validate(finalBlueprint).IsValid,
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(stageOverrides);
                Object.DestroyImmediate(baseAsset);
            }
        }

        // 같은 target의 Disable 충돌과 Marker 편집 및 base ID를 재사용하는 Add를 코드 기반 오류로 차단하는지 검사합니다.
        [Test]
        public void Validator_RejectsTargetConflictsMarkerEditsAndAddedIdCollisions()
        {
            DungeonBlueprintAsset baseAsset = CreateBlueprintAsset();
            DungeonStageOverrides stageOverrides = CreateValidOverrides(baseAsset);
            try
            {
                stageOverrides.contentOverrides.Add(
                    CreateContent(
                        "replace-disabled-enemy",
                        FindSpawn(baseAsset.blueprint, DisabledEnemyId),
                        "custom/enemy/replacement"));
                stageOverrides.transformOverrides.Add(
                    CreateTransform(
                        "transform-entrance-marker",
                        FindSpawn(baseAsset.blueprint, EntranceId),
                        Vector3.zero,
                        0f,
                        0f,
                        0f,
                        Vector3.one));
                stageOverrides.addedSpawns.Add(
                    CreateSpawn(
                        KeptEnemyId,
                        DungeonSpawnCategory.Enemy,
                        DungeonBuiltInContentKeys.Enemy,
                        new Vector2Int(0, 2),
                        999));
                stageOverrides.RefreshHash();

                DungeonValidationReport report =
                    DungeonStageOverridesValidator.Validate(
                        stageOverrides,
                        baseAsset);
                DungeonStageOverrideApplyResult applied =
                    DungeonStageOverrideApplier.Apply(
                        baseAsset,
                        stageOverrides);

                Assert.That(report.IsValid, Is.False);
                Assert.That(
                    report.ContainsCode(
                        DungeonStageOverrideValidationCodes.DisabledTargetConflict),
                    Is.True);
                Assert.That(
                    report.ContainsCode(
                        DungeonStageOverrideValidationCodes.ProtectedMarker),
                    Is.True);
                Assert.That(
                    report.ContainsCode(
                        DungeonStageOverrideValidationCodes.AddedSpawnIdCollision),
                    Is.True);
                Assert.That(applied.IsValid, Is.False);
                Assert.That(applied.FinalBlueprint, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(stageOverrides);
                Object.DestroyImmediate(baseAsset);
            }
        }

        // Override가 기록한 기준 hash와 현재 Blueprint hash가 다르면 적용 전에 명시적 재결합을 요구하는지 검사합니다.
        [Test]
        public void Validator_RejectsBaseBlueprintHashMismatchBeforeApply()
        {
            DungeonBlueprintAsset baseAsset = CreateBlueprintAsset();
            DungeonStageOverrides stageOverrides = CreateValidOverrides(baseAsset);
            try
            {
                stageOverrides.baseBlueprintHash =
                    "stale-r7-base-blueprint-hash";
                stageOverrides.RefreshHash();

                DungeonValidationReport report =
                    DungeonStageOverridesValidator.Validate(
                        stageOverrides,
                        baseAsset);
                DungeonStageOverrideApplyResult result =
                    DungeonStageOverrideApplier.Apply(
                        baseAsset,
                        stageOverrides);

                Assert.That(report.IsValid, Is.False);
                Assert.That(
                    report.ContainsCode(
                        DungeonStageOverrideValidationCodes.BaseHashMismatch),
                    Is.True);
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.FinalBlueprint, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(stageOverrides);
                Object.DestroyImmediate(baseAsset);
            }
        }

        // 같은 ID의 exact target과 ID만 달라진 유일 의미 후보를 결정적인 재결합 상태로 보고하는지 검사합니다.
        [Test]
        public void Rebaser_ReportsExactAndUniqueSemanticMatches()
        {
            DungeonBlueprintAsset baseAsset = CreateBlueprintAsset();
            DungeonStageOverrides stageOverrides = CreateValidOverrides(baseAsset);
            DungeonBlueprintAsset renamedAsset = null;
            try
            {
                DungeonStageOverrideRebindPlan exact =
                    DungeonStageOverrideRebaser.Analyze(
                        stageOverrides,
                        baseAsset);
                Assert.That(exact.CanCommit, Is.True);
                Assert.That(
                    FindRebindEntry(exact, "disable-enemy").Status,
                    Is.EqualTo(DungeonStageOverrideRebindStatus.Exact));
                Assert.That(
                    FindRebindEntry(exact, "replace-crate").Status,
                    Is.EqualTo(DungeonStageOverrideRebindStatus.Exact));
                Assert.That(
                    FindRebindEntry(exact, "transform-prop").Status,
                    Is.EqualTo(DungeonStageOverrideRebindStatus.Exact));

                DungeonBlueprint renamed = baseAsset.blueprint.DeepClone();
                FindSpawn(renamed, DisabledEnemyId).spawnId =
                    "regen:enemy:disable";
                renamed.spawns.Reverse();
                renamed.RefreshHash();
                renamedAsset = CreateBlueprintAsset(renamed);

                DungeonStageOverrideRebindPlan unique =
                    DungeonStageOverrideRebaser.Analyze(
                        stageOverrides,
                        renamedAsset);
                DungeonStageOverrideRebindEntry uniqueEntry =
                    FindRebindEntry(unique, "disable-enemy");

                Assert.That(unique.CanCommit, Is.True);
                Assert.That(
                    uniqueEntry.Status,
                    Is.EqualTo(
                        DungeonStageOverrideRebindStatus.UniqueSuggestion));
                Assert.That(
                    uniqueEntry.PreviousSpawnId,
                    Is.EqualTo(DisabledEnemyId));
                Assert.That(
                    uniqueEntry.ProposedSpawnId,
                    Is.EqualTo("regen:enemy:disable"));
                Assert.That(
                    uniqueEntry.CandidateSpawnIds,
                    Is.EqualTo(new[] { "regen:enemy:disable" }));
                Assert.That(
                    stageOverrides.disabledSpawns[0].binding.spawnId,
                    Is.EqualTo(DisabledEnemyId));
                Assert.That(
                    stageOverrides.baseBlueprint,
                    Is.SameAs(baseAsset));
            }
            finally
            {
                if (renamedAsset != null)
                    Object.DestroyImmediate(renamedAsset);
                Object.DestroyImmediate(stageOverrides);
                Object.DestroyImmediate(baseAsset);
            }
        }

        // 후보가 없거나 의미 anchor가 같은 후보가 여러 개인 재결합을 출시 차단 오류로 보고하는지 검사합니다.
        [Test]
        public void Rebaser_ReportsMissingAndAmbiguousTargetsAsBlockingConflicts()
        {
            DungeonBlueprintAsset baseAsset = CreateBlueprintAsset();
            DungeonStageOverrides stageOverrides = CreateValidOverrides(baseAsset);
            DungeonBlueprintAsset missingAsset = null;
            DungeonBlueprintAsset ambiguousAsset = null;
            try
            {
                DungeonBlueprint missing = baseAsset.blueprint.DeepClone();
                DungeonSpawnRecord original =
                    FindSpawn(missing, DisabledEnemyId);
                missing.spawns.Remove(original);
                missing.RefreshHash();
                missingAsset = CreateBlueprintAsset(missing);

                DungeonStageOverrideRebindPlan missingPlan =
                    DungeonStageOverrideRebaser.Analyze(
                        stageOverrides,
                        missingAsset);
                DungeonStageOverrideRebindEntry missingEntry =
                    FindRebindEntry(missingPlan, "disable-enemy");
                Assert.That(missingPlan.CanCommit, Is.False);
                Assert.That(
                    missingEntry.Status,
                    Is.EqualTo(DungeonStageOverrideRebindStatus.Missing));
                Assert.That(
                    missingPlan.ValidationReport.ContainsCode(
                        DungeonStageOverrideRebindValidationCodes.MissingTarget),
                    Is.True);

                DungeonBlueprint ambiguous =
                    baseAsset.blueprint.DeepClone();
                DungeonSpawnRecord ambiguousOriginal =
                    FindSpawn(ambiguous, DisabledEnemyId);
                ambiguous.spawns.Remove(ambiguousOriginal);
                DungeonSpawnRecord candidateB =
                    CloneSpawn(
                        ambiguousOriginal,
                        "candidate:enemy:b");
                DungeonSpawnRecord candidateA =
                    CloneSpawn(
                        ambiguousOriginal,
                        "candidate:enemy:a");
                ambiguous.spawns.Add(candidateB);
                ambiguous.spawns.Add(candidateA);
                ambiguous.RefreshHash();
                ambiguousAsset = CreateBlueprintAsset(ambiguous);

                DungeonStageOverrideRebindPlan ambiguousPlan =
                    DungeonStageOverrideRebaser.Analyze(
                        stageOverrides,
                        ambiguousAsset);
                DungeonStageOverrideRebindEntry ambiguousEntry =
                    FindRebindEntry(ambiguousPlan, "disable-enemy");
                Assert.That(ambiguousPlan.CanCommit, Is.False);
                Assert.That(
                    ambiguousEntry.Status,
                    Is.EqualTo(DungeonStageOverrideRebindStatus.Ambiguous));
                Assert.That(
                    ambiguousEntry.CandidateSpawnIds,
                    Is.EqualTo(
                        new[]
                        {
                            "candidate:enemy:a",
                            "candidate:enemy:b"
                        }));
                Assert.That(
                    ambiguousPlan.ValidationReport.ContainsCode(
                        DungeonStageOverrideRebindValidationCodes.AmbiguousTarget),
                    Is.True);
            }
            finally
            {
                if (ambiguousAsset != null)
                    Object.DestroyImmediate(ambiguousAsset);
                if (missingAsset != null)
                    Object.DestroyImmediate(missingAsset);
                Object.DestroyImmediate(stageOverrides);
                Object.DestroyImmediate(baseAsset);
            }
        }

        // 4x3 연결 floor와 고정 spawn 집합을 가진 유효 Blueprint 자산을 만듭니다.
        private static DungeonBlueprintAsset CreateBlueprintAsset()
        {
            return CreateBlueprintAsset(CreateBlueprint());
        }

        // 지정 Blueprint를 컬렉션까지 분리해 보관하는 메모리 Blueprint 자산을 만듭니다.
        private static DungeonBlueprintAsset CreateBlueprintAsset(
            DungeonBlueprint blueprint)
        {
            DungeonBlueprintAsset asset =
                ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            asset.Store(blueprint);
            return asset;
        }

        // 모든 R7 spawn 작업을 독립적으로 관찰할 수 있는 수제 Blueprint를 만듭니다.
        private static DungeonBlueprint CreateBlueprint()
        {
            DungeonBlueprint blueprint = new DungeonBlueprint
            {
                formatVersion = DungeonBlueprintFormat.CurrentVersion,
                generatorVersion = DungeonGeneratorVersions.LegacyV1,
                seed = 70707,
                recipeHash = "r7-fixture-recipe",
                catalogPlanningHash =
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                grid = new DungeonGridRecord
                {
                    width = 4,
                    depth = 3,
                    cellSize = 3f,
                    wallHeight = 3.2f
                },
                entrance = new Vector2Int(0, 0),
                exit = new Vector2Int(3, 2),
                rooms = new List<DungeonRoomRecord>
                {
                    new DungeonRoomRecord
                    {
                        roomId = RoomId,
                        bounds = new RectInt(0, 0, 4, 3)
                    }
                }
            };
            for (int z = 0; z < blueprint.grid.depth; z++)
            for (int x = 0; x < blueprint.grid.width; x++)
            {
                blueprint.cells.Add(new DungeonCellRecord
                {
                    coordinate = new Vector2Int(x, z),
                    flags = DungeonCellFlags.Floor,
                    roomId = RoomId,
                    distanceFromEntrance = x + z
                });
            }

            blueprint.spawns.Add(
                CreateSpawn(
                    EntranceId,
                    DungeonSpawnCategory.Marker,
                    DungeonBuiltInContentKeys.EntranceMarker,
                    blueprint.entrance,
                    1));
            blueprint.spawns.Add(
                CreateSpawn(
                    ExitId,
                    DungeonSpawnCategory.Marker,
                    DungeonBuiltInContentKeys.ExitMarker,
                    blueprint.exit,
                    2));
            blueprint.spawns.Add(
                CreateSpawn(
                    DisabledEnemyId,
                    DungeonSpawnCategory.Enemy,
                    DungeonBuiltInContentKeys.Enemy,
                    new Vector2Int(1, 0),
                    101));
            blueprint.spawns.Add(
                CreateSpawn(
                    KeptEnemyId,
                    DungeonSpawnCategory.Enemy,
                    DungeonBuiltInContentKeys.Enemy,
                    new Vector2Int(2, 0),
                    102));
            blueprint.spawns.Add(
                CreateSpawn(
                    ReplacedCrateId,
                    DungeonSpawnCategory.Destructible,
                    DungeonBuiltInContentKeys.Destructible,
                    new Vector2Int(1, 1),
                    201));
            blueprint.spawns.Add(
                CreateSpawn(
                    TransformedPropId,
                    DungeonSpawnCategory.Prop,
                    DungeonBuiltInContentKeys.PropCube,
                    new Vector2Int(2, 1),
                    301));
            blueprint.spawns.Add(
                CreateSpawn(
                    SecondPropId,
                    DungeonSpawnCategory.Prop,
                    DungeonBuiltInContentKeys.PropCylinder,
                    new Vector2Int(3, 1),
                    302));
            blueprint.spawns.Add(
                CreateSpawn(
                    GimmickId,
                    DungeonSpawnCategory.Gimmick,
                    DungeonBuiltInContentKeys.Gimmick,
                    new Vector2Int(1, 2),
                    401));
            blueprint.RefreshHash();
            Assert.That(DungeonBlueprintValidator.Validate(blueprint).IsValid, Is.True);
            return blueprint;
        }

        // 지정 ID와 배치 특성을 가진 완전한 spawn 레코드를 만듭니다.
        private static DungeonSpawnRecord CreateSpawn(
            string spawnId,
            DungeonSpawnCategory category,
            string contentKey,
            Vector2Int cell,
            int variantSeed)
        {
            return new DungeonSpawnRecord
            {
                spawnId = spawnId,
                category = category,
                contentKey = contentKey,
                instanceName = spawnId,
                cell = cell,
                localPosition = new Vector3(
                    cell.x * 3f,
                    category == DungeonSpawnCategory.Marker ? 0.08f : 0.5f,
                    cell.y * 3f),
                localScale = Vector3.one,
                roomId = RoomId,
                progression = (cell.x + cell.y) / 5f,
                tags = new List<string>(),
                variantSeed = variantSeed
            };
        }

        // Disable·Add·Replace·절대 Transform을 각각 하나씩 가진 유효 Override를 만듭니다.
        private static DungeonStageOverrides CreateValidOverrides(
            DungeonBlueprintAsset baseAsset)
        {
            DungeonStageOverrides stageOverrides =
                ScriptableObject.CreateInstance<DungeonStageOverrides>();
            stageOverrides.baseBlueprint = baseAsset;
            stageOverrides.baseBlueprintHash =
                baseAsset.blueprint.blueprintHash;
            stageOverrides.disabledSpawns.Add(
                CreateDisable(
                    "disable-enemy",
                    FindSpawn(baseAsset.blueprint, DisabledEnemyId)));
            stageOverrides.addedSpawns.Add(
                CreateSpawn(
                    AddedCrateId,
                    DungeonSpawnCategory.Destructible,
                    DungeonBuiltInContentKeys.Destructible,
                    new Vector2Int(2, 2),
                    702));
            stageOverrides.contentOverrides.Add(
                CreateContent(
                    "replace-crate",
                    FindSpawn(baseAsset.blueprint, ReplacedCrateId),
                    "custom/destructible/replacement"));
            stageOverrides.transformOverrides.Add(
                CreateTransform(
                    "transform-prop",
                    FindSpawn(baseAsset.blueprint, TransformedPropId),
                    new Vector3(6.5f, 0.75f, 3.25f),
                    10f,
                    35f,
                    -4f,
                    new Vector3(1.25f, 1.5f, 0.8f)));
            stageOverrides.RefreshHash();
            return stageOverrides;
        }

        // 재결합 후보용으로 spawn 전체 논리 필드를 복사하고 stable ID만 교체합니다.
        private static DungeonSpawnRecord CloneSpawn(
            DungeonSpawnRecord source,
            string spawnId)
        {
            return new DungeonSpawnRecord
            {
                spawnId = spawnId,
                category = source.category,
                contentKey = source.contentKey,
                instanceName = source.instanceName,
                cell = source.cell,
                localPosition = source.localPosition,
                pitchDegrees = source.pitchDegrees,
                yawDegrees = source.yawDegrees,
                rollDegrees = source.rollDegrees,
                localScale = source.localScale,
                roomId = source.roomId,
                progression = source.progression,
                tags = source.tags != null
                    ? new List<string>(source.tags)
                    : new List<string>(),
                variantSeed = source.variantSeed
            };
        }

        // 원본 spawn을 대상으로 하는 Disable 레코드를 만듭니다.
        private static DungeonSpawnDisableOverride CreateDisable(
            string recordId,
            DungeonSpawnRecord spawn)
        {
            return new DungeonSpawnDisableOverride
            {
                recordId = recordId,
                binding = DungeonSpawnBindingSnapshot.Capture(spawn)
            };
        }

        // 원본 spawn을 대상으로 하는 콘텐츠 교체 레코드를 만듭니다.
        private static DungeonSpawnContentOverride CreateContent(
            string recordId,
            DungeonSpawnRecord spawn,
            string replacementContentKey)
        {
            return new DungeonSpawnContentOverride
            {
                recordId = recordId,
                binding = DungeonSpawnBindingSnapshot.Capture(spawn),
                replacementContentKey = replacementContentKey
            };
        }

        // 원본 spawn을 대상으로 하는 절대 Transform 레코드를 만듭니다.
        private static DungeonSpawnTransformOverride CreateTransform(
            string recordId,
            DungeonSpawnRecord spawn,
            Vector3 localPosition,
            float pitch,
            float yaw,
            float roll,
            Vector3 localScale)
        {
            return new DungeonSpawnTransformOverride
            {
                recordId = recordId,
                binding = DungeonSpawnBindingSnapshot.Capture(spawn),
                localPosition = localPosition,
                pitchDegrees = pitch,
                yawDegrees = yaw,
                rollDegrees = roll,
                localScale = localScale
            };
        }

        // Blueprint에서 지정 stable ID의 spawn을 찾고 fixture 오류를 즉시 드러냅니다.
        private static DungeonSpawnRecord FindSpawn(
            DungeonBlueprint blueprint,
            string spawnId)
        {
            DungeonSpawnRecord spawn = FindSpawnOrNull(
                blueprint,
                spawnId);
            Assert.That(spawn, Is.Not.Null, "Missing fixture spawn: " + spawnId);
            return spawn;
        }

        // Blueprint에서 지정 stable ID의 spawn을 찾고 없으면 null을 반환합니다.
        private static DungeonSpawnRecord FindSpawnOrNull(
            DungeonBlueprint blueprint,
            string spawnId)
        {
            if (blueprint == null || blueprint.spawns == null) return null;
            return blueprint.spawns.Find(
                candidate =>
                    candidate != null &&
                    string.Equals(
                        candidate.spawnId,
                        spawnId,
                        StringComparison.Ordinal));
        }

        // 재결합 계획에서 지정 제작 record ID의 항목을 찾고 fixture 오류를 즉시 드러냅니다.
        private static DungeonStageOverrideRebindEntry FindRebindEntry(
            DungeonStageOverrideRebindPlan plan,
            string recordId)
        {
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.Entries, Is.Not.Null);
            DungeonStageOverrideRebindEntry entry = plan.Entries.Find(
                candidate =>
                    candidate != null &&
                    string.Equals(
                        candidate.RecordId,
                        recordId,
                        StringComparison.Ordinal));
            Assert.That(entry, Is.Not.Null, "Missing rebind entry: " + recordId);
            return entry;
        }
    }
}
