/*
┌────────────────────────────┐
│　Description：资源更新
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：ResourcesUpdater
└──────────────┘
*/

using Cysharp.Threading.Tasks;
using Lin.Runtime.Event;
using Lin.Runtime.Helper;
using Lin.Runtime.Tool;
using Lin.Runtime.Const;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lin.Runtime.Resource.Updater
{
    public class ResourcesUpdater : UpdaterBase<string[]>
    {
        private List<Subpackage> subpackages;
        private long totalSize;

        public ResourcesUpdater(string defaultServer, string fallbackServer, Action onUpdateStart = null, DownloadProgressDelegate onProgressRefresh = null, OperationFinishedEvent.OperationFinishedDelegate onFinish = null) : base(defaultServer, fallbackServer, onUpdateStart, onProgressRefresh, onFinish){ }

        protected override string RemoteVersionPath => $"{ResLoader.FOLDER}/{ResourceConst.VERSION_FILE_NAME}";

        protected override string RemoteVersionInfosPath => $"{ResLoader.FOLDER}/{ResourceConst.VERSION_INFOS_FILE_NAME}";

        public override async UniTask CheckVersion()
        {
            await base.CheckVersion();
            if (state == EUpdaterState.ERROR)
                return;

            ResLoader.version = localVersion;
            subpackages = new List<Subpackage>
            {
                await ResLoader.AddOrGetPackageAsync(GlobalConfig_SO.DEFAULT_PACKAGE_NAME, ResLoader.cdnServer)
            };

            if (subpackages.Exists(p => p.state == Subpackage.EState.ERROR_NETWORK))
                state = EUpdaterState.ERROR;
            else if (subpackages.Exists(p => p.state == Subpackage.EState.VERSION_SHOULD_UPDATE))
                state = EUpdaterState.SHOULD_UPDATE;
            else
            {
                localVersion = remoteVersion;
                PrefsHelper.Set(VersionKey, remoteVersion);
                state = EUpdaterState.NEWEST;
            }
        }

        public override async UniTask BeginDownload()
        {
            await base.BeginDownload();

            bool hasError = subpackages.Exists(p => p.state == Subpackage.EState.ERROR_NETWORK);
            totalSize = subpackages.Sum(p => p.totalDownloadSize);
            long totalCurrentSize = 0, lastSize = 0; 

            foreach (var sub in subpackages)
            {
                if (sub.state != Subpackage.EState.VERSION_SHOULD_UPDATE)
                    continue;

                sub.onDownloadProgressUpdate += (_, __, ___, currentSize) =>
                {
                    onProgressRefresh?.Invoke((float)(totalCurrentSize + currentSize) / totalSize, totalSize);
                    lastSize = currentSize;
                };
                bool finished = false;
                sub.onDownloadOver += isSucceeded =>
                {
                    hasError |= !isSucceeded;
                    finished = true;
                };
                sub.BeginDownload();
                await UniTask.WaitUntil(() => finished, PlayerLoopTiming.FixedUpdate);
            }

            if (!hasError)
            {
                PrefsHelper.Set(VersionKey, remoteVersion);
                ResLoader.version = remoteVersion;
            }
            onFinish?.Invoke(true, !hasError);
        }

        public override void Dispose()
        {
            subpackages?.Clear();
            subpackages = null;
        }
    }
}
