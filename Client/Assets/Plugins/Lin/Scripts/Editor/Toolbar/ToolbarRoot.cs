/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using Lin.Editor.Command;
using System.Drawing.Printing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.Toolbar
{
    [InitializeOnLoad]
    public static class ToolbarRoot
    {
        public static VisualElement leftAlign { get; private set; }
        public static VisualElement middleAlign { get; private set; }
        public static VisualElement rightAlign { get; private set; }

        static ToolbarRoot()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            var barType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
            var bars = Resources.FindObjectsOfTypeAll(barType);
            if (bars.Length > 0)
            {
                var toolbar = bars[0] as ScriptableObject;

                // 反射获取toolbar的m_Root
                var rootField = barType.GetField("m_Root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var root = (rootField.GetValue(toolbar) as VisualElement).Q("ToolbarContainerContent");
                leftAlign = new VisualElement();
                leftAlign.name = "CustomLeftAlign";
                root.Q("ToolbarZoneLeftAlign").Add(leftAlign);

                middleAlign = new VisualElement();
                middleAlign.name = "CustomMiddleAlign";
                root.Q("ToolbarZonePlayMode").Add(middleAlign);

                rightAlign = new VisualElement();
                rightAlign.name = "CustomRightAlign";
                root.Q("ToolbarZoneRightAlign").Add(rightAlign);

                EditorApplication.update -= OnUpdate;
            }
        }
    }
}