/*
┌────────────────────────────┐
│　Description: 无限列表
│　Remark: 
└────────────────────────────┘
*/
using Lin.Runtime.Attribute;
using Lin.Runtime.Interface;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZLinq;
using Cysharp.Text;
using Lin.Runtime.Manager;
using System.Collections;

namespace Lin.Runtime.UI
{
    public enum EScrollType
    {
        Horizontal, 
        Vertical,
        Grid
    }

    [RequireComponent(typeof(ScrollRect))]
    public class InfiniteScroller : MonoBehaviour
    {
        private const string ELEMENT_GROUP = "Element";
        private const string CONFIG_GROUP = "Config";

        [EnumToggleButtons]
        [BoxGroup(CONFIG_GROUP)]
        public EScrollType scrollType;
        [BoxGroup(CONFIG_GROUP)]
        public Vector2 spacing;
        [Get]
        [SerializeField]
        [BoxGroup(CONFIG_GROUP)]
        private ScrollRect targetScrollRect;

        [Required]
        [BoxGroup(ELEMENT_GROUP)]
        public GameObject elementPrefab;
        [BoxGroup(ELEMENT_GROUP)]
        public Vector2 elementSize;
        [Required]
        [BoxGroup(ELEMENT_GROUP)]
        public RectTransform content;

        [BoxGroup("Padding")]
        public float leftPadding, rightPadding, topPadding, bottomPadding;

        // 数据与对象池
        private readonly List<IScrollableData> dataList = new List<IScrollableData>();
        private readonly Dictionary<RectTransform, IScrollableElement> elementCache = new Dictionary<RectTransform, IScrollableElement>();
        private List<RectTransform> activeElements = new List<RectTransform>();
        private List<RectTransform> deactiveElements = new List<RectTransform>();

        // 视图与布局参数
        [SerializeField]
        [Tooltip("防止上下边界不显示")]
        [BoxGroup(CONFIG_GROUP)]
        private int extraBuffer = 2;
        private RectTransform viewport;
        private int columns = 1; // Grid/Horizontal 下动态计算
        private int visibleCapacity = 0;
        private int firstVisibleIndex = 0;
        private bool initialized = false;

        private Coroutine refreshCoroutine;

        // 便捷单元尺寸
        private float CellWidth => elementSize.x + spacing.x;
        private float CellHeight => elementSize.y + spacing.y;

        private void Awake()
        {
            EnsureInit();
        }

        private void OnEnable()
        {
            EnsureInit();
            targetScrollRect.onValueChanged.AddListener(OnScroll);
            RecalculateLayout();
            UpdateContentSize();
            RefreshVisible();
        }

        private void OnDisable()
        {
            if (targetScrollRect != null)
                targetScrollRect.onValueChanged.RemoveListener(OnScroll);
        }

        private void OnDestroy()
        {
            if (targetScrollRect != null)
                targetScrollRect.onValueChanged.RemoveListener(OnScroll);
        }

        private void OnScroll(Vector2 _)
        {
            RefreshVisible();
        }

        private void EnsureInit()
        {
            if (initialized) 
                return;

            if (targetScrollRect == null)
                targetScrollRect = GetComponent<ScrollRect>();

            if (targetScrollRect == null || elementPrefab == null)
                return;

            activeElements = new List<RectTransform>(extraBuffer);
            deactiveElements = new List<RectTransform>(extraBuffer);

            viewport = targetScrollRect.viewport ? targetScrollRect.viewport : (RectTransform)targetScrollRect.transform;

            if (this.content == null)
                this.content = viewport.GetChild(0) as RectTransform;

            // 统一为左上锚点，便于绝对定位
            var content = this.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);

            initialized = true;
        }

        #region - 对象池 -

        private RectTransform GetUnuseSlot()
        {
            RectTransform result = null;
            if (deactiveElements.Count > 0)
            {
                result = deactiveElements[0];
                deactiveElements.Remove(result);
            }
            else
            {
                result = Create();
            }

            activeElements.Add(result);
            result.gameObject.SetActive(true);
            return result;
        }

