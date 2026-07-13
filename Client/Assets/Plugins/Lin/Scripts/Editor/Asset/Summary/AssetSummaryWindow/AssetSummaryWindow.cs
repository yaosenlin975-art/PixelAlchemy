using System.IO;
using System.Text.RegularExpressions;
using Cysharp.Text;
using Lin.Editor.Helper;
using Lin.Runtime.Helper;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.Asset
{
    public partial class AssetSummaryWindow : EditorWindow
    {
        private ObjectField currentObjectField;
        private ColorField titleColorField;
        private TextField titleField;
        private TextField descriptionField;
        private Label previewLabel;

        private string assetPath;

        public static void ShowAssetSummary(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || (!File.Exists(assetPath) && !Directory.Exists(assetPath)))
            {
                Log.Error("资源注释", $"{assetPath} 资源不存在");
                return;
            }

            AssetSummaryWindow wnd = GetWindow<AssetSummaryWindow>(true, "文件注释", true);
            wnd.minSize = new Vector2(400, 500);
            wnd.maxSize = new Vector2(600, 700);
            wnd.assetPath = assetPath;
            wnd.InitializeComponents();
            wnd.Show();
        }

        public void CreateGUI()
        {
            // 加载UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Lin/Scripts/Editor/Asset/Summary/AssetSummaryWindow/AssetSummaryWindow.uxml");
            visualTree.CloneTree(rootVisualElement);

            // 查找获取UI组件
            currentObjectField = rootVisualElement.Q<ObjectField>("CurrentObjectField");
            titleColorField = rootVisualElement.Q<ColorField>("TitleColor");
            titleField = rootVisualElement.Q<TextField>("TitleField");
            descriptionField = rootVisualElement.Q<TextField>("ToolTipField");
            previewLabel = rootVisualElement.Q<Label>("previewLabel");

            var tooltipField = descriptionField.Q("unity-text-input");
            // 设置描述字段的最小高度为100像素
            if (tooltipField != null)
            {
                tooltipField.style.minHeight = 100;
            }

            // 绑定事件
            BindEvents();
        }
        
        /// <summary>
        /// 初始化UI组件的默认值
        /// </summary>
        private void InitializeComponents()
        {
            
            // 加载当前资源
            if (currentObjectField != null && !string.IsNullOrEmpty(assetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                currentObjectField.value = asset;
                currentObjectField.SetEnabled(false); // 设置为只读
            }
            
            // 加载现有的注释信息
            LoadExistingComment();
        }
        
        /// <summary>
        /// 绑定UI事件
        /// </summary>
        /// <param name="saveButton">保存按钮</param>
        /// <param name="cancelButton">取消按钮</param>
        /// <param name="deleteButton">删除按钮</param>
        private void BindEvents()
        {
            // 获取按钮组件
            var saveButton = rootVisualElement.Q<Button>("SaveButton");
            var saveAndCloseButton = rootVisualElement.Q<Button>("SaveAndCloseBtn");
            var cancelButton = rootVisualElement.Q<Button>("CancelButton");
            var deleteButton = rootVisualElement.Q<Button>("DeleteButton");

            // 保存按钮事件
            saveButton?.RegisterCallback<ClickEvent>(evt => SaveComment(false));
            saveAndCloseButton?.RegisterCallback<ClickEvent>(evt => SaveComment(true));

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
        private void LoadExistingComment()
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            var summary = importer.GetDescription();

            // 从title中提取颜色信息，然后移除颜色标签显示纯文本
            ColorUtility.TryParseHtmlString(ZString.Concat('#', summary.titleColor), out var color);
            titleColorField.value = color;
            titleField.value = RemoveColorTag(summary.title);
            descriptionField.value = summary.description;
            
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
        private void SaveComment(bool closeWindow)
        {
            AssetSummaryArchiver.GetInstance().SetDescription(AssetImporter.GetAtPath(assetPath), new AssetSummary
            {
                title = titleField.value,
                titleColor = titleColorField.value.ToHtmlStringRGB(),
                description = descriptionField.value,
            });
            if (closeWindow)
                Close();
        }
        
        /// <summary>
        /// 删除注释
        /// </summary>
        private void DeleteComment()
        {
            AssetSummaryArchiver.GetInstance().RemoveDescription(AssetImporter.GetAtPath(assetPath));
            Close();
        }

        // 从注释中提取颜色代码
        private Color ExtractColorFromComment(string comment)
        {
            if (string.IsNullOrEmpty(comment))
                return EditorConst.ASSET_SUMMARY_TITLE_DEFAULT_COLOR;
                
            Match match = Regex.Match(comment, @"<color=(#[0-9A-Fa-f]+)>", RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                string colorHex = match.Groups[1].Value;
                if (ColorUtility.TryParseHtmlString(colorHex, out Color parsedColor))
                    return parsedColor;
            }
            
            // 如果解析失败或没有匹配到颜色，返回默认颜色
            return EditorConst.ASSET_SUMMARY_TITLE_DEFAULT_COLOR;
        }

        /// <summary>
        /// 移除颜色标签，保留内部内容
        /// </summary>
        /// <param name="comment">包含富文本标签的字符串</param>
        /// <returns>移除标签后的纯文本</returns>
        private string RemoveColorTag(string comment)
        {
            if (string.IsNullOrEmpty(comment))
                return string.Empty;
                
            // 移除<color=#XXXXXX>...</color>标签，保留内部文本
            string result = Regex.Replace(comment, @"<color=#[0-9A-Fa-f]+>(.*?)</color>", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            // 移除其他可能的富文本标签（如<b>, <i>等）
            result = Regex.Replace(result, @"</?[bi]>", "", RegexOptions.IgnoreCase);
            
            return result;
        }
    }
}
