/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：EditorConst
└──────────────┘
*/

using UnityEngine;

namespace Lin.Editor
{
    public static class EditorConst
    {
        /// <summary> 项目Link </summary>
        public const string LINK_PATH = "Assets/Settings/link.xml";

        /// <summary> 主场景地址 </summary>
        public const string MAIN_SCENE_PATH = "Assets/Plugins/Lin/Scenes/Boot.unity";

#if HybridCLR
        /// <summary> AOT引用配置生成位置 </summary>
        public const string AOT_GENERIC_REFERENCE_FILE = "Scripts/Generated/AOTGenericReferences.cs";

        /// <summary> Hybrid用到的link文件地址需去掉Assets/ </summary>
        public static string LINK_PATH_4_HYBIRD => LINK_PATH.Replace("Assets/", string.Empty);
#endif

        // ------------- AssetSummary -------------
        public const string COLOR_PATTERN = @"<color=(#[0-9A-Fa-f]+)>";
        public const string COLOR_REMOVE_PATTERN = @"<color=#[0-9A-Fa-f]+>(.*?)</color>";
        public static readonly Color ASSET_SUMMARY_TITLE_DEFAULT_COLOR = new Color(0.267f, 2 / 3f, 1, 1);

        // ------------- EditorGUI -------------
        public const int BUTTON_WIDTH = 25;
    }
}