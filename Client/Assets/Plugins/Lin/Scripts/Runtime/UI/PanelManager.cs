/*
┌────────────────────────────┐
│　Description: UI管理器
│　Remark: 
└────────────────────────────┘
*/
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Lin.Runtime.DesignPattern.Singleton;
using Lin.Runtime.Helper;
using Lin.Runtime.Interface;
using Lin.Runtime.Resource;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using Lin.Runtime.Manager;
using UnityEngine.Pool;
using UnityEngine.UI;
using System.Reflection;
using Lin.Runtime.Attribute;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Lin.Runtime.UI
{
    [AssetPath("UI/Canvas", AssetPathAttribute.ELoaderType.Resources)]
    public class PanelManager : MonoSingleton<PanelManager>
    {
        private RectTransform top;
        private RectTransform lowerThanTop;
        private RectTransform middle;
        private RectTransform lowerThanMiddle;
        private RectTransform buttom;

        public Canvas canvas { get; private set; }

        public RectTransform rectTransform=> canvas.transform as RectTransform;

        private Dictionary<Type, PanelBase> panels = new Dictionary<Type, PanelBase>();

        //正在展示的全屏的Panel
        private HashSet<PanelBase> fullScreenPanels = new HashSet<PanelBase>();

        private static string uiPrefabsDirectory;
        private static string PRELOAD_PANEL_CONFIG_PATH
        {
            get
            {
#if UNITY_EDITOR
                // 需要在编辑器中写入, 因此全路径
                return $"{GlobalConfig_SO.GetInstance().prefabDirectory}/UI/PreloadPanels.json";
#else
                return "PreloadPanels";
#endif
            }
        }
        private const string PATH_FORMAT = "{0}/{1}.prefab";

        //用于UI遮罩
        private ObjectPool<RawImage> coverPool;
        private Dictionary<PanelBase, RawImage> coverMap;

        protected override void Init()
        {
            uiPrefabsDirectory = ZString.Concat(GlobalConfig_SO.GetInstance().prefabDirectory, "/UI");

            //Canvas
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
                if (canvas == null)
                    canvas = (Instantiate(Resources.Load("UI/Canvas"), transform) as GameObject).GetComponent<Canvas>();
            }

            // EventSystem
            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = Instantiate(Resources.Load<GameObject>("UI/EventSystem")).GetComponent<EventSystem>();
            eventSystem.transform.parent = null;
            eventSystem.GetOrAddComponent<EventSystemHelper>();
            DontDestroyOnLoad(eventSystem.gameObject);

            // Layer
            buttom = CreatePanelParent(EPanelLayer.Buttom);
            lowerThanMiddle = CreatePanelParent(EPanelLayer.Fourth);
            middle = CreatePanelParent(EPanelLayer.Middle);
            lowerThanTop = CreatePanelParent(EPanelLayer.Second);
            top = CreatePanelParent(EPanelLayer.Top);

            coverPool = new ObjectPool<RawImage>(CreateCover, OnCoverGet, OnCoverRelease);
            coverMap = new Dictionary<PanelBase, RawImage>();

            RectTransform CreatePanelParent(EPanelLayer layer)
            {
                this.Debug($"Create Layer.{layer}");

                var gameObject = new GameObject(layer.ToString());
                gameObject.transform.SetParent(canvas.transform, false);
                var result = gameObject.AddComponent<RectTransform>();
                result.anchorMin = Vector2.zero;
                result.anchorMax = Vector2.one;

                result.offsetMax = Vector2.zero;
                result.offsetMin = Vector2.zero;
                return result;
            }
        }

        public async UniTask<T> ShowAsync<T>(bool immeditely = false) where T : PanelBase
        {
            var result = await GetAsync<T>();
            await result.ShowAsync(immeditely);
            return result;
        }

        public T Show<T>(bool immeditely = false) where T : PanelBase
        {
            var result = Get<T>();
            result.Show(immeditely);
            return result;
        }

        public async UniTask<TPanel> ShowAsync<TPanel, TArg>(TArg arg, bool immeditely = false) where TPanel : PanelBase, IShow<TArg>
        {
            var result = await GetAsync<TPanel>();
            result.Show(arg, immeditely);
            return result;
        }

        public TPanel Show<TPanel, TArg>(TArg arg, bool immeditely = false) where TPanel : PanelBase, IShow<TArg>
        {
            var result = Get<TPanel>();
            result.Show(arg, immeditely);
            return result;
        }

        /// <summary>
        /// UI不存在时从 Prefabs/UI 加载
        /// </summary>
        public async UniTask<T> GetAsync<T>(bool forceLoad = true) where T : PanelBase
        {
            if ((!panels.TryGetValue(typeof(T), out var result) || result == null) && forceLoad)
                result = await LoadAsync<T>();

            return result as T;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="forceLoad">为True时, 当UI为加载会强制加载</param>
        /// <returns></returns>
        public T Get<T>(bool forceLoad = true) where T : PanelBase
        {
            if ((!panels.TryGetValue(typeof(T), out var result) || result == null) && forceLoad) 
                result = Load<T>();

            return result as T;
        }

        public string GetPanelPath(string name) => ZString.Format(PATH_FORMAT, uiPrefabsDirectory, name);

        /// <summary>
        /// UI不存在时从 path 加载
        /// </summary>
        private async UniTask<T> LoadAsync<T>() where T : PanelBase
        {
            var key = typeof(T);
            if (!panels.ContainsKey(key))
            {
                panels.Add(key, null);

                GameObject panelObject;
                var assetPath = key.GetCustomAttribute<AssetPathAttribute>();
                if (assetPath != null)
                    panelObject = await assetPath.LoadAsync<GameObject>(true);
                else
                    panelObject = await ResLoader.LoadGameObjectAsync(key.Name);

                panels[key] = panelObject.GetComponent<T>();
            }

            while (panels[key] == null)
                await UniTask.Yield();

            var result = panels[key] as T;

            //用于显示preload的panel
            if (!result.gameObject.activeSelf)
                result.gameObject.SetActive(true);

            return result;
        }

        private T Load<T>() where T : PanelBase
        {
            var key = typeof(T);
            if (!panels.ContainsKey(key))
            {
                GameObject panelObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetPanelPath(typeof(T).Name));
                    if (panelPrefab != null)
                    {
                        panelObject = Instantiate(panelPrefab);
                        panels[key] = panelObject.GetComponent<T>();

                        return panels[key] as T;
                    }
                }
#endif
                var assetPath = key.GetCustomAttribute<AssetPathAttribute>();
                if (assetPath != null)
                    panelObject =
#if !UNITY_WEBGL
                        assetPath.Load<GameObject>(true);
#else
                        assetPath.LoadAsync<GameObject>(true).GetAwaiter().GetResult();
#endif
                else
                    panelObject = ResLoader.LoadGameObject(key.Name);

                panels[key] = panelObject.GetComponent<T>();
            }
            return panels[key] as T;
        }

        public void Hide<T>(bool immeditely = false) where T : PanelBase => HideAsync<T>(immeditely).Forget();

        public async UniTask HideAsync<T>(bool immeditely = false) where T : PanelBase
        {
            if (panels.ContainsKey(typeof(T)))
                 await (await GetAsync<T>()).HideAsync(immeditely);
        }

        public void Register(PanelBase panel)
        {
            var panelType = panel.GetType();
            if (!panels.ContainsKey(panelType))
                panels.Add(panelType, panel);

            switch (panel.PanelLayer)
            {
                case EPanelLayer.Top:
                    panel.transform.SetParent(top, false);
                    break;

                case EPanelLayer.Second:
                    panel.transform.SetParent(lowerThanTop, false);
                    break;

                case EPanelLayer.Middle:
                    panel.transform.SetParent(middle, false);
                    break;

                case EPanelLayer.Fourth:
                    panel.transform.SetParent(lowerThanMiddle, false);
                    break;

                case EPanelLayer.Buttom:
                default:
                    panel.transform.SetParent(buttom, false);
                    break;
            }
        }

        public void Deregister(PanelBase panel)
        {
            Uncover(panel);
            panels.Remove(panel.GetType());
        }

        public void AddFullScreenPanel(PanelBase panel)
        {
            if (fullScreenPanels.Add(panel))
                CameraController.GetInstance().OnFullScreenPanelStateChanged(true);
        }

        public void RemoveFullScreenPanel(PanelBase panel)
        {
            if (fullScreenPanels.Remove(panel))
                CameraController.GetInstance().OnFullScreenPanelStateChanged(fullScreenPanels.Count > 0);
        }

        public async UniTask PreloadPanels()
        {
            var package = ResLoader.CheckLocationValid(PRELOAD_PANEL_CONFIG_PATH);
            if (package == null)
                return;

            var config = (await package.LoadTextAssetAsync(PRELOAD_PANEL_CONFIG_PATH)).text;
            var map = JsonConvert.DeserializeObject<Dictionary<string, bool>>(config);
            foreach (var pair in map)
            {
                var panel = await ResLoader.LoadGameObjectAsync(pair.Key);

                if (!pair.Value)
                    panel.SetActive(pair.Value);

                this.Debug(ZString.Format("预加载Panel: {0}, 激活状态: {1}", pair.Key, pair.Value));
            }
        }

        #region - 遮罩 -
        /// <summary>
        /// 遮罩作用域句柄：用于 using 模式自动回收遮罩
        /// 用法示例：
        /// using (PanelManager.GetInstance().CoverScoped(panel))
        ///     await DoAsync();
        /// 说明：
        /// - 构造时创建并注册遮罩（如未被其它逻辑覆盖）
        /// - Dispose 时自动调用 Uncover（仅当本作用域负责创建遮罩时）
        /// - 暴露 image 便于在作用域内调整颜色等属性
        /// </summary>
        public sealed class CoverScope : IDisposable
        {
            private readonly PanelBase panel;
            private readonly bool owned;
            public RawImage image { get; }

            internal CoverScope(PanelBase panel, RawImage image, bool owned)
            {
                this.panel = panel;
                this.image = image;
                this.owned = owned;
            }

            public void Dispose()
            {
                if (!owned)
                    return;

                GetInstance().Uncover(panel);
            }
        }

        private void OnCoverRelease(RawImage image)
        {
            image.transform.localScale = Vector3.zero;
            image.transform.SetParent(buttom);
            image.transform.SetAsFirstSibling();
        }

        private void OnCoverGet(RawImage image)
        {
            image.transform.localScale = Vector3.one;
            image.enabled = true;
        }

        private RawImage CreateCover()
        {
            // 创建一个跟自身RectTransform完全贴合的RawImage
            var gameObject = new GameObject("Cover");
            gameObject.transform.SetParent(transform, false);
            
            var rawImage = gameObject.AddComponent<RawImage>();
            rawImage.color = Color.clear;
            var rectTransform = rawImage.rectTransform;
            
            // 设置锚点为全屏
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            
            // 设置偏移为零，使其完全贴合父对象
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // 设置初始缩放为0（用于对象池的释放状态）
            rectTransform.localScale = Vector3.one;
            return rawImage;
        }

        public void Cover(PanelBase panel)
        {
            if (coverMap == null || coverMap.ContainsKey(panel))
                return;

            var cover = coverPool.Get();
            cover.rectTransform.SetParent(panel.transform);
            cover.rectTransform.SetAsLastSibling();
            coverMap.Add(panel, cover);
        }

        /// <summary>
        /// 创建遮罩并返回作用域对象，便于使用 using 自动回收
        /// 注意：
        /// - 若当前 Panel 已被其它逻辑覆盖，则本作用域不负责回收（owned=false）
        /// - 可通过返回的 scope.image 调整遮罩颜色与显示属性
        /// </summary>
        public CoverScope CoverScoped(PanelBase panel)
        {
            var alreadyCovered = coverMap != null && coverMap.ContainsKey(panel);
            if (!alreadyCovered)
                Cover(panel);

            RawImage image = null;
            if (coverMap != null)
                coverMap.TryGetValue(panel, out image);

            return new CoverScope(panel, image, !alreadyCovered);
        }

        public void Uncover(PanelBase panel)
        {
            if (coverMap == null || !coverMap.TryGetValue(panel, out var cover))
                return;

            coverMap.Remove(panel);
            coverPool.Release(cover);
        }

        public bool IsCovering(PanelBase panel) => coverMap.ContainsKey(panel);

        #endregion

#if UNITY_EDITOR

        private static JObject LoadPreloadConfigJson(out bool isCreate)
        {
            isCreate = !File.Exists(PRELOAD_PANEL_CONFIG_PATH);
            return
                !isCreate ?
                JObject.Parse(File.ReadAllText(PRELOAD_PANEL_CONFIG_PATH)) :
                new JObject();
        }

        public static void AddPreloadPanel(string name, bool active)
        {
            JObject config = LoadPreloadConfigJson(out var isCreate);
            if (config.ContainsKey(name))
                config[name] = active;
            else
                config.Add(name, active);

            IOHelper.InsureExist(PRELOAD_PANEL_CONFIG_PATH, true, false);
            File.WriteAllText(PRELOAD_PANEL_CONFIG_PATH, config.ToString());

            if (isCreate)
                AssetDatabase.Refresh();
        }

        public static void RemovePreloadPanel(string name)
        {
            JObject config = LoadPreloadConfigJson(out _);
            config.Remove(name);
            File.WriteAllText(PRELOAD_PANEL_CONFIG_PATH, config.ToString());
        }
#endif
    }

    public enum EPanelLayer
    {
        Top,
        Second,
        Middle,
        Fourth,
        Buttom
    }
}
