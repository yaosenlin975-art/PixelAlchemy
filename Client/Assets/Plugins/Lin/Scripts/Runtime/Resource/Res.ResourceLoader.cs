/*
┌────────────────────────────┐
│　Description: YooAsset管理器
│　Remark: 
└────────────────────────────┘
*/
using UnityEngine;
using YooAsset;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;
using Lin.Runtime.Helper;
using System.Collections;
using System;
#if !UNITY_EDITOR
using System.IO;
using UnityEngine.SceneManagement;
#endif

namespace Lin.Runtime.Resource
{
    public sealed partial class ResLoader
    {
        private static object packagesLocker = new object();
        public static string cdnServer => GlobalConfig_SO.GetInstance().usableCDN;
        public static string version;

        public static async UniTask InitializeAsync()
        {
            // 初始化资源系统
            YooAssets.Initialize(new Logger());
#if UNITY_WEBGL
            // 设置异步处理单帧消耗最大时间切片（单位：毫秒）
            YooAssets.SetOperationSystemMaxTimeSlice(100);
#endif
            await UniTask.WaitUntil(() => YooAssets.Initialized);
        }

        public static void UnloadUnusedAssets()
        {
            lock (packagesLocker)
            {
                foreach (var package in subpackages.Values)
                {
                    package.UnloadUnusedAssets();
                    // package.ClearUnusedCacheFilesAsync().GetAwaiter();
                }
            }

            Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        /// <returns> 资源在什么包里 </returns>
        public static Subpackage CheckLocationValid(string location)
        {
            lock (packagesLocker)
            {
                foreach (var sub in subpackages.Values)
                {
                    if (sub.CheckLocationValid(location))
                        return sub;
                }

                throw new System.Exception($"{location} is not valid.\tsubpackages count: {subpackages.Count}");
            }
        }

        #region - Sync -

        public static T LoadAsset<T>(string path, string packageName, bool instantiate = true) where T : Object
        {
            if (TryGetPackage(packageName, out var package))
                return package.LoadAsset<T>(path, instantiate);
            else
                throw new Exception($"{packageName} is not exist.");
        }

        public static T LoadAsset<T>(string path, bool instantiate = false) where T : Object
        {
            var package = CheckLocationValid(path);
            if (package is null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                    Debug.LogError($"无法找到 {path}, 尝试以AssetDataBase加载！");

                var result = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (result == null)
                    return null;

                return instantiate ? Object.Instantiate(result) : result;
#else
                Debug.LogError($"无法找到 {Path.GetFileNameWithoutExtension(path)}");
                return null;
#endif
            }
            return package.LoadAsset<T>(path, instantiate);
        }

        public static AnimationClip LoadAnimationClip(string path, string packageName) => LoadAsset<AnimationClip>(path, packageName);

        public static AudioClip LoadAudioClip(string path, string packageName) => LoadAsset<AudioClip>(path, packageName);

        public static TextAsset LoadTextAsset(string path, string packageName) => LoadAsset<TextAsset>(path, packageName);

        public static GameObject LoadGameObject(string path, string packageName) => LoadAsset<GameObject>(path, packageName);

        public static GameObject LoadPrefab(string path, string packageName) => LoadAsset<GameObject>(path, packageName, false);

        public static Sprite LoadSprite(string path, string packageName) => LoadAsset<Sprite>(path, packageName);

        public static AnimationClip LoadAnimationClip(string path) => LoadAsset<AnimationClip>(path);

        public static AudioClip LoadAudioClip(string path) => LoadAsset<AudioClip>(path);

        public static TextAsset LoadTextAsset(string path) => LoadAsset<TextAsset>(path);

        public static GameObject LoadGameObject(string path) => LoadAsset<GameObject>(path, true);

        public static GameObject LoadPrefab(string path) => LoadAsset<GameObject>(path);

        public static Sprite LoadSprite(string path) => LoadAsset<Sprite>(path);

        public static T LoadAssetFromResources<T>(string path, bool instantiate = true) where T : Object
        {
            var prefab = Resources.Load<T>(path);
            if (prefab.IsGameObject() && instantiate)
            {
                var result = Object.Instantiate(prefab);
                return result;
            }

            return prefab;
        }

        #endregion

        #region - UniTask -

        public static async UniTask<T> LoadAssetAsync<T>(string path, uint priority = 0, bool instantiate = false) where T : Object
        {
            var valid = CheckLocationValid(path);
#if UNITY_EDITOR
            if (valid is null)
                return LoadAsset<T>(path, instantiate);
#endif
            return await valid.LoadAssetAsync<T>(path, priority, instantiate);
        }

        public static async UniTask<T> LoadAssetAsync<T>(string path, string packageName, uint priority = 0, bool instantiate = true) where T : Object
        {
            var package = await AddOrGetPackageAsync(packageName, cdnServer);
            return await package.LoadAssetAsync<T>(path, priority, instantiate);
        }

        public static async UniTask<AnimationClip> LoadAnimationClipAsync(string path, string packageName) => await LoadAssetAsync<AnimationClip>(path, packageName);

        public static async UniTask<AudioClip> LoadAudioClipAsync(string path, string packageName) => await LoadAssetAsync<AudioClip>(path, packageName);

        public static async UniTask<TextAsset> LoadTextAssetAsync(string path, string packageName) => await LoadAssetAsync<TextAsset>(path, packageName);

        public static async UniTask<GameObject> LoadGameObjectAsync(string path, string packageName) => await LoadAssetAsync<GameObject>(path, packageName);

        public static async UniTask<GameObject> LoadPrefabAsync(string path, string packageName, uint priority = 0) => await LoadAssetAsync<GameObject>(path, packageName, priority, false);

        public static async UniTask<Sprite> LoadSpriteAsync(string path, string packageName) => await LoadAssetAsync<Sprite>(path, packageName);

        public static async UniTask LoadSceneAsync(string path, string packageName, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None, uint priority = 0)
        {
            var package = await AddOrGetPackageAsync(packageName, cdnServer);
            await package.LoadSceneAsync(path, sceneMode, physicsMode, priority);
        }

        public static async UniTask<AnimationClip> LoadAnimationClipAsync(string path) => await LoadAssetAsync<AnimationClip>(path);

        public static async UniTask<AudioClip> LoadAudioClipAsync(string path, uint priority = 0) => await LoadAssetAsync<AudioClip>(path, priority);

        public static async UniTask<TextAsset> LoadTextAssetAsync(string path, uint priority = 0) => await LoadAssetAsync<TextAsset>(path, priority);

        public static async UniTask<GameObject> LoadGameObjectAsync(string path, uint priority = 0) => await LoadAssetAsync<GameObject>(path, priority, true);

        public static async UniTask<GameObject> LoadPrefabAsync(string path, uint priority = 0) => await LoadAssetAsync<GameObject>(path, priority);

        public static async UniTask<Sprite> LoadSpriteAsync(string path, uint priority = 0) => await LoadAssetAsync<Sprite>(path, priority);

        public static async UniTask LoadSceneAsync(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, LocalPhysicsMode physicsMode = LocalPhysicsMode.None, uint priority = 0)
            => await CheckLocationValid(location).LoadSceneAsync(location, sceneMode, physicsMode, priority);

        public static async UniTask<T> LoadAssetFromResourcesAsync<T>(string path, bool instantiate = true) where T : Object
        {
            var handler = Resources.LoadAsync<T>(path);
            await handler;

            if (handler.asset.IsGameObject() && instantiate)
                return Object.Instantiate(handler.asset) as T;

            return handler.asset as T;
        }

        #endregion

        #region - Coroutine -

        public static IEnumerator LoadAssetCoroutine<T>(string location, Action<T> onComplete, uint priority = 0, bool instantiate = true) where T : Object
        {
            var valid = CheckLocationValid(location);
#if UNITY_EDITOR
            if (valid is null)
                onComplete(LoadAsset<T>(location, instantiate));

            yield return null;
#else
            yield return valid.LoadAssetCoroutine<T>(location, onComplete, priority, instantiate);
#endif
        }

        public static IEnumerator LoadAssetCoroutine<T>(string location, string packageName, Action<T> onComplete, uint priority = 0, bool instantiate = true) where T : Object
        {
            yield return AddOrGetPackageCoroutine(packageName, cdnServer);
            var package = subpackages[packageName];
            yield return package.LoadAssetCoroutine(location, onComplete, priority, instantiate);
        }

        #endregion
    }

    class Logger : YooAsset.ILogger
    {
        public void Error(string message)
        {
            Helper.Log.Error("YooAssets", message);
        }

        public void Exception(System.Exception exception)
        {
            Helper.Log.Error("YooAssets", $"{exception.Message}\\n{exception.StackTrace}");
        }

        public void Log(string message)
        {
            Helper.Log.Debug("YooAssets", message);
        }

        public void Warning(string message)
        {
            Helper.Log.Warning("YooAssets", message);
        }
    }
}
 