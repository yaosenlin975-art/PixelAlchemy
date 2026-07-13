/*
┌────────────────────────────┐
│　Description: 懒汉模式资源加载器
│　Remark: 
└────────────────────────────┘
*/

using Cysharp.Threading.Tasks;
using Lin.Runtime.Resource;
using UnityEngine;

namespace Lin.Runtime.Tool
{
    public class LazyAsset<T> where T : Object
    {
        private readonly string assetPath;
        private T asset;
        private UniTaskCompletionSource<T> loadTcs;

        public LazyAsset(string assetPath)
        {
            this.assetPath = assetPath;
        }

        public async UniTask<T> GetPrefabAsync()
        {
            if (asset != null)
                return asset;

            if (loadTcs != null)
                return await loadTcs.Task;

            loadTcs = new UniTaskCompletionSource<T>();

            try
            {
                asset = await ResLoader.LoadAssetAsync<T>(assetPath);

                var tcs = loadTcs;
                loadTcs = null;
                tcs.TrySetResult(asset);
            }
            catch (System.Exception ex)
            {
                var tcs = loadTcs;
                loadTcs = null;
                tcs?.TrySetException(ex);
                throw;
            }

            return asset;
        }

        public T GetPrefab()
        {
            if (asset != null)
                return asset;

#if UNITY_WEBGL
            // WebGL: UniTask不依赖SynchronizationContext，GetAwaiter().GetResult()不会死锁
            // 但会阻塞主线程直到资源加载完成，属于可接受的同步阻塞
            return GetPrefabAsync().GetAwaiter().GetResult();
#else
            asset = ResLoader.LoadAsset<T>(assetPath);
            return asset;
#endif
        }

        public async UniTask<T> InstantiateAsync()
        {
            var prefab = await GetPrefabAsync();
            var opt = Object.InstantiateAsync(prefab);
            await opt;
            return opt.Result[0];
        }

        public T Instantiate()
        {
#if UNITY_WEBGL
            // WebGL: 同GetPrefab()，阻塞主线程等待实例化完成
            return InstantiateAsync().GetAwaiter().GetResult();
#else
            return Object.Instantiate(GetPrefab());
#endif
        }
    }
}
