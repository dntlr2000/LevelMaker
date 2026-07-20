using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonBlueprintContractTests
    {
        // 설정 스냅샷이 원본을 바꾸지 않고 정규화되며 관련 값만 해시에 반영되는지 검사합니다.
        [Test]
        public void RecipeSnapshot_NormalizesWithoutMutatingSourceAndHashesDeterministically()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            try
            {
                settings.stageWidthCells = 2;
                settings.stageDepthCells = 500;
                settings.roomSizeMin = new Vector2Int(1, 200);
                settings.roomSizeMax = new Vector2Int(1, 2);
                settings.cellSize = -4f;
                settings.enemyProfile.baseDensity = 0.9f;
                settings.seed = 111;
                settings.spawnDropMarkers = true;

                DungeonRecipeSnapshot first = DungeonRecipeSnapshot.Capture(settings);
                DungeonRecipeSnapshot second = DungeonRecipeSnapshot.Capture(settings);

                Assert.That(settings.stageWidthCells, Is.EqualTo(2));
                Assert.That(settings.stageDepthCells, Is.EqualTo(500));
                Assert.That(settings.cellSize, Is.EqualTo(-4f));
                Assert.That(settings.enemyProfile.baseDensity, Is.EqualTo(0.9f));
                Assert.That(first.stageWidthCells, Is.EqualTo(12));
                Assert.That(first.stageDepthCells, Is.EqualTo(96));
                Assert.That(first.cellSize, Is.EqualTo(1.5f));
                Assert.That(first.roomSizeMin, Is.EqualTo(new Vector2Int(3, 92)));
                Assert.That(first.roomSizeMax, Is.EqualTo(new Vector2Int(3, 92)));
                Assert.That(first.enemyProfile.baseDensity, Is.EqualTo(0.5f));
                Assert.That(first.ComputeHash(), Is.EqualTo(second.ComputeHash()));

                string generationHash = first.ComputeHash();
                settings.seed = 222;
                settings.spawnDropMarkers = false;
                settings.resetDropStatsOnGenerate = true;
                Assert.That(DungeonRecipeSnapshot.Capture(settings).ComputeHash(), Is.EqualTo(generationHash));

                settings.enemyProfile.baseDensity = 0.25f;
                Assert.That(DungeonRecipeSnapshot.Capture(settings).ComputeHash(), Is.Not.EqualTo(generationHash));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        // 생성 요청이 시드·버전·추적 정보와 원본 비변경 스냅샷을 올바르게 묶는지 검사합니다.
        [Test]
        public void GenerationRequest_CapturesRecipeAndExplicitMetadata()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            try
            {
                settings.ApplyPreset(DungeonPreset.Compact);
                DungeonGenerationRequest request = DungeonGenerationRequest.Create(settings, -77, DungeonGeneratorVersions.LegacyV1, "builtin-v1", "request-42");

                Assert.That(request.seed, Is.EqualTo(-77));
                Assert.That(request.generatorVersion, Is.EqualTo(DungeonGeneratorVersions.LegacyV1));
                Assert.That(request.catalogPlanningHash, Is.EqualTo("builtin-v1"));
                Assert.That(request.requestId, Is.EqualTo("request-42"));
                Assert.That(request.recipeSnapshot.stageWidthCells, Is.EqualTo(26));
                Assert.That(request.RecipeHash, Has.Length.EqualTo(64));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        // Blueprint JSON round-trip, 깊은 복사, canonical 정렬과 비논리 메타데이터 제외를 검사합니다.
        [Test]
        public void Blueprint_JsonRoundTripPreservesCanonicalHash()
        {
            DungeonBlueprint blueprint = CreateValidBlueprint();
            string approvedHash = blueprint.blueprintHash;
            string json = DungeonBlueprintSerialization.ToJson(blueprint, true);
            DungeonBlueprint restored = DungeonBlueprintSerialization.FromJson(json);

            Assert.That(DungeonBlueprintHasher.Compute(restored), Is.EqualTo(approvedHash));
            Assert.That(restored.blueprintHash, Is.EqualTo(approvedHash));
            Assert.That(restored.cells, Is.Not.SameAs(blueprint.cells));
            Assert.That(restored.spawns[0], Is.Not.SameAs(blueprint.spawns[0]));

            restored.cells.Reverse();
            restored.rooms[0].tags.Reverse();
            restored.spawns[0].tags.Reverse();
            restored.createdUtcTicks = DateTime.UtcNow.Ticks;
            restored.authoringNote = "해시에 포함되지 않는 제작 메모";
            Assert.That(DungeonBlueprintHasher.Compute(restored), Is.EqualTo(approvedHash));

            restored.spawns[0].contentKey = "enemy/changed";
            Assert.That(DungeonBlueprintHasher.Compute(restored), Is.Not.EqualTo(approvedHash));
        }

        // BlueprintAsset 저장이 원본과 분리된 복사본을 소유하고 저장 해시를 갱신하는지 검사합니다.
        [Test]
        public void BlueprintAsset_StoresIndependentHashedCopy()
        {
            DungeonBlueprint source = CreateValidBlueprint();
            DungeonBlueprintAsset asset = ScriptableObject.CreateInstance<DungeonBlueprintAsset>();
            try
            {
                asset.Store(source);
                Assert.That(asset.blueprint, Is.Not.SameAs(source));
                Assert.That(asset.blueprint.cells, Is.Not.SameAs(source.cells));
                Assert.That(asset.blueprint.blueprintHash, Is.EqualTo(DungeonBlueprintHasher.Compute(asset.blueprint)));

                source.cells[0].distanceFromEntrance = 99;
                Assert.That(asset.blueprint.cells[0].distanceFromEntrance, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        // 정상 Blueprint와 손상 Blueprint가 안정적인 검증 코드로 구분되는지 검사합니다.
        [Test]
        public void BlueprintValidator_ReportsConnectivityIdentityAndHashCorruption()
        {
            DungeonBlueprint valid = CreateValidBlueprint();
            DungeonValidationReport validReport = DungeonBlueprintValidator.Validate(valid);
            Assert.That(validReport.IsValid, Is.True, JoinIssues(validReport));
            Assert.That(validReport.WarningCount, Is.EqualTo(0));

            DungeonBlueprint broken = valid.DeepClone();
            broken.cells.RemoveAll(delegate(DungeonCellRecord cell) { return cell.coordinate.x == 1; });
            broken.spawns.Add(new DungeonSpawnRecord
            {
                spawnId = broken.spawns[0].spawnId,
                category = DungeonSpawnCategory.Prop,
                contentKey = "prop/test",
                cell = new Vector2Int(2, 1),
                localScale = Vector3.one
            });
            DungeonValidationReport brokenReport = DungeonBlueprintValidator.Validate(broken);

            Assert.That(brokenReport.IsValid, Is.False);
            Assert.That(brokenReport.ContainsCode(DungeonBlueprintValidationCodes.DisconnectedFloor), Is.True, JoinIssues(brokenReport));
            Assert.That(brokenReport.ContainsCode(DungeonBlueprintValidationCodes.DuplicateSpawnId), Is.True, JoinIssues(brokenReport));
            Assert.That(brokenReport.ContainsCode(DungeonBlueprintValidationCodes.BlueprintHashMismatch), Is.True, JoinIssues(brokenReport));
        }

        // 직렬화와 검증 테스트에서 공유할 작은 연결형 Blueprint를 만듭니다.
        private static DungeonBlueprint CreateValidBlueprint()
        {
            DungeonBlueprint blueprint = new DungeonBlueprint
            {
                formatVersion = DungeonBlueprintFormat.CurrentVersion,
                generatorVersion = DungeonGeneratorVersions.LegacyV1,
                seed = 13579,
                recipeHash = new string('a', 64),
                catalogPlanningHash = "builtin-v1",
                grid = new DungeonGridRecord { width = 3, depth = 2, cellSize = 3f, wallHeight = 3.2f },
                entrance = new Vector2Int(0, 0),
                exit = new Vector2Int(2, 1),
                createdUtcTicks = 638000000000000000L,
                authoringNote = "contract fixture"
            };
            blueprint.rooms.Add(new DungeonRoomRecord
            {
                roomId = "room:0",
                bounds = new RectInt(0, 0, 3, 2),
                tags = new List<string> { "combat", "start" }
            });
            for (int z = 0; z < 2; z++)
            {
                for (int x = 0; x < 3; x++)
                {
                    blueprint.cells.Add(new DungeonCellRecord
                    {
                        coordinate = new Vector2Int(x, z),
                        flags = DungeonCellFlags.Floor,
                        roomId = "room:0",
                        distanceFromEntrance = x + z
                    });
                }
            }
            blueprint.spawns.Add(new DungeonSpawnRecord
            {
                spawnId = "enemy:1:1:0",
                category = DungeonSpawnCategory.Enemy,
                contentKey = "enemy/test",
                instanceName = "Test Enemy",
                cell = new Vector2Int(1, 1),
                localPosition = new Vector3(0f, 1f, 1.5f),
                yawDegrees = 90f,
                localScale = Vector3.one,
                roomId = "room:0",
                progression = 2f / 3f,
                tags = new List<string> { "melee", "test" },
                variantSeed = 2468
            });
            blueprint.RefreshHash();
            return blueprint;
        }

        // 실패 메시지에 모든 검증 코드와 설명을 한 줄로 연결합니다.
        private static string JoinIssues(DungeonValidationReport report)
        {
            List<string> values = new List<string>();
            for (int i = 0; i < report.issues.Count; i++)
            {
                DungeonValidationIssue issue = report.issues[i];
                if (issue != null) values.Add(issue.code + ":" + issue.message);
            }
            return string.Join(" | ", values);
        }
    }
}
