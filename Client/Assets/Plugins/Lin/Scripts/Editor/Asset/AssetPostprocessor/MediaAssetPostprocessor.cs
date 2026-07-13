/*
┌────────────────────────────┐
│　Description：资源导入时快速设置配置
│　Remark：
└────────────────────────────┘
*/
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Lin.Editor.Asset
{
    public class MediaAssetPostprocessor : AssetPostprocessor
    {
        #region - 纹理 - 

        private void OnPreprocessTexture()
        {
            if (!assetImporter.assetPath.StartsWith("Assets"))
                return;

            TextureImporter importer = assetImporter as TextureImporter;
            if (importer != null && IsFirstImport(importer))
                AssetModifier.OnPostprocessTexture(importer);
        }

        //贴图不存在、meta文件不存在、图片尺寸发生修改需要重新导入
        private static bool IsFirstImport(TextureImporter importer)
        {
            (int width, int height) = AssetModifier.GetTextureImporterSize(importer);
            Texture tex = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
            bool hasMeta = File.Exists(AssetDatabase.GetAssetPathFromTextMetaFilePath(importer.assetPath));
            return tex == null || !hasMeta || (tex.width != width && tex.height != height);
        }

        #endregion

        #region - 音效 -

        private void OnPreprocessAudio()
        {
            if (!assetImporter.assetPath.StartsWith("Assets"))
                return;

            AudioImporter importer = assetImporter as AudioImporter;
            if (importer != null && IsFirstImport(importer))
                AssetModifier.OnPreprocessAudio(importer);
        }

        private static bool IsFirstImport(AudioImporter importer)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(importer.assetPath);
            bool hasMeta = File.Exists(AssetDatabase.GetAssetPathFromTextMetaFilePath(importer.assetPath));
            return clip == null || !hasMeta;
        }

        #endregion

        #region - 模型 -

        private void OnPreprocessModel()
        {
            if (!assetImporter.assetPath.StartsWith("Assets"))
                return;

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer != null && IsFirstImport(importer))
                AssetModifier.OnPreprocessModel(importer);
        }

        private static bool IsFirstImport(ModelImporter importer)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath);
            bool hasMeta = File.Exists(AssetDatabase.GetAssetPathFromTextMetaFilePath(importer.assetPath));
            return model == null || !hasMeta;
        }

        #endregion
    }
}