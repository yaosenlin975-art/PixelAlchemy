// 职责：客户端启动入口
// Responsibility: Client bootstrap entry that initializes Fantasy framework and connects to the server once.
using System;
using UnityEngine;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;

namespace NoitaCA.Hotfix
{
    /// <summary>
    /// 客户端启动入口。
    /// 在场景中挂载本组件后，<see cref="Start"/> 会：
    /// 1. 调用 <see cref="Fantasy.Platform.Unity.Entry.Initialize"/> 初始化 Fantasy 框架。
    /// 2. 通过 <see cref="Runtime.Connect"/> 异步连接至 Server/Entity/Fantasy.config 中 Gate Scene 的外网地址（KCP 协议）。
    /// 3. 连接成功后，发送一次 <see cref="C2G_TestMessage"/> 与一次 <see cref="C2G_DeviceReport"/> 验证协议通路。
    /// 4. 失败时输出错误日志，不做断线重连或完整 Session 业务（最小入口）。
    /// </summary>
    /// <remarks>
    /// 仅做"启动 + 连一次"的最小入口。生产环境的 Session 管理、断线重连、消息分发等业务放在后续 Hotfix 模块。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FantasyEntry : MonoBehaviour
    {
        /// <summary>
        /// 服务器外网 IP（与 Server/Entity/Fantasy.config 中 Gate scene 的 outerIP 保持一致）。
        /// </summary>
        private const string RemoteIP = "127.0.0.1";

        /// <summary>
        /// 服务器外网端口（与 Server/Entity/Fantasy.config 中 Gate scene 的 outerPort 保持一致）。
        /// </summary>
        private const int RemotePort = 20000;

        /// <summary>
        /// 连接超时（毫秒）。
        /// </summary>
        private const int ConnectTimeoutMs = 5000;

        /// <summary>
        /// Unity 启动回调：异步初始化 Fantasy 框架并连接服务器一次。
        /// </summary>
        // 启动客户端：初始化框架并连接服务器一次
        // Bootstrap the client: initialize Fantasy and connect to the server once
        private async void Start()
        {
            try
            {
                Debug.Log("[FantasyEntry] 初始化 Fantasy 框架 / Initializing Fantasy framework");
                await Fantasy.Platform.Unity.Entry.Initialize();

                Debug.Log($"[FantasyEntry] 连接服务器 / Connecting to {RemoteIP}:{RemotePort} (KCP)");
                var session = await Runtime.Connect(
                    remoteIP: RemoteIP,
                    remotePort: RemotePort,
                    protocol: FantasyRuntime.NetworkProtocolType.KCP,
                    isHttps: false,
                    connectTimeout: ConnectTimeoutMs,
                    enableHeartbeat: false,
                    heartbeatInterval: 0,
                    heartbeatTimeOut: 0,
                    heartbeatTimeOutInterval: 0,
                    maxPingSamples: 0,
                    onConnectComplete: OnConnectComplete,
                    onConnectFail: OnConnectFail,
                    onConnectDisconnect: OnConnectDisconnect);

                if (session == null)
                {
                    Debug.LogError("[FantasyEntry] Connect 返回 null，连接未建立 / Connect returned null");
                    return;
                }

                // 通过客户端侧 NetworkProtocolHelper 扩展方法发送测试消息
                // Use the client-side NetworkProtocolHelper extension to send a test message
                session.C2G_TestMessage("hello-from-unity");
                session.C2G_DeviceReport(SystemInfo.deviceUniqueIdentifier, Application.identifier);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FantasyEntry] 启动失败 / Startup failed: {ex}");
            }
        }

        // 连接成功回调
        // Connection established callback
        private void OnConnectComplete()
        {
            Debug.Log("[FantasyEntry] 已连接 / Connected");
        }

        // 连接失败回调
        // Connection failed callback
        private void OnConnectFail()
        {
            Debug.LogError("[FantasyEntry] 连接失败 / Connection failed");
        }

        // 连接断开回调
        // Connection disconnected callback
        private void OnConnectDisconnect()
        {
            Debug.LogWarning("[FantasyEntry] 连接断开 / Connection disconnected");
        }
    }
}
