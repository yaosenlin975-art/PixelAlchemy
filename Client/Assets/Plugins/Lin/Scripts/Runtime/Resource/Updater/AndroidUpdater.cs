/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：UpdaterBase
└──────────────┘
*/
#if UNITY_ANDROID
using Cysharp.Threading.Tasks;
using Lin.Runtime.Event;
using Lin.Runtime.Tool;
using Lin.Runtime.Const;
using System;
using UnityEngine;
using Newtonsoft.Json;
using Cysharp.Text;

namespace Lin.Runtime.Resource.Updater
{
    public class AndroidUpdater : UpdaterBase<AndroidVersionDescriptions>
    {
        private FileDownloader apkDownloader;

        protected override string RemoteVersionPath => $"Android/Apk/{ResourceConst.VERSION_FILE_NAME}";

        protected override string RemoteVersionInfosPath => $"Android/Apk/{ResourceConst.VERSION_INFOS_FILE_NAME}";

        public AndroidUpdater(string defaultServer, string fallbackServer, Action onUpdateStart = null, DownloadProgressDelegate onProgressRefresh = null, OperationFinishedEvent.OperationFinishedDelegate onFinish = null) : base(defaultServer, fallbackServer, onUpdateStart, onProgressRefresh, onFinish) { }

        public override void Dispose() => apkDownloader?.Dispose();

        protected override string LoadLocalVersion() => Application.version;

        protected override void SaveVersion(string localVersion) { }

        public override async UniTask BeginDownload()
        {
            base.BeginDownload(); 
            apkDownloader?.Dispose();
            apkDownloader = new FileDownloader();
            apkDownloader.onDownloadProgress += onProgressRefresh;
            apkDownloader.onDownloadOver += ApkDownloader_onDownloadOver;
            await apkDownloader.Init($"{ResLoader.cdnServer}/Android/Apk/{ResourceConst.APK_NAME}", $"{Application.persistentDataPath}/Downloads/{ResourceConst.APK_NAME}");
            apkDownloader.StartAsync().GetAwaiter();
        }

        private void ApkDownloader_onDownloadOver(bool isSucceeded, string path)
        {
            if (isSucceeded)
            {
                using AndroidJavaClass javaClass = new AndroidJavaClass("com.ysl.install.InstallTool");
                javaClass.CallStatic<bool>("InstallApk", path);
            }
            else
                onFinish?.Invoke(true, false);
        }
    }

    /// <summary> 版本更新具体说明 </summary>
    [Serializable]
    public struct AndroidVersionDescriptions
    {
        public long apkSize;
        public string md5;
        public string[] descriptions;

        public override string ToString() => JsonConvert.SerializeObject(this);

        public string GetDescriptions()
        {
            using var sb = ZString.CreateStringBuilder();
            for (int i = 0; i < descriptions.Length; i++)
            {
                sb.Append(i + 1);
                sb.Append('.');
                sb.Append(descriptions[i]);
                if (i < descriptions.Length - 1)
                    sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
#endif
