/*
┌────────────────────────────┐
│　Description: 图集打包窗口
│　Author: 花球i
│　UpdateDate: 2022.08.25
│　Remark: https://blog.csdn.net/u014794120/article/details/102837879/
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: PackagerWindow
└──────────────┘
*/
using UnityEditor;
using Lin.Editor.Helper;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.U2D;
using UnityEditor.U2D;
using Lin.Runtime.Helper;
using System.IO;
using ZLinq;

namespace Lin.Editor.SpriteTool
{
    public static class SpriteAtlasHelper
    {
        private const string SPRITE_ATLAS_TAG = "SpriteAtlas-";

        public static void SetSpriteAtlasTag(this TextureImporter self, string tag)
        {
            //原本存在tag
            string currentTag = self.GetSpriteAtlasTag();
            if (currentTag is not null)
                self.RemoveUserTag(currentTag, false);

            self.AddUserTag($"{SPRITE_ATLAS_TAG}{tag}", true);
        }

        public static string GetSpriteAtlasTag(this TextureImporter self)
        {
            var userTags = self.GetUserTags();
            foreach (var tag in userTags)
            {
                if (!tag.Contains(SPRITE_ATLAS_TAG))
                    continue;

                return tag.Replace(SPRITE_ATLAS_TAG, string.Empty);
            }
            return null;
        }

        /// <summary>
        /// 打包特定的
        /// </summary>
        /// <param name="images"></param>
        /// <param name="atlasName"></param>
        public static bool PackSpriteAtlas(HashSet<TextureImporter> images, string atlasName)
        {
            var toPacks = images.AsValueEnumerable().Where(c => c != null && string.IsNullOrEmpty(c.GetSpriteAtlasTag()));

            if (toPacks.Count() == 0)
                return false;

            foreach (var importer in toPacks)
                importer.SetSpriteAtlasTag(atlasName);

            string outputPath = $"{GlobalConfig_SO.GetInstance().prefabDirectory}/SpriteAtlas/{atlasName}.spriteatlas";
            SpriteAtlas spriteAtlas;

            if (!File.Exists(outputPath))
            {
                IOHelper.InsureExist(Path.GetDirectoryName(outputPath), false, false);
                spriteAtlas = CreateSpriteAtlas();
                AssetDatabase.CreateAsset(spriteAtlas, outputPath);
            }
            else
                spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(outputPath);

            var exists = SpriteAtlasExtensions.GetPackables(spriteAtlas);
            HashSet<string> existPaths = new HashSet<string>();
            foreach (var e in exists.AsValueEnumerable())
            {
                var p = AssetDatabase.GetAssetPath(e);
                if (!string.IsNullOrEmpty(p))
                    existPaths.Add(p);
            }

            List<Object> toAdd = new List<Object>();
            foreach (var importer in toPacks)
            {
                var p = importer.assetPath;
                if (string.IsNullOrEmpty(p))
                    continue;

                if (existPaths.Contains(p))
                    continue;

                var obj = AssetDatabase.LoadAssetAtPath<Object>(p);
                if (obj != null)
                    toAdd.Add(obj);
            }

            if (toAdd.Count > 0)
            {
                SpriteAtlasExtensions.Add(spriteAtlas, toAdd.ToArray());
                EditorUtility.SetDirty(spriteAtlas);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(outputPath);
                Log.Debug(nameof(SpriteAtlasHelper), $"生成图集 {atlasName}", AssetImporter.GetAtPath(outputPath));
            }
            else
                Log.Debug(nameof(SpriteAtlasHelper), $"图集内容为空, 跳过生成 {atlasName}");

            return true;
        }

        //生成初始图集
        public static SpriteAtlas CreateSpriteAtlas()
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

            SpriteAtlas atlas = new SpriteAtlas();
            atlas.SetPackingSettings(packSetting);
            atlas.SetTextureSettings(textureSetting);

            TextureImporterPlatformSettings textureCompressDefault = new TextureImporterPlatformSettings
            {
                overridden = false,
                name = "DefaultTexturePlatform",
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = (int)TextureCompressionQuality.Best
            };
            atlas.SetPlatformSettings(textureCompressDefault);

            TextureImporterPlatformSettings textureCompressIOS = new TextureImporterPlatformSettings
            {
                name = "iPhone",
                overridden = true,
                textureCompression = TextureImporterCompression.Compressed,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = (int)TextureCompressionQuality.Best
            };
            atlas.SetPlatformSettings(textureCompressIOS);

            TextureImporterPlatformSettings textureCompressAndroid = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                textureCompression = TextureImporterCompression.Compressed,
                format = TextureImporterFormat.ASTC_6x6,
                compressionQuality = (int)TextureCompressionQuality.Best
            };
            atlas.SetPlatformSettings(textureCompressAndroid);

            atlas.SetIncludeInBuild(true);
            atlas.SetIsVariant(false);

            return atlas;
        }
    }
}
