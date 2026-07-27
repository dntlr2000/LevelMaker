using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonStageOverrideBakedPlayModeTests
    {
        private const string AddedTargetId =
            "r7-playmode:override-added-crate";

        // v2 Override BakedPrefab을 실제 로드하고 최종 hash·stable identity·클릭 드랍 1회를 검증합니다.
        [UnityTest]
        public IEnumerator OverrideV2BakedPrefab_ClickRecordsExactlyOneDropSample()
        {
            GameObject parent =
                new GameObject("R7 Override Baked Parent");
            GameObject cameraObject =
                new GameObject("R7 Override Baked Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            RogueDungeonClickInteractor interactor =
                cameraObject.AddComponent<RogueDungeonClickInteractor>();
            interactor.targetCamera = camera;
            Vector2 clickPosition = new Vector2(
                Mathf.Max(1f, Screen.width - 1f),
                Mathf.Max(1f, Screen.height * 0.5f));
            Vector3 targetPosition =
                camera.ScreenPointToRay(clickPosition).GetPoint(8f);

            DungeonBlueprintAsset sourceAsset =
                ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            sourceAsset.Store(CreateBaseBlueprint());
            DungeonStageOverrides stageOverrides =
                CreateOverrides(sourceAsset, targetPosition);
            DungeonStageOverrideApplyResult expected =
                DungeonStageOverrideApplier.Apply(
                    sourceAsset,
                    stageOverrides);
            Assert.That(expected.IsValid, Is.True);

            WeightedDropTable dropTable =
                ScriptableObject.CreateInstance<WeightedDropTable>();
            dropTable.name = "R7 Override Guaranteed Drop";
            dropTable.entries.Add(
                new DropEntry
                {
                    itemId = "R7OverrideGuaranteedDrop",
                    weight = 1f,
                    minQuantity = 1,
                    maxQuantity = 1
                });

            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = new Material(shader);
            DungeonBakeMaterialSet materialSet =
                ScriptableObject.CreateInstance<DungeonBakeMaterialSet>();
            FillMaterialSet(materialSet, material);
            GameObject template =
                new GameObject("R7 Override Baked Template");
            template.SetActive(false);
            DungeonSpawnRecord addedRecord =
                FindSpawn(expected.FinalBlueprint, AddedTargetId);
            GameObject targetObject =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = AddedTargetId;
            targetObject.transform.SetParent(template.transform, false);
            targetObject.transform.localPosition =
                addedRecord.localPosition;
            DungeonSpawnIdentity templateIdentity =
                targetObject.AddComponent<DungeonSpawnIdentity>();
            templateIdentity.Configure(addedRecord);
            DestructibleDropTarget templateTarget =
                targetObject.AddComponent<DestructibleDropTarget>();
            templateTarget.Configure(
                "R7OverrideAddedTarget",
                DropSourceKind.Destructible,
                dropTable,
                false);

            DungeonSceneBuildResult buildResult =
                new DungeonSceneBuildResult
                {
                    ContentCounts = new ContentSpawnCounts
                    {
                        DestructibleCount = 1
                    },
                    BuiltInFallbackCount = 1,
                    ValidationReport = new DungeonValidationReport()
                };
            DungeonBakedStageMetadata metadata =
                template.AddComponent<DungeonBakedStageMetadata>();
            metadata.Configure(
                DungeonBakeFormat.StageOverridesV2,
                DungeonBakeBuilderVersions.StageOverridesV2,
                expected.FinalBlueprintHash,
                buildResult);

            DungeonBakeManifest manifest =
                ScriptableObject.CreateInstance<DungeonBakeManifest>();
            manifest.formatVersion =
                DungeonBakeFormat.StageOverridesV2;
            manifest.builderVersion =
                DungeonBakeBuilderVersions.StageOverridesV2;
            manifest.sourceBlueprint = sourceAsset;
            manifest.sourceOverrides = stageOverrides;
            manifest.materialSet = materialSet;
            manifest.bakedPrefab = template;
            manifest.sourceBlueprintHash =
                expected.SourceBlueprintHash;
            manifest.finalBlueprintHash =
                expected.FinalBlueprintHash;
            manifest.catalogPlanningHash =
                sourceAsset.blueprint.catalogPlanningHash;
            manifest.contentRealizationHash =
                "r7-playmode-realization";
            manifest.gameplayBuildConfigHash =
                "r7-playmode-gameplay";
            manifest.materialDependencyHash =
                "r7-playmode-material";
            manifest.overrideHash = expected.OverrideHash;
            manifest.ownedArtifacts.Add(
                new DungeonBakeArtifactRecord
                {
                    role = "baked-prefab",
                    assetGuid = "r7-playmode-prefab-guid",
                    dependencyHash =
                        "r7-playmode-prefab-dependency"
                });

            DungeonStageDefinition definition =
                ScriptableObject.CreateInstance<DungeonStageDefinition>();
            definition.sourceMode =
                DungeonStageSourceMode.SavedBlueprint;
            definition.buildMode =
                DungeonStageBuildMode.BakedPrefab;
            definition.savedBlueprint = sourceAsset;
            definition.stageOverrides = stageOverrides;
            definition.bakedPrefab = template;
            definition.bakeManifest = manifest;

            GameObject serviceObject = null;
            DropValidationService service =
                DropValidationService.Active;
            if (service == null)
            {
                serviceObject =
                    new GameObject("R7 Override Drop Service");
                service =
                    serviceObject.AddComponent<DropValidationService>();
            }
            Mouse mouse = InputSystem.AddDevice<Mouse>(
                "R7 Override Baked Mouse");
            try
            {
                service.ResetStatistics();
                Assert.That(
                    DungeonBakeManifestValidator.Validate(
                        manifest,
                        sourceAsset,
                        template,
                        stageOverrides).IsValid,
                    Is.True);
                DungeonStageInstance loaded =
                    DungeonStageLoader.Load(
                        new DungeonLoadContext(
                            definition,
                            parent.transform));
                yield return null;

                Assert.That(
                    loaded.BuildMode,
                    Is.EqualTo(DungeonStageBuildMode.BakedPrefab));
                Assert.That(
                    loaded.AppliedOverrides,
                    Is.SameAs(stageOverrides));
                Assert.That(
                    loaded.SourceBlueprintHash,
                    Is.EqualTo(expected.SourceBlueprintHash));
                Assert.That(
                    loaded.OverrideHash,
                    Is.EqualTo(expected.OverrideHash));
                Assert.That(
                    loaded.FinalBlueprintHash,
                    Is.EqualTo(expected.FinalBlueprintHash));
                Assert.That(
                    loaded.FinalBlueprintHash,
                    Is.Not.EqualTo(sourceAsset.blueprint.blueprintHash));

                DungeonSpawnIdentity loadedIdentity =
                    FindIdentity(loaded.Root, AddedTargetId);
                Assert.That(loadedIdentity, Is.Not.Null);
                Assert.That(
                    loadedIdentity.ContentKey,
                    Is.EqualTo(
                        DungeonBuiltInContentKeys.Destructible));
                Assert.That(
                    loadedIdentity.Category,
                    Is.EqualTo(DungeonSpawnCategory.Destructible));
                Assert.That(
                    RuntimeLabHUD.IsPointerInside(clickPosition),
                    Is.False);
                Physics.SyncTransforms();
                RaycastHit clickHit;
                Assert.That(
                    Physics.Raycast(
                        camera.ScreenPointToRay(clickPosition),
                        out clickHit,
                        interactor.maximumDistance,
                        interactor.interactionMask,
                        QueryTriggerInteraction.Ignore),
                    Is.True);
                Assert.That(
                    clickHit.collider.GetComponentInParent<
                        DestructibleDropTarget>(),
                    Is.Not.Null);

                mouse.MakeCurrent();
                InputState.Change(
                    mouse,
                    new MouseState { position = clickPosition },
                    InputUpdateType.Dynamic);
                InputState.Change(
                    mouse,
                    new MouseState { position = clickPosition }
                        .WithButton(MouseButton.Left),
                    InputUpdateType.Dynamic);
                Assert.That(
                    mouse.leftButton.wasPressedThisFrame,
                    Is.True);
                interactor.SendMessage(
                    "Update",
                    SendMessageOptions.RequireReceiver);

                List<DropSourceStatisticsSnapshot> snapshots =
                    service.GetSnapshots();
                Assert.That(snapshots.Count, Is.EqualTo(1));
                Assert.That(
                    snapshots[0].SourceKind,
                    Is.EqualTo(DropSourceKind.Destructible));
                Assert.That(snapshots[0].Attempts, Is.EqualTo(1));
                Assert.That(snapshots[0].Entries.Count, Is.EqualTo(1));
                Assert.That(
                    snapshots[0].Entries[0].ItemId,
                    Is.EqualTo("R7OverrideGuaranteedDrop"));
                Assert.That(snapshots[0].Entries[0].Hits, Is.EqualTo(1));
                yield return null;
                Assert.That(loadedIdentity == null, Is.True);
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                if (mouse != null && mouse.added)
                    InputSystem.RemoveDevice(mouse);
                if (serviceObject != null)
                    Object.Destroy(serviceObject);
                Object.Destroy(definition);
                Object.Destroy(manifest);
                Object.Destroy(template);
                Object.Destroy(materialSet);
                Object.Destroy(material);
                Object.Destroy(dropTable);
                Object.Destroy(stageOverrides);
                Object.Destroy(sourceAsset);
                Object.Destroy(cameraObject);
                Object.Destroy(parent);
            }
        }

        // 입구·출구 Marker만 가진 최소 연결 Blueprint를 만듭니다.
        private static DungeonBlueprint CreateBaseBlueprint()
        {
            DungeonBlueprint blueprint = new DungeonBlueprint
            {
                formatVersion = DungeonBlueprintFormat.CurrentVersion,
                generatorVersion = DungeonGeneratorVersions.LegacyV1,
                seed = 72727,
                recipeHash = "r7-playmode-recipe",
                catalogPlanningHash =
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash,
                grid = new DungeonGridRecord
                {
                    width = 2,
                    depth = 1,
                    cellSize = 3f,
                    wallHeight = 3.2f
                },
                entrance = Vector2Int.zero,
                exit = Vector2Int.right,
                rooms = new List<DungeonRoomRecord>
                {
                    new DungeonRoomRecord
                    {
                        roomId = "room-r7-playmode",
                        bounds = new RectInt(0, 0, 2, 1)
                    }
                },
                cells = new List<DungeonCellRecord>
                {
                    new DungeonCellRecord
                    {
                        coordinate = Vector2Int.zero,
                        flags = DungeonCellFlags.Floor,
                        roomId = "room-r7-playmode",
                        distanceFromEntrance = 0
                    },
                    new DungeonCellRecord
                    {
                        coordinate = Vector2Int.right,
                        flags = DungeonCellFlags.Floor,
                        roomId = "room-r7-playmode",
                        distanceFromEntrance = 1
                    }
                }
            };
            blueprint.spawns.Add(
                CreateSpawn(
                    "r7-playmode:entrance",
                    DungeonSpawnCategory.Marker,
                    DungeonBuiltInContentKeys.EntranceMarker,
                    Vector2Int.zero,
                    Vector3.up * 0.08f,
                    1));
            blueprint.spawns.Add(
                CreateSpawn(
                    "r7-playmode:exit",
                    DungeonSpawnCategory.Marker,
                    DungeonBuiltInContentKeys.ExitMarker,
                    Vector2Int.right,
                    new Vector3(3f, 0.08f, 0f),
                    2));
            blueprint.RefreshHash();
            Assert.That(
                DungeonBlueprintValidator.Validate(blueprint).IsValid,
                Is.True);
            return blueprint;
        }

        // 클릭 ray 위치에 추가 파괴물을 배치하는 Stage Override를 만듭니다.
        private static DungeonStageOverrides CreateOverrides(
            DungeonBlueprintAsset sourceAsset,
            Vector3 targetPosition)
        {
            DungeonStageOverrides stageOverrides =
                ScriptableObject.CreateInstance<DungeonStageOverrides>();
            stageOverrides.baseBlueprint = sourceAsset;
            stageOverrides.baseBlueprintHash =
                sourceAsset.blueprint.blueprintHash;
            stageOverrides.addedSpawns.Add(
                CreateSpawn(
                    AddedTargetId,
                    DungeonSpawnCategory.Destructible,
                    DungeonBuiltInContentKeys.Destructible,
                    Vector2Int.zero,
                    targetPosition,
                    7001));
            stageOverrides.RefreshHash();
            return stageOverrides;
        }

        // 지정 identity와 transform을 가진 완전한 spawn 레코드를 만듭니다.
        private static DungeonSpawnRecord CreateSpawn(
            string spawnId,
            DungeonSpawnCategory category,
            string contentKey,
            Vector2Int cell,
            Vector3 localPosition,
            int variantSeed)
        {
            return new DungeonSpawnRecord
            {
                spawnId = spawnId,
                category = category,
                contentKey = contentKey,
                instanceName = spawnId,
                cell = cell,
                localPosition = localPosition,
                localScale = Vector3.one,
                roomId = "room-r7-playmode",
                progression = 0f,
                tags = new List<string>(),
                variantSeed = variantSeed
            };
        }

        // 최종 Blueprint에서 지정 stable ID의 spawn을 찾습니다.
        private static DungeonSpawnRecord FindSpawn(
            DungeonBlueprint blueprint,
            string spawnId)
        {
            DungeonSpawnRecord spawn = blueprint.spawns.Find(
                value => value != null && value.spawnId == spawnId);
            Assert.That(spawn, Is.Not.Null);
            return spawn;
        }

        // 로드된 Baked root에서 지정 stable ID의 identity를 찾습니다.
        private static DungeonSpawnIdentity FindIdentity(
            GameObject root,
            string spawnId)
        {
            DungeonSpawnIdentity[] identities =
                root.GetComponentsInChildren<DungeonSpawnIdentity>(true);
            for (int i = 0; i < identities.Length; i++)
            {
                if (identities[i].SpawnId == spawnId)
                    return identities[i];
            }
            return null;
        }

        // 하나의 Material을 v2 manifest 필수 재질 슬롯에 모두 배치합니다.
        private static void FillMaterialSet(
            DungeonBakeMaterialSet materialSet,
            Material material)
        {
            materialSet.floor = material;
            materialSet.wall = material;
            materialSet.enemy = material;
            materialSet.destructible = material;
            materialSet.prop = material;
            materialSet.gimmick = material;
            materialSet.entrance = material;
            materialSet.exit = material;
        }
    }
}
