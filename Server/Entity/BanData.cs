// ================================================================================
// 封禁数据实体（Entity 层 / AOT）
// ================================================================================
// 存储玩家封禁记录，通过 TTL 索引在 ExpireAt 到期后自动清除。
// 继承 Fantasy.Entitas.Entity 获得 MongoDB 持久化能力，实现 ISupportedSerialize
// 标记接口以支持框架序列化体系。
// ================================================================================

using Fantasy;
using Fantasy.Entitas.Interface;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Entity
{
    /// <summary>
    /// 职责：封禁数据实体，存储玩家封禁原因与到期时间。
    /// </summary>
    /// <remarks>
    /// Responsibility: Ban data entity, stores ban reason and expiration time.
    /// </remarks>
    public sealed class BanData : Fantasy.Entitas.Entity, ISupportedSerialize
    {
        // 被封禁玩家标识 / Banned player identifier
        public long PlayerId { get; set; }
        // 封禁原因 / Ban reason
        public string Reason { get; set; }
        // 封禁到期时间（UTC），TTL 索引依据此字段 / Ban expiration time (UTC), TTL index based on this field
        public DateTime ExpireAt { get; set; }
        // 执行封禁的操作者标识 / Operator identifier who issued the ban
        public long BannedBy { get; set; }
    }
}
