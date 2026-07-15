// 职责：MurmurHash3 哈希算法 Burst 兼容实现，用于状态哈希验证。
// Responsibility: Burst-compatible MurmurHash3 implementation for state hash verification.
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;

namespace AOT
{
    [BurstCompile]
    public static class MurmurHash3
    {
        private const uint Seed = 0x9747B28C;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Compute(NativeArray<PixelData> data)
        {
            uint h = Seed;
            int count = data.Length;
            int i = 0;

            while (i + 3 < count)
            {
                uint k = CombineFourPixels(data, i);
                k *= 0xcc9e2d51;
                k = (k << 15) | (k >> 17);
                k *= 0x1b873593;
                h ^= k;
                h = (h << 13) | (h >> 19);
                h = h * 5 + 0xe6546b64;
                i += 4;
            }

            if (i < count)
            {
                uint k = CombineRemainingPixels(data, i, count);
                k *= 0xcc9e2d51;
                k = (k << 15) | (k >> 17);
                k *= 0x1b873593;
                h ^= k;
            }

            h ^= (uint)count;
            h = FinalMix(h);
            return h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint CombineFourPixels(NativeArray<PixelData> data, int start)
        {
            uint h = 0;
            for (int j = 0; j < 4 && start + j < data.Length; j++)
            {
                PixelData p = data[start + j];
                h ^= (uint)(p.MaterialType)
                    | (((uint)(p.Temperature) & 0xFFFF) << 8)
                    | ((uint)(p.Density) << 24);
                h ^= (uint)(p.Flags)
                    | ((uint)(p.FallingFrames) << 8)
                    | ((uint)(p.Color) << 16);
                h ^= ((uint)(p.VelocityX) & 0xFFFF)
                    | (((uint)(p.VelocityY) & 0xFFFF) << 16);
                h ^= (uint)(p.Lifetime) & 0xFFFF;
            }
            return h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint CombineRemainingPixels(NativeArray<PixelData> data, int start, int count)
        {
            uint h = 0;
            for (int j = start; j < count; j++)
            {
                PixelData p = data[j];
                h ^= (uint)(p.MaterialType)
                    | (((uint)(p.Temperature) & 0xFFFF) << 8)
                    | ((uint)(p.Density) << 24);
                h ^= (uint)(p.Flags)
                    | ((uint)(p.FallingFrames) << 8)
                    | ((uint)(p.Color) << 16);
                h ^= ((uint)(p.VelocityX) & 0xFFFF)
                    | (((uint)(p.VelocityY) & 0xFFFF) << 16);
                h ^= (uint)(p.Lifetime) & 0xFFFF;
            }
            return h;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FinalMix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }
    }
}
