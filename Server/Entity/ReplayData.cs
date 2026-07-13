// ================================================================================
// 录像数据实体（Entity 层 / AOT）
// ================================================================================
// 存储单场录像的帧数据与快照，通过 TTL 索引自动过期。
// 继承 Fantasy.Entitas.Entity 获得 MongoDB 持久化能力，实现 ISupportedSerialize
// 标记接口以支持框架序列化体系。
// ================================================================================

using Fantasy;
using Fantasy.Entitas.Interface;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Entity
{
    /// <summary>
    /// 职责：录像数据实体，存储帧序列与初始快照。
    /// </summary>
    /// <remarks>
    /// Responsibility: Replay data entity, stores frame sequence and initial snapshot.
    /// </remarks>
    public sealed class ReplayData : Fantasy.Entitas.Entity, ISupportedSerialize
    {
        // 对战唯一标识 / Match unique identifier
        public long MatchId { get; set; }
        // 帧数据整体二进制 / Frame data as raw binary
        public byte[] Frames { get; set; }
        // 初始快照二进制 / Initial snapshot as raw binary
        public byte[] Snapshot { get; set; }
        // 录像总字节数 / Total replay size in bytes
        public long Size { get; set; }
        // 过期时间（UTC），TTL 索引依据此字段 / Expiration time (UTC), TTL index based on this field
        public DateTime ExpireAt { get; set; }
    }
}
