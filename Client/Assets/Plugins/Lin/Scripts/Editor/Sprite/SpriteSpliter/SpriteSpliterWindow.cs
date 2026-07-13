using System.IO;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lin.Editor.SpriteTool
{
    public class SpriteSpliterWindow : OdinEditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;
        private string targetPath;

        public static void ShowSpliter(string path)
        {
            SpriteSpliterWindow wnd = GetWindow<SpriteSpliterWindow>();
            wnd.titleContent = new GUIContent("精灵图分割工具");
            wnd.targetPath = path;
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;
            var visulaTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Editor/Sprite/SpriteSpliter/SpriteSpliterWindow.uxml");
            var labelFromUXML = visulaTree.Instantiate();
            root.Add(labelFromUXML);

            var customPivotInput = root.Q<Vector2Field>("customPivotInput");
            customPivotInput.style.display = DisplayStyle.None;

            var pivotDropdown = root.Q<DropdownField>("pivotDropdown");
            //TODO:添加常见锚点
            pivotDropdown.choices.Add("左上");
            pivotDropdown.choices.Add("上");
            pivotDropdown.choices.Add("右上");
            pivotDropdown.choices.Add("左");
            pivotDropdown.choices.Add("中");
            pivotDropdown.choices.Add("右");
            pivotDropdown.choices.Add("左下");
            pivotDropdown.choices.Add("下");
            pivotDropdown.choices.Add("右下");
            pivotDropdown.choices.Add("自定义");
            pivotDropdown.RegisterValueChangedCallback(evt =>
            {
                bool isCustom = evt.newValue == "自定义";
                customPivotInput.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
            });
            pivotDropdown.value = "中";

            root.Q<Button>("splitButton").clicked += OnSpliteButtonClick;
            root.Q<Label>("targetPathLabel").text = targetPath;
        }

        private void OnSpliteButtonClick()
        {
            var root = rootVisualElement;

            // 1.从输入框获取宽高
            int width = root.Q<IntegerField>("widthInput").value;
            int height = root.Q<IntegerField>("heightInput").value;

            // 2.从下拉框获取锚点
            string pivotValue = root.Q<DropdownField>("pivotDropdown").value;
            Vector2 pivot = pivotValue == "自定义"
                ? root.Q<Vector2Field>("customPivotInput").value
                : GetPivotFromString(pivotValue);

            // 3.处理目标路径
            if (string.IsNullOrEmpty(targetPath))
            {
                Debug.LogWarning("请选择有效的精灵或文件夹路径");
                return;
            }

            // 4/5.判断是文件夹还是单个精灵
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath);
            if (asset is DefaultAsset) // 文件夹
            {
                var textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { targetPath });
                foreach (var guid in textureGUIDs)
                {
                    ProcessTexture(AssetDatabase.GUIDToAssetPath(guid), width, height, pivot);
                }
            }
            else if (asset is Texture2D) // 单个精灵
            {
                ProcessTexture(targetPath, width, height, pivot);
            }

            AssetDatabase.Refresh();
        }

        private Vector2 GetPivotFromString(string pivotValue)
        {
            switch (pivotValue)
            {
                case "左上": return new Vector2(0, 1);
                case "上": return new Vector2(0.5f, 1);
                case "右上": return new Vector2(1, 1);
                case "左": return new Vector2(0, 0.5f);
                case "中": return new Vector2(0.5f, 0.5f);
                case "右": return new Vector2(1, 0.5f);
                case "左下": return new Vector2(0, 0);
                case "下": return new Vector2(0.5f, 0);
                case "右下": return new Vector2(1, 0);
                default:
                    return rootVisualElement.Q<Vector2Field>().value;
            }
        }

        private void ProcessTexture(string path, int width, int height, Vector2 pivot)
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null || importer is not TextureImporter textureImporter || textureImporter.spriteImportMode == SpriteImportMode.Multiple)
                return;

            textureImporter.spriteImportMode = SpriteImportMode.Multiple;
            textureImporter.spritePivot = pivot;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int rows = texture.height / height;
            int columns = texture.width / width;

            var slices = new SpriteMetaData[rows * columns];
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int index = y * columns + x;
                    slices[index] = new SpriteMetaData
                    {
                        name = $"{Path.GetFileNameWithoutExtension(path)}_{x}_{y}",
                        rect = new Rect(x * width, y * height, width, height),
                        pivot = pivot
                    };
                }
            }

            textureImporter.spritesheet = slices;
            EditorUtility.SetDirty(textureImporter);
            textureImporter.SaveAndReimport();
        }
    }
}
