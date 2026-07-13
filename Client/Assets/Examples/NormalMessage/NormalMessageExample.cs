// 演示 XXX 用法：普通消息发送
// Demo XXX usage: send normal messages
using UnityEngine;
using Fantasy;
using Fantasy.Network;

namespace NoitaCA.Examples
{
    /// <summary>
    /// 演示如何通过 <see cref="Session"/> 发送普通（fire-and-forget）消息。
    /// 依赖 <see cref="NetworkProtocolHelper"/> 提供的 C2G_TestMessage 扩展方法。
    /// </summary>
    // 演示用法：通过 session 发送普通消息（无响应）
    // Demo: send a normal (fire-and-forget) message via session
    public sealed class NormalMessageExample : MonoBehaviour
    {
        // 启动时发送一条普通消息
        // Send a normal message on startup
        private void Start()
        {
            var session = Runtime.Session;
            session.C2G_TestMessage("hello-from-normal-example");
            Debug.Log("[NormalMessageExample] C2G_TestMessage sent");
        }
    }
}
