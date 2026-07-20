using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonContentCatalogTests
    {
        // Inspector entry·tag 순서와 Prefab 표현 참조가 planning hash나 원본 목록을 바꾸지 않는지 검사합니다.
        [Test]
        public void CatalogPlanningHash_IsOrderIndependentAndIgnoresPrefab()
        {
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            GameObject prefab = new GameObject("Hash Only Prefab");
            try
            {
                DungeonContentCatalogEntry beta = Entry("enemy/beta", DungeonSpawnCategory.Enemy);
                beta.requiredRoomTags.Add("Boss");
                beta.requiredRoomTags.Add("Indoor");
                DungeonContentCatalogEntry alpha = Entry("enemy/alpha", DungeonSpawnCategory.Enemy);
                catalog.entries.Add(beta);
                catalog.entries.Add(alpha);

                string first = catalog.ComputePlanningHash();
                Assert.That(catalog.entries[0], Is.SameAs(beta), "Hashing must not sort the serialized list in place.");
                beta.prefab = prefab;
                catalog.entries.Reverse();
                beta.requiredRoomTags.Reverse();
                string second = catalog.ComputePlanningHash();

                Assert.That(second, Is.EqualTo(first));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(catalog);
            }
        }

        // 중복 key, 잘못된 progression·footprint·spacing이 안정적인 검증 코드로 보고되는지 검사합니다.
        [Test]
        public void CatalogValidator_ReportsDuplicateProgressionAndFootprintErrors()
        {
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            try
            {
                DungeonContentCatalogEntry first = Entry("enemy/duplicate", DungeonSpawnCategory.Enemy);
                DungeonContentCatalogEntry second = Entry("enemy/duplicate", DungeonSpawnCategory.Enemy);
                second.minProgression = 0.9f;
                second.maxProgression = 0.2f;
                second.footprintCells = new Vector2Int(0, 2);
                second.minimumSpacingCells = -1;
                catalog.entries.Add(first);
                catalog.entries.Add(second);

                DungeonValidationReport report = DungeonContentCatalogValidator.Validate(catalog);

                Assert.That(report.ContainsCode(DungeonContentCatalogValidationCodes.DuplicateKey), Is.True);
                Assert.That(report.ContainsCode(DungeonContentCatalogValidationCodes.InvalidProgression), Is.True);
                Assert.That(report.ContainsCode(DungeonContentCatalogValidationCodes.InvalidFootprint), Is.True);
                Assert.That(report.ContainsCode(DungeonContentCatalogValidationCodes.InvalidSpacing), Is.True);
                Assert.That(report.IsValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        // 예약 built-in key를 다른 범주로 재정의하면 catalog와 planning snapshot 모두 거부하는지 검사합니다.
        [Test]
        public void CatalogValidator_RejectsReservedKeyCategoryMismatch()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            try
            {
                catalog.entries.Add(Entry(DungeonBuiltInContentKeys.Enemy, DungeonSpawnCategory.Prop));

                DungeonValidationReport report = DungeonContentCatalogValidator.Validate(catalog);
                Assert.That(
                    report.ContainsCode(DungeonContentCatalogValidationCodes.ReservedKeyCategoryMismatch),
                    Is.True);

                DungeonGenerationRequest request =
                    DungeonGenerationRequest.CreateStableV2(settings, 125125, catalog);
                System.ArgumentException exception = Assert.Throws<System.ArgumentException>(delegate
                {
                    DungeonBlueprintGenerator.Generate(request);
                });
                Assert.That(
                    exception.Message,
                    Does.Contain(DungeonContentCatalogValidationCodes.ReservedKeyCategoryMismatch));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // 생성 요청이 만들어진 뒤 mutable Catalog를 바꿔도 캡처된 V2 결과가 변하지 않는지 검사합니다.
        [Test]
        public void StableV2Request_IsIsolatedFromLaterCatalogMutation()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            try
            {
                DungeonContentCatalogEntry enemy = Entry("enemy/original", DungeonSpawnCategory.Enemy);
                catalog.entries.Add(enemy);
                DungeonGenerationRequest request = DungeonGenerationRequest.CreateStableV2(settings, 445566, catalog);
                DungeonBlueprint before = DungeonBlueprintGenerator.Generate(request).Blueprint;

                enemy.contentKey = "enemy/mutated";
                enemy.weight = 99f;
                DungeonBlueprint after = DungeonBlueprintGenerator.Generate(request).Blueprint;

                Assert.That(after.blueprintHash, Is.EqualTo(before.blueprintHash));
                Assert.That(after.catalogPlanningHash, Is.EqualTo(request.contentCatalogSnapshot.ComputeHash()));
                Assert.That(CountKey(after, "enemy/original"), Is.GreaterThan(0));
                Assert.That(CountKey(after, "enemy/mutated"), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // 캡처된 요청 snapshot의 사후 변조와 중복 key가 Blueprint provenance를 만들기 전에 거부되는지 검사합니다.
        [Test]
        public void StableV2Generation_RejectsMutatedAndInvalidRequestSnapshots()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            DungeonContentCatalog duplicateCatalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            try
            {
                catalog.entries.Add(Entry("enemy/original", DungeonSpawnCategory.Enemy));
                DungeonGenerationRequest mutated =
                    DungeonGenerationRequest.CreateStableV2(settings, 515151, catalog);
                mutated.contentCatalogSnapshot.entries[0].contentKey = "enemy/tampered";

                System.ArgumentException hashException = Assert.Throws<System.ArgumentException>(delegate
                {
                    DungeonBlueprintGenerator.Generate(mutated);
                });
                Assert.That(hashException.Message, Does.Contain("planning hash"));

                duplicateCatalog.entries.Add(Entry("enemy/duplicate", DungeonSpawnCategory.Enemy));
                duplicateCatalog.entries.Add(Entry("enemy/duplicate", DungeonSpawnCategory.Enemy));
                DungeonGenerationRequest invalid =
                    DungeonGenerationRequest.CreateStableV2(settings, 616161, duplicateCatalog);
                System.ArgumentException validationException = Assert.Throws<System.ArgumentException>(delegate
                {
                    DungeonBlueprintGenerator.Generate(invalid);
                });
                Assert.That(
                    validationException.Message,
                    Does.Contain(DungeonContentCatalogValidationCodes.DuplicateKey));
            }
            finally
            {
                Object.DestroyImmediate(duplicateCatalog);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // 같은 논리 Catalog의 Inspector 순서만 바꾼 두 V2 요청이 같은 Blueprint를 만드는지 검사합니다.
        [Test]
        public void StableV2Generation_IgnoresCatalogInspectorOrder()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog firstCatalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            DungeonContentCatalog secondCatalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            try
            {
                DungeonContentCatalogEntry alpha = Entry("enemy/alpha", DungeonSpawnCategory.Enemy);
                alpha.weight = 2f;
                DungeonContentCatalogEntry beta = Entry("enemy/beta", DungeonSpawnCategory.Enemy);
                beta.weight = 1f;
                firstCatalog.entries.Add(alpha);
                firstCatalog.entries.Add(beta);
                secondCatalog.entries.Add(Clone(beta));
                secondCatalog.entries.Add(Clone(alpha));

                DungeonGenerationRequest firstRequest = DungeonGenerationRequest.CreateStableV2(settings, -246810, firstCatalog);
                DungeonGenerationRequest secondRequest = DungeonGenerationRequest.CreateStableV2(settings, -246810, secondCatalog);
                DungeonBlueprint first = DungeonBlueprintGenerator.Generate(firstRequest).Blueprint;
                DungeonBlueprint second = DungeonBlueprintGenerator.Generate(secondRequest).Blueprint;

                Assert.That(secondRequest.catalogPlanningHash, Is.EqualTo(firstRequest.catalogPlanningHash));
                Assert.That(second.blueprintHash, Is.EqualTo(first.blueprintHash));
                Assert.That(CaptureCategory(second, DungeonSpawnCategory.Enemy), Is.EqualTo(CaptureCategory(first, DungeonSpawnCategory.Enemy)));
            }
            finally
            {
                Object.DestroyImmediate(secondCatalog);
                Object.DestroyImmediate(firstCatalog);
                Object.DestroyImmediate(settings);
            }
        }

        // Enemy 후보 key 변경이 뒤 범주의 PRNG 출력과 최종 레코드를 밀지 않는지 검사합니다.
        [Test]
        public void StableV2_CategoryVariantSelectionDoesNotShiftLaterCategories()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog emptyCatalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            DungeonContentCatalog enemyCatalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            try
            {
                enemyCatalog.entries.Add(Entry("enemy/replacement", DungeonSpawnCategory.Enemy));
                DungeonBlueprint builtIn = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.CreateStableV2(settings, 987654, emptyCatalog)).Blueprint;
                DungeonBlueprint replaced = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.CreateStableV2(settings, 987654, enemyCatalog)).Blueprint;

                Assert.That(
                    CaptureCategory(replaced, DungeonSpawnCategory.Destructible),
                    Is.EqualTo(CaptureCategory(builtIn, DungeonSpawnCategory.Destructible)));
                Assert.That(
                    CaptureCategory(replaced, DungeonSpawnCategory.Prop),
                    Is.EqualTo(CaptureCategory(builtIn, DungeonSpawnCategory.Prop)));
            }
            finally
            {
                Object.DestroyImmediate(enemyCatalog);
                Object.DestroyImmediate(emptyCatalog);
                Object.DestroyImmediate(settings);
            }
        }

        // custom Prefab용 spawn은 built-in 회전·크기를 상속하지 않고 catalog yaw·scale만 적용하는지 검사합니다.
        [Test]
        public void StableV2_CustomContentUsesAuthoredTransformBaseline()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            try
            {
                DungeonContentCatalogEntry entry = Entry("enemy/authored", DungeonSpawnCategory.Enemy);
                catalog.entries.Add(entry);

                DungeonBlueprint unrotated = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.CreateStableV2(settings, 717171, catalog)).Blueprint;
                DungeonSpawnRecord first = FindFirst(unrotated, DungeonSpawnCategory.Enemy);
                Assert.That(first, Is.Not.Null);
                Assert.That(first.contentKey, Is.EqualTo(entry.contentKey));
                Assert.That(first.localPosition.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(first.localScale, Is.EqualTo(Vector3.one));
                Assert.That(first.yawDegrees, Is.EqualTo(0f).Within(0.0001f));

                entry.randomizeYaw = true;
                entry.yawDegreesRange = new Vector2(45f, 45f);
                DungeonBlueprint rotated = DungeonBlueprintGenerator.Generate(
                    DungeonGenerationRequest.CreateStableV2(settings, 717171, catalog)).Blueprint;
                DungeonSpawnRecord rotatedFirst = FindFirst(rotated, DungeonSpawnCategory.Enemy);
                Assert.That(rotatedFirst.yawDegrees, Is.EqualTo(45f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // Catalog Prefab이 built-in key를 교체하고 transform·identity·클릭 드랍 계약을 유지하는지 검사합니다.
        [Test]
        public void SceneBuilder_PrefabResolverOverridesBuiltInAndPreservesIdentity()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            GameObject prefab = new GameObject("Enemy Prefab Template");
            GameObject parent = new GameObject("R4 Prefab Build");
            WeightedDropTable prefabTable = ScriptableObject.CreateInstance<WeightedDropTable>();
            WeightedDropTable catalogTable = ScriptableObject.CreateInstance<WeightedDropTable>();
            try
            {
                prefab.AddComponent<BoxCollider>();
                GameObject targetChild = new GameObject("Authored Drop Target");
                targetChild.transform.SetParent(prefab.transform, false);
                DestructibleDropTarget authoredTarget = targetChild.AddComponent<DestructibleDropTarget>();
                authoredTarget.Configure("authored-enemy", DropSourceKind.Enemy, prefabTable, false);
                DungeonContentCatalogEntry enemy = Entry(DungeonBuiltInContentKeys.Enemy, DungeonSpawnCategory.Enemy);
                enemy.prefab = prefab;
                enemy.dropTable = catalogTable;
                enemy.gameplayId = "catalog-enemy";
                catalog.entries.Add(enemy);
                DungeonBlueprint blueprint = CreateLegacyBlueprint(settings, 112358);
                int expectedEnemies = CountCategory(blueprint, DungeonSpawnCategory.Enemy);

                DungeonSceneBuildResult result = DungeonSceneBuilder.Build(
                    parent.transform,
                    blueprint,
                    new DungeonSceneBuildOptions(
                        settings,
                        new DungeonPrefabContentResolver(catalog),
                        DungeonMissingContentPolicy.Error));

                Assert.That(result.ResolvedContentCount, Is.EqualTo(expectedEnemies));
                Transform enemies = parent.transform.Find("Contents/Enemies");
                Assert.That(enemies, Is.Not.Null);
                Assert.That(enemies.childCount, Is.EqualTo(expectedEnemies));
                for (int i = 0; i < enemies.childCount; i++)
                {
                    Transform instance = enemies.GetChild(i);
                    Assert.That(instance.GetComponent<DungeonSpawnIdentity>(), Is.Not.Null);
                    DestructibleDropTarget target =
                        instance.GetComponentInChildren<DestructibleDropTarget>(true);
                    Assert.That(target, Is.Not.Null);
                    Assert.That(target.TargetId, Is.EqualTo("authored-enemy"));
                    Assert.That(target.DropTable, Is.SameAs(prefabTable));
                    Assert.That(target.SourceKind, Is.EqualTo(DropSourceKind.Enemy));
                    Assert.That(instance.GetComponent<BoxCollider>(), Is.Not.Null);
                }
            }
            finally
            {
                DestroyBuildRoot(parent);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(catalogTable);
                Object.DestroyImmediate(prefabTable);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // Prefab에 target이 없으면 catalog의 dropTable·gameplayId가 자동 보강 target에 적용되는지 검사합니다.
        [Test]
        public void SceneBuilder_CatalogDropMetadataConfiguresAddedTarget()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            GameObject prefab = new GameObject("Enemy Without Drop Target");
            GameObject parent = new GameObject("R4 Catalog Metadata Build");
            WeightedDropTable catalogTable = ScriptableObject.CreateInstance<WeightedDropTable>();
            try
            {
                DungeonContentCatalogEntry enemy = Entry(
                    DungeonBuiltInContentKeys.Enemy,
                    DungeonSpawnCategory.Enemy);
                enemy.prefab = prefab;
                enemy.dropTable = catalogTable;
                enemy.gameplayId = "catalog-gameplay-enemy";
                catalog.entries.Add(enemy);
                DungeonBlueprint blueprint = CreateLegacyBlueprint(settings, 121212);

                DungeonSceneBuilder.Build(
                    parent.transform,
                    blueprint,
                    new DungeonSceneBuildOptions(
                        settings,
                        new DungeonPrefabContentResolver(catalog),
                        DungeonMissingContentPolicy.BuiltInFallback));

                Transform enemies = parent.transform.Find("Contents/Enemies");
                Assert.That(enemies, Is.Not.Null);
                Assert.That(enemies.childCount, Is.GreaterThan(0));
                DestructibleDropTarget target =
                    enemies.GetChild(0).GetComponent<DestructibleDropTarget>();
                Assert.That(target, Is.Not.Null);
                Assert.That(target.TargetId, Is.EqualTo("catalog-gameplay-enemy"));
                Assert.That(target.DropTable, Is.SameAs(catalogTable));
                Assert.That(target.SourceKind, Is.EqualTo(DropSourceKind.Enemy));
            }
            finally
            {
                DestroyBuildRoot(parent);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(catalogTable);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // 알 수 없는 key의 Error·fallback·Skip 정책이 부작용과 구축 개수를 구분하는지 검사합니다.
        [Test]
        public void SceneBuilder_MissingContentPoliciesErrorFallbackAndSkip()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            GameObject errorRoot = new GameObject("R4 Error Root");
            GameObject fallbackRoot = new GameObject("R4 Fallback Root");
            GameObject skipRoot = new GameObject("R4 Skip Root");
            try
            {
                DungeonBlueprint blueprint = CreateLegacyBlueprint(settings, 271828);
                DungeonSpawnRecord enemy = FindFirst(blueprint, DungeonSpawnCategory.Enemy);
                Assert.That(enemy, Is.Not.Null);
                enemy.contentKey = "missing/enemy";
                blueprint.RefreshHash();

                DungeonSceneBuildException exception = Assert.Throws<DungeonSceneBuildException>(delegate
                {
                    DungeonSceneBuilder.Build(
                        errorRoot.transform,
                        blueprint,
                        new DungeonSceneBuildOptions(settings, null, DungeonMissingContentPolicy.Error));
                });
                Assert.That(exception.ValidationReport.ContainsCode(DungeonSceneBuildValidationCodes.MissingContent), Is.True);
                Assert.That(errorRoot.transform.childCount, Is.Zero, "Error must be found before geometry creation.");

                DungeonSceneBuildResult fallback = DungeonSceneBuilder.Build(
                    fallbackRoot.transform,
                    blueprint,
                    new DungeonSceneBuildOptions(settings, null, DungeonMissingContentPolicy.BuiltInFallback));
                DungeonSceneBuildResult skipped = DungeonSceneBuilder.Build(
                    skipRoot.transform,
                    blueprint,
                    new DungeonSceneBuildOptions(settings, null, DungeonMissingContentPolicy.Skip));

                Assert.That(fallback.ValidationReport.WarningCount, Is.GreaterThan(0));
                Assert.That(fallback.ContentCounts.EnemyCount, Is.EqualTo(CountCategory(blueprint, DungeonSpawnCategory.Enemy)));
                Assert.That(skipped.SkippedContentCount, Is.EqualTo(1));
                Assert.That(skipped.ContentCounts.EnemyCount, Is.EqualTo(fallback.ContentCounts.EnemyCount - 1));
            }
            finally
            {
                DestroyBuildRoot(skipRoot);
                DestroyBuildRoot(fallbackRoot);
                DestroyBuildRoot(errorRoot);
                Object.DestroyImmediate(settings);
            }
        }

        // Blueprint와 Catalog의 progression·footprint 불일치를 교차 검증하는지 검사합니다.
        [Test]
        public void BlueprintCatalogValidation_ReportsProgressionAndFootprintMismatch()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            try
            {
                DungeonBlueprint blueprint = CreateLegacyBlueprint(settings, 1618033);
                DungeonSpawnRecord enemy = FindFirst(blueprint, DungeonSpawnCategory.Enemy);
                Assert.That(enemy, Is.Not.Null);
                enemy.contentKey = "enemy/large-late";
                enemy.progression = 0f;
                blueprint.RefreshHash();

                DungeonContentCatalogEntry entry = Entry("enemy/large-late", DungeonSpawnCategory.Enemy);
                entry.minProgression = 0.9f;
                entry.footprintCells = new Vector2Int(100, 100);
                catalog.entries.Add(entry);
                DungeonValidationReport report = DungeonContentCatalogValidator.ValidateBlueprint(
                    blueprint,
                    catalog,
                    DungeonMissingContentPolicy.BuiltInFallback);

                Assert.That(report.ContainsCode(DungeonContentValidationCodes.ProgressionMismatch), Is.True);
                Assert.That(report.ContainsCode(DungeonContentValidationCodes.FootprintOutsideFloor), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // spawn이 주장하는 room ID가 실제 floor 셀과 다르면 Blueprint와 catalog 배치 검증이 우회되지 않는지 검사합니다.
        [Test]
        public void BlueprintValidation_RejectsSpawnRoomMismatchAndUsesActualCellPlacement()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            try
            {
                DungeonBlueprint blueprint = CreateLegacyBlueprint(settings, 818181);
                DungeonSpawnRecord spawn = FindFirst(blueprint, DungeonSpawnCategory.Enemy);
                Assert.That(spawn, Is.Not.Null);
                DungeonCellRecord floorCell = FindCell(blueprint, spawn.cell);
                Assert.That(floorCell, Is.Not.Null);

                bool actuallyInRoom = !string.IsNullOrEmpty(floorCell.roomId);
                spawn.roomId = actuallyInRoom
                    ? string.Empty
                    : blueprint.rooms[0].roomId;
                spawn.contentKey = "enemy/placement-check";
                blueprint.RefreshHash();

                DungeonValidationReport blueprintReport = DungeonBlueprintValidator.Validate(blueprint);
                Assert.That(
                    blueprintReport.ContainsCode(DungeonBlueprintValidationCodes.SpawnRoomMismatch),
                    Is.True);

                DungeonContentCatalogEntry entry = Entry(
                    spawn.contentKey,
                    DungeonSpawnCategory.Enemy);
                entry.placement = actuallyInRoom
                    ? DungeonContentPlacement.CorridorOnly
                    : DungeonContentPlacement.RoomOnly;
                catalog.entries.Add(entry);
                DungeonValidationReport contentReport = DungeonContentCatalogValidator.ValidateBlueprint(
                    blueprint,
                    catalog,
                    DungeonMissingContentPolicy.BuiltInFallback);
                Assert.That(
                    contentReport.ContainsCode(DungeonContentValidationCodes.PlacementMismatch),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // StableV2 Definition이 Prefab을 만들고 다음 strict 실패에서도 기존 정상 root를 보존하는지 검사합니다.
        [Test]
        public void StageLoader_StableV2UsesCatalogAndPreservesRootOnStrictFailure()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            GameObject prefab = new GameObject("R4 Loader Enemy Prefab");
            GameObject parent = new GameObject("R4 Loader Parent");
            try
            {
                DungeonContentCatalogEntry enemy = Entry("enemy/runtime", DungeonSpawnCategory.Enemy);
                enemy.prefab = prefab;
                catalog.entries.Add(enemy);
                definition.sourceMode = DungeonStageSourceMode.Procedural;
                definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                definition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
                definition.fixedSeed = 4242001;
                definition.generatorVersion = DungeonGeneratorVersions.StableV2;
                definition.recipe = settings;
                definition.contentCatalog = catalog;
                definition.missingContentPolicy = DungeonMissingContentPolicy.Error;

                DungeonStageInstance first = DungeonStageLoader.Load(
                    new DungeonLoadContext(definition, parent.transform, settings));
                GameObject approvedRoot = first.Root;

                Assert.That(first.Blueprint.generatorVersion, Is.EqualTo(DungeonGeneratorVersions.StableV2));
                Assert.That(first.Blueprint.catalogPlanningHash, Is.EqualTo(catalog.ComputePlanningHash()));
                Assert.That(first.BuildResult.ResolvedContentCount, Is.GreaterThan(0));
                Assert.That(parent.transform.Find(DungeonStageLoader.GeneratedRootName), Is.SameAs(approvedRoot.transform));

                enemy.prefab = null;
                DungeonStageLoadException exception = Assert.Throws<DungeonStageLoadException>(delegate
                {
                    DungeonStageLoader.Load(new DungeonLoadContext(definition, parent.transform, settings));
                });

                Assert.That(exception.ValidationReport.ContainsCode(DungeonContentValidationCodes.MissingPrefab), Is.True);
                Assert.That(parent.transform.Find(DungeonStageLoader.GeneratedRootName), Is.SameAs(approvedRoot.transform));
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        // 기본 Prefab/factory 구축 부모가 교체 완료 전까지 비활성이고 승인된 root만 활성화되는지 검사합니다.
        [Test]
        public void StageLoader_BuildsUnderInactiveStagingRootBeforeActivation()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            GameObject parent = new GameObject("R4 Inactive Staging Parent");
            try
            {
                definition.sourceMode = DungeonStageSourceMode.Procedural;
                definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                definition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
                definition.fixedSeed = 919191;
                definition.generatorVersion = DungeonGeneratorVersions.LegacyV1;
                definition.recipe = settings;
                definition.missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback;
                InactiveParentResolver resolver = new InactiveParentResolver();
                DungeonLoadContext context = new DungeonLoadContext(
                    definition,
                    parent.transform,
                    settings)
                {
                    ContentResolver = resolver
                };

                DungeonStageInstance instance = DungeonStageLoader.Load(context);

                Assert.That(resolver.CallCount, Is.GreaterThan(0));
                Assert.That(resolver.AllFactoryParentsInactive, Is.True);
                Assert.That(instance.Root.activeInHierarchy, Is.True);
                Assert.That(instance.Root.name, Is.EqualTo(DungeonStageLoader.GeneratedRootName));
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(settings);
            }
        }

        // 실행별 누락 정책 override의 잘못된 enum이 root 생성 전 코드 기반 로드 오류로 변환되는지 검사합니다.
        [Test]
        public void StageLoader_RejectsInvalidMissingPolicyOverrideBeforeBuild()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            GameObject parent = new GameObject("R4 Invalid Policy Parent");
            try
            {
                definition.sourceMode = DungeonStageSourceMode.Procedural;
                definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                definition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
                definition.fixedSeed = 101010;
                definition.generatorVersion = DungeonGeneratorVersions.LegacyV1;
                definition.recipe = settings;
                DungeonLoadContext context = new DungeonLoadContext(
                    definition,
                    parent.transform,
                    settings)
                {
                    MissingContentPolicyOverride = (DungeonMissingContentPolicy)999
                };

                DungeonStageLoadException exception = Assert.Throws<DungeonStageLoadException>(delegate
                {
                    DungeonStageLoader.Load(context);
                });

                Assert.That(
                    exception.ValidationReport.ContainsCode(
                        DungeonStageDefinitionValidationCodes.InvalidMissingContentPolicy),
                    Is.True);
                Assert.That(parent.transform.childCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(settings);
            }
        }

        // generated 이름을 가진 Prefab 공유 메시가 Loader의 합성 메시 정리 대상에 포함되지 않는지 검사합니다.
        [Test]
        public void StageLoader_ClearGeneratedReleasesOnlyExplicitlyOwnedMeshes()
        {
            RogueDungeonSettings settings = CreateDenseSettings();
            DungeonContentCatalog catalog = ScriptableObject.CreateInstance<DungeonContentCatalog>();
            DungeonStageDefinition definition = ScriptableObject.CreateInstance<DungeonStageDefinition>();
            GameObject prefab = new GameObject("R4 Shared Mesh Prefab");
            GameObject parent = new GameObject("R4 Mesh Ownership Parent");
            Mesh sharedMesh = new Mesh { name = "Generated Dungeon Shared Prefab Mesh" };
            try
            {
                prefab.AddComponent<MeshFilter>().sharedMesh = sharedMesh;
                prefab.AddComponent<MeshRenderer>();
                DungeonContentCatalogEntry enemy = Entry(
                    "enemy/shared-mesh",
                    DungeonSpawnCategory.Enemy);
                enemy.prefab = prefab;
                catalog.entries.Add(enemy);
                definition.sourceMode = DungeonStageSourceMode.Procedural;
                definition.buildMode = DungeonStageBuildMode.RuntimeBuild;
                definition.seedPolicy = DungeonStageSeedPolicy.FixedSeed;
                definition.fixedSeed = 111111;
                definition.generatorVersion = DungeonGeneratorVersions.StableV2;
                definition.recipe = settings;
                definition.contentCatalog = catalog;
                definition.missingContentPolicy = DungeonMissingContentPolicy.BuiltInFallback;

                DungeonStageInstance instance = DungeonStageLoader.Load(
                    new DungeonLoadContext(definition, parent.transform, settings));
                Assert.That(instance.BuildResult.ResolvedContentCount, Is.GreaterThan(0));

                DungeonStageLoader.ClearGenerated(parent.transform);

                Assert.That(sharedMesh == null, Is.False);
                Assert.That(prefab.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(sharedMesh));
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(prefab);
                if (sharedMesh != null) Object.DestroyImmediate(sharedMesh);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(settings);
            }
        }

        private static RogueDungeonSettings CreateDenseSettings()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            settings.ApplyPreset(DungeonPreset.Compact);
            settings.specialGimmickCount = 2;
            settings.contentSpacingCells = 0;
            settings.enemyProfile.baseDensity = 0.5f;
            settings.enemyProfile.maxCount = 20;
            settings.destructibleProfile.baseDensity = 0.35f;
            settings.destructibleProfile.maxCount = 12;
            settings.propProfile.baseDensity = 0.35f;
            settings.propProfile.maxCount = 12;
            settings.ClampValues();
            return settings;
        }

        private static DungeonContentCatalogEntry Entry(string key, DungeonSpawnCategory category)
        {
            return new DungeonContentCatalogEntry
            {
                contentKey = key,
                category = category,
                weight = 1f,
                minProgression = 0f,
                maxProgression = 1f,
                footprintCells = Vector2Int.one,
                uniformScaleRange = Vector2.one
            };
        }

        private static DungeonContentCatalogEntry Clone(DungeonContentCatalogEntry source)
        {
            return new DungeonContentCatalogEntry
            {
                contentKey = source.contentKey,
                category = source.category,
                weight = source.weight,
                minProgression = source.minProgression,
                maxProgression = source.maxProgression,
                placement = source.placement,
                requiredRoomTags = new List<string>(source.requiredRoomTags),
                footprintCells = source.footprintCells,
                minimumSpacingCells = source.minimumSpacingCells,
                randomizeYaw = source.randomizeYaw,
                yawDegreesRange = source.yawDegreesRange,
                uniformScaleRange = source.uniformScaleRange
            };
        }

        private static DungeonBlueprint CreateLegacyBlueprint(RogueDungeonSettings settings, int seed)
        {
            return DungeonBlueprintGenerator.Generate(
                DungeonGenerationRequest.Create(
                    settings,
                    seed,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash)).Blueprint;
        }

        private static DungeonSpawnRecord FindFirst(DungeonBlueprint blueprint, DungeonSpawnCategory category)
        {
            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = blueprint.spawns[i];
                if (spawn != null && spawn.category == category) return spawn;
            }
            return null;
        }

        // 지정 좌표의 Blueprint cell 레코드를 선형 검색합니다.
        private static DungeonCellRecord FindCell(DungeonBlueprint blueprint, Vector2Int coordinate)
        {
            for (int i = 0; i < blueprint.cells.Count; i++)
            {
                DungeonCellRecord cell = blueprint.cells[i];
                if (cell != null && cell.coordinate == coordinate) return cell;
            }
            return null;
        }

        private sealed class InactiveParentResolver : IDungeonContentResolver
        {
            public bool AllFactoryParentsInactive { get; private set; } = true;
            public int CallCount { get; private set; }

            // 모든 spawn을 factory로 해석하고 factory 호출 시 부모의 활성 상태를 기록합니다.
            public bool TryResolve(
                DungeonSpawnRecord record,
                out DungeonContentResolution resolution)
            {
                DungeonSpawnCategory category = record.category;
                string name = record.instanceName;
                resolution = DungeonContentResolution.FromFactory(category, delegate(Transform parent)
                {
                    CallCount++;
                    if (parent.gameObject.activeInHierarchy) AllFactoryParentsInactive = false;
                    GameObject instance = new GameObject(name);
                    instance.transform.SetParent(parent, false);
                    return instance;
                });
                return true;
            }
        }

        private static int CountCategory(DungeonBlueprint blueprint, DungeonSpawnCategory category)
        {
            int count = 0;
            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = blueprint.spawns[i];
                if (spawn != null && spawn.category == category) count++;
            }
            return count;
        }

        private static int CountKey(DungeonBlueprint blueprint, string key)
        {
            int count = 0;
            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = blueprint.spawns[i];
                if (spawn != null && spawn.contentKey == key) count++;
            }
            return count;
        }

        private static string CaptureCategory(DungeonBlueprint blueprint, DungeonSpawnCategory category)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord spawn = blueprint.spawns[i];
                if (spawn == null || spawn.category != category) continue;
                builder.Append(spawn.spawnId).Append('|')
                    .Append(spawn.cell.x).Append(',').Append(spawn.cell.y).Append('|')
                    .Append(spawn.contentKey).Append('|')
                    .Append(spawn.variantSeed).Append('|')
                    .Append(spawn.localPosition.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(spawn.localPosition.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(spawn.localPosition.z.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }
            return builder.ToString();
        }

        private static void DestroyBuildRoot(GameObject root)
        {
            if (root == null) return;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null || !mesh.name.StartsWith("Generated Dungeon", System.StringComparison.Ordinal)) continue;
                filters[i].sharedMesh = null;
                Object.DestroyImmediate(mesh);
            }
            Object.DestroyImmediate(root);
        }
    }
}
