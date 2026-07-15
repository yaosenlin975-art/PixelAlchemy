// 职责：像素模拟统计信息记录，用于性能监控和调试。
// Responsibility: Records pixel simulation statistics for performance monitoring and debugging.
namespace AOT
{
    public struct PixelSimulationStats
    {
        public int ActivePixels;
        public int DirtyChunks;
        public float MovementMs;
        public float InteractionMs;
        public int TotalPixels;

        public void Reset()
        {
            ActivePixels = 0;
            DirtyChunks = 0;
            MovementMs = 0f;
            InteractionMs = 0f;
            TotalPixels = 0;
        }
    }
}
