// 职责：像素移动并行 Job，处理粉末/液体/气体的物理运动（重力、横向扩散、位移置换）。
// Responsibility: Parallel pixel movement job handling powder/liquid/gas physics (gravity, lateral spread, displacement).
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace AOT
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    public struct MovementJob : IJobChunk
    {
        [ReadOnly] public NativeArray<PixelData> CurrentPixels;
        public NativeArray<PixelData> NextPixels;
        public GridSize GridSize;
        public NativeReference<Xorshift128Plus> RngState;
        public int FrameIndex;

        public void Execute(in ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            Xorshift128Plus rng = RngState.Value;

            NativeArray<PixelData> chunkPixels = chunk.GetNativeArray<PixelData>(
                TypeManager.GetTypeIndex<PixelData>());

            bool forward = (FrameIndex & 1) == 0;
            int width = GridSize.Width;
            int height = GridSize.Height;

            for (int entityIdx = 0; entityIdx < chunkPixels.Length; entityIdx++)
            {
                int globalIndex = firstEntityIndex + entityIdx;
                int x = globalIndex % width;
                int y = globalIndex / width;

                if (x >= width || y >= height)
                    continue;

                PixelData pixel = CurrentPixels[globalIndex];
                if (pixel.MaterialType == (byte)MaterialType.Air)
                    continue;

                byte mt = pixel.MaterialType;

                if (PixelReactionUtility.IsPowder(mt))
                {
                    ProcessPowder(ref pixel, ref rng, x, y, width, height, forward);
                }
                else if (PixelReactionUtility.IsLiquid(mt))
                {
                    ProcessLiquid(ref pixel, ref rng, x, y, width, height, forward);
                }
                else if (PixelReactionUtility.IsGas(mt))
                {
                    ProcessGas(ref pixel, ref rng, x, y, width, height, forward);
                }

                NextPixels[globalIndex] = pixel;
            }

            RngState.Value = rng;
        }

        private void ProcessPowder(ref PixelData pixel, ref Xorshift128Plus rng,
            int x, int y, int width, int height, bool forward)
        {
            if (y <= 0)
            {
                pixel.FallingFrames = 0;
                return;
            }

            int below = (y - 1) * width + x;
            if (CurrentPixels[below].MaterialType == (byte)MaterialType.Air)
            {
                SwapWithBelow(ref pixel, below, forward);
                pixel.FallingFrames++;
                return;
            }

            int dir = (rng.Next() & 1) == 0 ? -1 : 1;
            if (!forward)
                dir = -dir;

            int leftX = x - 1;
            int rightX = x + 1;

            if (dir < 0 && leftX >= 0)
            {
                int belowLeft = (y - 1) * width + leftX;
                if (CurrentPixels[belowLeft].MaterialType == (byte)MaterialType.Air)
                {
                    SwapWithPosition(ref pixel, belowLeft, forward);
                    pixel.FallingFrames = 0;
                    return;
                }
            }

            if (dir > 0 && rightX < width)
            {
                int belowRight = (y - 1) * width + rightX;
                if (CurrentPixels[belowRight].MaterialType == (byte)MaterialType.Air)
                {
                    SwapWithPosition(ref pixel, belowRight, forward);
                    pixel.FallingFrames = 0;
                    return;
                }
            }

            pixel.FallingFrames = 0;
        }

        private void ProcessLiquid(ref PixelData pixel, ref Xorshift128Plus rng,
            int x, int y, int width, int height, bool forward)
        {
            if (y <= 0)
                return;

            int below = (y - 1) * width + x;
            if (CurrentPixels[below].MaterialType == (byte)MaterialType.Air)
            {
                SwapWithBelow(ref pixel, below, forward);
                return;
            }

            int dir = (rng.Next() & 1) == 0 ? -1 : 1;
            if (!forward)
                dir = -dir;

            int sideX = x + dir;
            if (sideX >= 0 && sideX < width)
            {
                int side = y * width + sideX;
                if (CurrentPixels[side].MaterialType == (byte)MaterialType.Air)
                {
                    SwapWithPosition(ref pixel, side, forward);
                    return;
                }
            }

            int otherX = x - dir;
            if (otherX >= 0 && otherX < width)
            {
                int otherSide = y * width + otherX;
                if (CurrentPixels[otherSide].MaterialType == (byte)MaterialType.Air)
                {
                    SwapWithPosition(ref pixel, otherSide, forward);
                }
            }
        }

        private void ProcessGas(ref PixelData pixel, ref Xorshift128Plus rng,
            int x, int y, int width, int height, bool forward)
        {
            if (y >= height - 1)
                return;

            int above = (y + 1) * width + x;
            if (CurrentPixels[above].MaterialType == (byte)MaterialType.Air)
            {
                SwapWithAbove(ref pixel, above, forward);
                return;
            }

            int dir = (rng.Next() & 1) == 0 ? -1 : 1;
            if (!forward)
                dir = -dir;

            int sideX = x + dir;
            if (sideX >= 0 && sideX < width)
            {
                int side = y * width + sideX;
                if (CurrentPixels[side].MaterialType == (byte)MaterialType.Air)
                {
                    SwapWithPosition(ref pixel, side, forward);
                }
            }
        }

        private void SwapWithBelow(ref PixelData pixel, int belowIndex, bool forward)
        {
            if (!forward)
                return;

            PixelData belowPixel = CurrentPixels[belowIndex];
            NextPixels[belowIndex] = pixel;
            NextPixels[belowIndex] = belowPixel;
        }

        private void SwapWithAbove(ref PixelData pixel, int aboveIndex, bool forward)
        {
            if (!forward)
                return;

            PixelData abovePixel = CurrentPixels[aboveIndex];
            NextPixels[aboveIndex] = pixel;
            NextPixels[aboveIndex] = abovePixel;
        }

        private void SwapWithPosition(ref PixelData pixel, int targetIndex, bool forward)
        {
            if (!forward)
                return;

            PixelData targetPixel = CurrentPixels[targetIndex];
            NextPixels[targetIndex] = pixel;
            NextPixels[targetIndex] = targetPixel;
        }
    }
}
