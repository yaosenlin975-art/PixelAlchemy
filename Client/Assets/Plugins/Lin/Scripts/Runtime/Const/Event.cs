/*
┌──────────────┐                                   
│　类名: int
│　功能说明: 事件标识ID
└──────────────┘
*/
using Lin.Runtime.Interface;
using UnityEngine.Events;

namespace Lin.Runtime.Event
{
    /// <summary> 云端版本信息 </summary>
    public struct VersionDetailEvent
    {
        /// <param name="isSucceeded">是否获取成功</param>
        public bool isSucceeded;

        /// <param name="version">云端版本信息</param>
        public IVersion version;

        /// <param name="startUpdate">开始更新的委托</param>
        public UnityAction startUpdate;

        /// <param name="localVersion">本地版本号</param>
        public string localVersion;

        public delegate void VersionDetailDelegate(bool isSucceeded, IVersion version, UnityAction startUpdate, string localVersion);
    }

    /// <summary> 操作结果 </summary>
    public struct OperationFinishedEvent
    {
        /// <summary> 是否执行操作 </summary>
        public bool hasDone;

        /// <summary> 操作是否成功 </summary>
        public bool isSucceeded;

        public delegate void OperationFinishedDelegate(bool hasDone, bool isSucceeded);
    }

    public struct HybridLoadFinishedEvent { }

    /// <summary> 下载进度 </summary>
    public struct DownloadProgressEvent
    {
        /// <summary> 已下载部分 </summary>
        public long downloadedBytes;
        /// <summary> 总大小 </summary>
        public long totalBytes;

        public float GetPercent() => downloadedBytes / (float)totalBytes;

        public delegate void DownloadProgressDelegate(long downloadedBytes, long totalBytes);
    }

    /// <summary> 场景加载进度 </summary>
    public struct SceneProgressEvent
    {
        /// <summary>
        /// 0.01
        /// </summary>
        public float percent;
    }

    /// <summary> 全屏Panel数量变化(是否正在展示全屏的panel) </summary>
    public struct FullScreenPanelEvent
    {
        public bool hasFullScreenPanel;
    }
}