        private void RecycleUsingSlot(RectTransform slot)
        {
            if (!activeElements.Contains(slot))
                throw new System.Exception(ZString.Concat("塞了个啥进来?  ", slot));

            activeElements.Remove(slot);
            deactiveElements.Add(slot);
            slot.gameObject.SetActive(false);
        }

        private RectTransform Create()
        {
            var go = Instantiate(elementPrefab, content);
            var rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = elementSize;

            if (!elementCache.ContainsKey(rt))
                elementCache[rt] = go.GetComponent<IScrollableElement>();
            return rt;
        }

        #endregion

        #region - 刷新显示 -

        public void Refresh(bool immediately = false)
        {
            if (immediately)
                RefreshInternal();
            else if (refreshCoroutine == null)
                refreshCoroutine = StartCoroutine(RefreshCoroutine());
        }

        private IEnumerator RefreshCoroutine()
        {
            yield return CoroutineCache.waitForEndOfFrame;
            RefreshInternal();
        }

        private void RefreshInternal()
        {
            RecalculateLayout();
            UpdateContentSize();
            RefreshVisible();
            refreshCoroutine = null;
        }

        private void RecalculateLayout()
        {
            if (!initialized)
                return;

            float viewW = viewport.rect.width;
            float viewH = viewport.rect.height;

            switch (scrollType)
            {
                case EScrollType.Vertical:
                    {
                        columns = 1;
                        int visibleRows = Mathf.Max(1, Mathf.CeilToInt(viewH / Mathf.Max(1e-3f, CellHeight)) + extraBuffer);
                        visibleCapacity = visibleRows * columns;
                    }
                    break;

                case EScrollType.Horizontal:
                    {
                        // 单行水平滚动：计算视口可见列
                        columns = Mathf.Max(1, Mathf.FloorToInt((viewW - leftPadding - rightPadding + spacing.x) / Mathf.Max(1e-3f, CellWidth)));
                        int visibleCols = Mathf.Max(1, Mathf.CeilToInt(viewW / Mathf.Max(1e-3f, CellWidth)) + extraBuffer);
                        visibleCapacity = visibleCols; // 单行
                    }
                    break;

                case EScrollType.Grid:
                    {
                        // 垂直滚动的网格：先计算列数，再按可见行数确定容量
                        columns = Mathf.Max(1, Mathf.FloorToInt((viewW - leftPadding - rightPadding + spacing.x) / Mathf.Max(1e-3f, CellWidth)));
                        int visibleRows = Mathf.Max(1, Mathf.CeilToInt(viewH / Mathf.Max(1e-3f, CellHeight)) + extraBuffer);
                        visibleCapacity = visibleRows * columns;
                    }
                    break;
            }

            ResizeActiveCapacity(visibleCapacity);
        }

        private void ResizeActiveCapacity(int capacity)
        {
            // 增加实例
            while (activeElements.Count < capacity)
                GetUnuseSlot();

            // 回收多余
            while (activeElements.Count > capacity)
            {
                var rt = activeElements[activeElements.Count - 1];
                RecycleUsingSlot(rt);
            }
        }

        private void UpdateContentSize()
        {
            int totalCols;
            int totalRows;
            switch (scrollType)
            {
                case EScrollType.Vertical:
                    totalCols = 1;
                    totalRows = dataList.Count;
                    break;
                case EScrollType.Horizontal:
                    totalCols = dataList.Count;
                    totalRows = 1;
                    break;
                default: // Grid 垂直滚动
                    totalCols = Mathf.Max(1, columns);
                    totalRows = Mathf.CeilToInt(dataList.Count / Mathf.Max(1f, totalCols));
                    break;
            }

            float contentWidth = leftPadding + rightPadding + totalCols * elementSize.x + Mathf.Max(0, totalCols - 1) * spacing.x;
            float contentHeight = topPadding + bottomPadding + totalRows * elementSize.y + Mathf.Max(0, totalRows - 1) * spacing.y;
            content.sizeDelta = new Vector2(contentWidth, contentHeight);
        }

