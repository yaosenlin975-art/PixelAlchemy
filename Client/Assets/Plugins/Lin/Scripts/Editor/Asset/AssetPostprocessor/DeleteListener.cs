/*
┌────────────────────────────┐
│　Description: 监听文件删除
│　Remark: 
└────────────────────────────┘
*/

using UnityEditor;

namespace Lin.Editor.Asset
{
    public class DeleteListener : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var deleted in deletedAssets)
            {
                var guid = AssetDatabase.AssetPathToGUID(deleted);

                //删除注释
                AssetSummaryArchiver.GetInstance().RemoveDescription(guid);
            }
        }
    }
}
