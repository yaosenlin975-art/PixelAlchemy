// 职责：渲染桥接系统，模拟后读取 PixelData chunk 写入 NativeArray<Color32>，供主线程 PixelWorldRenderer 使用。
// Responsibility: Post-simulation rendering bridge system that reads PixelData chunks into NativeArray<Color32> for main-thread PixelWorldRenderer.
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace AOT
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct RenderBridgeSystem : ISystem
    {
        private EntityQuery _pixelChunkQuery;
        private NativeArray<Color32> _colorBuffer;
        private int _lastWidth;
        private int _lastHeight;

        public void OnCreate(ref SystemState state)
        {
            _pixelChunkQuery = state.GetEntityQuery(typeof(PixelData));
            state.RequireForUpdate<GridSize>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<GridSize>())
                return;

            GridSize gridSize = SystemAPI.GetSingleton<GridSize>();
            int totalPixels = gridSize.Width * gridSize.Height;

            if (!_colorBuffer.IsCreated || _colorBuffer.Length != totalPixels
                || _lastWidth != gridSize.Width || _lastHeight != gridSize.Height)
            {
                if (_colorBuffer.IsCreated)
                    _colorBuffer.Dispose();

                _colorBuffer = new NativeArray<Color32>(totalPixels, Allocator.Persistent);
                _lastWidth = gridSize.Width;
                _lastHeight = gridSize.Height;
            }

            NativeArray<PixelData> pixelData = new NativeArray<PixelData>(totalPixels, Allocator.TempJob);

            int count = _pixelChunkQuery.CalculateEntityCount();
            if (count == 0)
            {
                pixelData.Dispose();
                return;
            }

            NativeArray<PixelData> chunkData = _pixelChunkQuery.ToComponentDataArrayAsync<PixelData>(
                Allocator.TempJob, state.Dependency, out JobHandle gatherJob);

            gatherJob.Complete();

            for (int i = 0; i < chunkData.Length && i < totalPixels; i++)
            {
                pixelData[i] = chunkData[i];
            }

            chunkData.Dispose();

            new ColorConversionJob
            {
                PixelData = pixelData,
                ColorBuffer = _colorBuffer
            }.Run(totalPixels);

            pixelData.Dispose();
        }

        public NativeArray<Color32> GetColorBuffer()
        {
            return _colorBuffer;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_colorBuffer.IsCreated)
                _colorBuffer.Dispose();
        }
    }

    [BurstCompile]
    public struct ColorConversionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PixelData> PixelData;
        public NativeArray<Color32> ColorBuffer;

        public void Execute(int index)
        {
            PixelData pixel = PixelData[index];
            ushort rgb565 = pixel.Color;

            byte r = (byte)((rgb565 >> 11) & 0x1F);
            byte g = (byte)((rgb565 >> 5) & 0x3F);
            byte b = (byte)(rgb565 & 0x1F);

            r = (byte)((r << 3) | (r >> 2));
            g = (byte)((g << 2) | (g >> 4));
            b = (byte)((b << 3) | (b >> 2));

            ColorBuffer[index] = new Color32(r, g, b, 255);
        }
    }
}
