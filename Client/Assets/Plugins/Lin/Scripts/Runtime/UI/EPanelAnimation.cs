/*
┌────────────────────────────┐
│　Description: UI面板动画类型
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: EPanelAnimation
└──────────────┘
*/

using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace Lin.Runtime.UI  
{
    /// <summary>
    /// 动画类型
    /// </summary>
    [Flags]
    public enum EPanelAnimation
    {
        None = 0,
        Fade = 1,
        Scale = 2,
        Slide = 4
    }

    [Serializable]
    public class PanelAnimationSettings
    {
        public EPanelAnimation animationType;
        public float animationDuration = 0.3f;

        [ShowIf(nameof(hasSlide))]
        public Vector2 startPoint;
        [ShowIf(nameof(hasSlide))]
        public Vector2 endPoint;

        private bool hasSlide => animationType.HasFlag(EPanelAnimation.Slide);
    }
}