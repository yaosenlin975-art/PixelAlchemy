/*
┌────────────────────────────┐
│　Description: 富文本辅助
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: RichTextHelper
└──────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.Helper
{
    public static class RichTextHelper
    {
        public static string GetBold(this string self) => $"<b>{self}</b>";

        public static string GetColor(this string self, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{self}</color>";
    }
}