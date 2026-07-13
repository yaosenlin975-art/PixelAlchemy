/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using Cysharp.Threading.Tasks;
using Lin.Runtime.Helper;
using Lin.Runtime.Resource;
using System.IO;
using UnityEngine;

namespace Lin.Runtime.Attribute
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
    public class AssetPathAttribute : System.Attribute
    {
        public AssetPathAttribute(string assetPath, ELoaderType loaderType = ELoaderType.AssetBundle, int priority = 0)
        {
            AssetPath = assetPath;
            LoaderType = loaderType;
            Priority = priority;
        }

        public string AssetPath { get; }
        public ELoaderType LoaderType { get; }
        public int Priority { get; }

        /// <summary>
        /// 同步加载资源
        /// WebGL 平台依赖 WebGLForceSyncLoadAsset = true，由 YooAsset 内部通过 WaitForAsyncComplete 实现
        /// </summary>
        public T Load<T>(bool instantiate = false) where T : Object
        {
            T prefab = null;
#if UNITY_EDITOR
            // Editor中可能要进行修改, 因此需要用AssetDatabase
            var guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).FullName}");

            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains(AssetPath))
                {
                    if (LoaderType == ELoaderType.AssetBundle && ResLoader.CheckLocationValid(AssetPath) is null)
                        this.Error($"{AssetPath} 未被YooAssetCollector收集");

                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                    if (prefab != null)
                        break;
                }
            }
            if (prefab == null)
#endif
            prefab = LoaderType == ELoaderType.Resources ? Resources.Load<T>(AssetPath) : ResLoader.LoadAsset<T>(AssetPath);
            if (prefab == null)
                throw new FileNotFoundException(AssetPath);
            else if (prefab.IsGameObject() && instantiate)
                return Object.Instantiate(prefab);

            return prefab;
        }

        public async UniTask<T> LoadAsync<T>(bool instantiate = false) where T : Object
        {
#if UNITY_EDITOR && !UNITY_WEBGL
            await UniTask.CompletedTask;
            return Load<T>(instantiate);
#else
            if (LoaderType == ELoaderType.Resources)
            {
                var loadOpt = Resources.LoadAsync<T>(AssetPath);
                await loadOpt;
                T prefab = loadOpt.asset as T;
                if (prefab == null)
                    throw new FileNotFoundException(AssetPath);
                else if (prefab.IsGameObject() && instantiate)
                {
                    var insOpt = Object.InstantiateAsync(prefab);
                    await insOpt;
                    return insOpt.Result[0];
                }
                return prefab;
            }
            else
                return await ResLoader.LoadAssetAsync<T>(AssetPath, instantiate: instantiate);
#endif
        }

        public enum ELoaderType
        {
            Resources,
            AssetBundle,
#if UNITY_EDITOR
            AssetDatabase
#endif
        }
    }
}
