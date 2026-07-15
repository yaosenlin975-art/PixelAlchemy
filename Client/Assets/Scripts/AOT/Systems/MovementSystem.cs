using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace AOT
{
    /// <summary>
    /// Movement system: schedules MovementJob per chunk.
    /// Uses double-buffered NativeArray for parallel-safe pixel writes.
    /// Iteration direction flips every frame (even=forward, odd=reverse).
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    [UpdateAfter(typeof(PixelSimulationSystem))]
    [UpdateBefore(typeof(InteractionSystem))]
    public partial struct MovementSystem : ISystem
    {
        private EntityQuery _pixelChunkQuery;
        private Xorshift128Plus _rng;
        private int _frameCounter;
        private NativeArray<PixelData> _currentPixels;
        private NativeArray<PixelData> _nextPixels;
        private NativeReference<Xorshift128Plus> _rngStateRef;

        public void OnCreate(ref SystemState state)
        {
            _pixelChunkQuery = state.GetEntityQuery(
                ComponentType.ReadWrite<PixelData>(),
                ComponentType.ReadOnly<ChunkPosition>()
            );

            // Stream tag: 0x4D6F7665UL = "Move" ASCII
            ulong seed = (ulong)SystemAPI.Get<SimulationConfig>().RandomSeed;
            _rng = new Xorshift128Plus(seed ^ 0x4D6F7665UL);
            _frameCounter = 0;

            _rngStateRef = new NativeReference<Xorshift128Plus>(Allocator.Persistent);
            _rngStateRef.Value = _rng;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_currentPixels.IsCreated) _currentPixels.Dispose();
            if (_nextPixels.IsCreated) _nextPixels.Dispose();
            if (_rngStateRef.IsCreated) _rngStateRef.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<GridSize>(out var gridSize))
                return;

            // Allocate double buffers on first update
            if (!_currentPixels.IsCreated)
            {
                int totalPixels = gridSize.Width * gridSize.Height;
                _currentPixels = new NativeArray<PixelData>(totalPixels, Allocator.Persistent);
                _nextPixels = new NativeArray<PixelData>(totalPixels, Allocator.Persistent);
            }

            var job = new MovementJob
            {
                CurrentPixels = _currentPixels,
                NextPixels = _nextPixels,
                GridSize = gridSize,
                RngState = _rngStateRef,
                FrameIndex = _frameCounter
            };

            state.Dependency = job.ScheduleParallel(_pixelChunkQuery, state.Dependency);

            _frameCounter++;

            // Swap buffers for next frame
            var temp = _currentPixels;
            _currentPixels = _nextPixels;
            _nextPixels = temp;

            // Read back RNG state
            _rng = _rngStateRef.Value;
        }
    }
}
