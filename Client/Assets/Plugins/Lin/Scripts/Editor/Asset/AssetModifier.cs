/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：AssetProcessor
└──────────────┘
*/
using UnityEngine;
using UnityEditor;
using Lin.Runtime.Helper;
using Lin.Editor.Helper;
using System.IO;

namespace Lin.Editor.Asset
{
    public static class AssetModifier
    {
        private const string MODIFIED_TAG = "ImporterModified";

        [MenuItem("Assets/优化资源设置")]
        private static void Postprocess()
        {
            string[] folders = new string[1];

            if (Selection.activeObject is null || !Directory.Exists(AssetDatabase.GetAssetPath(Selection.activeObject)))
                folders[0] = "Assets";
            else
                folders[0] = AssetDatabase.GetAssetPath(Selection.activeObject);

            foreach (var guid in AssetDatabase.FindAssets("t:Texture", folders))
                OnPostprocessTexture(UnityEditor.AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter);

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", folders))
                OnPreprocessAudio(UnityEditor.AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter);

            foreach (var guid in AssetDatabase.FindAssets("t:Model", folders))
                OnPreprocessModel(UnityEditor.AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter);
        }

        #region - 纹理 -

        public static void OnPostprocessTexture(TextureImporter importer)
        {
            if (importer == null)
                return;

            if (importer.ContainsUserTag(MODIFIED_TAG))
                return;

            if (!importer.assetPath.EndsWith("jpg") && !importer.assetPath.EndsWith("png"))
                return;

            switch (importer.textureType)
            {
                case TextureImporterType.Sprite:
                case TextureImporterType.GUI:
                    SetupUITexture(importer);
                    break;

                case TextureImporterType.NormalMap:
                    SetupNormalMap(importer);
                    break;

                case TextureImporterType.Lightmap:
                    SetupLightmap(importer);
                    break;

                case TextureImporterType.Default:
                    SetupDefaultTexture(importer);
                    break;
            }

            importer.isReadable = false; // 关闭读写权限
            importer.AddUserTag(MODIFIED_TAG);
            importer.SaveAndReimport();

            Log.Debug(nameof(AssetModifier), $"已设置 {importer.assetPath} 纹理压缩", importer);
        }

        private static void SetupUITexture(TextureImporter importer)
        {
            importer.crunchedCompression = true;
            importer.compressionQuality = 60;
            importer.mipmapEnabled = false;
            SetPlatformSpecificSettings(importer);
        }

        private static void SetupNormalMap(TextureImporter importer)
        {
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            importer.streamingMipmaps = true;
            importer.sRGBTexture = false;

            // WebGL平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL",
                format = TextureImporterFormat.DXT5, // WebGL平台使用DXT5压缩
                maxTextureSize = 1024,
                overridden = true,
            });

