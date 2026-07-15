using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace AOT
{
    /// <summary>
    /// Interaction system: schedules InteractionJob per chunk.
    /// Handles temperature conduction, chemical reactions, and phase changes.
    /// Runs after MovementSystem per [UpdateAfter] constraint.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct InteractionSystem : ISystem
    {
        private EntityQuery _pixelChunkQuery;
        private Xorshift128Plus _rng;
        private NativeArray<PixelData> _currentPixels;
        private NativeArray<PixelData> _nextPixels;
        private NativeReference<Xorshift128Plus> _rngStateRef;

        public void OnCreate(ref SystemState state)
        {
            _pixelChunkQuery = state.GetEntityQuery(
                ComponentType.ReadWrite<PixelData>(),
                ComponentType.ReadOnly<ChunkPosition>()
            );

            // Stream tag: 0x496E7465UL = "Inte" ASCII
            ulong seed = (ulong)SystemAPI.Get<SimulationConfig>().RandomSeed;
            _rng = new Xorshift128Plus(seed ^ 0x496E7465UL);

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

            if (!SystemAPI.TryGetSingleton<SimulationConfig>(out var config))
                return;

            // Allocate double buffers on first update (shared with MovementSystem)
            if (!_currentPixels.IsCreated)
            {
                int totalPixels = gridSize.Width * gridSize.Height;
                _currentPixels = new NativeArray<PixelData>(totalPixels, Allocator.Persistent);
                _nextPixels = new NativeArray<PixelData>(totalPixels, Allocator.Persistent);
            }

            var job = new InteractionJob
            {
                CurrentPixels = _currentPixels,
                NextPixels = _nextPixels,
                AmbientTemp = config.AmbientTemperature,
                RngState = _rngStateRef
            };

            state.Dependency = job.ScheduleParallel(_pixelChunkQuery, state.Dependency);

            // Swap buffers
            var temp = _currentPixels;
            _currentPixels = _nextPixels;
            _nextPixels = temp;

            // Read back RNG state
            _rng = _rngStateRef.Value;
        }
    }
}
