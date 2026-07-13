// ================================================================================
// 玩家数据实体（Entity 层 / AOT）
// ================================================================================
// 存玩家档案信息，关联账号与对战胜负记录。
// 继承 Fantasy.Entitas.Entity 获得 MongoDB 持久化能力，实现 ISupportedSerialize
// 标记接口以支持框架序列化体系。
// ================================================================================

using Fantasy;
using Fantasy.Entitas.Interface;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Entity
{
    /// <summary>
    /// 职责：玩家数据实体，存储玩家档案与 MMR 竞技数据。
    /// </summary>
    /// <remarks>
    /// Responsibility: Player data entity, stores player profile and MMR competitive data.
    /// </remarks>
    public sealed class PlayerData : Fantasy.Entitas.Entity, ISupportedSerialize
    {
        // 玩家唯一标识 / Player unique identifier
        public long PlayerId { get; set; }
        // 关联账号标识 / Associated account identifier
        public long AccountId { get; set; }
        // 玩家显示名称 / Player display name
        public string Name { get; set; }
        // 当前 MMR 评分 / Current MMR rating
        public int Mmr { get; set; }
        // 胜场数 / Win count
        public int Wins { get; set; }
        // 败场数 / Loss count
        public int Losses { get; set; }
        // 当前赛季编号 / Current season number
        public int Season { get; set; }
    }
}
