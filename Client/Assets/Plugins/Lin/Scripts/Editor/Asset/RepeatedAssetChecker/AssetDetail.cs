/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
*/
using Cysharp.Text;
using Lin.Runtime.Interface;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;
using ZLinq;

namespace Lin.Editor.Asset
{
    public class AssetDetail : IInitialize<string>, IPoolObject
    {
        public VisualElement root { get; }

        public bool IsInitialized { get; private set; }

        private Label pathLabel;
        private string assetPath;
        private RepeatedAssetCheckerWindow window;

        public AssetDetail(VisualElement detail, RepeatedAssetCheckerWindow window)
        {
            root = detail;
            this.window = window;

            pathLabel = root.Q<Label>("PathLabel");
            
            root.Q<Button>("SelectButton").clicked += OnSelectButtonClick;
            root.Q<Button>("IgnoreAssetButton").clicked += OnIgnoreAssetButtonClick;
            root.Q<Button>("DeleteButton").clicked += OnDeleteButtonClick;
            root.Q<Button>("SelectDependentButton").clicked += OnSelectDepentdentButtonClick;
        }

        public void Initialize(string path)
        {
            if (IsInitialized)
                return;

            IsInitialized = true;

            pathLabel.text = path;
            assetPath = ZString.Concat("Assets/", path);
        }

        public void OnGet() => root.SendToBack();

        public void OnRelease() => root.BringToFront();

        private void OnDeleteButtonClick()
        {
            //TODO: 找出依赖这个资源的资源, 替换成其他现有的资源
            root.visible = false;
        }

        private void OnIgnoreAssetButtonClick()
        {
            window.AddIgnoreAssetPath(assetPath);
        }

        private void OnSelectButtonClick()
        {
            // 加载资源对象
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                // 在Project窗口中选中资源
                Selection.activeObject = asset;
                // 在Project窗口中高亮显示资源
                EditorGUIUtility.PingObject(asset);
            }
        }

        private void OnSelectDepentdentButtonClick()
        {
            // 查找所有依赖这个资源的资源
            var allAssets = AssetDatabase.GetAllAssetPaths().AsValueEnumerable().Where(path => path != assetPath && IsSuitable(path));
            var dependentAssets = new System.Collections.Generic.List<string>();
            
            foreach (var asset in allAssets)
            {
                // 获取该资源的所有依赖
                var dependencies = AssetDatabase.GetDependencies(asset, false);
                
                // 检查是否依赖当前资源
                foreach (var dependency in dependencies)
                {
                    if (dependency == assetPath)
                    {
                        dependentAssets.Add(asset);
                        break;
                    }
                }
            }
            
            // 在Project窗口中选中所有依赖资源
            if (dependentAssets.Count > 0)
            {
                var objects = new UnityEngine.Object[dependentAssets.Count];
                for (int i = 0; i < dependentAssets.Count; i++)
                {
                    objects[i] = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dependentAssets[i]);
                }
                
                Selection.objects = objects;
                
                // 高亮显示第一个依赖资源
                if (objects[0] != null)
                {
                    EditorGUIUtility.PingObject(objects[0]);
                }
                
                UnityEngine.Debug.Log($"找到 {dependentAssets.Count} 个依赖资源: {assetPath}");
            }
            else
            {
                UnityEngine.Debug.Log($"没有找到依赖资源: {assetPath}");
            }
        }

        private static bool IsSuitable(string path)
        {
            if (!path.StartsWith("Assets/"))
                return false;

            if (Directory.Exists(path))
                return false;

            foreach (var ignore in IgnoreAssets)
            {
                if (path.EndsWith(ignore))
                    return false;
            }

            return true;
        }

        private static readonly string[] IgnoreAssets = new string[]
        {
            ".cs",
            ".json",
            ".uxml",
            ".bytes",
            ".xml",
            ".yml",
            ".txt",
            ".tif",
            ".ttf",

            ".asmref",
            ".dll",

            ".wav",
            ".mp3",

            ".png",
            ".jpg",
            ".jpeg",
            ".psd",

            ".pdf",
            ".unitypackage",

            ".shader",
            ".hlsl",
        };
    }
}
