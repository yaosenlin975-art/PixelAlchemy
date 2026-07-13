namespace Lin.Editor.FrameworkSynchronizer
{
    public struct AssetInfos
    {
        public string relativePath;
        public string md5;
        public string guid;

        public enum EAssetState
        {
            Same,
            New,
            Replace,
        }
    }
}
