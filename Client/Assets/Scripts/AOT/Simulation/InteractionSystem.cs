// 职责：像素交互系统 ISystem，调度 InteractionJob 处理温度传导、化学反应、相变。
// Responsibility: Pixel interaction ISystem, schedules InteractionJob for thermal conduction, chemical reactions, and phase changes.
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace AOT
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    [UpdateAfter(typeof(MovementSystem))]
    public partial struct InteractionSystem : ISystem
    {
        private EntityQuery _pixelChunkQuery;
        private Xorshift128Plus _rng;
        private NativeArray<MaterialDefinition> _materialDefs;
        private NativeArray<PixelData> _currentPixels;
        private NativeArray<PixelData> _nextPixels;
        private NativeReference<Xorshift128Plus> _rngStateRef;

        public void OnCreate(ref SystemState state)
        {
            _pixelChunkQuery = state.GetEntityQuery(typeof(PixelData), typeof(ChunkPosition));
            state.RequireForUpdate<GridSize>();
            state.RequireForUpdate<SimulationConfig>();

            // Stream tag 0x496E7465UL = "Inte"
            if (SystemAPI.HasSingleton<SimulationConfig>())
            {
                SimulationConfig config = SystemAPI.GetSingleton<SimulationConfig>();
                _rng = new Xorshift128Plus((ulong)config.RandomSeed ^ 0x496E7465UL);
            }
            else
            {
                _rng = new Xorshift128Plus(0x496E7465UL);
            }

            _materialDefs = MaterialDatabase.ToNativeArray(Allocator.Persistent);
            _rngStateRef = new NativeReference<Xorshift128Plus>(_rng, Allocator.Persistent);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<GridSize>())
                return;

            GridSize gridSize = SystemAPI.GetSingleton<GridSize>();
            int totalPixels = gridSize.Width * gridSize.Height;
            SimulationConfig config = SystemAPI.GetSingleton<SimulationConfig>();

            if (!_currentPixels.IsCreated || _currentPixels.Length != totalPixels)
            {
                if (_currentPixels.IsCreated)
                    _currentPixels.Dispose();
                if (_nextPixels.IsCreated)
                    _nextPixels.Dispose();

                _currentPixels = new NativeArray<PixelData>(totalPixels, Allocator.Persistent);
                _nextPixels = new NativeArray<PixelData>(totalPixels, Allocator.Persistent);
            }

            _rngStateRef.Value = _rng;

            InteractionJob job = new InteractionJob
            {
                CurrentPixels = _currentPixels,
                NextPixels = _nextPixels,
                MaterialDefs = _materialDefs,
                AmbientTemp = config.AmbientTemperature,
                RngState = _rngStateRef
            };

            state.Dependency = job.ScheduleParallel(_pixelChunkQuery, state.Dependency);

            NativeArray<PixelData> temp = _currentPixels;
            _currentPixels = _nextPixels;
            _nextPixels = temp;

            _rng = _rngStateRef.Value;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_materialDefs.IsCreated)
                _materialDefs.Dispose();
            if (_currentPixels.IsCreated)
                _currentPixels.Dispose();
            if (_nextPixels.IsCreated)
                _nextPixels.Dispose();
            if (_rngStateRef.IsCreated)
                _rngStateRef.Dispose();
        }
    }
}
