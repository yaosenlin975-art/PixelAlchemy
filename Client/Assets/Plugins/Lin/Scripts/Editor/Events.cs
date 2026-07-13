/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/

using System.Collections.Generic;

namespace Lin.Editor
{
    // -------------------- Build --------------------
    public struct BeforeBuildPackagesEvent { }

    public struct BeforeBuildPackageEvent
    {
        public string packageName;
    }

    public struct AfterBuildPackageEvent
    { 
        public string packageName;
        public bool isSuccessed;
    }

    public struct AfterBuildPackagesEvent
    {
        public Dictionary<string, bool> results;
    }
}
