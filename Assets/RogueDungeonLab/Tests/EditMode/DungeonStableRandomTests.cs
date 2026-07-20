using NUnit.Framework;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonStableRandomTests
    {
        // PCG32 상수·초기화·Layout stream seed 파생을 고정 출력 벡터로 보호합니다.
        [Test]
        public void StableRandom_LayoutStreamMatchesApprovedVector()
        {
            DungeonStableRandom random = DungeonStableRandomStreams.Create(0, DungeonStableRandomStreams.Layout);
            uint[] expected =
            {
                3511530157U,
                4116939007U,
                3310965006U,
                4176347190U,
                889213245U,
                2965736289U
            };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(random.NextUInt(), Is.EqualTo(expected[i]), "PCG32 output index " + i);
            }
        }

        // 음수 base seed도 32비트 패턴 그대로 고정된 Enemy stream을 만드는지 검사합니다.
        [Test]
        public void StableRandom_NegativeSeedMatchesApprovedVector()
        {
            DungeonStableRandom random = DungeonStableRandomStreams.Create(-77, DungeonStableRandomStreams.Enemy);
            Assert.That(random.NextUInt(), Is.EqualTo(3560885058U));
            Assert.That(random.NextUInt(), Is.EqualTo(1126102498U));
            Assert.That(random.NextUInt(), Is.EqualTo(630272725U));
            Assert.That(random.NextUInt(), Is.EqualTo(579849972U));
        }

        // spawn별 child stream이 재생성 가능하고 범주 밖 난수 소비와 무관한지 검사합니다.
        [Test]
        public void StableRandom_ChildStreamMatchesApprovedVectorAndRepeats()
        {
            DungeonStableRandom first = DungeonStableRandomStreams.CreateChild(
                12345,
                DungeonStableRandomStreams.Variant,
                2,
                9,
                11,
                3);
            DungeonStableRandom second = DungeonStableRandomStreams.CreateChild(
                12345,
                DungeonStableRandomStreams.Variant,
                2,
                9,
                11,
                3);
            uint[] expected = { 1162562393U, 1849531110U, 274782999U, 3067386279U };

            for (int i = 0; i < expected.Length; i++)
            {
                uint actual = first.NextUInt();
                Assert.That(actual, Is.EqualTo(expected[i]));
                Assert.That(second.NextUInt(), Is.EqualTo(actual));
            }
        }

        // rejection sampling을 사용하는 bounded 정수가 요청 범위를 벗어나지 않는지 검사합니다.
        [Test]
        public void StableRandom_NextIntStaysInsideRequestedRange()
        {
            DungeonStableRandom random = DungeonStableRandomStreams.Create(int.MaxValue, DungeonStableRandomStreams.Prop);
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(random.NextInt(-7, 13), Is.InRange(-7, 12));
            }
        }
    }
}
