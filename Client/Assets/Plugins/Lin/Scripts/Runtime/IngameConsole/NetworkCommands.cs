using Cysharp.Threading.Tasks;
using IngameDebugConsole;
using Lin.Runtime.Helper;
using Lin.Runtime.Const;
using System.IO;
using System.Net;
using UnityEngine;

namespace Lin.Runtime.IngameDebugConsole.Commands
{
    class NetworkCommands
    {
        [ConsoleMethod("ping", "ping ip"), UnityEngine.Scripting.Preserve]
        public static async UniTask Ping(string ip)
        {
            Debug.Log($"ping {ip}");
            if (!IPAddress.TryParse(ip, out var address))
            {
                IPAddress[] addresses = Dns.GetHostAddresses(ip);
                if (addresses.Length ==0)
                {
                    Debug.LogError($"{ip} is illegal.");
                    return;
                }
                ip = addresses[0].ToString();
            }

            int successCount = 0;
            for (int i = 0; i < 4; i++)
            {
                try
                {
                    Ping ping = new Ping(ip);
                    await UniTask.WaitUntil(() => ping.isDone);
                    Debug.Log($"第{i + 1}个数据包 耗时{ping.time}ms");
                    ping.DestroyPing();
                    successCount++;
                }
                catch (System.Exception e)
                {
                    Debug.Log($"第{i + 1}个数据包 超时");
                }
            }
            Debug.Log($"已发送4个数据包, 成功{successCount}个, 失败{4 - successCount}个");
        }

        static string CdnConfigPath => $"{Application.persistentDataPath}/CdnConfig.txt";

        [ConsoleMethod("cdn", "自定义CDN服务器地址并启用"), UnityEngine.Scripting.Preserve]
        public static void CDNConfig(string ip)
        {
            Debug.Log($"cdn {ip}");
            string path = CdnConfigPath;
            if (!File.Exists(CdnConfigPath))
                using (File.Create(CdnConfigPath)) { }

            if (!IPAddress.TryParse(ip, out var _))
            {
                IPAddress[] addresses = Dns.GetHostAddresses(ip);
                if (addresses.Length == 0)
                {
                    Debug.LogError($"{ip} is illegal.");
                    return;
                }
                ip = addresses[0].ToString();
            }

            PrefsHelper.Set(AppConst.CDN_CONFIG_ENABLE_KEY, true);
            string detail = $"{ip}\n{ip}";
            File.WriteAllText(path, detail);
            Debug.Log($"已将CDN地址设置为{ip}, 在下一次启动时生效");
        }

        [ConsoleMethod("cdn.enable", "启用CDN自定义设置"), UnityEngine.Scripting.Preserve]
        public static void CDNConfigEnable()
        {
            Debug.Log("cdn.enable");
            if (!File.Exists(CdnConfigPath))
            {
                Debug.Log("未自定义CDN服务器地址");
                return;
            }

            PrefsHelper.Set(AppConst.CDN_CONFIG_ENABLE_KEY, true);
            Debug.Log("已启用CDN自定义设置, 在下一次启动时生效");
        }

        [ConsoleMethod("cdn.disable", "关闭CDN自定义设置"), UnityEngine.Scripting.Preserve]
        public static void CDNConfigDisable()
        {
            Debug.Log("cdn.disable");
            if (!File.Exists(CdnConfigPath))
            {
                Debug.Log("未自定义CDN服务器地址");
                return;
            }

            PrefsHelper.Set(AppConst.CDN_CONFIG_ENABLE_KEY, false);
            Debug.Log("已关闭CDN自定义设置, 在下一次启动时生效");
        }
    }
}