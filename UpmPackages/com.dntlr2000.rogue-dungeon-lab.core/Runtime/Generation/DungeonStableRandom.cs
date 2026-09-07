using System;

namespace RogueDungeonLab
{
    // StableV2가 사용하는 난수 스트림 ID입니다. 값과 seed 파생 공식은 생성기 버전 계약의 일부입니다.
    public static class DungeonStableRandomStreams
    {
        public const ulong Layout = 0x52444C5F4C415932UL;      // "RDL_LAY2"
        public const ulong Gimmick = 0x52444C5F47494D32UL;     // "RDL_GIM2"
        public const ulong Enemy = 0x52444C5F454E4D32UL;       // "RDL_ENM2"
        public const ulong Destructible = 0x52444C5F44535432UL; // "RDL_DST2"
        public const ulong Prop = 0x52444C5F50525032UL;        // "RDL_PRP2"
        public const ulong Variant = 0x52444C5F56415232UL;     // "RDL_VAR2"

        private const ulong BaseSeedDomain = 0xD1B54A32D192ED03UL;
        private const ulong SequenceDomain = 0x8CB92BA72F3D8DD7UL;
        private const ulong ChildDomain = 0xDB4F0B9175AE2165UL;

        // signed int 시드의 32비트 패턴과 고정 stream ID에서 재현 가능한 PCG32 인스턴스를 만듭니다.
        public static DungeonStableRandom Create(int baseSeed, ulong streamId)
        {
            ulong initialState = DeriveSeed(baseSeed, streamId);
            ulong sequence = SplitMix64(unchecked(streamId ^ SequenceDomain));
            return new DungeonStableRandom(initialState, sequence);
        }

        // spawn 범주·셀·범주 내 순번에서 다른 범주의 호출 수에 영향받지 않는 child stream을 만듭니다.
        public static DungeonStableRandom CreateChild(
            int baseSeed,
            ulong streamId,
            int categoryKey,
            int cellX,
            int cellZ,
            int categoryIndex)
        {
            ulong childSeed = DeriveChildSeed(baseSeed, streamId, categoryKey, cellX, cellZ, categoryIndex);
            ulong sequence = SplitMix64(unchecked(childSeed ^ streamId ^ ChildDomain));
            return new DungeonStableRandom(childSeed, sequence);
        }

        // 테스트와 저장 지문에서 seed 파생 계약 자체를 고정할 수 있도록 결과를 공개합니다.
        public static ulong DeriveSeed(int baseSeed, ulong streamId)
        {
            ulong seedBits = unchecked((uint)baseSeed);
            ulong duplicatedSeed = unchecked((seedBits << 32) | seedBits);
            return SplitMix64(unchecked(duplicatedSeed ^ streamId ^ BaseSeedDomain));
        }

        // 문자열이나 런타임 hash에 의존하지 않고 명시적 정수 필드를 순서대로 혼합합니다.
        public static ulong DeriveChildSeed(
            int baseSeed,
            ulong streamId,
            int categoryKey,
            int cellX,
            int cellZ,
            int categoryIndex)
        {
            ulong value = unchecked(DeriveSeed(baseSeed, streamId) ^ ChildDomain);
            value = SplitMix64(unchecked(value ^ (uint)categoryKey));
            value = SplitMix64(unchecked(value ^ ((ulong)(uint)cellX << 32) ^ (uint)cellZ));
            return SplitMix64(unchecked(value ^ (uint)categoryIndex));
        }

        // SplitMix64의 고정 avalanche 단계입니다. 상수나 연산 순서를 바꾸면 새 생성기 버전이 필요합니다.
        public static ulong SplitMix64(ulong value)
        {
            unchecked
            {
                ulong mixed = value + 0x9E3779B97F4A7C15UL;
                mixed = (mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL;
                mixed = (mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL;
                return mixed ^ (mixed >> 31);
            }
        }
    }

    // PCG-XSH-RR 64/32의 명시적 고정 구현입니다. 원시 출력은 테스트용 golden vector API이기도 합니다.
    public sealed class DungeonStableRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private ulong _state;
        private readonly ulong _increment;

        public DungeonStableRandom(ulong initialState, ulong sequence)
        {
            unchecked
            {
                _state = 0UL;
                _increment = (sequence << 1) | 1UL;
                NextUInt();
                _state += initialState;
                NextUInt();
            }
        }

        // PCG32 원시 출력입니다. multiplier, rotation과 초기화 절차는 StableV2 계약입니다.
        public uint NextUInt()
        {
            unchecked
            {
                ulong previous = _state;
                _state = previous * Multiplier + _increment;
                uint xorShifted = (uint)(((previous >> 18) ^ previous) >> 27);
                int rotation = (int)(previous >> 59);
                return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
            }
        }

        // modulo bias를 거부 표본으로 제거한 [minInclusive, maxExclusive) 정수를 반환합니다.
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException("maxExclusive", "maxExclusive must be greater than or equal to minInclusive.");
            }
            if (maxExclusive == minInclusive) return minInclusive;

            uint bound = unchecked((uint)((long)maxExclusive - minInclusive));
            uint threshold = unchecked(0U - bound) % bound;
            uint value;
            do
            {
                value = NextUInt();
            }
            while (value < threshold);
            return unchecked((int)((long)minInclusive + value % bound));
        }

        // 상위 24비트만 사용해 정확히 정의된 [0, 1) float를 반환합니다.
        public float NextFloat01()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        // 두 PCG32 출력에서 53비트를 조합한 [0, 1) double을 반환합니다.
        public double NextDouble01()
        {
            ulong high = NextUInt() >> 5;
            ulong low = NextUInt() >> 6;
            return ((high << 26) | low) * (1.0 / 9007199254740992.0);
        }
    }
}
