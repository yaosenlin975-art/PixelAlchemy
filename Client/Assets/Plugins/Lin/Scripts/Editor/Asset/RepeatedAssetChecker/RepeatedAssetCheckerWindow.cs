using Cysharp.Text;
using Lin.Runtime.Helper;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;
using ZLinq;

namespace Lin.Editor.Asset
{
    public struct FileMD5Job : IJob
    {
        [ReadOnly]
        public NativeArray<byte> filePath;
        
        public NativeArray<byte> result;
        
        public void Execute()
        {
            // 将NativeArray<byte>转换为string
            var pathBytes = filePath.ToArray();
            var path = System.Text.Encoding.UTF8.GetString(pathBytes);
            
            // 计算MD5
            var md5 = HashHelper.FileMD5(path);
            var md5Bytes = System.Text.Encoding.UTF8.GetBytes(md5);
            
            // 将结果写入NativeArray
            if (result.Length >= md5Bytes.Length)
            {
                for (int i = 0; i < md5Bytes.Length; i++)
                {
                    result[i] = md5Bytes[i];
                }
            }
        }
    }

    public class RepeatedAssetCheckerWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        private VisualTreeAsset containerAsset;
        private ObjectPool<AssetDetailContainer> containerPool;
        private Queue<AssetDetailContainer> usingContainers;

        private VisualTreeAsset detailAsset;
        private ObjectPool<AssetDetail> detailPool;

        private HashSet<string> ignoreAssetMD5s;
        private const string IGNORE_ASSET_MD5S_KEY = nameof(IGNORE_ASSET_MD5S_KEY);
        private HashSet<string> ignoreAssetPaths;
        private const string IGNORE_ASSET_PATHS_KEY = nameof(IGNORE_ASSET_PATHS_KEY);
        private HashSet<string> ignoreFolders;
        private const string IGNORE_FOLDERS_KEY = nameof(IGNORE_FOLDERS_KEY);
        private bool shouldSave = false;

        private readonly static string[] assetExtensions = new string[] {
            ".jpg", ".png", //纹理
            ".fbx",         //模型, 动画
            ".wav", ".mp3"  //声音
        };

        private ScrollView assetsViewer;

        [MenuItem("Lin/重复资源探测")]
        public static void ShowExample()
        {
            RepeatedAssetCheckerWindow wnd = GetWindow<RepeatedAssetCheckerWindow>();
            wnd.titleContent = new GUIContent("重复资源探测");
            wnd.minSize = Vector2.one * 520;
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;
            var windowAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Lin/Scripts/Editor/Asset/RepeatedAssetChecker/RepeatedAssetCheckerWindow.uxml");
            root.Add(windowAsset.Instantiate());

            ignoreAssetMD5s = PrefsHelper.Get(IGNORE_ASSET_MD5S_KEY, new HashSet<string>());
            ignoreAssetPaths = PrefsHelper.Get(IGNORE_ASSET_PATHS_KEY, new HashSet<string>());
            ignoreFolders = PrefsHelper.Get(IGNORE_FOLDERS_KEY, new HashSet<string>());

            containerAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Lin/Scripts/Editor/Asset/RepeatedAssetChecker/AssetDetailContainer.uxml");
            detailAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Lin/Scripts/Editor/Asset/RepeatedAssetChecker/AssetDetail.uxml");

            root.Q<Button>("RefreshListBtn").clicked += OnRefreshListButtonClick;
            assetsViewer = root.Q<ScrollView>("AssetsViewer");

            containerPool = new ObjectPool<AssetDetailContainer>(CreateContainer, OnContainerGet, OnContainerRelease);
            usingContainers = new Queue<AssetDetailContainer>();
            detailPool = new ObjectPool<AssetDetail>(CreateDetail, OnDetailGet, OnDetailRelease);

            OnRefreshListButtonClick();
        }

        private void OnGUI()
        {
            //键盘检测
            Event e = Event.current;
            if (e == null)
                return;

            if (e.type == EventType.KeyDown && e.control)
            {
                if (e.keyCode == KeyCode.S)
                {
                    OnDisable();
                    titleContent.text = "重复资源探测";
                    e.Use();
                }
            }
        }

        private void OnDisable()
        {
            if (shouldSave)
            {
                PrefsHelper.Set(IGNORE_FOLDERS_KEY, ignoreFolders);
                PrefsHelper.Set(IGNORE_ASSET_PATHS_KEY, ignoreAssetPaths);
                PrefsHelper.Set(IGNORE_ASSET_MD5S_KEY, ignoreAssetMD5s);

                shouldSave = false;
            }
        }

        #region - 对象池 -

        private void OnDetailRelease(AssetDetail element) => element.OnRelease();

        private void OnDetailGet(AssetDetail element) => element.OnGet();

        private AssetDetail CreateDetail()
        {
            var detail = detailAsset.Instantiate();
            return new AssetDetail(detail, this);
        }

        private void OnContainerRelease(AssetDetailContainer element) => element.OnRelease();

