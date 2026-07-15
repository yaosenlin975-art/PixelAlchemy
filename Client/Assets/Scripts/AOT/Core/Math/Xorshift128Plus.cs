// 职责：Burst 兼容的 Xorshift128+ 确定性 RNG，用于帧同步随机数生成。
// Responsibility: Burst-compatible Xorshift128+ deterministic RNG for lockstep random number generation.
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace AOT
{
    [BurstCompile]
    public struct Xorshift128Plus
    {
        private ulong _s0;
        private ulong _s1;

        public Xorshift128Plus(ulong seed)
        {
            _s0 = SplitMix64(seed);
            _s1 = SplitMix64(_s0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SplitMix64(ulong input)
        {
            ulong z = input + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong Next()
        {
            ulong x = _s0;
            ulong y = _s1;
            _s0 = y;
            x ^= x << 23;
            _s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
            return _s1 + y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextInt(uint maxExclusive)
        {
            if (maxExclusive == 0)
                return 0;

            ulong r = Next();
            return (uint)(r % maxExclusive);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt(int minInclusive, int maxExclusive)
        {
            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)NextInt(range);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float NextFloat()
        {
            const float invMax = 1.0f / 18446744073709551615.0f;
            return (Next() >> 12) * invMax;
        }
    }
}
