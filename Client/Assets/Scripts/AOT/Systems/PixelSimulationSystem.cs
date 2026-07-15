using Unity.Burst;
using Unity.Entities;

namespace AOT
{
    /// <summary>
    /// Pixel simulation coordinator system.
    /// Manages frame counting, period harmonization (every 6 frames),
    /// and delegates to MovementSystem → InteractionSystem via [UpdateBefore]/[UpdateAfter].
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    [UpdateBefore(typeof(MovementSystem))]
    public partial struct PixelSimulationSystem : ISystem
    {
        private int _frameCounter;

        public void OnCreate(ref SystemState state)
        {
            _frameCounter = 0;
            state.RequireForUpdate<GridSize>();
            state.RequireForUpdate<SimulationConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _frameCounter++;

            // Period harmonization: every 6 frames (for lockstep reconciliation)
            if (_frameCounter % 6 == 0)
            {
                // Reserved for NoitaCA.Lockstep trigger (future netcode phase)
            }
        }
    }
}
