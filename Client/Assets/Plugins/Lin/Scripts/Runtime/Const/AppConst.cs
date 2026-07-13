/*
┌──────────────┐                                   
│　类名: AppConst
│　功能说明: 应用常量
└──────────────┘
*/

using UnityEngine;

namespace Lin.Runtime.Const
{
    public static class AppConst
    {
        public static readonly string CdnConfigPath = $"{Application.persistentDataPath}/CdnConfig.txt";
        public const string CDN_CONFIG_ENABLE_KEY = "CDN_CONFIG_ENABLE_KEY";
    }
}