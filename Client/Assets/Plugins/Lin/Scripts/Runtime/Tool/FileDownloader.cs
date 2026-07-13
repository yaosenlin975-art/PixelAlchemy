/*
┌────────────────────────────┐
│　Description: UnityWebRequest文件下载器
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: FileDownloader
└──────────────┘
*/
using Cysharp.Threading.Tasks;
using System;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

namespace Lin.Runtime.Tool
{
    public delegate void DownloadProgressDelegate(float progress, long totalDownloadBytes);
    public delegate void DownloadOverDelegate(bool isSucceeded, string path);

    public class FileDownloader : IDisposable
    {
        private string uri;
        public string path { get; private set; }
        private string tempPath;
        public event DownloadProgressDelegate onDownloadProgress;
        public event DownloadOverDelegate onDownloadOver;

        public EState state { get; private set; } = EState.NONE;

        /// <summary> 目标文件大小 </summary>
        public long totalDownloadBytes { get; private set; }

        private long downloadedBytes;

        /// <summary> 进度[0, 1] </summary>
        public float progress { get; private set; }

        public async UniTask Init(string uri, string path)
        {
            this.uri = uri;
            this.path = path;
            tempPath = $"{this.path}.temp";

            using (UnityWebRequest head = UnityWebRequest.Head(uri))
            {
                await head.SendWebRequest();
                if (head.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError(head.error);
                    state = EState.ERROR_NETWORK;
                    return;
                }

                if (File.Exists(tempPath))
                    downloadedBytes = new FileInfo(tempPath).Length;
                totalDownloadBytes = long.Parse(head.GetResponseHeader("Content-Length")) - downloadedBytes;
                state = EState.WAIT_TO_DOWNLOAD;
            }
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 开始下载
        /// </summary>
        public async UniTask StartAsync()
        {
            switch (state)
            {
                case EState.NONE:
                case EState.FINISH:
                    Debug.LogWarning("未进行初始化, 请初始化后再进行尝试");
                    return;

                case EState.DOWNLOADING:
                    Debug.LogError("正在进行下载, 请勿重复操作");
                    return;

                default:
                    break;
            }

            state = EState.DOWNLOADING;
            if (totalDownloadBytes != 0)
            {
                using (UnityWebRequest request = UnityWebRequest.Get(uri))
                {
                    request.SetRequestHeader("Range", $"bytes={downloadedBytes}-");
                    request.downloadHandler = new DownloadHandlerFile(tempPath, true);
                    request.SendWebRequest().GetAwaiter();
                    while (!request.isDone)
                    {
                        progress = request.downloadProgress;
                        onDownloadProgress?.Invoke(progress, totalDownloadBytes);
                        await UniTask.Yield();
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError(request.error);
                        state = EState.ERROR_NETWORK;
                        onDownloadOver?.Invoke(false, path);
                        return;
                    }
                }
            }

            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tempPath, path);
                Debug.Log($"下载完成, 文件在{path}");
                state = EState.FINISH;
                onDownloadProgress?.Invoke(1, totalDownloadBytes);
                onDownloadOver?.Invoke(true, path);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                state = EState.ERROR_IO;
                onDownloadOver?.Invoke(false, path);
            }
            await UniTask.CompletedTask;
        }

        public void Dispose()
        {
            onDownloadProgress = null;
            onDownloadOver = null;
        }

        public enum EState
        {
            NONE,
            WAIT_TO_DOWNLOAD,
            DOWNLOADING,
            FINISH,
            ERROR_NETWORK,
            ERROR_IO
        }
    }
}