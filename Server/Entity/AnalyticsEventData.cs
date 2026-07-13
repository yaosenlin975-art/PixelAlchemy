// ================================================================================
// 分析事件数据实体（Entity 层 / AOT）
// ================================================================================
// 存储埋点事件，Payload 为 JSON 文本以便 MongoDB 直接查询。
// 继承 Fantasy.Entitas.Entity 获得 MongoDB 持久化能力，实现 ISupportedSerialize
// 标记接口以支持框架序列化体系。
// ================================================================================

using Fantasy;
using Fantasy.Entitas.Interface;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Entity
{
    /// <summary>
    /// 职责：分析事件数据实体，存储埋点事件与 JSON 载荷。
    /// </summary>
    /// <remarks>
    /// Responsibility: Analytics event data entity, stores telemetry event and JSON payload.
    /// </remarks>
    public sealed class AnalyticsEventData : Fantasy.Entitas.Entity, ISupportedSerialize
    {
        // 事件唯一标识 / Event unique identifier
        public long EventId { get; set; }
        // 关联玩家标识 / Associated player identifier
        public long PlayerId { get; set; }
        // 关联对战标识 / Associated match identifier
        public long MatchId { get; set; }
        // 事件类型 / Event type
        public string EventType { get; set; }
        // JSON 格式载荷文本 / JSON payload text
        public string Payload { get; set; }
        // 事件时间戳（Unix 毫秒）/ Event timestamp (Unix milliseconds)
        public long Timestamp { get; set; }
    }
}
