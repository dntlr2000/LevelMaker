using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonBlueprintPipelineTests
    {
        // LegacyV1 요청이 검증 가능한 Blueprint와 기존 layout·콘텐츠 개수를 함께 생성하는지 검사합니다.
        [Test]
        public void BlueprintGenerator_ProducesValidLegacyBlueprint()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            try
            {
                settings.ApplyPreset(DungeonPreset.Balanced);
                DungeonGenerationRequest request = DungeonGenerationRequest.Create(
                    settings,
                    86420,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash);

                DungeonBlueprintGenerationResult result = DungeonBlueprintGenerator.Generate(request);
                DungeonValidationReport validation = DungeonBlueprintValidator.Validate(result.Blueprint);

                Assert.That(validation.IsValid, Is.True, JoinIssues(validation));
                Assert.That(result.Blueprint.generatorVersion, Is.EqualTo(DungeonGeneratorVersions.LegacyV1));
                Assert.That(result.Blueprint.catalogPlanningHash, Is.EqualTo(DungeonBuiltInContentKeys.LegacyCatalogPlanningHash));
                Assert.That(result.Blueprint.cells.Count, Is.EqualTo(result.Layout.WalkableCellCount));
                Assert.That(result.Blueprint.rooms.Count, Is.EqualTo(result.Layout.Rooms.Count));
                Assert.That(CountCategory(result.Blueprint, DungeonSpawnCategory.Marker), Is.EqualTo(2));
                Assert.That(CountCategory(result.Blueprint, DungeonSpawnCategory.Gimmick), Is.EqualTo(result.ContentCounts.GimmickCount));
                Assert.That(CountCategory(result.Blueprint, DungeonSpawnCategory.Enemy), Is.EqualTo(result.ContentCounts.EnemyCount));
                Assert.That(CountCategory(result.Blueprint, DungeonSpawnCategory.Destructible), Is.EqualTo(result.ContentCounts.DestructibleCount));
                Assert.That(CountCategory(result.Blueprint, DungeonSpawnCategory.Prop), Is.EqualTo(result.ContentCounts.PropCount));
                Assert.That(result.Blueprint.blueprintHash, Is.EqualTo(DungeonBlueprintHasher.Compute(result.Blueprint)));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        // 같은 Blueprint를 두 root에 구축했을 때 메시·계층·transform·stable spawn ID가 같은지 검사합니다.
        [Test]
        public void SceneBuilder_RebuildsSameBlueprintWithStableIdentities()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            GameObject firstRoot = new GameObject("R2 First Build");
            GameObject secondRoot = new GameObject("R2 Second Build");
            try
            {
                settings.ApplyPreset(DungeonPreset.Compact);
                DungeonGenerationRequest request = DungeonGenerationRequest.Create(
                    settings,
                    1357911,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash);
                DungeonBlueprint blueprint = DungeonBlueprintGenerator.Generate(request).Blueprint;

                DungeonSceneBuildResult first = DungeonSceneBuilder.Build(firstRoot.transform, blueprint, settings);
                DungeonSceneBuildResult second = DungeonSceneBuilder.Build(secondRoot.transform, blueprint, settings);

                Assert.That(second.MeshTriangleCount, Is.EqualTo(first.MeshTriangleCount));
                AssertCountsEqual(first.ContentCounts, second.ContentCounts);
                Assert.That(CaptureHierarchy(secondRoot.transform), Is.EqualTo(CaptureHierarchy(firstRoot.transform)));
                AssertStableIdentities(firstRoot, blueprint);
                AssertStableIdentities(secondRoot, blueprint);
            }
            finally
            {
                DestroyBuildRoot(firstRoot);
                DestroyBuildRoot(secondRoot);
                Object.DestroyImmediate(settings);
            }
        }

        // 기존 layout 기반 Spawn wrapper와 Blueprint 기반 overload가 같은 콘텐츠 계층을 만드는지 검사합니다.
        [Test]
        public void LegacyContentSpawnerWrapper_MatchesBlueprintContentBuild()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            GameObject legacyRoot = new GameObject("R2 Legacy Content");
            GameObject blueprintRoot = new GameObject("R2 Blueprint Content");
            try
            {
                settings.ApplyPreset(DungeonPreset.Chaos);
                const int seed = -24681357;
                DungeonGenerationRequest request = DungeonGenerationRequest.Create(
                    settings,
                    seed,
                    DungeonGeneratorVersions.LegacyV1,
                    DungeonBuiltInContentKeys.LegacyCatalogPlanningHash);
                DungeonBlueprintGenerationResult generation = DungeonBlueprintGenerator.Generate(request);

                ContentSpawnCounts legacyCounts = DungeonContentSpawner.Spawn(
                    legacyRoot.transform,
                    generation.Layout,
                    settings,
                    seed);
                ContentSpawnCounts blueprintCounts = DungeonContentSpawner.Spawn(
                    blueprintRoot.transform,
                    generation.Blueprint,
                    settings);

                AssertCountsEqual(legacyCounts, blueprintCounts);
                Assert.That(CaptureHierarchy(blueprintRoot.transform), Is.EqualTo(CaptureHierarchy(legacyRoot.transform)));
            }
            finally
            {
                Object.DestroyImmediate(legacyRoot);
                Object.DestroyImmediate(blueprintRoot);
                Object.DestroyImmediate(settings);
            }
        }

        // 지정 spawn 범주의 레코드 수를 계산합니다.
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

        // GenerationReport 호환 개수 네 항목이 모두 같은지 검사합니다.
        private static void AssertCountsEqual(ContentSpawnCounts expected, ContentSpawnCounts actual)
        {
            Assert.That(actual.GimmickCount, Is.EqualTo(expected.GimmickCount));
            Assert.That(actual.EnemyCount, Is.EqualTo(expected.EnemyCount));
            Assert.That(actual.DestructibleCount, Is.EqualTo(expected.DestructibleCount));
            Assert.That(actual.PropCount, Is.EqualTo(expected.PropCount));
        }

        // 구축된 identity가 모든 Blueprint spawn을 중복 없이 가리키는지 검사합니다.
        private static void AssertStableIdentities(GameObject root, DungeonBlueprint blueprint)
        {
            DungeonSpawnIdentity[] identities = root.GetComponentsInChildren<DungeonSpawnIdentity>(true);
            Dictionary<string, DungeonSpawnRecord> records = new Dictionary<string, DungeonSpawnRecord>();
            for (int i = 0; i < blueprint.spawns.Count; i++)
            {
                DungeonSpawnRecord record = blueprint.spawns[i];
                records.Add(record.spawnId, record);
            }

            Assert.That(identities.Length, Is.EqualTo(records.Count));
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < identities.Length; i++)
            {
                DungeonSpawnIdentity identity = identities[i];
                DungeonSpawnRecord record;
                Assert.That(records.TryGetValue(identity.SpawnId, out record), Is.True, identity.SpawnId);
                Assert.That(seen.Add(identity.SpawnId), Is.True, "Duplicate built identity: " + identity.SpawnId);
                Assert.That(identity.ContentKey, Is.EqualTo(record.contentKey));
                Assert.That(identity.Category, Is.EqualTo(record.category));
                Assert.That(identity.Cell, Is.EqualTo(record.cell));
            }
        }

        // 자식 순서와 모든 로컬 transform·identity를 문화권 독립 문자열로 기록합니다.
        private static string CaptureHierarchy(Transform root)
        {
            StringBuilder builder = new StringBuilder(32768);
            for (int i = 0; i < root.childCount; i++) AppendTransform(builder, root.GetChild(i), string.Empty, i);
            return builder.ToString();
        }

        // 단일 Transform과 자식을 재귀적으로 hierarchy 지문에 추가합니다.
        private static void AppendTransform(StringBuilder builder, Transform current, string parentPath, int siblingIndex)
        {
            string path = parentPath + "/" + siblingIndex.ToString(CultureInfo.InvariantCulture) + ":" + current.name;
            Quaternion rotation = current.localRotation;
            builder.Append(path).Append('|');
            AppendVector(builder, current.localPosition);
            AppendVector(builder, current.localScale);
            AppendFloat(builder, rotation.x);
            AppendFloat(builder, rotation.y);
            AppendFloat(builder, rotation.z);
            AppendFloat(builder, rotation.w);
            DungeonSpawnIdentity identity = current.GetComponent<DungeonSpawnIdentity>();
            builder.Append(identity != null ? identity.SpawnId : string.Empty).Append(';');
            for (int i = 0; i < current.childCount; i++) AppendTransform(builder, current.GetChild(i), path, i);
        }

        // Vector3 세 축을 round-trip 형식으로 hierarchy 지문에 추가합니다.
        private static void AppendVector(StringBuilder builder, Vector3 value)
        {
            AppendFloat(builder, value.x);
            AppendFloat(builder, value.y);
            AppendFloat(builder, value.z);
        }

        // float 값을 문화권 독립 round-trip 형식으로 hierarchy 지문에 추가합니다.
        private static void AppendFloat(StringBuilder builder, float value)
        {
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        }

        // 합성 mesh를 명시적으로 해제한 뒤 테스트 root를 제거합니다.
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

        // 검증 실패 메시지에 모든 코드와 설명을 연결합니다.
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
