/*
┌────────────────────────────┐
│　Description: YooAsset子包
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: Subpackage
└──────────────┘
*/
using Cysharp.Threading.Tasks;
using Lin.Runtime.Event;
using Lin.Runtime.Helper;
using Lin.Runtime.Interface;
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using static YooAsset.DownloaderOperation;
using Object = UnityEngine.Object;
using SceneHandle = YooAsset.SceneHandle;

namespace Lin.Runtime.Resource
{
    public class Subpackage : IDisposable
    {
        public delegate void OnStartDownloadFileHandler(string fileName, long sizeBytes);
        public delegate void OnDownloadProgressUpdateHandler(int totalDownloadCount, int currentDownloadCount, long totalDownloadBytes, long currentDownloadBytes);
        public delegate void OnDownloadErrorHandler(string fileName, string error);
        public delegate void OnDownloadOverHandler(bool isSucceeded);

        private ResourcePackage package;
        private ResourceDownloaderOperation downloader;

        /// <summary> 单个文件开始下载 </summary>
        public event OnStartDownloadFileHandler onStartDownloadFile;
        /// <summary> 整包更新进度 </summary>
        public event OnDownloadProgressUpdateHandler onDownloadProgressUpdate;
        /// <summary> 下载错误 </summary>
        public event DownloadError onDownloadError;
        /// <summary> 整包更新结果 </summary>
        public event OnDownloadOverHandler onDownloadOver;

        public bool IsInitialized => state > EState.INITIALIZING;
        public bool IsUsable => state == EState.VERSION_NEWEST;
        public EState state { get; private set; }
        public string localVersion { get; private set; }
        public string remoteVersion { get; private set; }
        public long totalDownloadSize { get; private set; }
        public long totalDownloadCount { get; private set; }
        public string packageName { get; }
        public string directory { get; }
        public string uri { get; private set; }
        public string tag { get; }
        public long size => new DirectoryInfo(directory).GetSize();

        public Subpackage(string packageName, string uri)
        {
            this.packageName = packageName;
            this.uri = uri;
            directory =
#if UNITY_EDITOR
            $"yoo/{packageName}";
#else
            $"{Application.persistentDataPath}/yoo/{packageName}";
#endif
        }

        public Subpackage(string packageName, string tag, string uri) : this(packageName, uri)
        {
            this.tag = tag;
        }

        public async UniTask Init()
        {
            if (state == EState.INITIALIZING)
                throw new Exception($"{package.PackageName} 正在初始化, 请勿重复操作！");
            else if (state > EState.INITIALIZING && state != EState.ERROR_NETWORK)
                throw new Exception($"{package.PackageName} 已完成初始化, 请勿重复操作！");

            await TryReloadPackage();

            //获取包体信息
            try
            {
                localVersion = package.GetPackageVersion();
            }
            catch (Exception) //本地没有这个包
            {
                localVersion = "-";
            }

            state = EState.INITIALIZED;
            await CheckUpdate();
        }

        private async UniTask CheckUpdate()
        {
            CheckInit();
            switch (state)
            {
                case EState.VERSION_CHECKING:
                    Debug.LogWarning($"{package.PackageName} 正在检测更新。");
                    return;

                case EState.VERSION_UPDATING_PAUSED:
                case EState.VERSION_UPDATING:
                    Debug.LogWarning($"{package.PackageName} 正在更新。");
                    return;

                case EState.VERSION_NEWEST:
                    Debug.LogWarning($"{package.PackageName} 无需更新。");
                    return;

                default:
                    break;
            }
            //版本
            var versionOperation = package.RequestPackageVersionAsync();
            await versionOperation.ToUniTask();
            if (versionOperation.Status != EOperationStatus.Succeed)
            {
                this.Error($"{package.PackageName} 获取云端资源版本失败。 {versionOperation.Error}");
                state = EState.ERROR_NETWORK;
                return;
            }
            remoteVersion = versionOperation.PackageVersion;

            //文件
            UpdatePackageManifestOperation manifestOperation = package.UpdatePackageManifestAsync(versionOperation.PackageVersion);
            await manifestOperation.ToUniTask();
            if (manifestOperation.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"{package.PackageName} 获取Manifest失败。 {manifestOperation.Error}");
                state = EState.ERROR_NETWORK;
                return;
            }
            int downloadingMaxNum = 10;
            int failedTryAgain = 3;
            if (string.IsNullOrEmpty(tag))
                downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);
            else
                downloader = package.CreateResourceDownloader(tag, downloadingMaxNum, failedTryAgain);

