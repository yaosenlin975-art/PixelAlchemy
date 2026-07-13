//Description: AssetBundle打包
using Sirenix.OdinInspector;
using System;

namespace Lin.Editor.BuildTool
{
    [Serializable]
    public struct PackageConfig
    {
        [ReadOnly]
        public string packageName;
        public bool build;
    }
}
