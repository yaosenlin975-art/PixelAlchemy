using Unity.Burst;

namespace AOT
{
    /// <summary>
    /// Per-frame simulation statistics. Updated by simulation systems.
    /// </summary>
    public struct PixelSimulationStats
    {
        public int TotalPixels;
        public int ActivePixels;
        public int DirtyChunks;
        public float SimTimeMs;
        public int ProcessedPixels;

        public void Reset()
        {
            TotalPixels = 0;
            ActivePixels = 0;
            DirtyChunks = 0;
            SimTimeMs = 0f;
            ProcessedPixels = 0;
        }
    }
}
