/*
┌────────────────────────────┐
│　Description: 图集打包窗口
│　Remark: https://blog.csdn.net/u014794120/article/details/102837879/
└────────────────────────────┘
*/
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEditor.U2D;
using Sirenix.OdinInspector.Editor;
using Lin.Runtime.Helper;
using Newtonsoft.Json;

namespace Lin.Editor.SpriteTool
{
    public class PackagerWindow : OdinEditorWindow
    {
        [MenuItem("Lin/SpriteAtlas Packager")]
        static void Init()
        {
            var window = GetWindow<PackagerWindow>();
            window.ReadConfig();
            window.Show();
        }

        #region --------- 打包 ---------

        [Button("打包图集"), ButtonGroup, EnableIf("Savable")]
        void PackSpriteAtlas()
        {
            DirectoryInfo[] directs = config.GetDirectoryInfos();
            if (!Directory.Exists(config.outputPath))
                Directory.CreateDirectory(config.outputPath);

            for (int i = 0; i < directs.Length; i++)
            {
                var dirInfo = directs[i];
                var dir = $"Assets/{config.paths[i]}";
                string output = GetOutputPath(dirInfo.Name);

                List<Object> assets = FilterAssets(dir, config.packType);
                if (assets.Count < 1)
                    continue;

                UnityEngine.U2D.SpriteAtlas atlas = GenerateAtlas();
                atlas.Add(assets.ToArray());    //将资源加入图集

                AssetDatabase.DeleteAsset(output);
                AssetDatabase.CreateAsset(atlas, output);

                AssetImporter importer = AssetImporter.GetAtPath(output.Substring(output.IndexOf("Asset")));
                importer.assetBundleName = "sprite";

                Debug.Log($"创建图集 {dirInfo.Name}, 路径为: {output}");
            }

            AssetDatabase.Refresh();

            string GetOutputPath(string name) => $"{config.outputPath}/{name}.spriteatlas";

            //生成图集
            UnityEngine.U2D.SpriteAtlas GenerateAtlas()
            {
                // 设置参数 可根据项目具体情况进行设置
                SpriteAtlasPackingSettings packSetting = new SpriteAtlasPackingSettings()
                {
                    blockOffset = 1,
                    enableRotation = false,
                    enableTightPacking = false,
                    padding = 2,
                };

                SpriteAtlasTextureSettings textureSetting = new SpriteAtlasTextureSettings()
                {
                    readable = false,
                    generateMipMaps = false,
                    sRGB = true,
                    filterMode = FilterMode.Bilinear,
                };

                UnityEngine.U2D.SpriteAtlas atlas = new UnityEngine.U2D.SpriteAtlas();
                atlas.SetPackingSettings(packSetting);
                atlas.SetTextureSettings(textureSetting);

                TextureImporterPlatformSettings textureCompressDefault = new TextureImporterPlatformSettings();
                textureCompressDefault.overridden = false;
                textureCompressDefault.name = "DefaultTexturePlatform";
                textureCompressDefault.textureCompression = TextureImporterCompression.Compressed;
                textureCompressDefault.compressionQuality = (int)TextureCompressionQuality.Best;
                atlas.SetPlatformSettings(textureCompressDefault);

                TextureImporterPlatformSettings textureCompressIOS = new TextureImporterPlatformSettings();
                textureCompressIOS.name = "iPhone";
                textureCompressIOS.overridden = true;
                textureCompressIOS.textureCompression = TextureImporterCompression.Compressed;
                textureCompressIOS.format = TextureImporterFormat.ASTC_6x6;
                textureCompressIOS.compressionQuality = (int)TextureCompressionQuality.Best;
                atlas.SetPlatformSettings(textureCompressIOS);

                TextureImporterPlatformSettings textureCompressAndroid = new TextureImporterPlatformSettings();
                textureCompressAndroid.name = "Android";
                textureCompressAndroid.overridden = true;
                textureCompressAndroid.textureCompression = TextureImporterCompression.Compressed;
                textureCompressAndroid.format = TextureImporterFormat.ASTC_6x6;
                textureCompressAndroid.compressionQuality = (int)TextureCompressionQuality.Best;
                atlas.SetPlatformSettings(textureCompressAndroid);

                atlas.SetIncludeInBuild(true);
                atlas.SetIsVariant(false);
                return atlas;
            }

            //获取路径下的图资源
            List<Object> FilterAssets(string folderPath, PackType packType)
            {
                List<Object> objects = new List<Object>();
                DefaultAsset folderAsset = AssetDatabase.LoadAssetAtPath(folderPath, typeof(DefaultAsset)) as DefaultAsset;
                switch (packType)
                {
                    case PackType.DeepAssets:
                        string[] assetsGUIDs = AssetDatabase.FindAssets("t:texture", new string[] { folderPath });
                        foreach (var guid in assetsGUIDs)
                        {
                            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
                            if (sp != null)
                                objects.Add(sp);
                        }
                        break;

                    case PackType.Assets:
                        if (Directory.Exists(folderPath))
                        {
                            DirectoryInfo dir = new DirectoryInfo(folderPath);
                            FileInfo[] files = dir.GetFiles("*", SearchOption.TopDirectoryOnly);
                            foreach (var fi in files)
                            {
                                string spritePath = FullPath2Relative(fi.FullName);
                                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                                if (sp != null)
                                    objects.Add(sp);
                            }
                        }
                        break;

                    case PackType.Folder:
                        if (folderAsset != null)
                            objects.Add(folderAsset);
                        break;

                    default:
                        if (folderAsset != null)
                            objects.Add(folderAsset);
                        break;
                }
                return objects;

                string FullPath2Relative(string fullPath)
                {
                    string relativePath = fullPath.Substring(fullPath.IndexOf("Assets"));
                    relativePath = relativePath.Replace("\\", "/");
                    return relativePath;
                }
            }
        }

        #endregion

        #region --------- 配置文件 ---------

        const string CONFIG_PATH = "Assets/Plugins/Lin/Editor/SpriteAtlas/config.txt";
        [SerializeField, HideLabel] Config config;

        void ReadConfig()
        {
            if (!File.Exists(CONFIG_PATH))
            {
                config = new Config();
                IOHelper.InsureExist(Path.GetDirectoryName(CONFIG_PATH), false, false);
                File.WriteAllText(CONFIG_PATH, JsonConvert.SerializeObject(config));
            }
            else
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(CONFIG_PATH));

        }

        bool Savable => config.Savable();

        [Button("保存配置"), ButtonGroup, EnableIf("Savable")]
        void SaveConfig() => File.WriteAllText(CONFIG_PATH, JsonConvert.SerializeObject(config));

        [System.Serializable]
        class Config
        {
            [FolderPath(ParentFolder = "Assets"), LabelText("图片文件夹路径")]
            public List<string> paths;

            [FolderPath, LabelText("输出路径")]
            public string outputPath;

            [LabelText("图集类型"), EnumToggleButtons]
            public PackType packType;

            public bool Savable() => paths.Find(s => s.Length == 0) is null && !string.IsNullOrEmpty(outputPath);

            public DirectoryInfo[] GetDirectoryInfos()
            {
                var result = new DirectoryInfo[paths.Count];
                for (int i = 0; i < paths.Count; i++)
                    result[i] = new DirectoryInfo(paths[i]);
                return result;
            }
        }

        public enum PackType
        {
            DeepAssets = 0, //指定路径下所有Sprite资源, 包含子文件夹
            Assets = 1,     //指定路径下所有Sprite资源, 不包含子文件夹
            Folder = 2,     //打包一个Folder
        }

        #endregion
    }
}