using Lin.Runtime.DesignPattern.Singleton;
using System;
using System.Collections.Generic;

namespace Lin.Editor.FrameworkSynchronizer
{
    [Serializable]
    class SynchronizerConfig : ArchiverSingleton<SynchronizerConfig>
    {
        public Dictionary<string, bool> plugins = new Dictionary<string, bool>();
        public Dictionary<string, bool> toolProejcts = new Dictionary<string, bool>();
        public HashSet<string> extraAssets = new HashSet<string>();
        public HashSet<string> ignoreAssets = new HashSet<string>();
        public Dictionary<string, ESyncOperationType> targetProejcts = new Dictionary<string, ESyncOperationType>();

        public enum ESyncOperationType
        {
            不操作,
            自动替换,
            手动选择,
        }
    }
}
