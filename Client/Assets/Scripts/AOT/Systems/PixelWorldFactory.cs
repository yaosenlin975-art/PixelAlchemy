using Unity.Collections;
using Unity.Entities;

namespace AOT
{
    /// <summary>
    /// Deterministic world generator. Creates ECS entities for pixel grid chunks
    /// and populates them with terrain, caves, and minerals using a seeded RNG.
    /// Server-provided seed ensures cross-platform determinism.
    /// </summary>
    public static class PixelWorldFactory
    {
        /// <summary>
        /// Build the pixel world ECS entities.
        /// </summary>
        /// <param name="em">EntityManager (must be called on main thread)</param>
        /// <param name="width">Grid width</param>
        /// <param name="height">Grid height</param>
        /// <param name="seed">Deterministic seed</param>
        /// <param name="heightMap">Terrain height map (width entries)</param>
        public static void BuildWorld(EntityManager em, int width, int height, int seed, NativeArray<int> heightMap)
        {
            const int chunkSize = 16;

            // Create GridSize singleton
            var gridSize = new GridSize { Width = width, Height = height };
            em.CreateSingleton(gridSize);

            // Create SimulationConfig singleton
            var config = new SimulationConfig
            {
                SimulationMode = 2,
                ProcessingBudget = width * height,
                AmbientTemperature = 2000, // 20°C
                RandomSeed = seed
            };
            em.CreateSingleton(config);

            // Create chunk entities
            int chunksX = (width + chunkSize - 1) / chunkSize;
            int chunksY = (height + chunkSize - 1) / chunkSize;

            var rng = new Xorshift128Plus((ulong)seed);

            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    Entity chunkEntity = em.CreateEntity(
                        typeof(PixelData),
                        typeof(ChunkPosition),
                        typeof(ChunkDirtyFlag)
                    );

                    em.SetSharedComponentData(chunkEntity, new ChunkPosition
                    {
                        ChunkX = cx,
                        ChunkY = cy
                    });

                    em.SetComponentData(chunkEntity, new ChunkDirtyFlag
                    {
                        IsDirty = 1,
                        SleepFrames = 0
                    });

                    // Fill chunk with initial terrain
                    int startX = cx * chunkSize;
                    int startY = cy * chunkSize;
                    int endX = System.Math.Min(startX + chunkSize, width);
                    int endY = System.Math.Min(startY + chunkSize, height);

                    for (int y = startY; y < endY; y++)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            byte materialType = DetermineTerrain(x, y, width, height, heightMap, ref rng);
                            ushort color = GetDefaultColor(materialType);

                            var pixel = new PixelData
                            {
                                MaterialType = materialType,
                                Temperature = 2000, // 20°C default
                                Lifetime = 0,
                                Density = GetDensity(materialType),
                                FallingFrames = 0,
                                Color = color,
                                Flags = 1, // alive
                                VelocityX = 0,
                                VelocityY = 0
                            };

                            // Add pixel buffer for this chunk
                            DynamicBuffer<PixelData> buffer = em.AddBuffer<PixelData>(chunkEntity);
                            buffer.Add(pixel);
                        }
                    }
                }
            }

            heightMap.Dispose();
        }

        private static byte DetermineTerrain(int x, int y, int width, int height,
            NativeArray<int> heightMap, ref Xorshift128Plus rng)
        {
            int groundLevel = heightMap.Length > 0 ? heightMap[x % heightMap.Length] : height / 2;

            if (y > groundLevel + 3)
            {
                // Dirt/stone
                if (rng.NextInt(100) < 15)
                    return 5; // Stone
                return 1; // Sand/earth
            }
            else if (y > groundLevel - 2)
            {
                // Surface transition - mixed
                if (rng.NextInt(100) < 40)
                    return 1; // Sand
                return 0; // Air
            }
            else
            {
                // Above ground - air
                return 0; // Air
            }
        }

        private static byte GetDensity(byte materialType)
        {
            return materialType switch
            {
                0 => 0,   // Air
                1 => 200, // Sand/powder
                2 => 80,  // Water
                3 => 10,  // Smoke/gas
                4 => 50,  // Fire
                5 => 255, // Stone/solid
                6 => 220, // Wood
                7 => 150, // Ash
                8 => 80,  // Poison
                9 => 200, // Ice
                10 => 150, // Lava
                11 => 120, // Debris
                _ => 0
            };
        }

        private static ushort GetDefaultColor(byte materialType)
        {
            // RGB565 packed colors
            return materialType switch
            {
                0 => 0,           // Air (transparent)
                1 => 0xDD44,      // Sand (yellow-tan)
                2 => 0x04DF,      // Water (blue)
                3 => 0xCE79,      // Smoke (gray-white)
                4 => 0xF800,      // Fire (red)
                5 => 0x8410,      // Stone (gray)
                6 => 0x8440,      // Wood (brown)
                7 => 0xAD55,      // Ash (light gray)
                8 => 0x04D8,      // Poison (green)
                9 => 0xFFDF,      // Ice (white)
                10 => 0xFC00,     // Lava (orange-red)
                11 => 0xA514,     // Debris (dark gray)
                _ => 0
            };
        }
    }
}
