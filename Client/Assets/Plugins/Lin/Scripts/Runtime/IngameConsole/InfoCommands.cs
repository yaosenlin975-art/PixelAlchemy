using Cysharp.Threading.Tasks;
using IngameDebugConsole;
using Lin.Runtime.UI;
using UnityEngine;

namespace Lin.Runtime.IngameDebugConsole.Commands
{
    static class InfoCommands
    {
        [ConsoleMethod("info.r.enable", "启动渲染信息显示"), UnityEngine.Scripting.Preserve]
        public static void DeviceInfoEnable()
        {
            Debug.Log("info.r.enable");
            PanelManager.GetInstance().Show<AnalysisPanel>();
        }

        [ConsoleMethod("info.r.disable", "关闭渲染信息显示"), UnityEngine.Scripting.Preserve]
        public static void DeviceInfoDisable()
        {
            Debug.Log("info.r.enable");
            PanelManager.GetInstance().HideAsync<AnalysisPanel>().Forget();
        }
    }
}