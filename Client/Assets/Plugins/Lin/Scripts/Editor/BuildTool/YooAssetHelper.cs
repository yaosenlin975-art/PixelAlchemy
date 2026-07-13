using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Lin.Editor.Helper;
using Lin.Runtime.Event;
using Lin.Runtime.Helper;
using Lin.Runtime.Resource;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;
using ZLinq;

namespace Lin.Editor.BuildTool
{
    [InitializeOnLoad]
    public static class YooAssetHelper
    {
        public const string SETTINGS_PATH = "Assets/Settings/AssetBundleCollectorSetting.asset";

        public static AssetBundleCollectorSetting LoadAssetBundleCollectorSetting() => ScriptableObjectHelper.Find<AssetBundleCollectorSetting>();

        #region - CollectorWindow -

        private static ListView packageListView;
        private static ListView groupListView;

        private static EnumField textureSizeField;

        static YooAssetHelper()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            var window = EditorWindow.focusedWindow;
            if (window == null)
                return;

            if (window is not AssetBundleCollectorWindow)
                return;

            var root = window.rootVisualElement;
            const string TEXTURE_SIZE_FIELD_NAME = "TextureSizeField";
            if (root.Q(TEXTURE_SIZE_FIELD_NAME) != null)
                return;

            

            packageListView = root.Q<ListView>("PackageListView");
            groupListView = root.Q<ListView>("GroupListView");

            var container = root.Q("CollectorContainer");
            var currentGroup = GetGroupPath();

            textureSizeField = new EnumField("纹理大小", ETextureSize.NoOverride);
            textureSizeField.name = TEXTURE_SIZE_FIELD_NAME;
            if (!string.IsNullOrEmpty(currentGroup))
            {
                var settings = AssetsCompressSettings.GetInstance().Get(currentGroup);
                textureSizeField.value = settings.textureSize;
            }
            textureSizeField.RegisterValueChangedCallback(OnTextureSizeChanged);

            container.Insert(5, textureSizeField);

#if UNITY_2022_3_OR_NEWER
            groupListView.selectionChanged += OnSelectedGroupChanged;
#elif UNITY_2020_1_OR_NEWER
            groupListView.onSelectionChange += OnSelectedGroupChanged;
#else
            groupListView.onSelectionChanged += OnSelectedGroupChanged;
#endif
        }

        private static void OnTextureSizeChanged(ChangeEvent<Enum> evt)
        {
            var key = GetGroupPath();
            if (string.IsNullOrEmpty(key))
                return;

            var acs = AssetsCompressSettings.GetInstance();
            var settings = acs.Get(key);
            var newValue = (ETextureSize)evt.newValue;
            if (settings.textureSize == newValue)
                return; 

            settings.textureSize = (ETextureSize)evt.newValue;
            acs.Save();
        }

        private static string GetGroupPath()
        {
            if (packageListView.selectedItem is null)
                return string.Empty;

            if (groupListView.selectedItem is null)
                return string.Empty;

            return GetGroupPath((packageListView.selectedItem as AssetBundleCollectorPackage).PackageName, (groupListView.selectedItem as AssetBundleCollectorGroup).GroupName);
        }

        public static string GetGroupPath(string packageName, string groupName) => ZString.Concat(packageName, '_', groupName);

        private static void OnSelectedGroupChanged(IEnumerable<object> enumerable)
        {
            var selectGroup = groupListView.selectedItem as AssetBundleCollectorGroup;
            if (selectGroup is null)
                return;

            var path = GetGroupPath();
            var settings = AssetsCompressSettings.GetInstance().Get(path);
            textureSizeField.value = settings.textureSize;
        }

        #endregion

        // 编辑器中加载所有包体
        public static async UniTask LoadAllPackagesFromSetting()
        {
            var settings = LoadAssetBundleCollectorSetting();

            for (int i = 0; i < settings.Packages.Count; i++)
            {
                var package = settings.Packages[i];
                var count = package.Groups.Sum(g => g.Collectors.Count);
                if (count != 0)
                    await ResLoader.AddOrGetPackageAsync(package.PackageName, string.Empty);

                new DownloadProgressEvent
                {
                    downloadedBytes = i + 1,
                    totalBytes = settings.Packages.Count
                }.Dispatch();
            }
            await ResLoader.WaitUntilAllPackageUsable();
        }

        private const string CLEAR_BUNDLES = "YooAsset/Clear Bundles";

        [MenuItem(CLEAR_BUNDLES)]
        public static void ClearBundles()
        {
            string[] ignores = new string[]
            {
                "OutputCache",
                "Simulate"
            };

            DirectoryInfo bundleDir = new DirectoryInfo("Bundles");
            var platform = bundleDir.GetDirectories();
            foreach (var platformDir in platform)
            {
                foreach (var bundle in platformDir.GetDirectories())
                {
                    foreach (var version in bundle.GetDirectories())
                    {
                        if (ignores.Contains(version.Name))
                            continue;

                        version.Delete(true);
                    }
                }
            }
            Debug.Log("已完成Bundles清理");
        }

        public static string FindPackage(UnityEngine.Object asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var collectorSettings = LoadAssetBundleCollectorSetting();
            foreach (var package in collectorSettings.Packages)
                foreach (var group in package.Groups)
                    foreach (var collector in group.Collectors)
                        if (assetPath.Contains(collector.CollectPath))
                            return package.PackageName;

            return null;
        }

        public static bool FindPackage(this AssetBundleCollectorSetting self, string path, out AssetBundleCollectorPackage result)
        {
            foreach (var package in self.Packages)
            {
                foreach (var group in package.Groups)
                {
                    foreach (var collector in group.Collectors)
                    {
                        if (collector.CollectPath == path)
                        {
                            result = package;
                            return true;
                        }
                    }
                }
            }
            result = null;
            return false;
        }

        public static AssetBundleCollectorPackage FindPackageByFolder(string directory)
        {
            var collectorSettings = LoadAssetBundleCollectorSetting();
            foreach (var package in collectorSettings.Packages)
                foreach (var group in package.Groups)
                    foreach (var collector in group.Collectors)
                        if (collector.CollectPath.Contains(directory))
                            return package;

            return null;
        }

        public static bool Contains(this AssetBundleCollectorPackage package, string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            bool isDirectory = Directory.Exists(path);
            string directory = isDirectory ? path : Path.GetDirectoryName(path);

            foreach (var group in package.Groups)
            {
                foreach (var collector in group.Collectors)
                {
                    if (collector.CollectPath == path)
                        return true;

                    if (collector.CollectPath == directory && collector.CollectorType == ECollectorType.MainAssetCollector)
                        return true;
                }
            }

            return false;
        }

        public static AssetBundleCollectorGroup InsureGroupExist(this AssetBundleCollectorPackage package, string groupName)
        {
            var group = package.Groups.Find(g => g.GroupName == groupName);
            if (group is null)
            {
                group = new AssetBundleCollectorGroup()
                {
                    ActiveRuleName = "EnableGroup",
                    GroupDesc = "Generated by code.",
                    Collectors = new System.Collections.Generic.List<AssetBundleCollector>(),
                    GroupName = groupName
                };
                package.Groups.Add(group);
            }

            return group;
        }

        public static string[] GetPackageNames() => LoadAssetBundleCollectorSetting().Packages.AsValueEnumerable().Select(p => p.PackageName).ToArray();
    }
}
