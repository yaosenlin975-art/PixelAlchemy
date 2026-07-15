// 职责：标记像素 Chunk 的网格坐标，用于并行分块处理。
// Responsibility: Marks a pixel chunk's grid coordinates for parallel chunked processing.
using Unity.Entities;

namespace AOT
{
    public struct ChunkPosition : ISharedComponentData
    {
        public int ChunkX;
        public int ChunkY;
    }
}
