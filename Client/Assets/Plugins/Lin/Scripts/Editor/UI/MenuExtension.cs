using Lin.Editor.Helper;
using Lin.Editor.SpriteTool;
using Lin.Editor.UI.UIControl;
using Lin.Runtime.Helper;
using Lin.Runtime.UI;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Lin.Editor.UI
{
    public static class MenuExtension
    {
        #region - Mark UI -

        [MenuItem("GameObject/UI/标记UI组件", false, 0)]
        private static void MarkUIComponent()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0) 
                return;

            foreach (var selectedObject in selectedObjects)
                MarkUIComponent(selectedObject);
        }

        public static void MarkUIComponent(GameObject target)
        {
            var uiComponent = GetMainUIComponent(target);
            if (uiComponent == null)
                return;

            var componentType = uiComponent.GetType().Name;
            var currentName = target.name;

            // 如果已经有标记，则不重复添加
            if (currentName.StartsWith($"[{componentType}]"))
                return;

            target.name = $"[{componentType}] {currentName}";
            EditorUtility.SetDirty(target);
        }

        [MenuItem("GameObject/UI/标记UI组件", true)]
        private static bool ValidateMarkUIComponent()
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null) return false;

            return GetMainUIComponent(selectedObject) != null;
        }

        private static Component GetMainUIComponent(GameObject gameObject)
        {
            // 按优先级检查UI组件
            if (gameObject.TryGetComponent<Button>(out var button)) return button;
            if (gameObject.TryGetComponent<Text>(out var text)) return text;
            if (gameObject.TryGetComponent<InputField>(out var inputField)) return inputField;
            if (gameObject.TryGetComponent<Slider>(out var slider)) return slider;
            if (gameObject.TryGetComponent<Scrollbar>(out var scrollbar)) return scrollbar;
            if (gameObject.TryGetComponent<Dropdown>(out var dropdown)) return dropdown;

            if (gameObject.TryGetComponent<InfiniteScroller>(out var infiniteScrollRect)) return infiniteScrollRect;
            if (gameObject.TryGetComponent<ScrollRect>(out var scrollRect)) return scrollRect;

            if (gameObject.TryGetComponent<RawImage>(out var rawImage)) return rawImage;
            if (gameObject.TryGetComponent<Image>(out var image)) return image;

            if (gameObject.TryGetComponent<TMP_Text>(out var tmp)) return tmp;
            if (gameObject.TryGetComponent<TMP_InputField>(out var tmpInput)) return tmpInput;

            if (gameObject.TryGetComponent<Toggle>(out var toggle)) return toggle;
            if (gameObject.TryGetComponent<ToggleGroup>(out var toggleGroup)) return toggleGroup;

            return gameObject.transform as RectTransform;
        }

        public static void UnmarkUIComponent(GameObject gameObject)
        {
            var currentName = gameObject.name;
            var startIndex = currentName.IndexOf('[');
            var endIndex = currentName.IndexOf(']');

            if (startIndex == -1 || endIndex == -1)
                return;

            // 移除标记部分，保留原始名称
            var originalName = currentName.Substring(endIndex + 2);
            gameObject.name = originalName;
            EditorUtility.SetDirty(gameObject);
        }

        #endregion

        #region - Generate Control -

        [MenuItem("Lin/UI/生成UI控件", true), MenuItem("GameObject/UI/生成UI控件", true)]
        private static bool ValidateGenerateControl()
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null) return false;

            return selectedObject.GetComponent<RectTransform>() != null && !selectedObject.TryGetComponent<Canvas>(out _);
        }

        [MenuItem("Lin/UI/生成UI控件"), MenuItem("GameObject/UI/生成UI控件")]
        public static void GenerateControl()
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
                return;

            // 检查是否是UI类型物体
            if (!selectedObject.GetComponent<RectTransform>() || selectedObject.TryGetComponent<Canvas>(out _))
                return;

            // 生成预制体
            var globalConfig = GlobalConfig_SO.GetInstance();
            var prefabPath = $"{globalConfig.prefabDirectory}/UIControl/{selectedObject.name}.prefab";
            var prefabDir = Path.GetDirectoryName(prefabPath);
            if (!Directory.Exists(prefabDir))
                Directory.CreateDirectory(prefabDir);

            // 检查是否存在同名预制体，如果存在则添加数字后缀
            var basePath = prefabPath;
            var counter = 1;
            while (File.Exists(prefabPath))
            {
                var fileName = Path.GetFileNameWithoutExtension(basePath);
                var extension = Path.GetExtension(basePath);
                prefabPath = $"{globalConfig.prefabDirectory}/UIControl/{fileName}_{counter}{extension}";
                counter++;
            }

            PrefabUtility.SaveAsPrefabAsset(selectedObject, prefabPath);
            AssetDatabase.Refresh();

            string originalScene = SceneManager.GetActiveScene().path;
            UIPreviewElement.LoadPreview(prefabPath);

            if (originalScene != SceneManager.GetActiveScene().path)
                EditorSceneManager.OpenScene(originalScene);
        }

        #endregion

        #region - Preview -

        [MenuItem("GameObject/UI/加载预览图")]
        private static void LoadPreviewTexture()
        {
            LoadPreviewTexture(Selection.activeGameObject);
        }

        public static void LoadPreviewTexture(GameObject panelObject)
        {

            var path = UnityEditor.EditorUtility.OpenFilePanel("请选择UI目标图片", "", "png,jpg");
            if (string.IsNullOrEmpty(path)) return;

            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(bytes);

            var rawImage = panelObject.transform.Find("Preview")?.GetComponent<RawImage>() ?? new GameObject("Preview").AddComponent<RawImage>();
            rawImage.transform.SetParent(panelObject.transform, false);
            rawImage.texture = texture;
            rawImage.SetNativeSize();
            rawImage.transform.SetAsLastSibling();
        }

        [MenuItem("GameObject/UI/加载预览图", true)]
        private static bool ValidateLoadPreviewTexture()
        {
            var selectedObject = Selection.activeGameObject;
            return selectedObject != null && selectedObject.name.Contains("Panel");
        }

        [MenuItem("GameObject/UI/优化Batch")]
        private static void OptimizeBatchForMenu() => OptimizeBatch(Selection.activeGameObject);

        public static void OptimizeBatch(GameObject uiObject, bool refreshAssetDatabase = false)
        {
            if (uiObject == null)
                return;

            HashSet<TextureImporter> toPack = new HashSet<TextureImporter>();
            OptimizeBatch(uiObject, uiObject.name, toPack);

            //打包图集
            if (toPack.Count > 0)
                SpriteAtlasHelper.PackSpriteAtlas(toPack, uiObject.name);

            // 收集所有MaskableGraphic组件及其Sprite信息
            var panelBase = uiObject.GetComponent<PanelBase>();
            if (!panelBase)
                return;

            HashSet<string> loadedAltas = new HashSet<string>();
            var gc = GlobalConfig_SO.GetInstance();
            foreach ( var graphic in uiObject.GetComponentsInChildren<Graphic>())
            {
                var texture = graphic.mainTexture;
                if (texture == null)
                    continue;

                var assetPath = AssetDatabase.GetAssetPath(texture);
                if (!assetPath.StartsWith("Assets"))
                    continue;

                var atlasName = (AssetImporter.GetAtPath(assetPath) as TextureImporter).GetSpriteAtlasTag();
                if (string.IsNullOrEmpty(atlasName) || loadedAltas.Contains(atlasName)) 
                    continue;

                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>($"{gc.prefabDirectory}/SpriteAltas/{atlasName}.spriteatlas");
                loadedAltas.Add(atlasName);
                panelBase.usingAtlas.Add(atlas);
            }

            panelBase.EditorSave(refreshAssetDatabase);
        }

        /// <summary>
        /// 优化UI批处理，通过调整相同图集的Image和相同字体的Text组件的渲染顺序来减少Draw Call
        /// </summary>
        /// <param name="uiObject">需要优化的根节点Transform</param>
        private static void OptimizeBatch(GameObject uiObject, string rootPanelName, HashSet<TextureImporter> toPack)
        {
            // 用于存储相同图集的Image组件（List 而非 HashSet，保留插入顺序以便用 IndexOf 定位分组）
            List<string> atlasList = new List<string>();
            
            // 用于存储相同字体的Text组件
            Dictionary<string, List<Transform>> textGroup = new Dictionary<string, List<Transform>>();

            // 保持图集分组的添加顺序
            List<List<Transform>> sortedImgageGroup = new List<List<Transform>>();

            // 保持字体分组的添加顺序
            List<List<Transform>> sortedTextGroup = new List<List<Transform>>();

            // 遍历所有子节点
            for (int i = 0; i < uiObject.transform.childCount; i++)
            {
                // 获取当前子节点
                Transform child = uiObject.transform.GetChild(i);
                var maskableGraphic = child.GetComponent<MaskableGraphic>();

                switch (maskableGraphic)
                {
                    case Image:
                    case RawImage:
                        Add2ImageGroup(child, maskableGraphic.mainTexture);
                        break;

                    case Text text:
                        // 获取字体名称
                        string fontName = text.font.name;
                        if (!textGroup.ContainsKey(fontName))
                        {
                            // 如果字体分组不存在，创建新的分组
                            List<Transform> list = new List<Transform>();
                            sortedTextGroup.Add(list);
                            textGroup.Add(fontName, list);
                        }
                        // 将当前节点添加到对应的字体分组
                        textGroup[fontName].Add(child);
                        break;

                    case TextMeshProUGUI tmp:

                        // 获取字体名称
                        fontName = tmp.font.name;
                        if (!textGroup.ContainsKey(fontName))
                        {
                            // 如果字体分组不存在，创建新的分组
                            List<Transform> list = new List<Transform>();
                            sortedTextGroup.Add(list);
                            textGroup.Add(fontName, list);
                        }
                        // 将当前节点添加到对应的字体分组
                        textGroup[fontName].Add(child);
                        break;

                    default:
                        break;
                }

                // 递归处理子节点
                OptimizeBatch(child.gameObject, rootPanelName, toPack);
            }

            // 调整渲染顺序：将相同图集的Image放在一起，并保持它们之间的相对顺序
            for (int i = sortedImgageGroup.Count - 1; i >= 0; i--)
            {
                List<Transform> children = sortedImgageGroup[i];
                for (int j = children.Count - 1; j >= 0; j--)
                {
                    // 将同一图集的Image移到最前面
                    children[j].SetAsFirstSibling();
                }
            }

            // 调整渲染顺序：将相同字体的Text放在一起，并保持它们之间的相对顺序
            foreach (var item in sortedTextGroup)
            {
                List<Transform> children = item;
                for (int i = 0; i < children.Count; i++)
                {
                    // 将同一字体的Text移到最后面
                    children[i].SetAsLastSibling();
                }
            }

            Add2ImageGroup(uiObject.transform, uiObject.GetComponent<MaskableGraphic>()?.mainTexture);

            void Add2ImageGroup(Transform transform, Texture target)
            {
                if (target == null)
                    return;

                // 获取贴图的资源路径
                string cur_path = AssetDatabase.GetAssetPath(target);
                TextureImporter importer = AssetImporter.GetAtPath(cur_path) as TextureImporter;
                if (importer != null)
                {
                    // 获取贴图所属的图集标签
                    string atlas = importer.GetSpriteAtlasTag();
                    if (atlas is null)
                    {
                        atlas = rootPanelName;
                        toPack.Add(importer);
                    }
                    else
                    {
                        // 已有 SpriteAtlasTag：校验对应 SpriteAtlas 资产是否健康（存在 + 已包含该贴图）
                        VerifySpriteAtlasMembership(importer, atlas, cur_path);
                    }

                    // 如果图集不存在，创建新的分组
                    if (!atlasList.Contains(atlas))
                    {
                        sortedImgageGroup.Add(new List<Transform>());
                        atlasList.Add(atlas);
                    }
                    // 把当前 Transform 加入对应图集分组（atlasList 与 sortedImgageGroup 索引一一对应）
                    sortedImgageGroup[atlasList.IndexOf(atlas)].Add(transform);
                }
            }

            void VerifySpriteAtlasMembership(TextureImporter importer, string atlasName, string texturePath)
            {
                var outputPath = $"{GlobalConfig_SO.GetInstance().prefabDirectory}/SpriteAtlas/{atlasName}.spriteatlas";

                SpriteAtlas spriteAtlas;
                // (1) SpriteAtlas 资产是否存在，不存在则重建并补加该贴图
                if (!File.Exists(outputPath) || AssetDatabase.LoadAssetAtPath<SpriteAtlas>(outputPath) == null)
                {
                    IOHelper.InsureExist(Path.GetDirectoryName(outputPath), false, false);
                    spriteAtlas = SpriteAtlasHelper.CreateSpriteAtlas();
                    AssetDatabase.CreateAsset(spriteAtlas, outputPath);
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(texturePath);
                    if (obj != null)
                    {
                        SpriteAtlasExtensions.Add(spriteAtlas, new[] { obj });
                        EditorUtility.SetDirty(spriteAtlas);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(outputPath);
                    }
                    Log.Debug(nameof(MenuExtension), $"SpriteAtlas '{atlasName}' 资产不存在已重建并补加贴图 '{texturePath}'", importer);
                    return;
                }
                else
                    spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(outputPath);

                // (2) SpriteAtlas 是否已包含该贴图（packable 可能是单张图，也可能是文件夹）
                bool found = false;
                foreach (var packable in SpriteAtlasExtensions.GetPackables(spriteAtlas))
                {
                    var packablePath = AssetDatabase.GetAssetPath(packable);
                    if (string.IsNullOrEmpty(packablePath))
                        continue;

                    if (packablePath == texturePath)
                    {
                        found = true;
                        break;
                    }

                    // packable 是文件夹时，校验贴图是否在其下
                    if (AssetDatabase.IsValidFolder(packablePath) && texturePath.StartsWith(packablePath, System.StringComparison.Ordinal))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // SpriteAtlas 存在但未包含该贴图，补加上去
                    var obj = AssetDatabase.LoadAssetAtPath<Object>(texturePath);
                    if (obj != null)
                    {
                        SpriteAtlasExtensions.Add(spriteAtlas, new[] { obj });
                        EditorUtility.SetDirty(spriteAtlas);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(outputPath);
                        Log.Debug(nameof(MenuExtension), $"SpriteAtlas '{atlasName}' 中已补加贴图 '{texturePath}'", importer);
                    }
                    else
                        Debug.LogWarning($"[OptimizeBatch] 无法加载贴图 '{texturePath}'，跳过补加", importer);
                }
            }
        }

        #endregion

        [MenuItem("Lin/UI/生成Panel预制体")]
        [MenuItem("GameObject/UI/生成Panel预制体")]
        private static void GenerateUIScripts()
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Debug.Log("错误: 请先选择一个UI面板对象");
                return;
            }

            //生成图集，优化Batch
            OptimizeBatch(selectedObject);
            //生成脚本
            Generator.GeneratePanelScripts(selectedObject);
        }

        [MenuItem("Lin/UI/生成UI预制体")]
        [MenuItem("GameObject/UI/生成UI预制体")]
        private static void GenerateUIItemScripts()
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                Debug.Log("错误: 请先选择一个UI面板对象");
                return;
            }
            //生成图集，优化Batch
            OptimizeBatch(selectedObject);
            //生成脚本
            Generator.GenerateItemScripts(selectedObject);
        }

        #region - Text to TMP -

        /// <summary>
        /// 一键将 UnityEngine.UI.Text 替换为 TextMeshProUGUI
        /// 范围：Hierarchy 中选中的 GameObject（包含子物体），无选中时作用于整个当前激活场景
        /// 保留属性：文本内容、字号、字体样式、颜色、对齐、行间距、富文本、超出模式、射线检测、自动字号
        /// 注意：UnityEngine.Font 资产无法直接映射为 TMP_FontAsset，字体资产需手动指定
        /// </summary>
        [MenuItem("GameObject/UI/将Text替换为TextMeshProUGUI")]
        private static void ReplaceTextWithTMP()
        {
            // 收集目标根节点：优先使用 Hierarchy 选中物体，否则使用当前激活场景的所有根节点
            var targetRoots = new List<GameObject>();
            var selectedObjects = Selection.gameObjects;

            if (selectedObjects != null && selectedObjects.Length > 0)
            {
                foreach (var go in selectedObjects)
                    targetRoots.Add(go);
            }
            else
            {
                var activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    Debug.LogWarning("[替换Text为TMP] 当前没有打开的场景");
                    return;
                }

                foreach (var root in activeScene.GetRootGameObjects())
                    targetRoots.Add(root);
            }

            int replacedCount = 0;
            foreach (var root in targetRoots)
            {
                // true：包含未激活的 GameObject
                var texts = root.GetComponentsInChildren<Text>(true);
                foreach (var text in texts)
                {
                    if (text == null)
                        continue;

                    if (ReplaceSingleTextWithTMP(text))
                        replacedCount++;
                }
            }

            if (replacedCount > 0)
                Debug.Log($"[替换Text为TMP] 已完成，共替换 {replacedCount} 个 Text 组件");
            else
                Debug.Log("[替换Text为TMP] 未找到任何 UnityEngine.UI.Text 组件");
        }

        /// <summary>
        /// 将单个 Text 替换为 TextMeshProUGUI，保留可映射的属性
        /// </summary>
        /// <param name="text">原始 Text 组件</param>
        /// <returns>是否成功替换</returns>
        private static bool ReplaceSingleTextWithTMP(Text text)
        {
            var go = text.gameObject;

            // 备份可映射的属性
            string textStr = text.text;
            float fontSize = text.fontSize;
            FontStyle fontStyle = text.fontStyle;
            Color color = text.color;
            TextAnchor alignment = text.alignment;
            float lineSpacing = text.lineSpacing;
            bool richText = text.supportRichText;
            HorizontalWrapMode horizontalOverflow = text.horizontalOverflow;
            VerticalWrapMode verticalOverflow = text.verticalOverflow;
            bool raycastTarget = text.raycastTarget;
            bool resizeTextForBestFit = text.resizeTextForBestFit;
            int fontSizeMin = text.resizeTextMinSize;
            int fontSizeMax = text.resizeTextMaxSize;
            string fontName = text.font != null ? text.font.name : null;

            // 删除旧组件（支持撤销）
            Undo.DestroyObjectImmediate(text);

            // 添加新组件
            var tmp = Undo.AddComponent<TextMeshProUGUI>(go);

            // 还原属性
            tmp.text = textStr;
            tmp.fontSize = fontSize;
            tmp.fontStyle = ConvertFontStyle(fontStyle);
            tmp.color = color;
            tmp.alignment = ConvertAlignment(alignment);
            // Text 的 lineSpacing 是行高的乘数（1=无额外间距），TMP 的 lineSpacing 是按像素的额外间距，做近似换算
            tmp.lineSpacing = Mathf.Max(0f, (lineSpacing - 1f) * fontSize);
            tmp.richText = richText;
            tmp.raycastTarget = raycastTarget;
            tmp.overflowMode = ConvertOverflowMode(horizontalOverflow, verticalOverflow);

            if (resizeTextForBestFit)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = fontSizeMin;
                tmp.fontSizeMax = fontSizeMax;
            }

            // 字体资产无法直接转换，提示用户手动设置
            if (!string.IsNullOrEmpty(fontName))
                Debug.LogWarning($"[替换Text为TMP] '{go.name}' 的字体 '{fontName}' 需手动指定 TMP 字体资产", go);

            EditorUtility.SetDirty(go);
            return true;
        }

        /// <summary>
        /// UnityEngine.FontStyle -> TMPro.FontStyles
        /// </summary>
        private static FontStyles ConvertFontStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Normal:
                    return FontStyles.Normal;
                case FontStyle.Bold:
                    return FontStyles.Bold;
                case FontStyle.Italic:
                    return FontStyles.Italic;
                case FontStyle.BoldAndItalic:
                    return FontStyles.Bold | FontStyles.Italic;
                default:
                    return FontStyles.Normal;
            }
        }

        /// <summary>
        /// UnityEngine.TextAnchor -> TMPro.TextAlignmentOptions
        /// </summary>
        private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter:
                    return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }

        /// <summary>
        /// 水平/垂直超出模式 -> TMP.overflowMode
        /// TMP 的 overflowMode 同时控制水平和垂直；任一为 Overflow 即按 Overflow 处理
        /// </summary>
        private static TextOverflowModes ConvertOverflowMode(HorizontalWrapMode horizontal, VerticalWrapMode vertical)
        {
            if (horizontal == HorizontalWrapMode.Overflow || vertical == VerticalWrapMode.Overflow)
                return TextOverflowModes.Overflow;
            return TextOverflowModes.Truncate;
        }

        #endregion
    }
}