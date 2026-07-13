/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using Lin.Editor.Attribute;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.Toolbar.Element
{
    public abstract class ToolbarElementBase : IToolbarElement
    {
        protected readonly Texture2D icon;
        protected readonly string label;
        protected readonly string tooltip;

        public EAlign align { get; }
        public EVisibleMode visibleMode { get; }
        public abstract float width { get; }

        public ToolbarElementBase(ToolbarElementAttribute toolbarButtonAttribute)
        {
            align = toolbarButtonAttribute.align;
            visibleMode = toolbarButtonAttribute.visibleMode;
            try
            {
                icon = 
                    AssetDatabase.LoadAssetAtPath<Texture2D>(toolbarButtonAttribute.iconPathOrLabel) 
                    ?? EditorGUIUtility.IconContent(toolbarButtonAttribute.iconPathOrLabel).image as Texture2D;
            }
            catch (System.Exception) { }
            label = toolbarButtonAttribute.iconPathOrLabel;
            tooltip = toolbarButtonAttribute.tooltip;
        }

        public VisualElement Create()
        {
            var imgui = new IMGUIContainer(OnGUI);
            imgui.tooltip = tooltip;
            return imgui;
        }

        protected abstract void OnGUI();
    }
}