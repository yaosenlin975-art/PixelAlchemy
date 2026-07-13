/*
┌────────────────────────────┐
│　Description: Hierarchy窗口扩展
│　Remark: 为带有RectTransform的物体添加快捷按钮
└────────────────────────────┘
*/
using UnityEngine;
using UnityEditor;
using Lin.Editor.Interface;

namespace Lin.Editor.UI
{
    public class ButtonsDrawer : IHierarchyDrawable
    {
        private Texture2D generateIcon;
        private Texture2D markIcon;
        private Texture2D unmarkIcon;

        public int drawPriority => int.MaxValue;

        public ButtonsDrawer()
        {
            LoadIcons();
        }
        
        private void LoadIcons()
        {
            generateIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Plugins/Lin/Arts/Sprites/生成代码.png");
            markIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Plugins/Lin/Arts/Sprites/标记.png");
            unmarkIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Plugins/Lin/Arts/Sprites/取消标记.png");
        }
        
        public float DrawInHierarchy(int instanceID, Rect drawablePoint)
        {
            if (Application.isPlaying)
                return 0;

            GameObject target = UnityEditor.EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (target == null)
                return 0;

            // 检查是否有RectTransform组件
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform == null)
                return 0;

            // 计算按钮位置
            Rect buttonRect = new Rect(drawablePoint.xMax - 20, drawablePoint.y, 12, 12);
            
            if (target.name.EndsWith("Panel"))
            {
                // 显示生成按钮
                if (generateIcon != null)
                {
                    if (GUI.Button(buttonRect, new GUIContent(generateIcon, "生成脚本和预制体"), GUIStyle.none))
                    {
                        Selection.activeGameObject = target;
                        Generator.GeneratePanelScripts(target);
                    }
                }
                else
                {
                    if (GUI.Button(buttonRect, new GUIContent("G", "生成脚本和预制体"), EditorStyles.miniButton))
                    {
                        Selection.activeGameObject = target;
                        Generator.GeneratePanelScripts(target);
                    }
                }
            }
            else if(target.transform.parent != null)
            {
                // 检查是否已标记
                bool isMarked = IsUIComponentMarked(target);
                
                if (isMarked)
                {
                    // 显示取消标记按钮
                    Texture2D icon = unmarkIcon ?? markIcon;
                    if (icon != null)
                    {
                        if (GUI.Button(buttonRect, new GUIContent(icon, "去除标记"), GUIStyle.none))
                        {
                            Selection.activeGameObject = target;
                            MenuExtension.UnmarkUIComponent(target);
                        }
                    }
                    else
                    {
                        if (GUI.Button(buttonRect, new GUIContent("U", "去除标记"), EditorStyles.miniButton))
                        {
                            Selection.activeGameObject = target;
                            MenuExtension.UnmarkUIComponent(target);
                        }
                    }
                }
                else
                {
                    // 显示标记按钮
                    if (markIcon != null)
                    {
                        
                        if (GUI.Button(buttonRect, new GUIContent(markIcon, "标记为需要获取的组件"), GUIStyle.none))
                        {
                            Selection.activeGameObject = target;
                            MenuExtension.MarkUIComponent(target);
                        }
                    }
                    else
                    {
                        if (GUI.Button(buttonRect, new GUIContent("M", "标记为需要获取的组件"), EditorStyles.miniButton))
                        {
                            Selection.activeGameObject = target;
                            MenuExtension.MarkUIComponent(target);
                        }
                    }
                }
            }

            return buttonRect.width;
        }
        
        private bool IsUIComponentMarked(GameObject obj)
        {
            // 检查物体名称是否包含标记前缀（如[Button]、[Image]等）
            return obj.name.StartsWith("[") && obj.name.Contains("]");
        }
    }
}