            if (downloader.TotalDownloadCount > 0)
            {
                //注册回调方法
                totalDownloadCount = downloader.TotalDownloadCount;
                totalDownloadSize = downloader.TotalDownloadBytes;
                state = EState.VERSION_SHOULD_UPDATE;
                downloader.DownloadErrorCallback += OnDownloadErrorFunction;
                downloader.DownloadUpdateCallback += OnDownloadProgressUpdateFunction;
                downloader.DownloadFinishCallback += OnDownloadOverFunction;
                downloader.DownloadFileBeginCallback += OnStartDownloadFileFunction;
                Debug.Log($"{package.PackageName} 需要更新, 远端版本为{remoteVersion}, 大小为{downloader.TotalDownloadBytes / 1024f / 1024f}MB");
                return;
            }
            state = EState.VERSION_NEWEST;
            this.Debug("已是最新版本");
            await UniTask.Yield();
        }

        public void BeginDownload()
        {
            CheckInit();
            switch (state)
            {
                case EState.INITIALIZED:
                case EState.VERSION_CHECKING:
                    this.Warning("正在检测更新。");
                    return;

                case EState.VERSION_UPDATING:
                    this.Warning("正在更新。");
                    return;

                case EState.VERSION_NEWEST:
                    this.Warning("无需更新。");
                    return;

                case EState.VERSION_UPDATING_PAUSED:
                    downloader.ResumeDownload();
                    return;

                case EState.ERROR_NETWORK:
                    this.Error("连接错误, 请再次初始化后重试。");
                    return;

                default:
                    break;
            }

            state = EState.VERSION_UPDATING;
            downloader.BeginDownload();
            this.Debug("开始更新。");
        }

        public void PauseDownload()
        {
            CheckInit();
            if (state != EState.VERSION_UPDATING)
            {
                this.Error("非下载状态。");
                return;
            }
            state = EState.VERSION_UPDATING_PAUSED;
            downloader.PauseDownload();
        }

        public void ResumeDownload()
        {
            CheckInit();
            if (state != EState.VERSION_UPDATING_PAUSED)
            {
                this.Error("非暂停状态。");
                return;
            }
            state = EState.VERSION_UPDATING;
            downloader.ResumeDownload();
        }

        public void CancelDownload()
        {
            CheckInit();
            if (state != EState.VERSION_UPDATING && state != EState.VERSION_UPDATING_PAUSED)
            {
                this.Error("非下载状态。");
                return;
            }
            state = EState.VERSION_SHOULD_UPDATE;
            downloader.CancelDownload();
        }

        private void OnStartDownloadFileFunction(DownloadFileData data) => onStartDownloadFile?.Invoke(data.FileName, data.FileSize);

        private void OnDownloadOverFunction(DownloaderFinishData data)
        {
            //更新完后重新初始化（不重载的话加载资源有问题）
            if (data.Succeed)
                this.Debug("完成更新。");

            state = data.Succeed ? EState.VERSION_NEWEST : EState.ERROR_NETWORK;
            onDownloadOver?.Invoke(data.Succeed);

            onDownloadError = null;
            onDownloadOver = null;
            onDownloadProgressUpdate = null;
            onStartDownloadFile = null;
            downloader = null;
        }

        private void OnDownloadProgressUpdateFunction(DownloadUpdateData data)
        {
            onDownloadProgressUpdate?.Invoke(data.TotalDownloadCount, data.CurrentDownloadCount, data.TotalDownloadBytes, data.CurrentDownloadBytes);
        }

        private void OnDownloadErrorFunction(DownloadErrorData data) => onDownloadError?.Invoke(data);

        public void Delete()
        {
            Dispose();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
                state = EState.NONE;
            }
        }

        private void CheckInit()
        {
            if (IsInitialized)
                return;

            throw new Exception($"{package.PackageName} 未初始化！");
        }

        public bool CheckLocationValid(string location)
        {
            CheckInit();
            try
            {
                return package.CheckLocationValid(location);
            }
            catch (Exception e)
            {
                this.Error($"{package is null}\n{location}");
                throw e;
            }
        }

        public void Dispose()
        {
            switch (state)
            {
                case EState.VERSION_UPDATING:
                case EState.VERSION_UPDATING_PAUSED:
                    CancelDownload();
                    break;

                case EState.VERSION_NEWEST:
                    break;
                case EState.ERROR_NETWORK:
                    break;
                default:
                    break;
            }

            var packageName = this.packageName;
            ResLoader.RemovePackage(packageName);
            YooAssets.RemovePackage(packageName);
            package = null;
            downloader = null;
        }

        public void UnloadUnusedAssets()
        {
            if (state != EState.VERSION_NEWEST)
                return;

            package.UnloadUnusedAssetsAsync();
        }

        /// <returns>True：加载成功</returns>
        private async UniTask<bool> TryReloadPackage()
        {
            if (YooAssets.ContainsPackage(packageName))
                YooAssets.RemovePackage(packageName);

            package = YooAssets.CreatePackage(packageName);
            state = EState.INITIALIZING;
            //RecoveryManifest();
            InitializeParameters initializeParameters = ResLoader.GetInitializeParameters(uri, uri, packageName);
            var initOperation = package.InitializeAsync(initializeParameters);
            await initOperation;
            if (initOperation.Status != EOperationStatus.Succeed)
            {
                this.Error("资源包初始化失败！");
                state = EState.NOT_INITIALIZED;
                return false;
            }

            return true;
        }

        #region - Load - 

        public AssetHandle GetSyncHandle<T>(string path) where T : Object => package.LoadAssetSync<T>(path);

        /// <summary>
        /// 同步加载资源
        /// WebGL 平台依赖 WebGLForceSyncLoadAsset = true，由 YooAsset 内部通过 WaitForAsyncComplete 实现
        /// </summary>
        public T LoadAsset<T>(string path, bool instantiate = true) where T : Object
        {
            using var operation = package.LoadAssetSync<T>(path);
            if (instantiate && operation.AssetObject.IsGameObject())
                return operation.InstantiateSync() as T;

            return operation.AssetObject as T;
        }

        /// <summary>
        /// 同步加载资源（指定类型）
        /// WebGL 平台依赖 WebGLForceSyncLoadAsset = true，由 YooAsset 内部通过 WaitForAsyncComplete 实现
        /// </summary>
        public Object LoadAsset(string path, Type type, bool instantiate = true)
        {
            using var operation = package.LoadAssetSync(path, type);
            if (instantiate && operation.AssetObject.IsGameObject())
                return operation.InstantiateSync();

            return operation.AssetObject;
        }

        public AudioClip LoadClip(string path) => LoadAsset<AudioClip>(path);

        public TextAsset LoadTextAsset(string path) => LoadAsset<TextAsset>(path);

        public GameObject LoadGameObject(string path) => LoadAsset<GameObject>(path);

        public GameObject LoadPrefab(string path) => LoadAsset<GameObject>(path, false);

        public Sprite LoadSprite(string path) => LoadAsset<Sprite>(path);

        public void LoadScene(string path, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None) => package.LoadSceneSync(path, sceneMode, physicsMode);


        #region - UniTask -

        public AssetHandle GetAsyncHandle<T>(string path, uint priority) where T : UnityEngine.Object => package.LoadAssetAsync<T>(path, priority);

        public async UniTask<Object> LoadAssetAsync(string path, Type type, uint priority = 0, bool instantiate = true)
        {
            using var operation = package.LoadAssetAsync(path, type, priority);
            await operation;
            if (instantiate && operation.AssetObject.IsGameObject())
            {
                var insOperation = operation.InstantiateAsync();
                await insOperation;
                return insOperation.Result;
            }
            return operation.AssetObject;
        }

        public async UniTask<T> LoadAssetAsync<T>(string path, uint priority = 0, bool instantiate = true) where T : UnityEngine.Object
        {
            using (var operation = package.LoadAssetAsync<T>(path, priority))
            {
                await operation;
                if (instantiate && operation.AssetObject.IsGameObject())
                {
                    var insOperation = operation.InstantiateAsync();
                    await insOperation;
                    return insOperation.Result as T;
                }

                return operation.AssetObject as T;
            }
        }

        public async UniTask<AudioClip> LoadClipAsync(string path, uint priority = 0) => await LoadAssetAsync<AudioClip>(path, priority);

        public async UniTask<TextAsset> LoadTextAssetAsync(string path, uint priority = 0) => await LoadAssetAsync<TextAsset>(path, priority);

        public async UniTask<GameObject> LoadGameObjectAsync(string path, uint priority = 0) => await LoadAssetAsync<GameObject>(path, priority);

        public async UniTask<GameObject> LoadPrefabAsync(string path, uint priority = 0) => await LoadAssetAsync<GameObject>(path, priority, false);

        public async UniTask<Sprite> LoadSpriteAsync(string path, uint priority = 0) => await LoadAssetAsync<Sprite>(path, priority);

        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="location">场景的定位地址</param>
        /// <param name="sceneMode">场景加载模式</param>
        /// <param name="suspendLoad">场景加载到90%自动挂起</param>
        /// <param name="priority">加载的优先级</param>
        public async UniTask LoadSceneAsync(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None, uint priority = 0)
        {
            using var handler = package.LoadSceneAsync(location, sceneMode, physicsMode, false, priority);
            await handler;
        }

        /// <summary>
        /// 获取加载场景的句柄  记得释放句柄
        /// </summary>
        /// <param name="location">场景的定位地址</param>
        /// <param name="sceneMode">场景加载模式</param>
        /// <param name="suspendLoad">场景加载到90%自动挂起</param>
        /// <param name="priority">加载的优先级</param>
        /// <returns></returns>
        public SceneHandle GetSceneAsyncHandle(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None, bool suspendLoad = false, uint priority = 0) => package.LoadSceneAsync(location, sceneMode, physicsMode, suspendLoad, priority);

        #endregion

        #region - Coroutine -

        public IEnumerator LoadAssetCoroutine<T>(string location, Action<T> onComplete, uint priority = 0, bool instantiate = true) where T : Object
        {
            using var opt = package.LoadAssetAsync<T>(location, priority);
            yield return opt;
            if (instantiate && opt.AssetObject.IsGameObject())
            {
                var insOperation = opt.InstantiateAsync();
                yield return insOperation;
                onComplete(insOperation.Result as T);
            }
            else
                onComplete(opt.AssetObject as T);
        }

        public IEnumerator LoadSceneCoroutine(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None, bool suspendLoad = false, uint priority = 0)
        {
            using var opt = package.LoadSceneAsync(location, sceneMode, physicsMode, suspendLoad, priority);
            yield return opt;
        }

        #endregion

#endregion

        public override string ToString() => $"{packageName} - {state}";

        public enum EState
        {
            NONE,

            //错误
            ERROR_NETWORK,

            //初始化
            NOT_INITIALIZED,
            INITIALIZING,
            INITIALIZED,

            //资源更新
            VERSION_CHECKING,
            VERSION_SHOULD_UPDATE,
            VERSION_UPDATING,
            VERSION_UPDATING_PAUSED,
            VERSION_NEWEST,
        }
    }
}
