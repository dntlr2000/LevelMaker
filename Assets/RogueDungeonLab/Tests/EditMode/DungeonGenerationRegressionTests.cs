using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonGenerationRegressionTests
    {
        private const string GeneratedRootName = "__RogueDungeonLab_Generated";

        // 승인된 프리셋·시드의 레이아웃과 콘텐츠 셀 지문이 바뀌지 않았는지 검사합니다.
        [TestCase(DungeonPreset.Compact, 12345, "9c26c4dd4b8cfc71b5c2579a154e5c05d01a35ec71d5a08448f52110b5e8c2a3")]
        [TestCase(DungeonPreset.Balanced, -987654321, "f6ac5da2f96eb84809dbc87b35967c1184487ef1919885c0837784adf4a92ce8")]
        [TestCase(DungeonPreset.Chaos, 20260719, "cdd9e177e2fef8a6d632d97f01666318a333e0eb787df0ef2fd6292740cf7008")]
        public void LegacyGenerationFingerprint_MatchesApprovedBaseline(DungeonPreset preset, int seed, string expected)
        {
            string actual = CaptureGeneratedFingerprint(preset, seed);
            Assert.That(actual, Is.EqualTo(expected));
        }

        // 같은 프리셋과 시드를 두 번 생성했을 때 전체 회귀 지문이 같은지 검사합니다.
        [Test]
        public void LegacyGenerationFingerprint_RepeatsForSameSeed()
        {
            string first = CaptureGeneratedFingerprint(DungeonPreset.Balanced, 73125);
            string second = CaptureGeneratedFingerprint(DungeonPreset.Balanced, 73125);
            Assert.That(second, Is.EqualTo(first));
        }

        // 세 프리셋에 분산한 100개 시드에서 모든 floor 셀이 입구 BFS에 연결되는지 검사합니다.
        [Test]
        public void LayoutGenerator_OneHundredSeedsRemainConnected()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    DungeonPreset preset = (DungeonPreset)(i % 3);
                    settings.ApplyPreset(preset);
                    int seed = unchecked(104729 * (i + 1) ^ (int)0x9E3779B9);
                    DungeonLayout layout = DungeonLayoutGenerator.Generate(settings, seed);
                    int visited = 0;
                    foreach (Vector2Int cell in layout.EnumerateFloorCells())
                    {
                        Assert.That(layout.GetDistance(cell), Is.GreaterThanOrEqualTo(0), "Disconnected cell for seed " + seed + ": " + cell);
                        visited++;
                    }
                    Assert.That(visited, Is.EqualTo(layout.WalkableCellCount));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        // 기존 facade의 공개 상태, 완료 이벤트와 generated root 이름을 고정 계약으로 검사합니다.
        [Test]
        public void RogueDungeonGenerator_PublicFacadeAndRootRemainCompatible()
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            GameObject generatorObject = new GameObject("R0 Public Contract Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            try
            {
                settings.ApplyPreset(DungeonPreset.Compact);
                generator.settings = settings;
                int eventCount = 0;
                GenerationReport completed = null;
                generator.GenerationCompleted += delegate(GenerationReport report)
                {
                    eventCount++;
                    completed = report;
                };

                generator.GenerateWithSeed(424242);

                Assert.That(generator.ActiveSeed, Is.EqualTo(424242));
                Assert.That(generator.CurrentLayout, Is.Not.Null);
                Assert.That(generator.CurrentBlueprint, Is.Not.Null);
                Assert.That(generator.CurrentBlueprint.blueprintHash, Is.EqualTo(DungeonBlueprintHasher.Compute(generator.CurrentBlueprint)));
                Assert.That(generator.LastReport, Is.SameAs(completed));
                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(generator.transform.Find(GeneratedRootName), Is.Not.Null);
                Assert.That(generator.GeneratedBounds.size.x, Is.GreaterThan(0f));
            }
            finally
            {
                generator.ClearGenerated();
                UnityEngine.Object.DestroyImmediate(generatorObject);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        // 실제 생성 계층에서 레이아웃, BFS와 콘텐츠 셀을 canonical 문자열로 모아 SHA-256을 계산합니다.
        private static string CaptureGeneratedFingerprint(DungeonPreset preset, int seed)
        {
            RogueDungeonSettings settings = ScriptableObject.CreateInstance<RogueDungeonSettings>();
            GameObject generatorObject = new GameObject("R0 Fingerprint Generator");
            RogueDungeonGenerator generator = generatorObject.AddComponent<RogueDungeonGenerator>();
            try
            {
                settings.ApplyPreset(preset);
                generator.settings = settings;
                generator.GenerateWithSeed(seed);
                DungeonLayout layout = generator.CurrentLayout;
                GenerationReport report = generator.LastReport;
                Transform root = generator.transform.Find(GeneratedRootName);
                Assert.That(root, Is.Not.Null);

                StringBuilder builder = new StringBuilder(32768);
                Append(builder, "preset", ((int)preset).ToString(CultureInfo.InvariantCulture));
                Append(builder, "seed", seed.ToString(CultureInfo.InvariantCulture));
                Append(builder, "size", layout.Width + "," + layout.Depth);
                Append(builder, "entrance", CellText(layout.Entrance));
                Append(builder, "exit", CellText(layout.Exit));
                Append(builder, "maxDistance", layout.MaxDistance.ToString(CultureInfo.InvariantCulture));
                Append(builder, "walkable", layout.WalkableCellCount.ToString(CultureInfo.InvariantCulture));
                Append(builder, "counts", report.roomCount + "," + report.enemyCount + "," + report.destructibleCount + "," + report.propCount + "," + report.gimmickCount);

                for (int i = 0; i < layout.Rooms.Count; i++)
                {
                    RectInt room = layout.Rooms[i];
                    Append(builder, "room", i + ":" + room.x + "," + room.y + "," + room.width + "," + room.height);
                }
                foreach (Vector2Int cell in layout.EnumerateFloorCells())
                {
                    Append(builder, "cell", CellText(cell) + ":" + layout.GetRoomId(cell) + ":" + layout.GetDistance(cell));
                }

                Transform contents = root.Find("Contents");
                Assert.That(contents, Is.Not.Null);
                AppendSpawnGroup(builder, contents, "Special Gimmicks", layout, settings.cellSize);
                AppendSpawnGroup(builder, contents, "Enemies", layout, settings.cellSize);
                AppendSpawnGroup(builder, contents, "Destructibles", layout, settings.cellSize);
                AppendSpawnGroup(builder, contents, "Terrain Props", layout, settings.cellSize);
                return ComputeSha256(builder.ToString());
            }
            finally
            {
                generator.ClearGenerated();
                UnityEngine.Object.DestroyImmediate(generatorObject);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        // 콘텐츠 그룹의 자식 이름과 그리드 셀 위치를 생성 순서대로 지문 문자열에 추가합니다.
        private static void AppendSpawnGroup(StringBuilder builder, Transform contents, string groupName, DungeonLayout layout, float cellSize)
        {
            Transform group = contents.Find(groupName);
            Assert.That(group, Is.Not.Null, groupName);
            Append(builder, "group", groupName + ":" + group.childCount);
            for (int i = 0; i < group.childCount; i++)
            {
                Transform child = group.GetChild(i);
                Vector3 position = child.localPosition;
                int x = Mathf.RoundToInt(position.x / cellSize + layout.Width * 0.5f - 0.5f);
                int z = Mathf.RoundToInt(position.z / cellSize + layout.Depth * 0.5f - 0.5f);
                Append(builder, "spawn", groupName + ":" + child.name + ":" + x + "," + z);
            }
        }

        // 필드 이름과 길이 접두사가 붙은 값을 지문 문자열에 추가합니다.
        private static void Append(StringBuilder builder, string field, string value)
        {
            builder.Append(field.Length).Append(':').Append(field);
            builder.Append(value.Length).Append(':').Append(value).Append(';');
        }

        // 셀 좌표를 문화권에 영향을 받지 않는 정수 문자열로 변환합니다.
        private static string CellText(Vector2Int cell)
        {
            return cell.x.ToString(CultureInfo.InvariantCulture) + "," + cell.y.ToString(CultureInfo.InvariantCulture);
        }

        // UTF-8 문자열의 소문자 SHA-256 값을 계산합니다.
        private static string ComputeSha256(string value)
        {
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
            StringBuilder builder = new StringBuilder(digest.Length * 2);
            for (int i = 0; i < digest.Length; i++) builder.Append(digest[i].ToString("x2"));
            return builder.ToString();
        }
    }
}