        private int ComputeFirstVisibleIndex()
        {
            Vector2 ap = content.anchoredPosition;
            switch (scrollType)
            {
                case EScrollType.Vertical:
                    {
                        int firstRow = Mathf.Max(0, Mathf.FloorToInt((ap.y - topPadding) / Mathf.Max(1e-3f, CellHeight)));
                        return Mathf.Max(0, firstRow * 1);
                    }
                case EScrollType.Horizontal:
                    {
                        float xOffset = -ap.x - leftPadding;
                        int firstCol = Mathf.Max(0, Mathf.FloorToInt(xOffset / Mathf.Max(1e-3f, CellWidth)));
                        return firstCol;
                    }
                default: // Grid 垂直滚动
                    {
                        int firstRow = Mathf.Max(0, Mathf.FloorToInt((ap.y - topPadding) / Mathf.Max(1e-3f, CellHeight)));
                        return Mathf.Max(0, firstRow * Mathf.Max(1, columns));
                    }
            }
        }

        private void RefreshVisible()
        {
            if (!initialized)
                return;

            firstVisibleIndex = ComputeFirstVisibleIndex();

            for (int i = 0; i < activeElements.Count; i++)
            {
                int dataIndex;
                int row, col;
                switch (scrollType)
                {
                    case EScrollType.Vertical:
                        dataIndex = firstVisibleIndex + i; // 单列
                        row = dataIndex;
                        col = 0;
                        break;
                    case EScrollType.Horizontal:
                        dataIndex = firstVisibleIndex + i; // 单行
                        row = 0;
                        col = dataIndex;
                        break;
                    default: // Grid 垂直滚动
                        dataIndex = firstVisibleIndex + i;
                        row = dataIndex / Mathf.Max(1, columns);
                        col = dataIndex % Mathf.Max(1, columns);
                        break;
                }

                var rt = activeElements[i];
                if (dataIndex >= 0 && dataIndex < dataList.Count)
                {
                    // 定位
                    float x = leftPadding + col * CellWidth;
                    float y = -topPadding - row * CellHeight;
                    rt.anchoredPosition = new Vector2(x, y);

                    // 刷新元素
                    var element = elementCache[rt];
                    rt.gameObject.SetActive(true);
                    element.Refresh(dataList[dataIndex], dataIndex);
                }
                else
                    rt.gameObject.SetActive(false);
            }
        }

        #endregion

        #region - 数据集操作 -

        public void Add(IScrollableData data)
        {
            if (data == null)
                return;

            dataList.Add(data);
            Refresh();
        }

        public void AddRange<T>(ValueEnumerable<ZLinq.Linq.FromList<T>, T> collection) where T : IScrollableData
        {
            foreach (var d in collection)
            {
                if (d != null)
                    dataList.Add(d);
            }
            Refresh();
        }

        public void Remove(IScrollableData data)
        {
            int idx = dataList.IndexOf(data);
            if (idx < 0)
                return;

            dataList.RemoveAt(idx);
            Refresh();
        }

        public void Clear()
        {
            dataList.Clear();
            UpdateContentSize();
            // 回收所有激活的元素
            while (activeElements.Count > 0)
            {
                RecycleUsingSlot(activeElements[0]);
            }

            // 缩减容量到最小可见
            Refresh();
        }

        #endregion

#if UNITY_EDITOR

        [OnInspectorGUI]
        [HideInEditorMode]
        private void OnInspectorGUI()
        {
            GUILayout.Label($"数据量: {dataList?.Count}");
            GUILayout.Label($"展示中的槽数: {activeElements?.Count}");
            GUILayout.Label($"待机中的槽数: {deactiveElements?.Count}");
            GUILayout.Label($"总槽数: {activeElements?.Count + deactiveElements.Count}");
        }
#endif
    }
}
