using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.Hierarchy.SceneObject
{
    public partial class SceneObjectDescriptionWindow : EditorWindow
    {
        private ColorField titleColorField;
        private TextField titleField;
        private TextField descriptionField;
        private Label previewLabel;
        private ObjectField targetField;

        private int instanceId;

        /// <summary>
        /// 根据当前选中的GameObject显示描述窗口
        /// </summary>
        [MenuItem("GameObject/修改注释", false, 48)]
        private static void ShowBySelect()
        {
            GameObject selectedObject = Selection.activeGameObject;
            int instanceId = selectedObject.GetInstanceID();
            ShowDescription(instanceId);
        }

        /// <summary>
        /// 验证ShowBySelect菜单项是否应该显示
        /// </summary>
        [MenuItem("GameObject/修改注释", true)]
        private static bool ValidateShowBySelect()
        {
            return Selection.activeGameObject != null;
        }

        public static void ShowDescription(int instanceId)
        {
            SceneObjectDescriptionWindow wnd = GetWindow<SceneObjectDescriptionWindow>(true, "场景物体注释", true);
            wnd.minSize = new Vector2(400, 500);
            wnd.maxSize = new Vector2(600, 700);
            wnd.LoadExistingComment(instanceId);
            wnd.Show();
        }

        public void CreateGUI()
        {
            // 加载UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Lin/Scripts/Editor/Hierarchy/SceneObject/SceneObjectDescriptionWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            // 查找获取UI组件
            titleColorField = rootVisualElement.Q<ColorField>("TitleColor");
            titleField = rootVisualElement.Q<TextField>("TitleField");
            descriptionField = rootVisualElement.Q<TextField>("ToolTipField");
            previewLabel = rootVisualElement.Q<Label>("previewLabel");
            targetField = rootVisualElement.Q<ObjectField>("CurrentObjectField");

            var tooltipField = descriptionField.Q("unity-text-input");
            // 设置描述字段的最小高度为100像素
            if (tooltipField != null)
            {
                tooltipField.style.minHeight = 100;
            }

            // 获取按钮组件
            var saveButton = rootVisualElement.Q<Button>("SaveButton");
            var cancelButton = rootVisualElement.Q<Button>("CancelButton");
            var deleteButton = rootVisualElement.Q<Button>("DeleteButton");
            
            // 绑定事件
            BindEvents(saveButton, cancelButton, deleteButton);
        }
        
        /// <summary>
        /// 绑定UI事件
        /// </summary>
        /// <param name="saveButton">保存按钮</param>
        /// <param name="cancelButton">取消按钮</param>
        /// <param name="deleteButton">删除按钮</param>
        private void BindEvents(Button saveButton, Button cancelButton, Button deleteButton)
        {
            // 保存按钮事件
            saveButton?.RegisterCallback<ClickEvent>(evt => SaveComment());
            
            // 取消按钮事件
            cancelButton?.RegisterCallback<ClickEvent>(evt => Close());
            
            // 删除按钮事件
            deleteButton?.RegisterCallback<ClickEvent>(evt => DeleteComment());
            
            // 标题和描述字段变化时更新预览
            titleField?.RegisterValueChangedCallback(evt => UpdatePreview());
            descriptionField?.RegisterValueChangedCallback(evt => UpdatePreview());
            titleColorField?.RegisterValueChangedCallback(evt => UpdatePreview());
        }
        
        /// <summary>
        /// 加载现有的注释信息
        /// </summary>
        private void LoadExistingComment(int instanceId)
        {
            this.instanceId = instanceId;
            var description = SceneObjectDescriptionsMap.GetInstance().GetDescription(instanceId);
            // 从title中提取颜色信息，然后移除颜色标签显示纯文本
            titleColorField.value = description.color;
            titleField.value = description.title;
            descriptionField.value = description.description;
            targetField.value = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);
            
            UpdatePreview();
        }
        
        /// <summary>
        /// 更新预览显示
        /// </summary>
        private void UpdatePreview()
        {
            if (previewLabel == null) return;
            
            string title = titleField?.value ?? "";
            string description = descriptionField?.value ?? "";
            Color color = titleColorField?.value ?? EditorConst.ASSET_SUMMARY_TITLE_DEFAULT_COLOR;
            
            string colorHex = ColorUtility.ToHtmlStringRGBA(color);
            string previewText = "";
            
            if (!string.IsNullOrEmpty(title))
            {
                previewText += $"<color=#{colorHex}><b>{titleField.value}</b></color>\n";
            }
            
            if (!string.IsNullOrEmpty(description))
            {
                previewText += description;
            }
            
            previewLabel.text = string.IsNullOrEmpty(previewText) ? "预览将在这里显示..." : previewText;
        }
        
        /// <summary>
        /// 保存注释
        /// </summary>
        private void SaveComment()
        {
            SceneObjectDescriptionsMap.GetInstance().SetDescription(instanceId,
                titleField.value,
                descriptionField.value, 
                titleColorField.value);

            Close();
        }
        
        /// <summary>
        /// 删除注释
        /// </summary>
        private void DeleteComment()
        {
            SceneObjectDescriptionsMap.GetInstance().RemoveDescription(instanceId);
            Close();
        }
    }
}
