// 演示 XXX 用法：事件系统订阅
// Demo XXX usage: subscribe via the event system
using UnityEngine;
using Fantasy.Event;

namespace NoitaCA.Examples
{
    /// <summary>
    /// 演示如何继承 <see cref="EventSystem{T}"/> 注册一个事件处理器。
    /// 实际触发时，调用 <c>Scene.EventComponent.PublishAsync(evt)</c> 即可分发到本处理器。
    /// </summary>
    // 演示用法：继承 EventSystem<T> 注册一个事件处理
    // Demo: inherit EventSystem<T> to register an event handler
    public sealed class EventSystemExample : MonoBehaviour
    {
        // 示例事件数据类型（实际项目中通常是 struct / AMessage 派生类型）
        // Sample event data type (in production this is usually a struct or AMessage)
        public sealed class PlayerLevelUpEvent
        {
            // 玩家 ID / Player ID
            public long PlayerId;
            // 升级后的等级 / Level after upgrade
            public int NewLevel;
        }

        // 事件处理器：继承 EventSystem<T> 即可注册（依赖 Fantasy 源码生成器扫描）
        // Event handler: inherit EventSystem<T> to register (relies on Fantasy source generator)
        public sealed class PlayerLevelUpHandler : EventSystem<PlayerLevelUpEvent>
        {
            // 处理事件：实际项目中将升级奖励/UI 更新等逻辑放在这里
            // Handle event: place level-up rewards / UI updates in production
            protected override void Handler(PlayerLevelUpEvent self)
            {
                Debug.Log($"[EventSystemExample] PlayerLevelUp: {self.PlayerId} -> Lv.{self.NewLevel}");
            }
        }

        // 启动时仅打印提示，不实际触发事件（注册由 SourceGenerator 自动完成）
        // On startup only print a hint; events are not actually triggered
        // (registration is done automatically by the SourceGenerator)
        private void Start()
        {
            Debug.Log("[EventSystemExample] EventSystem demo ready. " +
                      "Publish via Scene.EventComponent.PublishAsync(new PlayerLevelUpEvent{...})");
        }
    }
}
