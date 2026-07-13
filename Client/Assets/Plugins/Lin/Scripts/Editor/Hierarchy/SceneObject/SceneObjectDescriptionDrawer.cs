/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using Cysharp.Text;
using Lin.Editor.Interface;
using Lin.Editor.Settings;
using Lin.Runtime.DesignPattern.Singleton;
using Lin.Runtime.Helper;
using Lin.Runtime.Tool;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lin.Editor.Hierarchy.SceneObject
{
    public class SceneObjectDescriptionDrawer : IHierarchyDrawable
    {
        // 双击检测相关字段
        private static float lastClickTime = 0f;
        private static Vector2 lastClickPosition = Vector2.zero;
        private static int lastClickedInstanceId = -1;
        private const float DOUBLE_CLICK_TIME = 0.3f; // 双击时间间隔
        private const float DOUBLE_CLICK_DISTANCE = 5f; // 双击位置容差

        public int drawPriority => 0;

        public float DrawInHierarchy(int instanceId, Rect drawablePoint)
        {
            var description = SceneObjectDescriptionsMap.GetInstance().GetDescription(instanceId);
            Rect labelRect = new Rect(drawablePoint.xMax - 60, drawablePoint.y, 60, 12);
            if (string.IsNullOrEmpty(description.title))
                return 0;

            var settings = EditorSettings_SO.GetInstance();

            // 创建样式并启用富文本
            var style = new GUIStyle(EditorStyles.miniLabel);
            style.richText = true;
            style.fontSize = settings.sceneObjectDescriptionTitleSize;
            style.alignment = TextAnchor.MiddleRight;

            // 对象Tooltip 
            var content = new GUIContent(ZString.Format("<color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGB(description.color), description.title));
            content.tooltip = description.description;

            var size = style.CalcSize(content);
            labelRect.width = size.x;
            labelRect.x = drawablePoint.xMax - size.x - 5;

            // 绘制注释
            GUI.Label(labelRect, content, style);

            // 处理鼠标点击事件
            Event currentEvent = Event.current;

            // 检查鼠标是否在标签区域内
            if (labelRect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.type == EventType.MouseDown)
                {
                    switch (settings.sceneObjectDescriptionEditWay)
                    {
                        case EClickType.无响应:
                            break;

                        case EClickType.单击:
                            SceneObjectDescriptionWindow.ShowDescription(instanceId);
                            Event.current.Use();
                            break;

                        case EClickType.双击:
                        default:
                            // 实现双击检测逻辑
                            float currentTime = (float)EditorApplication.timeSinceStartup;
                            Vector2 currentPosition = currentEvent.mousePosition;
                            
                            // 检查是否为双击
                            bool isDoubleClick = (currentTime - lastClickTime) <= DOUBLE_CLICK_TIME &&
                                               Vector2.Distance(currentPosition, lastClickPosition) <= DOUBLE_CLICK_DISTANCE &&
                                               lastClickedInstanceId == instanceId;
                            
                            if (isDoubleClick)
                            {
                                // 双击事件：显示描述窗口
                                SceneObjectDescriptionWindow.ShowDescription(instanceId);
                                
                                // 重置双击检测状态
                                lastClickTime = 0f;
                                lastClickedInstanceId = -1;
                                Event.current.Use();
                            }
                            else
                            {
                                // 记录当前点击信息，用于下次双击检测
                                lastClickTime = currentTime;
                                lastClickPosition = currentPosition;
                                lastClickedInstanceId = instanceId;
                            }
                            break;
                    }
                }
            }

            return labelRect.width;
        }
    }
}