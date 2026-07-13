// ================================================================================
// 房间配置数据实体（Entity 层 / AOT）
// ================================================================================
// 存储房间元数据，供匹配房间管理与状态查询使用。
// 继承 Fantasy.Entitas.Entity 获得 MongoDB 持久化能力，实现 ISupportedSerialize
// 标记接口以支持框架序列化体系。
// ================================================================================

using Fantasy;
using Fantasy.Entitas.Interface;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Entity
{
    /// <summary>
    /// 职责：房间配置数据实体，存储房间元数据与状态。
    /// </summary>
    /// <remarks>
    /// Responsibility: Room config data entity, stores room metadata and status.
    /// </remarks>
    public sealed class RoomConfigData : Fantasy.Entitas.Entity, ISupportedSerialize
    {
        // 房间唯一标识 / Room unique identifier
        public long RoomId { get; set; }
        // 房主玩家标识 / Host player identifier
        public long HostPlayerId { get; set; }
        // 当前玩家数 / Current player count
        public int PlayerCount { get; set; }
        // 竞技场标识 / Arena identifier
        public string Arena { get; set; }
        // 房间状态 / Room status
        public string Status { get; set; }
        // 创建时间（Unix 毫秒）/ Creation time (Unix milliseconds)
        public long CreatedAt { get; set; }
    }
}
