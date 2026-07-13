/*
┌──────────────┐                                   
│　类名: ResourceConst
│　功能说明: 资源管理常量
└──────────────┘
*/
namespace Lin.Runtime.Const
{
    public static class ResourceConst
    {
        /// <summary> Bundle偏移常量 </summary>
        public const ulong BundleOffset = 297 + 975 + 0219 + 2024;

        /// <summary> 软件版本文件名 </summary>
        public const string VERSION_FILE_NAME = "version.txt";
        /// <summary> 软件版本信息文件名 </summary>
        public const string VERSION_INFOS_FILE_NAME = "infos.json";

        /// <summary> 安卓环境下安装包名字 </summary>
        public const string APK_NAME = "update.apk";
    }
}