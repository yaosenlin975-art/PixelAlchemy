/*
┌────────────────────────────┐
│　Description: UIPanel基类
│　Remark: 
└────────────────────────────┘
*/
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Lin.Runtime.Helper;
using Lin.Runtime.Interface;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lin.Runtime.UI
{
    public enum EPanelState
    {
        Show,
        Animate2Show,
        Animate2Hide,
        Hide,
    }

    [RequireComponent(typeof(CanvasGroup))]
    public abstract class PanelBase : UIBehaviour, IShow
    {
        private const string BASE_NAME = "Base";

        [SerializeField, BoxGroup(BASE_NAME)]
        private EPanelLayer panelLayer;
        public EPanelLayer PanelLayer => panelLayer;

        [SerializeField, BoxGroup(BASE_NAME)]
        private bool defaultInteractable = true;
        [SerializeField, BoxGroup(BASE_NAME)]
        private float defaultAlpha = 1;

        [SerializeField, BoxGroup(BASE_NAME)]
        private bool destroyAfterHide;

        [SerializeField, BoxGroup(BASE_NAME)]
        private PanelAnimationSettings showAnimation;

        [SerializeField, BoxGroup(BASE_NAME)]
        private PanelAnimationSettings hideAnimation;

        //全屏显示时控制游戏主相机关闭
        [SerializeField]
        [BoxGroup(BASE_NAME)]
        [Tooltip("UI显示时是否会遮挡住整个场景")]
        private bool fullScreen;

        protected CanvasGroup canvasGroup;

        private Dictionary<Type, Dictionary<string, UnityEngine.Object>> controls;
        [SerializeField, BoxGroup(BASE_NAME), Tooltip("可以直接通过控件Object的名字找到控件")]
        private bool controlsAddressable;

        public bool interactable 
        {
            get => !PanelManager.GetInstance().IsCovering(this);
            set
            {
                var uiMgr = PanelManager.GetInstance();
                if (value)
                    uiMgr.Uncover(this);
                else
                    uiMgr.Cover(this);
            }        
        }

        public EPanelState panelState { get; private set; }

#if UNITY_EDITOR

        [SerializeField]
        [BoxGroup(BASE_NAME)]
        [OnValueChanged(nameof(OnPreloadToggleValueChanged))]
        private bool shouldPreload;

        [SerializeField]
        [BoxGroup(BASE_NAME)]
        [ShowIf(nameof(shouldPreload))]
        [OnValueChanged(nameof(OnPreloadToggleValueChanged))]
        private bool preloadActive;

        private void OnPreloadToggleValueChanged()
        {
            if (shouldPreload)
                PanelManager.AddPreloadPanel(GetType().Name, preloadActive);
            else
                PanelManager.RemovePreloadPanel(GetType().Name);
        }
#endif

        protected override void Init()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return;
#endif
            name = GetType().Name;

            if (controlsAddressable)
                controls = new Dictionary<Type, Dictionary<string, UnityEngine.Object>>();

            canvasGroup = gameObject.GetOrAddComponent<CanvasGroup>();
            canvasGroup.alpha = defaultAlpha;
            interactable = defaultInteractable;
        }

        private void Start()
        {
            PanelManager.GetInstance().Register(this);
        }

        private void OnEnable() => Show();

        public void Show(bool immeditely = false) => ShowAsync(immeditely).Forget();

        protected virtual void OnShow() { }

        public async UniTask ShowAsync(bool immeditely = false)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            if (panelState == EPanelState.Animate2Show)
                return;

            OnShow();

            panelState = EPanelState.Animate2Show;
            await PanelAnimation(showAnimation, true, immeditely, 1, Vector3.one, AfterShow);

            void AfterShow()
            {
                if (fullScreen)
                    PanelManager.GetInstance().AddFullScreenPanel(this);

                panelState = EPanelState.Show;
            }
        }

        [Button, HideInEditorMode, ButtonGroup]
        public void Hide(bool immeditely = false) => HideAsync(immeditely).Forget();

        protected virtual void OnHide()
        {
            panelState = EPanelState.Hide;

            if (destroyAfterHide)
                Destroy(gameObject);
            else
            {
                rectTransform.SetAsFirstSibling();
                rectTransform.localScale = Vector3.zero;
            }
        }

        public async UniTask HideAsync(bool immeditely = false)
        {
            if (panelState == EPanelState.Animate2Hide)
                return;

            PanelManager.GetInstance().Cover(this);
            panelState = EPanelState.Animate2Hide;

            if (fullScreen)
                PanelManager.GetInstance().RemoveFullScreenPanel(this);

            await PanelAnimation(hideAnimation, false, immeditely, 0, Vector3.zero, OnHide);
        }

        protected override void OnRelease()
        {
            PanelManager.GetInstance().Deregister(this);

            if (controlsAddressable && controls != null)
            {
                foreach (var list in controls.Values)
                    list.Clear();

                controls.Clear();
                controls = null;
            }
        }

        //interactable可判断是展示还是隐藏 True:展示
        private async UniTask PanelAnimation(PanelAnimationSettings settings, bool interactable, bool immeditely, float alpha, Vector3 endScale, Action onComplete = null)
        {
            rectTransform.DOKill();
            canvasGroup.DOKill();
            rectTransform.SetAsLastSibling();
            this.interactable = false;

            if (immeditely || settings.animationType == EPanelAnimation.None)
            {
                rectTransform.localScale = endScale;
                canvasGroup.alpha = alpha;
                if (interactable)
                    this.interactable = true;
                onComplete?.Invoke();
                return;
            }

            Tweener tweener = null;
            if (settings.animationType.HasFlag(EPanelAnimation.Fade))
            {
                if (interactable)
                {
                    if (canvasGroup.alpha > 0)
                        canvasGroup.alpha = 0;
                }
                else if (canvasGroup.alpha == 0)
                    canvasGroup.alpha = 1;

                tweener = canvasGroup.DOFade(alpha, settings.animationDuration);
            }
            //在别的模式下能看到panel
            else if (interactable)
                canvasGroup.alpha = alpha;

            if (settings.animationType.HasFlag(EPanelAnimation.Scale))
                tweener = rectTransform.DOScale(endScale, settings.animationDuration);
            else if (endScale.sqrMagnitude != 0)
                rectTransform.localScale = endScale;

            if (settings.animationType.HasFlag(EPanelAnimation.Slide))
            {
                if (rectTransform.anchoredPosition == settings.endPoint)
                    rectTransform.anchoredPosition = settings.startPoint;

                tweener = rectTransform.DOAnchorPos(settings.endPoint, settings.animationDuration);
            }

            tweener.onComplete = OnComplete;
            await UniTask.WaitForSeconds(settings.animationDuration);

            async void OnComplete()
            {
                rectTransform.localScale = endScale;

                if (canvasGroup == null)
                    await UniTask.WaitUntil(() => canvasGroup != null);

                canvasGroup.alpha = alpha;

                if (interactable)
                    this.interactable = true;

                onComplete?.Invoke();
            }
        }

        public T Get<T>(string name) where T : UnityEngine.Object
        {
            if (!controlsAddressable)
            {
                this.Error("未启用 controlsAddressable, 无法寻址");
                return null;
            }
            var type = typeof(T);
            if (!controls.TryGetValue(type, out var map))
            {
                map = new Dictionary<string, UnityEngine.Object>();
                controls.Add(type, map);
            }

            if (!map.TryGetValue(name, out var result))
            {
                result = FindRecursive(transform, name);
                if (result != null)
                    map.Add(name, result);
            }

            return result as T;

            T FindRecursive(Transform parent, string name)
            {
                foreach (Transform child in parent)
                {
                    if (child.name.Equals(name))
                        return child.GetComponent<T>();

                    var found = FindRecursive(child, name);
                    if (found is not null)
                        return found;
                }

                return null;
            }
        }

    }
}