// ================================================================================
// Fantasy.Net 场景事件处理器集合（Hotfix 层）
// ================================================================================
// 本文件集中放置所有 Scene 生命周期事件的处理器实现。
// Fantasy-Net 2026.0.1023 起，Scene 创建事件通过继承 AsyncEventSystem<OnCreateScene>
// 实现，重写 Handler(OnCreateScene self) 方法；self.Scene 获取已创建的 Scene 实例。
// Source Generator 会自动注册所有 AsyncEventSystem<T> 子类，无需手动注册。
// ================================================================================

using Fantasy;
using Fantasy.Async;
using Fantasy.Event;

namespace Hotfix
{
    /// <summary>
    /// 职责：Gate Scene 启动初始化（仅日志，不连数据库、不创建 Entity）。
    /// </summary>
    /// <remarks>
    /// Responsibility: Gate Scene startup hook (log only, no DB or Entity creation).
    /// </remarks>
    public sealed class GateSceneEventHandler : AsyncEventSystem<OnCreateScene>
    {
        /// <summary>
        /// 职责：当 Scene 创建完成时触发；仅处理 Gate 场景，输出启动日志。
        /// </summary>
        /// Responsibility: Triggered when a Scene is created; only handles Gate scene and emits a startup log.
        /// <param name="self">OnCreateScene 事件参数，通过 self.Scene 获取场景实例 / Event args; access the scene via self.Scene.</param>
        protected override async FTask Handler(OnCreateScene self)
        {
            // 2026 版不再按类名绑定 SceneType，需显式过滤目标场景类型。
            // The 2026 release no longer binds SceneType by class name; filter explicitly.
            if (self.Scene.SceneType != SceneType.Gate)
            {
                await FTask.CompletedTask;
                return;
            }

            // 通过 SceneLogExtensions 输出 Scene 维度日志（scene.LogInfo 内部经 Fantasy.Log
            // 路由到注册的 ILog，并以 scene.LogSceneName 设置 sceneName 属性，支持 7 Scene 分离）。
            // Emit a Scene-scoped log via SceneLogExtensions. scene.LogInfo routes through Fantasy.Log
            // to the registered ILog, tagging the event with scene.LogSceneName for 7-Scene separation.
            self.Scene.LogInfo("Gate Scene created. SceneId={SceneId}", self.Scene.Id);
            await FTask.CompletedTask;
        }
    }
}
