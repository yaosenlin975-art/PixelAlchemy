// 演示 XXX 用法：连接服务器
// Demo XXX usage: connect to the server
using UnityEngine;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;

namespace NoitaCA.Examples
{
    /// <summary>
    /// 演示如何通过 <see cref="Runtime.Connect"/> 异步连接至 Fantasy 服务器（KCP 协议）。
    /// 挂到任意 GameObject 即可触发；不要在生产代码中直接使用本类。
    /// </summary>
    // 演示用法：调用 Runtime.Connect 建立 KCP 连接
    // Demo: call Runtime.Connect to establish a KCP connection
    public sealed class ConnectToServerExample : MonoBehaviour
    {
        // 目标服务器：与 Server/Entity/Fantasy.config 中 Gate scene 一致
        // Target server: matches the Gate scene in Server/Entity/Fantasy.config
        private const string RemoteIP = "127.0.0.1";
        private const int RemotePort = 20000;

        // 启动时建立一次连接
        // Establish one connection on startup
        private async void Start()
        {
            Debug.Log($"[ConnectToServerExample] Connecting to {RemoteIP}:{RemotePort}");
            await Runtime.Connect(
                remoteIP: RemoteIP,
                remotePort: RemotePort,
                protocol: FantasyRuntime.NetworkProtocolType.KCP,
                isHttps: false,
                connectTimeout: 5000,
                enableHeartbeat: false,
                heartbeatInterval: 0,
                heartbeatTimeOut: 0,
                heartbeatTimeOutInterval: 0,
                maxPingSamples: 0,
                onConnectComplete: () => Debug.Log("[ConnectToServerExample] Connected"),
                onConnectFail: () => Debug.LogError("[ConnectToServerExample] Connect failed"),
                onConnectDisconnect: () => Debug.LogWarning("[ConnectToServerExample] Disconnected"));
        }
    }
}
