using Cysharp.Text;
using Lin.Runtime.DesignPattern.Singleton;
using System;
using System.Collections.Generic;
using UnityEditor;
using YooAsset.Editor;
using ZLinq;

namespace Lin.Editor.BuildTool
{
    [Serializable]
    public class AssetsCompressSettings : ArchiverSingleton<AssetsCompressSettings>
    {
        [Serializable]
        public class GroupSettings
        {
            public ETextureSize textureSize;

            public void SetGroupAssets(string key, AssetBundleCollectorGroup group)
            {
                HashSet<string> dependencies = new HashSet<string>();
                foreach (var asset in group.Collectors)
                {
                    var collectPath = asset.CollectPath;

                    if (string.IsNullOrEmpty(collectPath))
                        collectPath = AssetDatabase.GUIDToAssetPath(asset.CollectorGUID);

                    if (string.IsNullOrEmpty(collectPath))
                        continue;

                    if (AssetDatabase.IsValidFolder(collectPath))
                    {
                        var guids = AssetDatabase.FindAssets(string.Empty, new[] { collectPath });
                        foreach (var guid in guids.AsValueEnumerable())
                        {
                            var path = AssetDatabase.GUIDToAssetPath(guid);
                            dependencies.UnionWith(AssetDatabase.GetDependencies(path));
                        }
                    }
                    else
                    {
                        dependencies.UnionWith(AssetDatabase.GetDependencies(collectPath));
                    }
                }

                int count = dependencies.Count;
                int index = 0;
                if (count == 0)
                    return;

                bool changed = false;
                foreach (var path in dependencies.AsValueEnumerable())
                {
                    EditorUtility.DisplayProgressBar(ZString.Concat(key, "资源配置"), ZString.Concat(index, '/', count), (float)index / count);
                    index++;
                    if (!path.StartsWith("Assets"))
                        continue;

                    var importer = AssetImporter.GetAtPath(path);
                    switch (importer)
                    {
                        case TextureImporter textureImporter:
                            changed |= SetTextureImproter(textureImporter);
                            break;

                        default:
                            break;
                    }
                }

                EditorUtility.ClearProgressBar();

                if (changed)
                    AssetDatabase.Refresh();
            }

            private bool SetTextureImproter(TextureImporter importer)
            {
                if (textureSize == ETextureSize.NoOverride)
                    return false;

                int size;
                bool isLimit = textureSize <= ETextureSize.Max64;

                switch (textureSize)
                {
                    case ETextureSize.NoOverride:
                        return false;

                    case ETextureSize.Target8192:
                    case ETextureSize.Max8192:
                        size = 8192;
                        break;

                    case ETextureSize.Max4096:
                    case ETextureSize.Target4096:
                        size = 4096;
                        break;

                    case ETextureSize.Max2048:
                    case ETextureSize.Target2048:
                        size = 2048;
                        break;

                    case ETextureSize.Max1024:
                    case ETextureSize.Target1024:
                        size = 1024;
                        break;

                    case ETextureSize.Max512:
                    case ETextureSize.Target512:
                        size = 512;
                        break;

                    case ETextureSize.Max256:
                    case ETextureSize.Target256:
                        size = 256;
                        break;

                    case ETextureSize.Target128:
                    case ETextureSize.Max128:
                        size = 128;
                        break;

                    case ETextureSize.Max64:
                    case ETextureSize.Target64:
                        size = 64;
                        break;

                    default:
                        size = 2048;
                        break;
                }

                bool changed = false;
                SetTexutureSize();

                if (changed)
                    importer.SaveAndReimport();

                return changed;

                void SetTexutureSize()
                {
                    importer.maxTextureSize = size;
                    ApplyPlatformSize(importer, "Standalone");
                    ApplyPlatformSize(importer, "Android");
                    ApplyPlatformSize(importer, "iPhone");
                    ApplyPlatformSize(importer, "WebGL");
                }

                void ApplyPlatformSize(TextureImporter importer, string platform)
                {
                    var ps = importer.GetPlatformTextureSettings(platform);
                    int current = (ps.overridden && ps.maxTextureSize > 0) ? ps.maxTextureSize : importer.maxTextureSize;

                    if (isLimit)
                    {
                        if (current > size)
                        {
                            ps.name = platform;
                            ps.overridden = true;
                            ps.maxTextureSize = size;
                            importer.SetPlatformTextureSettings(ps);
                            changed = true;
                        }
                    }
                    else
                    {
                        if (current != size)
                        {
                            ps.name = platform;
                            ps.overridden = true;
                            ps.maxTextureSize = size;
                            importer.SetPlatformTextureSettings(ps);
                            changed = true;
                        }
                    }
                }
            }
        }

        public AssetsCompressSettings()
        {
            groupSettings = new Dictionary<string, GroupSettings>();
        }

        public Dictionary<string, GroupSettings> groupSettings;

        public GroupSettings Get(string key)
        {
            if (!groupSettings.TryGetValue(key, out var result))
            {
                result = new GroupSettings();
                groupSettings.Add(key, result);
            }

            return result;
        }

        public AssetsCompressSettings Remove(string key)
        {
            groupSettings.Remove(key);
            return this;
        }

        public AssetsCompressSettings Rename(string oldKey, string newKey)
        {
            var value = Get(oldKey);
            groupSettings.Remove(oldKey);
            groupSettings.Add(newKey, value);
            return this;
        }
    }
}
