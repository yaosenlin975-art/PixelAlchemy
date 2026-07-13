/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.Toolbar.Element
{
    public class TimeScaleSlider : IToolbarElement
    {
        public EAlign align => EAlign.Middle;

        public EVisibleMode visibleMode => EVisibleMode.Runtime;

        public float width => 300;

        public VisualElement Create()
        {
            var imgui = new IMGUIContainer(OnGUI);
            imgui.tooltip = "TimeScale控制器";
            return imgui;
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Space(5);
                ToolbarElementDrawer.Button(OnResetBtnClick, "R");
                GUILayout.Label("TimeScale");
                Rect sliderRect = EditorGUILayout.GetControlRect(GUILayout.Width(200), GUILayout.Height(20));
                Time.timeScale = EditorGUI.Slider(sliderRect, Time.timeScale, 0.2f, 5f);
            }
            GUILayout.EndHorizontal();
        }

        private void OnResetBtnClick() => Time.timeScale = 1;
    }
}
