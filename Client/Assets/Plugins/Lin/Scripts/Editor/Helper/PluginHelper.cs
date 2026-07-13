/*
┌────────────────────────────┐
│　Description: 菜单拓展
│　Author: 花球i
└────────────────────────────┘
*/
using Cysharp.Threading.Tasks;
using System.Linq;
using UnityEditor.PackageManager;
using YooAsset.Editor;

namespace Lin.Editor.Helper
{
    public static class PluginHelper
    {
        /// <summary>
        /// 检测对应包是否安装
        /// </summary>
        /// <param name="packageName"></param>
        /// <returns></returns>
        public static async UniTask<bool> Check(string packageName)
        {
            var listRequest = Client.List();
            await UniTask.WaitUntil(() => listRequest.IsCompleted, timing: PlayerLoopTiming.FixedUpdate);
            if (listRequest.Status == StatusCode.Success)
            {
                return listRequest.Result.Any(p => p.name == packageName);
            }
            return false;
        }
    }
}