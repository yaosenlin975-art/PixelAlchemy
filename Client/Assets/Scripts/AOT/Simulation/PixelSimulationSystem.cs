// 职责：像素模拟协调入口 ISystem，替代原 PixelSimulation.cs，负责调度 Movement→Interaction 顺序与周期调和。
// Responsibility: Pixel simulation coordination ISystem, replaces PixelSimulation.cs. Schedules Movement→Interaction order and periodic harmonization.
using Unity.Burst;
using Unity.Entities;

namespace AOT
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    [UpdateAfter(typeof(MovementSystem))]
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

            if (_frameCounter % 6 == 0)
            {
                // 周期调和：由 NoitaCA.Lockstep 触发，本文档不实现具体逻辑
            }
        }
    }
}
