// ================================================================================
// 账号数据实体（Entity 层 / AOT）
// ================================================================================
// 存储账号基础信息，用于登录鉴权与设备绑定。
// 继承 Fantasy.Entitas.Entity 获得 MongoDB 持久化能力，实现 ISupportedSerialize
// 标记接口以支持框架序列化体系。
// ================================================================================

using Fantasy;
using Fantasy.Entitas.Interface;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

namespace Entity
{
    /// <summary>
    /// 职责：账号数据实体，存储账号基础信息。
    /// </summary>
    /// <remarks>
    /// Responsibility: Account data entity, stores basic account information.
    /// </remarks>
    public sealed class AccountData : Fantasy.Entitas.Entity, ISupportedSerialize
    {
        // 账号唯一标识 / Account unique identifier
        public long AccountId { get; set; }
        // 登录平台标识 / Login platform identifier
        public string Platform { get; set; }
        // 设备唯一标识 / Device unique identifier
        public string DeviceId { get; set; }
        // 账号创建时间（Unix 毫秒）/ Account creation time (Unix milliseconds)
        public long CreateTime { get; set; }
    }
}
