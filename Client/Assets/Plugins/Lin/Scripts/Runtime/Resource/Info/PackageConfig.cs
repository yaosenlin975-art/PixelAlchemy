/*
┌────────────────────────────┐
│　Description: 资源包更新信息
└────────────────────────────┘
*/
using System;
using Sirenix.OdinInspector;

namespace Lin.Runtime.Resource
{
    [Serializable]
    public struct PackageConfig
    {
        [ReadOnly] 
        public string packageName;
        public bool build;
        public bool isDefaultPackage;
    }
}