            // Android平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                format = TextureImporterFormat.ASTC_4x4, // 法线图需要更高精度
                maxTextureSize = 1024,
                overridden = true,
            });

            // iOS平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "iPhone",
                format = TextureImporterFormat.ASTC_4x4, // 法线图需要更高精度
                maxTextureSize = 1024,
                overridden = true,
            });


            // PC平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone",
                format = TextureImporterFormat.BC5, // 法线图需要更高精度
                maxTextureSize = 2048,
                overridden = true,
            });
        }

        private static void SetupLightmap(TextureImporter importer)
        {
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;

            // WebGL平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                overridden = true,
                maxTextureSize = 2048,
                name = "WebGL",
                format = TextureImporterFormat.DXT1 // WebGL平台使用DXT1压缩
            });

            // Android平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                overridden = true,
                maxTextureSize = 2048,
                name = "Android",
                format = TextureImporterFormat.ASTC_8x8 // 光照图可以用更高压缩比
            });

            // iOS平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                overridden = true,
                maxTextureSize = 2048,
                name = "iPhone",
                format = TextureImporterFormat.ASTC_8x8 // 光照图可以用更高压缩比
            });

            // PC平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                overridden = true,
                maxTextureSize = 4096,
                name = "Standalone",
                format = TextureImporterFormat.BC6H // 光照图可以用更高压缩比
            });
        }

        private static void SetupDefaultTexture(TextureImporter importer)
        {
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            SetPlatformSpecificSettings(importer);
        }

        private static void SetPlatformSpecificSettings(TextureImporter importer)
        {
            bool doesSourceTextureHaveAlpha = importer.DoesSourceTextureHaveAlpha();
            bool isPowerOfTwo = IsPowerOfTwo(importer);
            bool divisible4 = IsDivisibleOf4(importer);

            // WebGL平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                overridden = true,
                maxTextureSize = 2048,
                name = "WebGL",
                format = TextureImporterFormat.DXT5
            });

            // Android平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                overridden = true,
                maxTextureSize = 2048,
                name = "Android",
                format = doesSourceTextureHaveAlpha ?
                (divisible4 ? TextureImporterFormat.ETC2_RGBA8Crunched : TextureImporterFormat.ASTC_4x4) :
                (divisible4 ? TextureImporterFormat.ETC_RGB4Crunched : TextureImporterFormat.ASTC_6x6)
            });

            // iOS平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                overridden = true,
                maxTextureSize = 2048,
                name = "iPhone",
                format = doesSourceTextureHaveAlpha ?
                (isPowerOfTwo ? TextureImporterFormat.PVRTC_RGBA4 : TextureImporterFormat.ASTC_4x4) :
                (isPowerOfTwo ? TextureImporterFormat.PVRTC_RGB4 : TextureImporterFormat.ASTC_6x6)
            });

            // PC平台
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                overridden = true,
                maxTextureSize = 4096,
                name = "Standalone",
                format = TextureImporterFormat.BC7 // 光照图可以用更高压缩比
            });
        }

        //被4整除
        private static bool IsDivisibleOf4(TextureImporter importer)
        {
            (int width, int height) = GetTextureImporterSize(importer);
            return (width % 4 == 0 && height % 4 == 0);
        }

        //2的整数次幂
        private static bool IsPowerOfTwo(TextureImporter importer)
        {
            (int width, int height) = GetTextureImporterSize(importer);
            return (width == height) && (width > 0) && ((width & (width - 1)) == 0);
        }

        //获取导入图片的宽高
        public static (int, int) GetTextureImporterSize(TextureImporter importer)
        {
            if (importer != null)
            {
                object[] args = new object[2];
                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                return (width, height);
            }
            return (0, 0);
        }

        #endregion

        #region - 音效 -

        public static void OnPreprocessAudio(AudioImporter importer)
        {
            if (importer == null)
                return;

            if (importer.ContainsUserTag(MODIFIED_TAG))
                return;

            string assetPath = importer.assetPath;

            //默认视为通用音效
            var settings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.DecompressOnLoad,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.5f
            };

            //背景音
            if (assetPath.Contains("BGM") || assetPath.Contains("背景"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.quality = 0.8f;
            }
            //人物对话
            else if (assetPath.Contains("Voice"))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.quality = 0.6f;
            }

            importer.forceToMono = true;

            foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                importer.SetOverrideSampleSettings(platform, settings);

            importer.AddUserTag(MODIFIED_TAG);
            importer.SaveAndReimport();
            Log.Debug(nameof(AssetModifier), $"已设置 {assetPath} 音频压缩", importer);
        }

        #endregion

        #region - 模型 动画 -

        public static void OnPreprocessModel(ModelImporter importer)
        {
            if (importer == null)
                return;

            if (importer.ContainsUserTag(MODIFIED_TAG))
                return;

            string assetPath = importer.assetPath;

            // 基础设置
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importBlendShapes = true;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;

            // 网格压缩
            importer.meshCompression = ModelImporterMeshCompression.Medium;

            // 读写性能优化
            importer.isReadable = false;

            // 根据文件名判断类型
            if (assetPath.Contains("_Static"))
            {
                // 静态物体
                importer.animationType = ModelImporterAnimationType.None;
                importer.generateSecondaryUV = true; // 静态物体生成光照UV
            }
            else if (assetPath.Contains("_Skinned"))
            {
                // 骨骼模型
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }

            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
                clip.loopTime = clip.name.EndsWith("_Loop");

            importer.AddUserTag(MODIFIED_TAG);
            importer.SaveAndReimport();
            Log.Debug(nameof(AssetModifier), $"已设置 {assetPath} 模型配置", importer);
        }

        #endregion
    }
}