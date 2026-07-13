/*
┌────────────────────────────┐
│　Description: 游戏初始化, 热更入口
│　Remark: 
└────────────────────────────┘
*/
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Lin.Runtime.Event;
using Lin.Runtime.Helper;
using Lin.Runtime.Resource.Updater;
using Lin.Runtime.UI;
using System;
using UnityEngine;
using static Lin.Runtime.Resource.ResLoader;

namespace Lin.Runtime
{
    public class Initializer : MonoBehaviour
    {

        private static readonly string GAME_ASSEMBLY_FOTMAT =
#if HybridCLR
            "{0},Hotfix"
#else
            "{0},Assembly-CSharp"
#endif
            ;

#if UNITY_ANDROID
        private AndroidUpdater androidUpdater;
#elif UNITY_IOS
        private iOSUpdater appleUpdater;
#endif
        private ResourcesUpdater resourcesUpdater;

        private void Awake()
        {
#if !UNITY_EDITOR && UNITY_ANDROID || UNITY_IOS
            Application.targetFrameRate = 60;
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            // 设置较低的纹理质量
            QualitySettings.globalTextureMipmapLimit = 1;

            // 禁用不必要的缓存
            //Caching.compressionEnabled = true;
#endif
        }

        private async void Start()
        {
            await InitializeAsync();
            var config = GlobalConfig_SO.GetInstance();
            // 先载入 Update 包, 再 ShowAsync<UpdatePanel> 才找得到资源
            // Standalone 用空 URI(走离线), 其他模式走主机模式, 用 defaultCDN 构建 CDN URL
            var updatePackageUri = config.isStandaloneMode ? string.Empty : config.defaultCDN;
            await AddOrGetPackageAsync(UPDATE_PANEL_PACKAGE_NAME, updatePackageUri);
            var updatePanel = await PanelManager.GetInstance().ShowAsync<UpdatePanel>();
#if UNITY_EDITOR
            if (config.isEditorMode)
            {
                await LoadAllPackagesFromSetting();
                OnResourceUpdateFinish(false, true);
                return;
            }

#elif UNITY_ANDROID
            //APK
            androidUpdater = new AndroidUpdater(config.defaultCDN, config.fallbackCDN, updatePanel.OnUpdateAndroidStart, updatePanel.RefreshProgress, updatePanel.OnUpdateApkFinished);
            await androidUpdater.CheckVersion();
            updatePanel.SetAppVersion(androidUpdater.localVersion, androidUpdater.remoteVersion);
            switch (androidUpdater.state)
            {
                case EUpdaterState.ERROR:
                    updatePanel.OnUpdateApkFinished(true, false);
                    return;

                case EUpdaterState.SHOULD_UPDATE:
                    updatePanel.ShowAndroidInfosAsync(androidUpdater).Forget();
                    return;

                default:
                    break;
            }
#elif UNITY_IOS
            //iOS
            appleUpdater = new iOSUpdater(config.defaultCDN, config.fallbackCDN);
            await appleUpdater.CheckVersion();
            updatePanel.SetAppVersion(appleUpdater.localVersion, appleUpdater.remoteVersion);
            switch (appleUpdater.state)
            {
                case EUpdaterState.ERROR:
                    updatePanel.OnUpdateApkFinished(true, false);
                    return;

                case EUpdaterState.SHOULD_UPDATE:
                    updatePanel.ShowAndroidInfosAsync(androidUpdater).Forget();
                    return;

                default:
                    break;
            }
#endif
            //本地资源模式
            if (config.isStandaloneMode)
            {
                //直接加载默认包
                await AddOrGetPackageAsync(GlobalConfig_SO.DEFAULT_PACKAGE_NAME, string.Empty);
                OnResourceUpdateFinish(false, true);
                return;
            }

            //CDN资源模式
            resourcesUpdater = new ResourcesUpdater(config.defaultCDN, config.fallbackCDN, updatePanel.OnUpdatePackagesStart, updatePanel.RefreshProgress, OnResourceUpdateFinish);
            await resourcesUpdater.CheckVersion();
            //updatePanel.SetResVersion(resourcesUpdater.localVersion, resourcesUpdater.remoteVersion);
            switch (resourcesUpdater.state)
            {
                case EUpdaterState.SHOULD_UPDATE:
                    updatePanel.ShowResourceInfosAsync(resourcesUpdater).Forget();
                    break;

                case EUpdaterState.NEWEST:
                    OnResourceUpdateFinish(false, true);
                    break;

                default:
                    OnResourceUpdateFinish(true, false);
                    break;
            }

            async void OnResourceUpdateFinish(bool @do, bool isSucceeded)
            {
                updatePanel.OnUpdatePackagesFinished(@do, isSucceeded);
                new OperationFinishedEvent
                {
                    hasDone = @do,
                    isSucceeded = isSucceeded
                }.Dispatch();

                if (@do && !isSucceeded)
                    return;

#if HybridCLR
                await HybridLoader.Load();
#endif
                StartGame();
            }
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID
            androidUpdater?.Dispose();
            androidUpdater = null;
#elif UNITY_IOS
            appleUpdater?.Dispose();
            appleUpdater = null;
#endif
            resourcesUpdater?.Dispose();
            resourcesUpdater = null;
        }

        public async static void StartGame()
        {
            await ReflectionHelper.CallStaticMethod<UniTask>(ZString.Format(GAME_ASSEMBLY_FOTMAT, "Generated.SingletonPreload"), "PreloadAsync", null);
            await PanelManager.GetInstance().PreloadPanels();
#if HybridCLR
            ReflectionHelper.InvokeStaticMethod("Hotfix.Game,Hotfix", "Start", null);
#else
            ReflectionHelper.InvokeStaticMethod("Game,Assembly-CSharp", "Start");
#endif
        }
    }
}