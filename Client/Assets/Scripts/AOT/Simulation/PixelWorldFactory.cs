// 职责：确定性世界工厂，使用 Xorshift128Plus 生成像素世界地形。
// Responsibility: Deterministic world factory using Xorshift128Plus for pixel terrain generation.
using Unity.Collections;
using Unity.Entities;

namespace AOT
{
    public static class PixelWorldFactory
    {
        public const int ChunkSize = 16;

        public static Entity CreateGridSingleton(EntityManager em, int width, int height)
        {
            Entity entity = em.CreateEntity();
            em.AddComponentData(entity, new GridSize { Width = width, Height = height });
            return entity;
        }

        public static Entity CreateSimulationConfig(EntityManager em, SimulationConfig config)
        {
            Entity entity = em.CreateEntity();
            em.AddComponentData(entity, config);
            return entity;
        }

        public static void BuildWorld(EntityManager em, int width, int height, int seed, NativeArray<int> heightMap)
        {
            CreateGridSingleton(em, width, height);

            SimulationConfig config = new SimulationConfig
            {
                SimulationMode = 0,
                ProcessingBudget = width * height,
                AmbientTemperature = 200,
                RandomSeed = seed
            };
            CreateSimulationConfig(em, config);

            int chunksX = (width + ChunkSize - 1) / ChunkSize;
            int chunksY = (height + ChunkSize - 1) / ChunkSize;

            for (int cy = 0; cy < chunksY; cy++)
            {
                for (int cx = 0; cx < chunksX; cx++)
                {
                    Entity chunkEntity = em.CreateEntity();
                    em.AddSharedComponentData(chunkEntity, new ChunkPosition
                    {
                        ChunkX = cx,
                        ChunkY = cy
                    });
                    em.AddComponentData(chunkEntity, new ChunkDirtyFlag
                    {
                        IsDirty = 1,
                        SleepFrames = 0
                    });

                    int startX = cx * ChunkSize;
                    int startY = cy * ChunkSize;
                    int endX = System.Math.Min(startX + ChunkSize, width);
                    int endY = System.Math.Min(startY + ChunkSize, height);

                    for (int y = startY; y < endY; y++)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            Entity pixelEntity = em.CreateEntity();
                            PixelData pixel = GenerateTerrainPixel(x, y, width, height, seed, heightMap);
                            em.AddComponentData(pixelEntity, pixel);
                            em.AddSharedComponentData(pixelEntity, new ChunkPosition
                            {
                                ChunkX = cx,
                                ChunkY = cy
                            });
                        }
                    }
                }
            }
        }

        private static PixelData GenerateTerrainPixel(int x, int y, int width, int height, int seed, NativeArray<int> heightMap)
        {
            PixelData pixel = new PixelData();
            pixel.Flags = 0x04;
            pixel.Temperature = 200;

            int groundY = (heightMap.IsCreated && heightMap.Length > x)
                ? heightMap[x]
                : height / 3;

            if (y < groundY)
            {
                if (y < groundY - 5)
                {
                    pixel.MaterialType = (byte)MaterialType.Stone;
                    pixel.Color = 0x8C92;
                    pixel.Density = 4;
                }
                else
                {
                    pixel.MaterialType = (byte)MaterialType.Sand;
                    pixel.Color = 0xDCB3;
                    pixel.Density = 3;
                }
            }
            else
            {
                pixel.MaterialType = (byte)MaterialType.Air;
                pixel.Color = 0;
                pixel.Density = 0;
            }

            return pixel;
        }
    }
}
