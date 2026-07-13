/*
┌────────────────────────────┐
│　Description: UI基类
│　Remark: 
└────────────────────────────┘
*/
using Cysharp.Threading.Tasks;
using Lin.Runtime.Helper;
using Lin.Runtime.Interface;
using Lin.Runtime.Manager;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.U2D;

namespace Lin.Runtime.UI
{
    public abstract class UIBehaviour : MonoBehaviour, IEvent
    {
        protected const string CONTROLS_NAME = "Controls";
        public RectTransform rectTransform => transform as RectTransform;

        //拖拽
        private const string DRAG_NAME = "Drag";
        [BoxGroup(DRAG_NAME), LabelText("能否拖拽移动"), SerializeField]
        private bool draggable;
        [BoxGroup(DRAG_NAME), LabelText("松手时回归原位"), ShowIf(nameof(draggable))]
        public bool resetOnPointerUp;
        [BoxGroup(DRAG_NAME), ShowIf(nameof(draggable))]
        public UnityEvent<RectTransform, Vector2> onPointerDown;
        [BoxGroup(DRAG_NAME), ShowIf(nameof(draggable))]
        public UnityEvent<RectTransform, Vector2> onPointerUp;
        public bool isDragging { get; private set; }
        private Vector2 pointDownObjectPosition, pointDownPosition;
        private Coroutine setAsLastSiblingWhenDragging;

        // WEBGL 下提前保证图片不被卸载
        [HideInInspector]
        public List<SpriteAtlas> usingAtlas;

        private void Awake()
        {
            Init();

            SetDraggerInner(draggable);
            RegisterEvents();
        }

        protected virtual void Init() { }

        private void OnDisable()
        {
            StopDraggingCoroutine();
        }

        private void OnDestroy()
        {
            DeregisterEvents();
            OnRelease();
        }

        protected virtual void OnRelease() { }

        #region - Drag -

        public void SetDraggable(bool active)
        {
            if (!draggable ^ active)
                return;

            SetDraggerInner(active);
        }

        private void SetDraggerInner(bool active)
        {
            if (active)
            {
                var trigger = gameObject.GetOrAddComponent<EventTrigger>();
                trigger.AddEvent(EventTriggerType.PointerDown, OnPointerDown);
                trigger.AddEvent(EventTriggerType.Drag, OnPointerMove);
                trigger.AddEvent(EventTriggerType.PointerUp, OnPointerUp);
            }
            else
            {
                var trigger = gameObject.GetComponent<EventTrigger>();
                if (trigger != null)
                    Destroy(trigger);
            }
            draggable = active;
        }

        private void OnPointerDown(BaseEventData data)
        {
            var eventData = data as PointerEventData;
            pointDownPosition = eventData.position;
            pointDownObjectPosition = rectTransform.anchoredPosition;
            isDragging = true;
            onPointerDown.Invoke(rectTransform, eventData.position);
            transform.SetAsLastSibling();

            StopDraggingCoroutine();
            setAsLastSiblingWhenDragging = MonoRunner.GetInstance().StartCoroutine(SetAsLastSiblingWhenDragging());

            eventData.Use();
        }

        private void OnPointerMove(BaseEventData data)
        {
            var eventData = data as PointerEventData;
            rectTransform.anchoredPosition += (eventData.position - pointDownPosition) / transform.lossyScale.x;
            pointDownPosition = eventData.position;
            eventData.Use();
        }

        private void OnPointerUp(BaseEventData data)
        {
            var eventData = data as PointerEventData;
            isDragging = false;

            if (draggable && resetOnPointerUp)
                rectTransform.anchoredPosition = pointDownObjectPosition;

            onPointerUp.Invoke(rectTransform, eventData.position);
            eventData.Use();
        }

        private IEnumerator SetAsLastSiblingWhenDragging()
        {
            while (isDragging)
            {
                yield return CoroutineCache.waitForEndOfFrame;
                rectTransform.SetAsLastSibling();
            }
        }

        private void StopDraggingCoroutine()
        {
            if (setAsLastSiblingWhenDragging != null)
            {
                StopCoroutine(setAsLastSiblingWhenDragging);
                setAsLastSiblingWhenDragging = null;
            }
        }

        public void ShowPanel<T>() where T : PanelBase => PanelManager.GetInstance().Show<T>();

        public async UniTask ShowPanelAsync<T>() where T : PanelBase => await PanelManager.GetInstance().ShowAsync<T>();

        public void ShowPanel<TPanel, TArg>(TArg arg) where TPanel : PanelBase, IShow<TArg> => PanelManager.GetInstance().Show<TPanel, TArg>(arg);

        public async UniTask ShowPanelAsync<TPanel, TArg>(TArg arg) where TPanel : PanelBase, IShow<TArg> => await PanelManager.GetInstance().ShowAsync<TPanel, TArg>(arg);

        public void HidePanel<T>() where T : PanelBase => PanelManager.GetInstance().Hide<T>();

        public async UniTask HidePanelAsync<T>() where T : PanelBase => await PanelManager.GetInstance().HideAsync<T>();

        #endregion

        public virtual void RegisterEvents() { }

        public virtual void DeregisterEvents() { }
    }
}