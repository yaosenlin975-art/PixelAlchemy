/*
┌────────────────────────────┐
│　Description: 红点节点
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: RedDotNode
└──────────────┘
*/
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Lin.Runtime.Helper;

namespace Lin.Runtime.UI.RedDot
{
    [RequireComponent(typeof(EventTrigger)), DisallowMultipleComponent]
    public class RedDotNode : MonoBehaviour
    {
        [SerializeField] RedDotNode parentNode;
        int activeCount;
        HashSet<RedDotNode> children;
        [SerializeField] GameObject redDotObject;
        [SerializeField, HideInPlayMode] bool edited;
        EventTrigger trigger;
        
        /// <summary>
        /// 当红点被激活 子件激活数量发生改变时
        /// </summary>
        public event Action<uint> onActived;

        bool HasChildren => children?.Count > 0;

        public bool RedDotActive => activeCount > 0;

        private void Awake()
        {
            children = new HashSet<RedDotNode>();

            trigger = GetComponent<EventTrigger>();
            trigger.AddEvent(EventTriggerType.PointerClick, OnClick);
        }

        private void Start()
        {
            if (edited)
            {
                parentNode?.RegistNode(this);
                RedDotManager.GetInstance().Register(this);
            }
        }

        private void OnDestroy()
        {
            children.Clear();
            children = null;

            RedDotManager.GetInstance().Unregister(this);
        }

        private void OnEnable()
        {
            RefreshRedNodeState();
        }

        public void RegistNode(RedDotNode node)
        {
            children.Add(node);
            node.parentNode = this;
        }

        public void UnregistNode(RedDotNode node) => children.Remove(node);

        [HideIf("HasChildren"), HideIf("RedDotActive"), Button, HideInEditorMode]
        public void Show()
        {
            if (HasChildren)
                return;

            RefreshActiveNodeCount(1);
        }

        [HideIf("HasChildren"), ShowIf("RedDotActive"), Button, HideInEditorMode]
        public void Hide()
        {
            if (HasChildren)
                return;

            RefreshActiveNodeCount(-1);
        }

        void RefreshActiveNodeCount(int sign)
        {
            int before = activeCount;
            int childrenCount = children?.Count ?? 0;
            activeCount = Mathf.Clamp(activeCount + sign, 0, childrenCount == 0 ? 1 : childrenCount); 
            if (before == activeCount)
                return;

            RefreshRedNodeState();
        }

        void RefreshRedNodeState()
        {
            bool before = redDotObject.activeInHierarchy;
            bool active = activeCount > 0;
            redDotObject.SetActive(active);
            print($"{name}'s count: {activeCount}");

            int sign = before == active ? 0 : active ? 1 : -1;
            parentNode?.RefreshActiveNodeCount(sign);
            onActived?.Invoke((uint)activeCount);
        }

        private void OnClick(BaseEventData bed) => Hide();
    }
}