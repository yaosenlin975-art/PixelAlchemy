// ================================================================================
// 对战历史数据实体（Entity 层 / AOT）
// ================================================================================
// 存储单场对战结算信息，Players 为子文档数组（MatchPlayerRef）。
// 继承 Fantasy.Entitas.Entity 获得 MongoDB 持久化能力，实现 ISupportedSerialize
// 标记接口以支持框架序列化体系。
// MatchPlayerRef 为纯数据子对象，不继承 Entity。
// ================================================================================

using Fantasy;
using Fantasy.Entitas.Interface;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Entity
{
    /// <summary>
    /// 职责：对战玩家引用子对象，记录单场对战中玩家的排名与 MMR 变化。
    /// </summary>
    /// <remarks>
    /// Responsibility: Match player reference sub-object, records player rank and MMR delta in a single match.
    /// </remarks>
    public sealed class MatchPlayerRef
    {
        // 玩家唯一标识 / Player unique identifier
        public long PlayerId { get; set; }
        // 本场排名 / Rank in this match
        public int Rank { get; set; }
        // MMR 变化值 / MMR delta
        public int MmrDelta { get; set; }
    }

    /// <summary>
    /// 职责：对战历史数据实体，存储单场对战结算信息。
    /// </summary>
    /// <remarks>
    /// Responsibility: Match history data entity, stores settlement info for a single match.
    /// </remarks>
    public sealed class MatchHistoryData : Fantasy.Entitas.Entity, ISupportedSerialize
    {
        // 对战唯一标识 / Match unique identifier
        public long MatchId { get; set; }
        // 竞技场标识 / Arena identifier
        public string Arena { get; set; }
        // 参战玩家列表 / Participating players list
        public List<MatchPlayerRef> Players { get; set; }
        // 胜者玩家标识 / Winner player identifier
        public long Winner { get; set; }
        // 结算时间（Unix 毫秒）/ Settlement time (Unix milliseconds)
        public long SettleAt { get; set; }
        // 总帧数 / Total frames
        public int TotalFrames { get; set; }
    }
}
