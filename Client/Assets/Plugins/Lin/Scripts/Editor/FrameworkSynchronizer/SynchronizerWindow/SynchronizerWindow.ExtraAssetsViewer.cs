/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using Cysharp.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.FrameworkSynchronizer
{
    public partial class SynchronizerWindow
    {
        private ListView extraAssetsViwer;
        private VisualTreeAsset assetContainerTemplate;
        private HashSet<string> extraAssets;

        private const string ASSET_HEAD = "Assets";

        private void ExtraAssetsViwerInit()
        {
            extraAssets = new HashSet<string>();

            extraAssetsViwer = rootVisualElement.Q<ListView>("ExtraAssetsViewer");
            assetContainerTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Lin/Scripts/Editor/FrameworkSynchronizer/SynchronizerWindow/AssetContainer.uxml");
            rootVisualElement.Q<Button>("AddExtraAssetButton").clicked += OnAddExtraAssetBtnClick;
            rootVisualElement.Q<Button>("AddExtraFolderButton").clicked += OnAddExtraAssetFolderBtnClick;

            RefreshExtraAssetsViwer();
        }

        private void RefreshExtraAssetsViwer()
        {
            var content = extraAssetsViwer.Q<VisualElement>("unity-content-container");
            content.Clear();

            var list = SynchronizerConfig.GetInstance().extraAssets;
            foreach (var path in list)
                AddExtraAssetElement(path);
        }

        private void OnAddExtraAssetFolderBtnClick()
        {
            var folderPath = UnityEditor.EditorUtility.OpenFolderPanel("请选择要同步的文件夹", ASSET_HEAD, string.Empty);
            if (string.IsNullOrEmpty(folderPath))
                return;

            var relativePath = ZString.Concat(ASSET_HEAD, folderPath.Replace(Application.dataPath, string.Empty));
            AddExtraAssetElement(relativePath);
        }

        private void OnAddExtraAssetBtnClick()
        {
            var assetPath = UnityEditor.EditorUtility.OpenFilePanel("请选择要同步的资源", ASSET_HEAD, "*");
            if (string.IsNullOrEmpty(assetPath))
                return;

            var relativePath = ZString.Concat(ASSET_HEAD, assetPath.Replace(Application.dataPath, string.Empty));
            AddExtraAssetElement(relativePath);
        }

        private void AddExtraAssetElement(string assetPath) => AddAssetElement(assetPath, extraAssets, SynchronizerConfig.GetInstance().extraAssets, extraAssetsViwer);
    }
}
