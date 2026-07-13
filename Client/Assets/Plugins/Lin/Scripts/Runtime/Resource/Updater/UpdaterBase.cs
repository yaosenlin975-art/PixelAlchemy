/*
┌────────────────────────────┐
│　Description：更新器基类
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：UpdaterBase
└──────────────┘
*/

using Cysharp.Threading.Tasks;
using Lin.Runtime.Event;
using Lin.Runtime.Helper;
using Lin.Runtime.Tool;
using System;
using Newtonsoft.Json;

namespace Lin.Runtime.Resource.Updater
{
    public abstract class UpdaterBase<T> : IDisposable
    {
        protected string defaultServer;
        protected string fallbackServer;

        public string localVersion { get; protected set; }
        public string remoteVersion { get; protected set; }

        protected VersionDetailEvent.VersionDetailDelegate onGetVersion;
        protected Action onUpdateStart;
        protected DownloadProgressDelegate onProgressRefresh;
        protected OperationFinishedEvent.OperationFinishedDelegate onFinish;

        public EUpdaterState state { get; protected set; }

        protected string VersionKey => $"{GetType().Name}_{nameof(VersionKey)}";

        protected abstract string RemoteVersionPath { get; }
        protected abstract string RemoteVersionInfosPath { get; }

        protected UpdaterBase() { }

        protected UpdaterBase(string defaultServer, string fallbackServer, Action onUpdateStart = null, DownloadProgressDelegate onProgressRefresh = null, OperationFinishedEvent.OperationFinishedDelegate onFinish = null)
        {
            this.defaultServer = defaultServer;
            this.fallbackServer = fallbackServer;
            this.onUpdateStart = onUpdateStart;
            this.onProgressRefresh = onProgressRefresh;
            this.onFinish = onFinish;
        }

        /// <returns>获取成功时返回具体版本号，获取失败时返回string.Empty</returns>
        protected virtual string LoadLocalVersion() => PrefsHelper.Get(VersionKey, "-");
        protected virtual void SaveVersion(string localVersion) => PrefsHelper.Set(VersionKey, localVersion);

        /// <returns>获取成功时返回具体版本号，获取失败时返回null</returns>
        protected async UniTask<string> LoadRemoteVersion()
        {
            var config = GlobalConfig_SO.GetInstance();
            string uri;
            string remoteVersion;
            if (string.IsNullOrEmpty(ResLoader.cdnServer))
            {
                uri = $"{defaultServer}/{RemoteVersionPath}";
                remoteVersion = await UnityWebRequestHelper.GetString(uri);
                if (remoteVersion is null)
                {
                    uri = $"{fallbackServer}/{RemoteVersionPath}";
                    remoteVersion = await UnityWebRequestHelper.GetString(uri);
                    config.usableCDN = fallbackServer;
                }
                else
                    config.usableCDN = defaultServer;
            }
            else
            {
                uri = $"{ResLoader.cdnServer}/{RemoteVersionPath}";
                remoteVersion = await UnityWebRequestHelper.GetString(uri);
            }

            return remoteVersion;
        }

        public virtual async UniTask CheckVersion()
        {
            if (state != EUpdaterState.NONE && state != EUpdaterState.ERROR)
                throw new Exception($"Do not repeat '{nameof(CheckVersion)}'.");

            localVersion = LoadLocalVersion();
            remoteVersion = await LoadRemoteVersion();
            if (string.IsNullOrEmpty(remoteVersion))
            {
                state = EUpdaterState.ERROR;
                return;
            }

            if (string.IsNullOrEmpty(localVersion) || !localVersion.Equals(remoteVersion))
                state = EUpdaterState.SHOULD_UPDATE;
            else
                state = EUpdaterState.NEWEST;
        }

        /// <summary> 获取更新信息 </summary>
        public async UniTask<T> GetVersionDescriptions()
        {
            if (state == EUpdaterState.NONE || state == EUpdaterState.ERROR)
                throw new Exception($"Please call '{nameof(CheckVersion)}' before call '{nameof(GetVersionDescriptions)}'.");

            var json = await UnityWebRequestHelper.GetString($"{ResLoader.cdnServer}/{RemoteVersionInfosPath}");
            return JsonConvert.DeserializeObject<T>(json);
        }

        public virtual UniTask BeginDownload()
        {
            if (state != EUpdaterState.SHOULD_UPDATE)
                throw new Exception($"Please call '{nameof(CheckVersion)}' before call '{nameof(BeginDownload)}'.");

            state = EUpdaterState.UPDATING;
            onUpdateStart?.Invoke();
            return UniTask.CompletedTask;
        }

        public abstract void Dispose();
    }

    public enum EUpdaterState
    {
        NONE,
        ERROR,
        CHECKING,
        SHOULD_UPDATE,
        UPDATING,
        NEWEST
    }
}
