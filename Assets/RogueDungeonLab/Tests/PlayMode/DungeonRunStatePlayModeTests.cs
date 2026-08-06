using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonRunStatePlayModeTests
    {
        private GameObject _generatorObject;
        private RogueDungeonSettings _settings;
        private RogueDungeonGenerator _generator;
        private MemoryDungeonRunStateStore _store;
        private DungeonBlueprintAsset _blueprintAsset;
        private DungeonStageDefinition _definition;

        // 각 PlayMode 테스트에 콘텐츠가 충분한 소형 절차 던전과 메모리 저장소를 준비합니다.
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _settings =
                ScriptableObject.CreateInstance<
                    RogueDungeonSettings>();
            _settings.ApplyPreset(DungeonPreset.Compact);
            _settings.stageWidthCells = 24;
            _settings.stageDepthCells = 24;
            _settings.desiredRoomCount = 7;
            _settings.contentSpacingCells = 0;
            _settings.enemyProfile.baseDensity = 0.35f;
            _settings.enemyProfile.maxCount = 12;
            _settings.destructibleProfile.baseDensity = 0.35f;
            _settings.destructibleProfile.maxCount = 12;
            _settings.propProfile.baseDensity = 0f;
            _settings.specialGimmickCount = 1;
            _settings.generateOnPlay = false;

            _generatorObject =
                new GameObject("R8 PlayMode Generator");
            _generator =
                _generatorObject.AddComponent<
                    RogueDungeonGenerator>();
            _generator.settings = _settings;
            _store = new MemoryDungeonRunStateStore();
            _generator.SetRunStateStore(_store);
            yield return null;
        }

        // 테스트가 만든 플레이어·generated root·ScriptableObject와 Generator를 정리합니다.
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PrototypePlayerController.DestroyActive();
            if (_generator != null)
                _generator.ClearGenerated();
            if (_generatorObject != null)
                Object.Destroy(_generatorObject);
            if (_definition != null)
                Object.Destroy(_definition);
            if (_blueprintAsset != null)
                Object.Destroy(_blueprintAsset);
            if (_settings != null)
                Object.Destroy(_settings);
            yield return null;
        }

        // 클릭 파괴, 원래 procedural seed와 stage-local 플레이어 pose가 슬롯 재개에서 복원되는지 검사합니다.
        [UnityTest]
        public IEnumerator ProceduralRun_RestoresRemovedSpawnSeedAndPlayerPose()
        {
            const int savedSeed = 731246;
            _generator.GenerateWithSeed(savedSeed);
            DestructibleDropTarget target =
                FindActiveDropTarget();
            Assert.That(target, Is.Not.Null);
            DungeonSpawnIdentity identity =
                target.GetComponentInParent<
                    DungeonSpawnIdentity>();
            string removedId = identity.SpawnId;
            Assert.That(
                target.TryDestroy(target.transform.position),
                Is.True);
            Assert.That(
                ContainsRemoved(
                    _generator.ActiveRunState,
                    removedId),
                Is.True);

            PrototypePlayerController player =
                PrototypePlayerController.Spawn(_generator);
            Assert.That(player, Is.Not.Null);
            DungeonRunPlayerState expectedPose =
                new DungeonRunPlayerState
                {
                    isPresent = true,
                    localPosition =
                        new Vector3(5.25f, 1.1f, 6.75f),
                    localEulerAngles =
                        new Vector3(0f, 123f, 0f)
                };
            player.RestoreStageLocalPose(
                _generator.transform,
                expectedPose);
            DungeonRunState saved =
                _generator.SaveRunState(
                    "procedural",
                    player);
            Assert.That(saved.runSeed, Is.EqualTo(savedSeed));
            Assert.That(saved.player.isPresent, Is.True);

            yield return null;
            _generator.GenerateWithSeed(savedSeed + 1);
            Assert.That(
                _generator.ActiveSeed,
                Is.EqualTo(savedSeed + 1));

            Assert.That(
                _generator.LoadRunState("procedural"),
                Is.True);
            Assert.That(
                _generator.ActiveSeed,
                Is.EqualTo(savedSeed));
            Assert.That(
                FindSpawn(
                    removedId,
                    false),
                Is.Null);
            Vector3 restoredLocal =
                _generator.transform.InverseTransformPoint(
                    player.transform.position);
            Assert.That(
                Vector3.Distance(
                    restoredLocal,
                    expectedPose.localPosition),
                Is.LessThan(0.001f));
            Assert.That(
                Quaternion.Angle(
                    player.transform.rotation,
                    _generator.transform.rotation *
                    Quaternion.Euler(
                        expectedPose.localEulerAngles)),
                Is.LessThan(0.01f));
            yield return null;
        }

        // SavedBlueprint stage ID가 같은 슬롯은 복원하고 다른 stage ID는 기존 정상 root를 보존한 채 거부하는지 검사합니다.
        [UnityTest]
        public IEnumerator SavedStage_RestoresByPersistentStageIdAndRejectsForeignId()
        {
            DungeonBlueprint blueprint =
                DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.Create(
                        _settings,
                        884422,
                        DungeonGeneratorVersions.LegacyV1,
                        DungeonBuiltInContentKeys
                            .LegacyCatalogPlanningHash))
                    .Blueprint;
            _blueprintAsset =
                ScriptableObject.CreateInstance<
                    DungeonBlueprintAsset>();
            _blueprintAsset.Store(blueprint);
            _definition =
                ScriptableObject.CreateInstance<
                    DungeonStageDefinition>();
            _definition.stageId =
                "saved-stage-r8-playmode";
            _definition.sourceMode =
                DungeonStageSourceMode.SavedBlueprint;
            _definition.buildMode =
                DungeonStageBuildMode.RuntimeBuild;
            _definition.savedBlueprint =
                _blueprintAsset;
            _definition.loadOnPlay = false;
            _generator.stageDefinition = _definition;

            _generator.LoadStageDefinition();
            DestructibleDropTarget target =
                FindActiveDropTarget();
            Assert.That(target, Is.Not.Null);
            DungeonSpawnIdentity identity =
                target.GetComponentInParent<
                    DungeonSpawnIdentity>();
            string removedId = identity.SpawnId;
            Assert.That(
                target.TryDestroy(target.transform.position),
                Is.True);
            DungeonRunState saved =
                _generator.SaveRunState("saved-stage");
            Assert.That(
                saved.stageId,
                Is.EqualTo(_definition.stageId));
            Assert.That(
                saved.sourceMode,
                Is.EqualTo(
                    DungeonStageSourceMode.SavedBlueprint));

            yield return null;
            _generator.LoadStageDefinition();
            Assert.That(
                FindSpawn(removedId, false),
                Is.Not.Null);
            Assert.That(
                _generator.LoadRunState("saved-stage"),
                Is.True);
            Assert.That(
                FindSpawn(removedId, false),
                Is.Null);

            GameObject preservedRoot =
                _generator.CurrentStageInstance.Root;
            _definition.stageId =
                "another-saved-stage";
            Assert.Throws<DungeonStageLoadException>(
                delegate
                {
                    _generator.LoadRunState(
                        "saved-stage");
                });
            Assert.That(
                _generator.CurrentStageInstance.Root,
                Is.SameAs(preservedRoot));
            Assert.That(
                preservedRoot == null,
                Is.False);
            yield return null;
        }

        // 현재 generated root에서 클릭 가능한 활성 적 또는 파괴물을 찾습니다.
        private DestructibleDropTarget FindActiveDropTarget()
        {
            DestructibleDropTarget[] targets =
                _generator.CurrentStageInstance.Root
                    .GetComponentsInChildren<
                        DestructibleDropTarget>(false);
            return targets.Length > 0
                ? targets[0]
                : null;
        }

        // stable spawn ID와 활성 상태가 일치하는 현재 identity를 찾습니다.
        private DungeonSpawnIdentity FindSpawn(
            string spawnId,
            bool includeInactive)
        {
            DungeonSpawnIdentity[] identities =
                _generator.CurrentStageInstance.Root
                    .GetComponentsInChildren<
                        DungeonSpawnIdentity>(
                        includeInactive);
            for (int i = 0; i < identities.Length; i++)
            {
                DungeonSpawnIdentity identity =
                    identities[i];
                if (identity != null &&
                    identity.SpawnId == spawnId)
                {
                    return identity;
                }
            }
            return null;
        }

        // RunState 제거 목록에 지정 stable spawn ID가 포함되는지 확인합니다.
        private static bool ContainsRemoved(
            DungeonRunState state,
            string spawnId)
        {
            if (state == null ||
                state.removedSpawnIds == null)
            {
                return false;
            }
            for (int i = 0;
                 i < state.removedSpawnIds.Count;
                 i++)
            {
                if (state.removedSpawnIds[i] ==
                    spawnId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
