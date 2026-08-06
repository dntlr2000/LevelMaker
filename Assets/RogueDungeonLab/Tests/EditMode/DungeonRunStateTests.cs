using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RogueDungeonLab.Tests
{
    public sealed class EditModeRunStateParticipant :
        MonoBehaviour,
        IDungeonRunStateParticipant
    {
        public string key = "switch";
        public string payload = string.Empty;

        public string RunStateKey
        {
            get { return key; }
        }

        // 테스트 기믹의 현재 문자열 상태를 반환합니다.
        public string CaptureRunState()
        {
            return payload;
        }

        // 테스트 기믹의 문자열 상태를 저장 payload로 교체합니다.
        public void RestoreRunState(string value)
        {
            payload = value ?? string.Empty;
        }
    }

    public sealed class DungeonRunStateTests
    {
        private sealed class RebindingMigrator :
            IDungeonRunStateMigrator
        {
            public int Calls;

            // 테스트 상태를 대상 fingerprint로 명시적으로 재결박합니다.
            public bool TryMigrate(
                DungeonRunState source,
                DungeonRunStateTarget target,
                out DungeonRunState migrated,
                out string message)
            {
                Calls++;
                migrated = source.DeepClone();
                migrated.formatVersion =
                    DungeonRunStateFormat.CurrentVersion;
                migrated.stageId = target.StageId;
                migrated.sourceMode = target.SourceMode;
                migrated.runSeed = target.RunSeed;
                migrated.finalBlueprintHash =
                    target.FinalBlueprintHash;
                migrated.RefreshHash();
                message = "rebinding-test";
                return true;
            }
        }

        // 목록 입력 순서와 저장 시각이 달라도 canonical hash와 JSON round-trip 본문이 유지되는지 검사합니다.
        [Test]
        public void CanonicalHash_IsOrderIndependentAndDetectsTampering()
        {
            DungeonRunState first = CreateState(
                "stage-hash",
                DungeonStageSourceMode.Procedural,
                42,
                "final-hash");
            first.removedSpawnIds.Add("enemy-b");
            first.removedSpawnIds.Add("enemy-a");
            first.gimmickStates.Add(
                new DungeonGimmickRunState
                {
                    spawnId = "gimmick-b",
                    stateKey = "switch",
                    payload = "on"
                });
            first.gimmickStates.Add(
                new DungeonGimmickRunState
                {
                    spawnId = "gimmick-a",
                    stateKey = "door",
                    payload = "open"
                });
            first.player = new DungeonRunPlayerState
            {
                isPresent = true,
                localPosition = new Vector3(1f, 2f, 3f),
                localEulerAngles =
                    new Vector3(0f, 90f, 0f)
            };
            first.savedUtcTicks = 100;
            first.RefreshHash();

            DungeonRunState second = first.DeepClone();
            second.removedSpawnIds.Reverse();
            second.gimmickStates.Reverse();
            second.savedUtcTicks = 999;
            second.RefreshHash();

            Assert.That(
                second.stateHash,
                Is.EqualTo(first.stateHash));
            DungeonRunState restored =
                DungeonRunStateSerialization.FromJson(
                    DungeonRunStateSerialization.ToJson(first));
            Assert.That(
                DungeonRunStateValidator.Validate(restored)
                    .IsValid,
                Is.True);

            restored.runSeed++;
            DungeonValidationReport tampered =
                DungeonRunStateValidator.Validate(restored);
            Assert.That(tampered.IsValid, Is.False);
            Assert.That(
                tampered.ContainsCode(
                    DungeonRunStateValidationCodes
                        .StateHashMismatch),
                Is.True);
        }

        // JSON 저장소가 정상 round-trip하고 잘못된 새 저장이 기존 슬롯을 덮지 않으며 손상 파일을 거부하는지 검사합니다.
        [Test]
        public void JsonStore_RoundTripsPreservesOldSlotAndRejectsCorruption()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "RogueDungeonLab_R8_" +
                Guid.NewGuid().ToString("N"));
            JsonFileDungeonRunStateStore store =
                new JsonFileDungeonRunStateStore(directory);
            try
            {
                DungeonRunState valid = CreateState(
                    "stage-store",
                    DungeonStageSourceMode.SavedBlueprint,
                    77,
                    "saved-hash");
                valid.AddRemovedSpawn("enemy-1");
                store.Save("slot_a", valid);

                DungeonRunState loaded;
                Assert.That(
                    store.TryLoad("slot_a", out loaded),
                    Is.True);
                Assert.That(
                    loaded.removedSpawnIds,
                    Is.EquivalentTo(
                        new[] { "enemy-1" }));

                DungeonRunState invalid = valid.DeepClone();
                invalid.stageId = string.Empty;
                Assert.Throws<DungeonRunStateStoreException>(
                    delegate
                    {
                        store.Save("slot_a", invalid);
                    });
                Assert.That(
                    store.TryLoad("slot_a", out loaded),
                    Is.True);
                Assert.That(
                    loaded.stageId,
                    Is.EqualTo("stage-store"));

                string blockedBackup =
                    store.GetSlotPath("slot_a") +
                    ".bak";
                Directory.CreateDirectory(blockedBackup);
                DungeonRunState replacement =
                    valid.DeepClone();
                replacement.runSeed = 999;
                replacement.RefreshHash();
                Assert.Throws<DungeonRunStateStoreException>(
                    delegate
                    {
                        store.Save(
                            "slot_a",
                            replacement);
                    });
                Directory.Delete(blockedBackup);
                Assert.That(
                    store.TryLoad("slot_a", out loaded),
                    Is.True);
                Assert.That(
                    loaded.runSeed,
                    Is.EqualTo(77));

                Assert.Throws<DungeonRunStateStoreException>(
                    delegate
                    {
                        store.Exists("../escape");
                    });
                File.WriteAllText(
                    store.GetSlotPath("slot_a"),
                    "{}");
                Assert.Throws<DungeonRunStateStoreException>(
                    delegate
                    {
                        DungeonRunState ignored;
                        store.TryLoad(
                            "slot_a",
                            out ignored);
                    });
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        // 엄격 hash 정책, matching-ID 재결합과 명시적 migration 결과 재검증을 함께 검사합니다.
        [Test]
        public void Applier_EnforcesMismatchPolicyAndMigrationHook()
        {
            DungeonBlueprint blueprint = CreateTargetBlueprint(
                "new-final",
                new DungeonSpawnRecord
                {
                    spawnId = "enemy-keep",
                    category = DungeonSpawnCategory.Enemy
                });
            DungeonStageDefinition definition =
                ScriptableObject.CreateInstance<
                    DungeonStageDefinition>();
            definition.stageId = "stage-policy";
            GameObject strictRoot = null;
            GameObject bestEffortRoot = null;
            GameObject migrationRoot = null;
            try
            {
                DungeonRunStateTarget target =
                    DungeonRunStateTargetFactory.Create(
                        definition,
                        DungeonStageSourceMode.Procedural,
                        blueprint);
                DungeonRunState old = CreateState(
                    target.StageId,
                    target.SourceMode,
                    target.RunSeed,
                    "old-final");
                old.removedSpawnIds.Add("enemy-keep");
                old.removedSpawnIds.Add("enemy-missing");
                old.RefreshHash();

                strictRoot = CreateSceneRoot(
                    blueprint.spawns[0],
                    null);
                DungeonRunStateApplyResult strict =
                    DungeonRunStateApplier.Apply(
                        strictRoot,
                        old,
                        target);
                Assert.That(strict.WasApplied, Is.False);
                Assert.That(
                    strict.ValidationReport.ContainsCode(
                        DungeonRunStateValidationCodes
                            .FinalBlueprintHashMismatch),
                    Is.True);

                bestEffortRoot = CreateSceneRoot(
                    blueprint.spawns[0],
                    null);
                DungeonRunStateApplyResult bestEffort =
                    DungeonRunStateApplier.Apply(
                        bestEffortRoot,
                        old,
                        target,
                        DungeonRunStateHashMismatchPolicy
                            .ApplyMatchingSpawnIds);
                Assert.That(bestEffort.WasApplied, Is.True);
                Assert.That(
                    bestEffort.WasBestEffort,
                    Is.True);
                Assert.That(
                    bestEffort.RemovedSpawnCount,
                    Is.EqualTo(1));
                Assert.That(
                    bestEffort.AppliedState
                        .finalBlueprintHash,
                    Is.EqualTo("new-final"));
                Assert.That(
                    bestEffort.AppliedState
                        .removedSpawnIds,
                    Is.EquivalentTo(
                        new[] { "enemy-keep" }));

                migrationRoot = CreateSceneRoot(
                    blueprint.spawns[0],
                    null);
                DungeonRunState foreign =
                    old.DeepClone();
                foreign.formatVersion = 0;
                foreign.stageId = "legacy-stage";
                foreign.runSeed = -100;
                foreign.removedSpawnIds.Remove(
                    "enemy-missing");
                foreign.RefreshHash();
                RebindingMigrator migrator =
                    new RebindingMigrator();
                DungeonRunStateApplyResult migrated =
                    DungeonRunStateApplier.Apply(
                        migrationRoot,
                        foreign,
                        target,
                        DungeonRunStateHashMismatchPolicy
                            .Reject,
                        migrator);
                Assert.That(migrated.WasApplied, Is.True);
                Assert.That(migrated.WasMigrated, Is.True);
                Assert.That(migrator.Calls, Is.EqualTo(1));
                Assert.That(
                    migrated.AppliedState.stageId,
                    Is.EqualTo(target.StageId));
            }
            finally
            {
                DestroyImmediateIfPresent(strictRoot);
                DestroyImmediateIfPresent(bestEffortRoot);
                DestroyImmediateIfPresent(migrationRoot);
                Object.DestroyImmediate(definition);
            }
        }

        // 후보 root에서 Enemy 제거와 기믹 participant payload가 mutation 전 검증 뒤 함께 복원되는지 검사합니다.
        [Test]
        public void Applier_RemovesTargetsAndRestoresGimmickParticipant()
        {
            DungeonSpawnRecord enemyRecord =
                new DungeonSpawnRecord
                {
                    spawnId = "enemy-removed",
                    category = DungeonSpawnCategory.Enemy
                };
            DungeonSpawnRecord gimmickRecord =
                new DungeonSpawnRecord
                {
                    spawnId = "gimmick-state",
                    category = DungeonSpawnCategory.Gimmick
                };
            DungeonBlueprint blueprint =
                CreateTargetBlueprint(
                    "participant-final",
                    enemyRecord,
                    gimmickRecord);
            DungeonStageDefinition definition =
                ScriptableObject.CreateInstance<
                    DungeonStageDefinition>();
            definition.stageId = "stage-participant";
            GameObject root =
                new GameObject("R8 Participant Root");
            root.SetActive(false);
            try
            {
                DungeonSpawnIdentity enemy =
                    AddIdentity(
                        root.transform,
                        enemyRecord);
                DungeonSpawnIdentity gimmick =
                    AddIdentity(
                        root.transform,
                        gimmickRecord);
                EditModeRunStateParticipant participant =
                    gimmick.gameObject.AddComponent<
                        EditModeRunStateParticipant>();
                participant.payload = "closed";

                DungeonRunStateTarget target =
                    DungeonRunStateTargetFactory.Create(
                        definition,
                        DungeonStageSourceMode.SavedBlueprint,
                        blueprint);
                DungeonRunState state = CreateState(
                    target.StageId,
                    target.SourceMode,
                    target.RunSeed,
                    target.FinalBlueprintHash);
                state.removedSpawnIds.Add(
                    enemyRecord.spawnId);
                state.gimmickStates.Add(
                    new DungeonGimmickRunState
                    {
                        spawnId =
                            gimmickRecord.spawnId,
                        stateKey = "switch",
                        payload = "open"
                    });
                state.RefreshHash();

                DungeonRunStateApplyResult result =
                    DungeonRunStateApplier.Apply(
                        root,
                        state,
                        target);

                Assert.That(result.WasApplied, Is.True);
                Assert.That(
                    result.RemovedSpawnCount,
                    Is.EqualTo(1));
                Assert.That(
                    result.RestoredGimmickStateCount,
                    Is.EqualTo(1));
                Assert.That(enemy == null, Is.True);
                Assert.That(
                    participant.payload,
                    Is.EqualTo("open"));
            }
            finally
            {
                DestroyImmediateIfPresent(root);
                Object.DestroyImmediate(definition);
            }
        }

        // 잘못된 RunState가 RuntimeBuild 후보에서 거부될 때 기존 generated root가 그대로 유지되는지 검사합니다.
        [Test]
        public void Loader_InvalidRunStatePreservesExistingGeneratedRoot()
        {
            RogueDungeonSettings settings =
                ScriptableObject.CreateInstance<
                    RogueDungeonSettings>();
            GameObject parent =
                new GameObject("R8 Rollback Parent");
            try
            {
                settings.ApplyPreset(
                    DungeonPreset.Compact);
                DungeonStageInstance initial =
                    DungeonStageLoader.LoadProcedural(
                        parent.transform,
                        settings,
                        515151);
                GameObject originalRoot = initial.Root;
                DungeonRunStateTarget target =
                    DungeonRunStateTargetFactory.Create(
                        null,
                        initial.SourceMode,
                        initial.Blueprint);
                DungeonRunState invalid = CreateState(
                    target.StageId,
                    target.SourceMode,
                    target.RunSeed,
                    "wrong-final-hash");

                Assert.Throws<DungeonStageLoadException>(
                    delegate
                    {
                        DungeonStageLoader.LoadProcedural(
                            parent.transform,
                            settings,
                            initial.ActiveSeed,
                            DungeonGeneratorVersions.LegacyV1,
                            settings,
                            null,
                            DungeonMissingContentPolicy
                                .BuiltInFallback,
                            null,
                            "r8-rollback",
                            invalid);
                    });
                Assert.That(originalRoot == null, Is.False);
                Assert.That(
                    parent.transform.Find(
                        DungeonStageLoader
                            .GeneratedRootName)
                        .gameObject,
                    Is.SameAs(originalRoot));
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(
                    parent.transform);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(settings);
            }
        }

        // 지정 fingerprint의 빈 RunState를 생성하고 canonical hash를 기록합니다.
        private static DungeonRunState CreateState(
            string stageId,
            DungeonStageSourceMode sourceMode,
            int seed,
            string finalHash)
        {
            DungeonRunState result =
                new DungeonRunState
                {
                    stageId = stageId,
                    sourceMode = sourceMode,
                    runSeed = seed,
                    finalBlueprintHash = finalHash
                };
            result.RefreshHash();
            return result;
        }

        // RunState target 생성에 필요한 최소 spawn fingerprint Blueprint를 만듭니다.
        private static DungeonBlueprint CreateTargetBlueprint(
            string finalHash,
            params DungeonSpawnRecord[] spawns)
        {
            DungeonBlueprint blueprint =
                new DungeonBlueprint
                {
                    generatorVersion =
                        DungeonGeneratorVersions.StableV2,
                    seed = 24680,
                    recipeHash = "recipe-r8",
                    catalogPlanningHash = "catalog-r8",
                    blueprintHash = finalHash
                };
            for (int i = 0; i < spawns.Length; i++)
                blueprint.spawns.Add(spawns[i]);
            return blueprint;
        }

        // 한 stable spawn identity를 포함한 비활성 후보 root를 만듭니다.
        private static GameObject CreateSceneRoot(
            DungeonSpawnRecord record,
            EditModeRunStateParticipant participant)
        {
            GameObject root =
                new GameObject("R8 Apply Root");
            root.SetActive(false);
            DungeonSpawnIdentity identity =
                AddIdentity(root.transform, record);
            if (participant != null)
                participant.transform.SetParent(
                    identity.transform,
                    false);
            return root;
        }

        // 지정 spawn 레코드를 표현하는 자식 identity 오브젝트를 만듭니다.
        private static DungeonSpawnIdentity AddIdentity(
            Transform parent,
            DungeonSpawnRecord record)
        {
            GameObject child =
                new GameObject(record.spawnId);
            child.transform.SetParent(parent, false);
            DungeonSpawnIdentity identity =
                child.AddComponent<DungeonSpawnIdentity>();
            identity.Configure(record);
            return identity;
        }

        // 이미 파괴되지 않은 테스트 GameObject만 즉시 정리합니다.
        private static void DestroyImmediateIfPresent(
            GameObject value)
        {
            if (value != null)
                Object.DestroyImmediate(value);
        }
    }
}
