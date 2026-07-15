// 职责：标识 Chunk 是否需要重新模拟（脏标记），含休眠计数。
// Responsibility: Flags a chunk for re-simulation (dirty mark) with a sleep counter.
using Unity.Entities;

namespace AOT
{
    public struct ChunkDirtyFlag : IComponentData
    {
        public byte IsDirty;
        public byte SleepFrames;
    }
}
