// 职责：像素移动系统 ISystem，调度 MovementJob 并行执行，控制偶数帧正向/奇数帧反向迭代。
// Responsibility: Pixel movement ISystem, schedules MovementJob in parallel, controls even-forward/odd-reverse iteration.
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace AOT
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
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
            _pixelChunkQuery = state.GetEntityQuery(typeof(PixelData), typeof(ChunkPosition));
            state.RequireForUpdate<GridSize>();
            state.RequireForUpdate<SimulationConfig>();

            _frameCounter = 0;
            NativeReference<Xorshift128Plus> rngRef = new NativeReference<Xorshift128Plus>(
                new Xorshift128Plus(0), Allocator.Persistent);
            _rngStateRef = rngRef;

            // RNG seed from SimulationConfig, stream tag 0x4D6F7665UL = "Move"
            if (SystemAPI.HasSingleton<SimulationConfig>())
            {
                SimulationConfig config = SystemAPI.GetSingleton<SimulationConfig>();
                _rng = new Xorshift128Plus((ulong)config.RandomSeed ^ 0x4D6F7665UL);
            }
            else
            {
                _rng = new Xorshift128Plus(0x4D6F7665UL);
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<GridSize>())
                return;

            GridSize gridSize = SystemAPI.GetSingleton<GridSize>();
            int totalPixels = gridSize.Width * gridSize.Height;

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

            MovementJob job = new MovementJob
            {
                CurrentPixels = _currentPixels,
                NextPixels = _nextPixels,
                GridSize = gridSize,
                RngState = _rngStateRef,
                FrameIndex = _frameCounter
            };

            state.Dependency = job.ScheduleParallel(_pixelChunkQuery, state.Dependency);

            // Swap buffers
            NativeArray<PixelData> temp = _currentPixels;
            _currentPixels = _nextPixels;
            _nextPixels = temp;

            _frameCounter++;

            // Read back RNG state
            _rng = _rngStateRef.Value;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_currentPixels.IsCreated)
                _currentPixels.Dispose();
            if (_nextPixels.IsCreated)
                _nextPixels.Dispose();
            if (_rngStateRef.IsCreated)
                _rngStateRef.Dispose();
        }
    }
}
