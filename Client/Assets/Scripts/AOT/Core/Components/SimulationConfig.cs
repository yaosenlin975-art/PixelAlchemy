// 职责：像素模拟全局参数，服务器下发种子确保确定性。
// Responsibility: Global pixel simulation parameters; server-distributed seed ensures determinism.
using Unity.Entities;

namespace AOT
{
    public struct SimulationConfig : IComponentData
    {
        public int SimulationMode;
        public int ProcessingBudget;
        public short AmbientTemperature;    // 定点数 ×100
        public int RandomSeed;
    }
}
