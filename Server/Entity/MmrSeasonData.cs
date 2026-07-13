// ================================================================================
// MMR 赛季数据实体（Entity 层 / AOT）
// ================================================================================
// 存储 MMR 赛季配置，包括赛季时间区间与基础 MMR 值。
// 继承 Fantasy.Entitas.Entity 获得 MongoDB 持久化能力，实现 ISupportedSerialize
// 标记接口以支持框架序列化体系。
// ================================================================================

using Fantasy;
using Fantasy.Entitas.Interface;

namespace Entity
{
    /// <summary>
    /// 职责：MMR 赛季数据实体，存储赛季时间区间与基础 MMR。
    /// </summary>
    /// <remarks>
    /// Responsibility: MMR season data entity, stores season time range and base MMR.
    /// </remarks>
    public sealed class MmrSeasonData : Fantasy.Entitas.Entity, ISupportedSerialize
    {
        // 赛季唯一标识 / Season unique identifier
        public long SeasonId { get; set; }
        // 赛季开始时间（Unix 毫秒）/ Season start time (Unix milliseconds)
        public long StartAt { get; set; }
        // 赛季结束时间（Unix 毫秒）/ Season end time (Unix milliseconds)
        public long EndAt { get; set; }
        // 基础 MMR 值 / Base MMR value
        public int BaseMmr { get; set; }
    }
}
