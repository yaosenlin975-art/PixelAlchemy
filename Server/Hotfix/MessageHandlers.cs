// ================================================================================
// Fantasy.Net 消息处理器集合（Hotfix 层）
// ================================================================================
// 本文件集中放置所有客户端→服务端的消息处理器实现。
// Fantasy-Net 2026.0.1023 起，处理器基类为：
//   - Message<T>：处理单向消息（IMessage，无响应）
//   - MessageRPC<TRequest, TResponse>：处理 RPC 请求（IRequest，需返回 IResponse）
// 通过 Source Generator 按 OpCode 自动注册到框架，无需手动注册。
//
// 当前阶段仅提供示例 echo 处理器用于联调测试，未引入任何业务逻辑。
// ================================================================================

using System;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace Hotfix
{
    /// <summary>
    /// 职责：处理 C2G_TestRequest 测试请求，回传 G2C_TestResponse。
    /// </summary>
    /// <remarks>
    /// Responsibility: Handle C2G_TestRequest test RPC and echo back via G2C_TestResponse.
    /// </remarks>
    public sealed class C2G_TestRequestHandler : MessageRPC<C2G_TestRequest, G2C_TestResponse>
    {
        /// <summary>
        /// 职责：echo 客户端 Tag，作为最小可运行的 RPC 验证通路。
        /// </summary>
        /// <remarks>
        /// Responsibility: Echo the client Tag back to verify end-to-end RPC wiring.
        /// </remarks>
        /// <param name="session">客户端会话 / Client session.</param>
        /// <param name="request">测试请求 / Test request.</param>
        /// <param name="response">框架预创建的响应对象，填充后由框架自动发送 / Pre-allocated response; framework auto-sends after Run returns.</param>
        /// <param name="reply">手动触发响应发送的回调（通常无需调用）/ Callback to manually trigger response send (usually unnecessary).</param>
        protected override async FTask Run(Session session, C2G_TestRequest request,
                                           G2C_TestResponse response, Action reply)
        {
            response.Tag = request.Tag;
            await FTask.CompletedTask;
        }
    }
}
