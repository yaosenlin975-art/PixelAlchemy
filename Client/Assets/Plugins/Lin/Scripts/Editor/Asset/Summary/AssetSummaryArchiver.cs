/*
┌────────────────────────────┐
│　Description: 资源注释信息存储
│　Remark: 
└────────────────────────────┘
*/

using dnlib.DotNet;
using Lin.Editor.Helper;
using Lin.Runtime.DesignPattern.Singleton;
using Lin.Runtime.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZLinq;

namespace Lin.Editor.Asset
{
    public class AssetSummaryArchiver : Singleton<AssetSummaryArchiver>
    {
        /// <summary>
        /// GUID, Summary
        /// </summary>
        private Dictionary<string, AssetSummary> summaryMap = new Dictionary<string, AssetSummary>();
        
        /// <summary>
        /// JSON文件保存路径
        /// </summary>
        private static readonly string JsonFilePath = Path.Combine("ProjectSettings", "AssetSummaryData.json");

        public AssetSummaryArchiver() => LoadFromJson();

        /// <summary>
        /// 从ProjectSettings文件夹中读取Json数据
        /// </summary>
        private void LoadFromJson()
        {
            try
            {
                if (File.Exists(JsonFilePath))
                {
                    string jsonContent = File.ReadAllText(JsonFilePath);
                    if (!string.IsNullOrEmpty(jsonContent))
                        summaryMap = JsonConvert.DeserializeObject<Dictionary<string, AssetSummary>>(jsonContent);
                    else
                        summaryMap = new Dictionary<string, AssetSummary>();
                }
                else
                {
                    this.Warning("未找到资源注释数据文件，将创建新的数据集");
                    summaryMap = new Dictionary<string, AssetSummary>();
                    Refresh();
                }
            }
            catch (Exception ex)
            {
                this.Error($"加载资源注释数据失败: {ex.Message}");
                summaryMap = new Dictionary<string, AssetSummary>();
                Refresh();
            }
        }

        public void SetDescription(AssetImporter importer, AssetSummary summary)
        {
            var guid = importer.GetAssetGUID();
            if (summaryMap.ContainsKey(guid))
            {
                summary.createTime = summaryMap[guid].createTime;
                summary.updateTime = DateTime.Now;
                summaryMap[guid] = summary;
            }
            else
            {
                summary.createTime = DateTime.Now;
                summary.updateTime = DateTime.Now;
                summaryMap.Add(guid, summary);
            }

            AssetSummaryDrawer.Refresh(guid);
            Refresh();
        }

        public void RemoveDescription(string guid)
        {
            if (!summaryMap.Remove(guid))
                return;

            AssetSummaryDrawer.Refresh(guid);
            Refresh();
        }

        public void RemoveDescription(AssetImporter importer)
        {
            var guid = importer.GetAssetGUID();
            if (!summaryMap.Remove(guid))
                return;

            AssetSummaryDrawer.Refresh(importer.GetAssetGUID());
            Refresh();
        }

        /// <summary>
        /// 把summaryMap序列化成Json写入ProjectSettings文件夹
        /// </summary>
        private void Refresh()
        {
            try
            {
                // 确保ProjectSettings目录存在
                string directory = Path.GetDirectoryName(JsonFilePath);
                IOHelper.InsureExist(directory, false, false);
                
                // 序列化数据为JSON
                string jsonContent = JsonConvert.SerializeObject(summaryMap, Formatting.Indented);

                // 写入文件
                File.WriteAllText(JsonFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                this.Error($" 保存资源注释数据失败: {ex.Message}");
            }
        }

        [MenuItem("Lin/Asset Summary/清除丢失文件注释")]
        private static void RemoveAllMissFileSummary()
        {
            var instance = GetInstance();
            var summaryMap = instance.summaryMap;
            var toRemove = summaryMap.Keys.AsValueEnumerable().Where(c =>
            {
                var path = AssetDatabase.GUIDToAssetPath(c);
                return !File.Exists(path);
            }).ToArray();
            foreach (var item in toRemove)
                summaryMap.Remove(item);

            instance.Refresh();
            instance.Debug($"已移除 {toRemove.Length} 条注释");
        }

        /// <summary>
        /// 获取指定GUID的资源注释
        /// </summary>
        /// <param name="guid">资源GUID</param>
        /// <returns>资源注释，如果不存在则返回默认值</returns>
        public AssetSummary Get
            (AssetImporter importer) => summaryMap.TryGetValue(importer.GetAssetGUID(), out AssetSummary summary) ? summary : default;

        public AssetSummary Get(string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            summaryMap.TryGetValue(guid, out var summary);
            return summary;
        }
        
        /// <summary>
        /// 获取所有资源注释数据
        /// </summary>
        /// <returns>只读的资源注释字典</returns>
        public IReadOnlyDictionary<string, AssetSummary> GetAllSummaries()
        {
            return summaryMap;
        }
    }
}
