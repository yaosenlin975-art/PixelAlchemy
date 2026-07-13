/*
┌────────────────────────────┐
│　Description：控件预览
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：UIPreviewElement
└──────────────┘
*/

using UnityEngine.UIElements;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement;
using Lin.Runtime.Helper;

namespace Lin.Editor.UI.UIControl
{
    public class UIPreviewElement : VisualElement
    {
        private const string PREVIEW_FOLDER = "UIControlPreviews";

        public string assetPath { get; }
        
        public UIPreviewElement(string uiPrefabPath)
        {
            assetPath = uiPrefabPath;

            // 加载预览图并显示
            string previewPath = LoadPreview(uiPrefabPath);
            var texture = new Texture2D(2, 2);
            if (File.Exists(previewPath))
            {
                var imageData = File.ReadAllBytes(previewPath);
                texture.LoadImage(imageData);
            }
            var image = new Image { image = texture };
            Add(image);

            // 添加1像素灰色边框
            style.borderLeftWidth = style.borderRightWidth = style.borderTopWidth = style.borderBottomWidth = 1;
            style.borderLeftColor = style.borderRightColor = style.borderTopColor = style.borderBottomColor = new Color(0.5f, 0.5f, 0.5f);

            // 添加底部控件名称标签
            var label = new Label(Path.GetFileNameWithoutExtension(uiPrefabPath));
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginTop = 5;
            Add(label);

            // 添加拖拽功能
            RegisterCallback<MouseDownEvent>(evt =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(uiPrefabPath);
                if (prefab == null) return;

                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { prefab };
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                DragAndDrop.StartDrag(prefab.name);

                // 在Project窗口中高亮显示预制体
                EditorGUIUtility.PingObject(prefab);

                evt.StopPropagation();
            });
        }

        public static string LoadPreview(string uiPrefabPath)
        {
            // 确保预览图文件夹存在
            var previewDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), PREVIEW_FOLDER);
            IOHelper.InsureExist(previewDir, false, false);

            // 生成预览图路径
            var previewName = Path.GetFileNameWithoutExtension(uiPrefabPath.Replace('/', '_')) + "_preview.png";
            var previewPath = Path.Combine(previewDir, previewName);

            // 如果预览图不存在，则创建
            if (!File.Exists(previewPath))
            {
                // 创建临时场景
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // 创建UI相机
                var camera = new GameObject("UIPreviewCamera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
                camera.orthographic = true;

                // 加载UI预制体
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(uiPrefabPath);
                if (prefab == null) 
                    return string.Empty;

                // 查找或创建Canvas
                var canvas = Object.FindObjectOfType<Canvas>();
                if (canvas == null)
                {
                    var canvasPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefab/Canvas.prefab");
                    if (canvasPrefab == null)
                        return string.Empty;
                    canvas = Object.Instantiate(canvasPrefab).GetComponent<Canvas>();
                }

                // 设置Canvas的渲染相机
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;

                // 实例化预制体
                var instance = Object.Instantiate(prefab, canvas.transform);

                // 调整相机位置和大小
                var rect = instance.GetComponent<RectTransform>().rect;
                camera.orthographicSize = rect.height / 2;
                camera.transform.position = new Vector3(rect.center.x, rect.center.y, -10);

                // 创建RenderTexture并设置相机
                var rt = new RenderTexture((int)rect.width, (int)rect.height, 24);
                camera.targetTexture = rt;

                // 渲染相机
                camera.Render();

                // 创建Texture2D并读取RenderTexture
                var texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, rect.width, rect.height), 0, 0);
                texture.Apply();

                // 保存为PNG
                File.WriteAllBytes(previewPath, texture.EncodeToPNG());

                // 清理资源
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(instance);
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(texture);
            }

            return previewPath;
        }
    }
}