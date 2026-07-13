using Cysharp.Threading.Tasks;
using Lin.Runtime.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace Lin.Editor.BuildTool
{
    public static class ShaderVariantsCollector
    {
        [MenuItem("Build/Res/Collect Shaders Of All Packages"), MenuItem("YooAsset/Collect Shaders Of All Packages")]
        public static void CollectAllShaders() => CollectAllShaders(null);

        public static async UniTask CollectShaders(List<PackageConfig> packages)
        {
            AssetBundleCollectorSetting settings = YooAssetHelper.LoadAssetBundleCollectorSetting();
            RemoveAllShadervariants(ref settings);
            var map = packages.ToDictionary(c => c.packageName, c => c);
            foreach (var package in settings.Packages)
            {
                if (map[package.PackageName].build)
                {
                    string variantsPath = $"Assets/Prefabs/Shadervariants/{package.PackageName}.shadervariants";
                    string jsonPath = $"Assets/Prefabs/Shadervariants/{package.PackageName}.json";
                    bool isCompleted = false;
                    Action onCompleted = () => isCompleted = true;
                    ShaderVariantCollector.Run(variantsPath, package.PackageName, 1000, onCompleted);
                    await UniTask.WaitUntil(() => isCompleted);

                    // OnFinished
                    Debug.Log($"Collect {package.PackageName}'s Shaders.");
                    var group = package.Groups.Find(g => g.GroupName == "Shaders");
                    if (group == null)
                    {
                        group = new AssetBundleCollectorGroup()
                        {
                            ActiveRuleName = "EnableGroup",
                            GroupName = "Shaders",
                            GroupDesc = "包体的Shader变体集",
                            Collectors = new List<AssetBundleCollector>()
                        };
                        package.Groups.Add(group);
                    }

                    InsureExist(variantsPath);
                    InsureExist(jsonPath);

                    void InsureExist(string path)
                    {
                        var target = group.Collectors.Find(c => c.CollectPath == path);
                        if (target is null)
                        {
                            group.Collectors.Add(new AssetBundleCollector()
                            {
                                CollectPath = path,
                                CollectorGUID = AssetDatabase.AssetPathToGUID(path),
                                CollectorType = ECollectorType.MainAssetCollector,
                                AddressRuleName = "AddressByFileNameAndExt",
                                PackRuleName = "PackDirectory",
                                FilterRuleName = "CollectAll",
                            });
                        }
                    }
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            settings.EditorSave();
        }

        public static void CollectAllShaders(Action onfinsh)
        {
            AssetBundleCollectorSetting settings = YooAssetHelper.LoadAssetBundleCollectorSetting();
            RemoveAllShadervariants(ref settings);
            int index = 0;
            string variantsPath = $"Assets/Prefabs/Shadervariants/{settings.Packages[index].PackageName}.shadervariants";
            string jsonPath = $"Assets/Prefabs/Shadervariants/{settings.Packages[index].PackageName}.json";
            ShaderVariantCollector.Run(variantsPath, settings.Packages[index].PackageName, 1000, PackNext);

            void PackNext()
            {
                Debug.Log($"Collect {settings.Packages[index].PackageName}'s Shaders.");
                var group = settings.Packages[index].Groups.Find(g => g.GroupName == "Shaders");
                if (group == null)
                {
                    group = new AssetBundleCollectorGroup()
                    {
                        ActiveRuleName = "EnableGroup",
                        GroupName = "Shaders",
                        GroupDesc = "包体的Shader变体集",
                        Collectors = new List<AssetBundleCollector>()
                    };
                    settings.Packages[index].Groups.Add(group);
                }

                InsureExist(variantsPath);
                InsureExist(jsonPath);
                AssetDatabase.Refresh();

                if (++index < settings.Packages.Count)
                {
                    variantsPath = $"Assets/Prefabs/Shadervariants/{settings.Packages[index].PackageName}.shadervariants";
                    jsonPath = $"Assets/Prefabs/Shadervariants/{settings.Packages[index].PackageName}.json";
                    ShaderVariantCollector.Run(variantsPath, settings.Packages[index].PackageName, 1000, PackNext);
                }
                else
                {
                    SaveSettings(settings);
                    onfinsh?.Invoke();
                    Debug.Log("Finished all shaders collect.");
                }

                void InsureExist(string path)
                {
                    var target = group.Collectors.Find(c => c.CollectPath == path);
                    if (target is null)
                    {
                        group.Collectors.Add(new AssetBundleCollector()
                        {
                            CollectPath = path,
                            CollectorGUID = AssetDatabase.AssetPathToGUID(path),
                            CollectorType = ECollectorType.MainAssetCollector,
                            AddressRuleName = "AddressByFileNameAndExt",
                            PackRuleName = "PackDirectory",
                            FilterRuleName = "CollectAll",
                        });
                    }
                }
            }
        }

        private static void RemoveAllShadervariants(ref AssetBundleCollectorSetting settings)
        {
            foreach (var package in settings.Packages)
                foreach (var group in package.Groups)
                {
                    for (int i = 0; i < group.Collectors.Count; i++)
                    {
                        var collector = group.Collectors[i];
                        if (string.IsNullOrEmpty(collector.CollectPath) || collector.CollectPath.EndsWith(".shadervariants"))
                            group.Collectors.Remove(collector);
                    }
                }

            SaveSettings(settings);
        }

        private static void SaveSettings(AssetBundleCollectorSetting settings)
        {
            UnityEditor.EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            AssetDatabase.Refresh();
        }
    }
}