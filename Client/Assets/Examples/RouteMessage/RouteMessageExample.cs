// 演示 XXX 用法：路由消息
// Demo XXX usage: route messages
using UnityEngine;
using Fantasy;
using Fantasy.Network;

namespace NoitaCA.Examples
{
    /// <summary>
    /// 演示路由（Route）消息用法。
    /// 注意：本示例仅展示 API 调用形态；Noita 当前生成的协议中尚未定义路由消息，
    /// 实际接入时需在 Tools/NetworkProtocol 中追加并重新生成。
    /// </summary>
    // 演示用法：通过 session.Call 发送路由消息并等待响应
    // Demo: send a routed message via session.Call and await the response
    public sealed class RouteMessageExample : MonoBehaviour
    {
        // 启动时演示一次路由调用（仅示意，不要求服务器真实实现）
        // On startup, demonstrate a routed call (illustrative only)
        private async void Start()
        {
            var session = Runtime.Session;
            // 实际项目中替换为真实路由消息类型
            // Replace with the real routed message type in production
            await session.Call(new Fantasy.C2G_TestRequest { Tag = "route-demo" });
            Debug.Log("[RouteMessageExample] Route call returned");
        }
    }
}