        private void OnContainerGet(AssetDetailContainer asset)
        {
            usingContainers.Enqueue(asset);
            asset.OnGet();
        }

        private AssetDetailContainer CreateContainer()
        {
            var container = containerAsset.Instantiate();
            assetsViewer.contentContainer.Add(container);
            return new AssetDetailContainer(container, this);
        }

        public AssetDetail GetDetail() => detailPool.Get();

        public void ReleaseDetail(AssetDetail target) => detailPool.Release(target);

        public void ReleaseContainer(AssetDetailContainer target) => containerPool.Release(target);

        #endregion

        private void ShouldSave()
        {
            shouldSave = true;
            titleContent.text = "重复资源探测 *";
        }

        public void AddIgnoreAssetPath(string path)
        {
            if (!path.StartsWith("Assets/"))
                path = path.Replace(Application.dataPath, "Assets").Replace("\\", "/");
            ignoreAssetPaths.Add(path);
            ShouldSave();
        }

        public void AddIgnoreAssetMD5(string md5)
        {
            ignoreAssetMD5s.Add(md5);
            ShouldSave();
        }

        public void AddIgnoreFolder(string folder)
        {
            ignoreFolders.Add(folder);
            ShouldSave();
        }

        private async void OnRefreshListButtonClick()
        {
            //刷新列表
            while (usingContainers.TryDequeue(out var container))
            {
                try
                {
                    containerPool.Release(container);
                }
                catch (System.Exception) { }
            }

            //重新检测
            var folder = Application.dataPath;
            List<Task<Dictionary<string, List<string>>>> tasks = new List<Task<Dictionary<string, List<string>>>>(assetExtensions.Length);
            foreach (var asset in assetExtensions)
                tasks.Add(Check(asset, folder));

            await Task.WhenAll(tasks);
            List<List<string>> repeates = new List<List<string>>();
            foreach (var task in tasks)
            {
                var map = task.Result;
                foreach (var list in map.Values.AsValueEnumerable())
                {
                    if (list.Count > 1)
                        repeates.Add(list);
                }
            }

            foreach (var repeate in repeates)
            {
                var container = containerPool.Get();
                container.Initialize(repeate);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="extension"></param>
        /// <returns>key: md5, Value: Assets</returns>
        private async Task<Dictionary<string, List<string>>> Check(string extension, string folder)
        {
            if (!Directory.Exists(folder))
                return null;

            var result = new Dictionary<string, List<string>>();
            var files = Directory.GetFiles(folder, $"*{extension}", SearchOption.AllDirectories);
            
            // 过滤掉忽略的路径
            var validFiles = new List<string>();
            foreach (var file in files)
            {
                string path = ZString.Concat("Assets", file.Replace(folder, string.Empty).Replace("\\", "/"));
                if (!ignoreAssetPaths.Contains(path))
                {
                    validFiles.Add(file);
                }
            }

            if (validFiles.Count == 0)
                return result;

            // 创建Job数组和相关的NativeArray
            var jobHandles = new NativeArray<JobHandle>(validFiles.Count, Allocator.TempJob);
            var filePathArrays = new NativeArray<NativeArray<byte>>(validFiles.Count, Allocator.TempJob);
            var resultArrays = new NativeArray<NativeArray<byte>>(validFiles.Count, Allocator.TempJob);

            // 为每个文件创建Job
            for (int i = 0; i < validFiles.Count; i++)
            {
                var filePathBytes = System.Text.Encoding.UTF8.GetBytes(validFiles[i]);
                filePathArrays[i] = new NativeArray<byte>(filePathBytes, Allocator.TempJob);
                resultArrays[i] = new NativeArray<byte>(32, Allocator.TempJob); // MD5通常32字符

                var job = new FileMD5Job
                {
                    filePath = filePathArrays[i],
                    result = resultArrays[i]
                };

                jobHandles[i] = job.Schedule();
            }

            // 等待所有Job完成
            JobHandle.CompleteAll(jobHandles);

            // 收集结果
            for (int i = 0; i < validFiles.Count; i++)
            {
                var md5Bytes = resultArrays[i].ToArray();
                var md5 = System.Text.Encoding.UTF8.GetString(md5Bytes).TrimEnd('\0');
                
                if (!ignoreAssetMD5s.Contains(md5))
                {
                    if (!result.TryGetValue(md5, out var list))
                    {
                        list = new List<string>();
                        result.Add(md5, list);
                    }
                    list.Add(validFiles[i]);
                }
            }

            // 清理NativeArray
            for (int i = 0; i < validFiles.Count; i++)
            {
                if (filePathArrays[i].IsCreated)
                    filePathArrays[i].Dispose();
                if (resultArrays[i].IsCreated)
                    resultArrays[i].Dispose();
            }
            
            jobHandles.Dispose();
            filePathArrays.Dispose();
            resultArrays.Dispose();

            await Task.CompletedTask;
            return result;
        }
    }
}
