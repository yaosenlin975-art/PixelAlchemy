//Description: AssetBundle 打包设置
using Lin.Runtime.Helper;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace Lin.Editor.BuildTool
{
    [Serializable]
    public class BuildPackagesSettings
    {
        public const string KEY = "BUILD_SETTINGS_KEY";

        [LabelText("加密方式"), HideInInspector]
        public string encryType;

        [LabelText("压缩方式")]
        public ECompressOption compressOption = ECompressOption.LZ4;

        [LabelText("文件名风格")]
        public EFileNameStyle fileNameStyle = EFileNameStyle.HashName;

        [LabelText("重新打包")]
        public bool clearBuildCacheFiles = false;

        [LabelText("收集资源依赖")]
        public bool useAssetDependencyDB = true;

        [LabelText("版本号")]
        public string version;

        [LabelText("时间戳后缀")]
        public bool useTimeStamp;

        [LabelText("额外复制")]
        public bool extraCopy;

        [LabelText("目标文件夹"), FolderPath(AbsolutePath = true), ShowIf(nameof(extraCopy))]
        public string extraPath;

        [LabelText("收集Shader变体")]
        public bool collectShaderVariants;

#if HybridCLR
        [LabelText("编译Hybrid")]
        public bool compileHybrid;
#endif

#if !UNITY_WEBGL
        [LabelText("StreamingAssets操作")]
        public EBuildinFileCopyOption buildinFileCopyOption = EBuildinFileCopyOption.None;

        [LabelText("强制更新")]
        public bool isForceUpdate;

        [LabelText("更新信息"), TextArea]
        public List<string> infos = new List<string>();
#endif

        [LabelText("打包管线"), Tooltip("推荐ScriptableBuildPipeline")]
        public EBuildPipeline pipelineType = EBuildPipeline.ScriptableBuildPipeline;

        public BuildPackagesSettings()
        {
            version = IOHelper.GetLocalTimeStamp();
            extraPath = string.Empty;
            encryType = Path.GetFileName("");
        }
    }
}