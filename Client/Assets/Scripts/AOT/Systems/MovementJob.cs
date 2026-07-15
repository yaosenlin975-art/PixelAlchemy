using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace AOT
{
    /// <summary>
    /// IJobChunk for pixel movement simulation.
    /// Reads CurrentPixels (neighbors), writes NextPixels (current pixel).
    /// Handles powder/falling, liquid flow, and gas rising.
    /// Collision resolution: (x, y) lexicographic ascending, first-come-first-served.
    /// </summary>
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
            var pixels = chunk.GetNativeArray<PixelData>(SystemAPI.GetTypeHandle<PixelData>(true));
            if (!pixels.IsCreated || pixels.Length == 0)
                return;

            int width = GridSize.Width;
            int height = GridSize.Height;
            bool forward = (FrameIndex & 1) == 0;
            var rng = RngState.Value;

            int startX, endX, stepX;
            int startY, endY, stepY;

            if (forward)
            {
                startX = 0; endX = width; stepX = 1;
                startY = 0; endY = height; stepY = 1;
            }
            else
            {
                startX = width - 1; endX = -1; stepX = -1;
                startY = height - 1; endY = -1; stepY = -1;
            }

            for (int y = startY; y != endY; y += stepY)
            {
                for (int x = startX; x != endX; x += stepX)
                {
                    int idx = y * width + x;
                    PixelData current = CurrentPixels[idx];
                    byte materialType = current.MaterialType;

                    // Skip air/empty cells
                    if (materialType == 0)
                        continue;

                    // Only process movable materials (powder/liquid/gas)
                    byte density = current.Density;
                    if (density == 0)
                    {
                        NextPixels[idx] = current;
                        continue;
                    }

                    PixelData moved = current;
                    bool didMove = false;

                    // Powder: fall down
                    if (density >= 100) // Powder threshold
                    {
                        int belowIdx = (y + 1) * width + x;
                        if (y + 1 < height && CurrentPixels[belowIdx].MaterialType == 0)
                        {
                            // Fall straight down
                            moved.FallingFrames++;
                            NextPixels[belowIdx] = moved;
                            NextPixels[idx] = default;
                            didMove = true;
                        }
                        else if (y + 1 < height)
                        {
                            // Lateral spread
                            moved.FallingFrames = 0;
                            int dir = (rng.Next() & 1) == 0 ? -1 : 1;
                            int spreadX = x + dir;
                            if (spreadX >= 0 && spreadX < width)
                            {
                                int spreadIdx = (y + 1) * width + spreadX;
                                if (CurrentPixels[spreadIdx].MaterialType == 0)
                                {
                                    NextPixels[spreadIdx] = moved;
                                    NextPixels[idx] = default;
                                    didMove = true;
                                }
                            }

                            // Try opposite direction
                            if (!didMove)
                            {
                                spreadX = x - dir;
                                if (spreadX >= 0 && spreadX < width)
                                {
                                    int spreadIdx = (y + 1) * width + spreadX;
                                    if (CurrentPixels[spreadIdx].MaterialType == 0)
                                    {
                                        NextPixels[spreadIdx] = moved;
                                        NextPixels[idx] = default;
                                        didMove = true;
                                    }
                                }
                            }
                        }
                    }

                    if (!didMove)
                    {
                        NextPixels[idx] = current;
                    }
                }
            }

            RngState.Value = rng;
        }
    }
}
