/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/

using Cysharp.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.FrameworkSynchronizer
{
    public partial class SynchronizerWindow
    {
        private ListView ignoreAssetsViwer;
        private HashSet<string> ignoreAssets;

        private void IgnoreAssetsViwerInit()
        {
            ignoreAssets = new HashSet<string>();

            ignoreAssetsViwer = rootVisualElement.Q<ListView>("IgnoreAssetsViewer");
            rootVisualElement.Q<Button>("AddIgnoreAssetButton").clicked += OnAddIgnoreAssetBtnClick;
            rootVisualElement.Q<Button>("AddIgnoreFolderButton").clicked += OnAddIgnoreAssetFolderBtnClick;

            RefreshIgnoreAssetsViwer();
        }

        private void RefreshIgnoreAssetsViwer()
        {
            var content = ignoreAssetsViwer.Q<VisualElement>("unity-content-container");
            content.Clear();

            var list = SynchronizerConfig.GetInstance().ignoreAssets;
            foreach (var path in list)
                AddIgnoreAsset(path);
        }

        private void OnAddIgnoreAssetFolderBtnClick()
        {
            var folderPath = UnityEditor.EditorUtility.OpenFolderPanel("请选择要同步的文件夹", ASSET_HEAD, string.Empty);
            if (string.IsNullOrEmpty(folderPath))
                return;

            var relativePath = ZString.Concat(ASSET_HEAD, folderPath.Replace(Application.dataPath, string.Empty));
            AddIgnoreAsset(relativePath);
        }

        private void OnAddIgnoreAssetBtnClick()
        {
            var assetPath = UnityEditor.EditorUtility.OpenFilePanel("请选择要同步的资源", ASSET_HEAD, "*");
            if (string.IsNullOrEmpty(assetPath))
                return;

            var relativePath = ZString.Concat(ASSET_HEAD, assetPath.Replace(Application.dataPath, string.Empty));
            AddIgnoreAsset(relativePath);
        }

        private void AddIgnoreAsset(string assetPath) => AddAssetElement(assetPath, ignoreAssets, SynchronizerConfig.GetInstance().ignoreAssets, ignoreAssetsViwer);
    }
}