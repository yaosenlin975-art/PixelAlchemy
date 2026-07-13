// ================================================================================
// Fantasy.Net 实体与组件定义（Entity 层 / AOT）
// ================================================================================
// 本文件位于 Entity 层，对应 AOT 不可热更数据。
// Entity 与 Component 类型的字段全部为 blittable 值类型，不持有托管堆引用，
// 与 Session/Scene 等运行时对象的交互由 Hotfix 层承担。
//
// 当前仅放置骨架结构（1 Entity + 1 Component），TODO 业务字段后续按需追加。
// 基类 Fantasy.Entitas.Entity 已通过 Fantasy-Net 2026.0.1023 DLL 元数据扫描确认
// （Fantasy ECS 文档：Component 就是 Entity，所有功能模块都继承自 Entity）。
// ================================================================================

using Fantasy;

namespace Entity
{
    /// <summary>
    /// 职责：示例 Entity 子类骨架。
    /// </summary>
    /// <remarks>
    /// Responsibility: Skeleton sample Entity subclass (AOT, not hot-updatable).
    /// </remarks>
    public sealed class SampleEntity : Fantasy.Entitas.Entity
    {
        // TODO: 添加 SampleEntity 业务字段
        // TODO: Add SampleEntity business fields.
    }

    /// <summary>
    /// 职责：示例 Component 子类骨架，承载数值型业务数据。
    /// </summary>
    /// <remarks>
    /// Responsibility: Skeleton sample Component subclass (AOT, not hot-updatable).
    /// </remarks>
    public sealed class SampleComponent : Fantasy.Entitas.Entity
    {
        // 玩家标识 / Player identifier
        public long PlayerId;
        // 当前 MMR / Current MMR rating
        public int Mmr;
    }
}
