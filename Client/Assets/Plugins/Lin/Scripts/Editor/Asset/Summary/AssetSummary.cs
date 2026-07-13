/*
┌────────────────────────────┐
│　Description: 资源注释信息结构
│　Remark: 
└────────────────────────────┘
*/

using Lin.Editor.Settings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Lin.Editor.Asset
{
    [Serializable]
    public struct AssetSummary
    {
        public string title;
        public string titleColor;

        public string description;
        public DateTime createTime;
        public DateTime updateTime;

        public string GetRichTitle() => $"<size={EditorSettings_SO.GetInstance().assetSummaryTitleSize}><color=#{titleColor}><b>{title}</b></color></size>";

        public override string ToString() => JsonConvert.SerializeObject(this);

        public static AssetSummary FromJson(string json) => JsonConvert.DeserializeObject<AssetSummary>(json);
    }
}
