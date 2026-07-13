/*
┌────────────────────────────┐
│　Description: YooAsset管理器
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: ResourceLoader
└──────────────┘
*/
using Cysharp.Threading.Tasks;
using Lin.Runtime.Helper;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YooAsset;

namespace Lin.Runtime.Resource
{
    public sealed partial class ResLoader
    {
        public const string DEFAULT_PACKAGE_NAME = "Game";
        public const string UPDATE_PANEL_PACKAGE_NAME = "Update";

        private static Dictionary<string, Subpackage> subpackages = new Dictionary<string, Subpackage>();

        public const string FOLDER =
#if UNITY_ANDROID
            "Android";
#elif UNITY_STANDALONE
            "PC";
#elif UNITY_IOS
            "IOS";
#elif UNITY_WEBGL
            "WebGL";
#else
            "Default";
#endif

        public static InitializeParameters GetInitializeParameters(string defaultServer, string fallbackServer, string packageName)
        {
            var config = GlobalConfig_SO.GetInstance();
#if UNITY_EDITOR
            if (config.isEditorMode)
                return GetEditorPlayModeParameters(packageName);
#endif

            if (config.isStandaloneMode)
                return GetOfflinePlayModeParameters();

            return GetHostPlayModeParameters(defaultServer, fallbackServer, packageName);
        }

        public static InitializeParameters GetHostPlayModeParameters(string defaultHostServer, string fallbackHostServer, string packageName)
        {
            defaultHostServer = defaultHostServer.EndsWith("/")?$"{defaultHostServer}{FOLDER}/{packageName}":$"{defaultHostServer}/{FOLDER}/{packageName}";
            fallbackHostServer = fallbackHostServer.EndsWith("/") ? $"{fallbackHostServer}{FOLDER}/{packageName}" : $"{fallbackHostServer}/{FOLDER}/{packageName}";

            IRemoteServices remoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
#if UNITY_WEBGL 
            var webServerFileSystemParams = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
            var webRemoteFileSystemParams = FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remoteServices); //支持跨域下载

            var initParameters = new WebPlayModeParameters();
            initParameters.WebServerFileSystemParameters = webServerFileSystemParams;
            initParameters.WebRemoteFileSystemParameters = webRemoteFileSystemParams;
            initParameters.WebGLForceSyncLoadAsset = true;
#else
            HostPlayModeParameters initParameters = new HostPlayModeParameters();
            initParameters.BuildinFileSystemParameters = null;
            initParameters.CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices);
#endif
            return initParameters;
        }

        public static InitializeParameters GetOfflinePlayModeParameters()
        {
            OfflinePlayModeParameters offlinePlayModeParameters = new OfflinePlayModeParameters();
            offlinePlayModeParameters.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
            return offlinePlayModeParameters;
        }

        public static async UniTask<Subpackage> AddOrGetPackageAsync(string packageName, string uri)
        {
            Subpackage subpackage;
            UniTask operation = default;
            lock (packagesLocker)
            {
                if (subpackages.TryGetValue(packageName, out subpackage))
                {
                    if (subpackage.IsUsable)
                        return subpackage;
                }
                else
                {
                    subpackage = new Subpackage(packageName, uri);
                    subpackages.Add(packageName, subpackage);
                    operation = subpackage.Init();
                }
            }

            await operation;
            return subpackage;
        }

        public static IEnumerator AddOrGetPackageCoroutine(string packageName, string uri)
        {
            Subpackage subpackage;
            UniTask operation = default;
            if (!subpackages.TryGetValue(packageName, out subpackage))
            {
                subpackage = new Subpackage(packageName, uri);
                subpackages.Add(packageName, subpackage);
                operation = subpackage.Init();
                yield return operation;
            }
            else if (!subpackage.IsUsable)
                yield return new WaitUntil(() => subpackage.IsUsable);
        }

        /// <returns> 包是否存在 </returns>
        private static bool TryGetPackage(string packageName, out Subpackage subpackage)
        {
            lock (packagesLocker)
                return subpackages.TryGetValue(packageName, out subpackage);
        }
        /// <summary>
        /// 仅在SubPackage被主动释放时调用
        /// </summary>
        /// <param name="packageName"></param>
        public static void RemovePackage(string packageName)
        {
            lock (packagesLocker)
            {
                if (subpackages.ContainsKey(packageName))
                    subpackages.Remove(packageName);
            }
        }

        public static async UniTask WaitUntilAllPackageUsable() => await UniTask.WaitUntil(() =>
        {
            lock (packagesLocker)
                return subpackages.Values.All(p => p.IsUsable);
        }, PlayerLoopTiming.FixedUpdate);

#if UNITY_EDITOR

        public static InitializeParameters GetEditorPlayModeParameters(string packageName)
        {
            var buildResult = EditorSimulateModeHelper.SimulateBuild(packageName);
            var packageRoot = buildResult.PackageRootDirectory;
            var editorFileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
            var initParameters = new EditorSimulateModeParameters();
            initParameters.EditorFileSystemParameters = editorFileSystemParams;
            return initParameters;
        }

        public static async UniTask LoadAllPackagesFromSetting() => await ReflectionHelper.CallStaticMethod<UniTask>("Lin.Editor.BuildTool.YooAssetHelper,Lin.Editor", nameof(LoadAllPackagesFromSetting));
#endif
    }
}