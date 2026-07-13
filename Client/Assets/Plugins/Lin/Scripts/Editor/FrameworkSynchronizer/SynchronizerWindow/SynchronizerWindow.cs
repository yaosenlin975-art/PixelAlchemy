using Cysharp.Text;
using Lin.Runtime.Helper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Lin.Editor.FrameworkSynchronizer
{

    public partial class SynchronizerWindow : EditorWindowBase
    {
        private static string[] ignoreExtensions = new string[]
        {
            ".asmdef"
        };

        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;
        private volatile bool showGuidScanProgress;
        private int guidScanDone;
        private int guidScanTotal;

        [MenuItem("Lin/框架同步")]
        public static void ShowExample()
        {
            SynchronizerWindow wnd = GetWindow<SynchronizerWindow>();
            wnd.titleContent = new GUIContent("框架同步");
        }

        private void OnEnable()
        {
            SynchronizerConfig.GetInstance();
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            root.Add(m_VisualTreeAsset.Instantiate());

            PluginsViewerInit();
            ToolProjectsViewerInit();
            ExtraAssetsViwerInit();
            TargetProjectsViewerInit();
            IgnoreAssetsViwerInit();

            root.Q<Button>("SyncButton").clicked += OnSyncBtnClick;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        }

        private void BeforeAssemblyReload()
        {
            EditorUtility.ClearProgressBar();
        }

        protected override void Refresh()
        {
            RefreshPluginsViewer();
            RefreshExtraAssetsViwer();
            RefreshTargetProjectsViewer();
            RefreshToolProjectsViewer();
            RefreshIgnoreAssetsViwer();
        }

        protected override void OnSave() => SynchronizerConfig.GetInstance().Save();

        private void OnSyncBtnClick()
        {
            var cfg = SynchronizerConfig.GetInstance();
            cfg.Save();

            var results = new List<AssetInfos>(1024);
            var absMap = new Dictionary<string, string>(1024);
            CollectSelectedAssets(cfg, results, absMap);
            ApplyIgnores(cfg, results, absMap);

            var ctx = new SyncContext
            {
                results = results,
                absMap = absMap,
                md5Map = new ConcurrentDictionary<string, string>(),
                swMd5 = new Stopwatch(),
                swCopy = new Stopwatch(),
                copyProgress = new CopyProgress(),
                copyTotal = Math.Max(1, results.Count * Math.Max(1, cfg.targetProejcts.Count)),
            };

            ctx.overallTask = Task.Run(() =>
            {
                ComputeMd5Phase(ctx);
                CopyPhase(ctx, cfg);
            });

            AttachProgressUpdater(ctx);
        }

        private void CollectSelectedAssets(SynchronizerConfig cfg, List<AssetInfos> results, Dictionary<string, string> absMap)
        {
            foreach (var pair in cfg.plugins.AsValueEnumerable())
                if (pair.Value)
                    CollectFromPath(pair.Key, results, absMap);

            foreach (var path in cfg.extraAssets.AsValueEnumerable())
                CollectAssetWithDependencies(path, results, absMap);

            foreach (var pair in cfg.toolProejcts.AsValueEnumerable())
                if (pair.Value)
                    CollectFromPath(pair.Key, results, absMap);
        }

        private void ApplyIgnores(SynchronizerConfig cfg, List<AssetInfos> results, Dictionary<string, string> absMap)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var ignore in cfg.ignoreAssets.AsValueEnumerable())
            {
                var fullPath = Path.Combine(projectRoot, ignore);
                var isFolder = AssetDatabase.IsValidFolder(ignore) || Directory.Exists(fullPath);

                if (isFolder)
                {
                    results.RemoveAll(a => a.relativePath.StartsWith(ignore, StringComparison.OrdinalIgnoreCase));

                    var removeKeys = new List<string>();
                    foreach (var kv in absMap)
                        if (kv.Key.StartsWith(ignore, StringComparison.OrdinalIgnoreCase))
                            removeKeys.Add(kv.Key);

                    foreach (var key in removeKeys)
                        absMap.Remove(key);
                }
                else
                {
                    results.RemoveAll(a => string.Equals(a.relativePath, ignore, StringComparison.OrdinalIgnoreCase));
                    absMap.Remove(ignore);
                }
            }
        }

        private void ComputeMd5Phase(SyncContext ctx)
        {
            ctx.swMd5.Start();
            Parallel.ForEach(ctx.results, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, info =>
            {
                if (!ctx.absMap.TryGetValue(info.relativePath, out var full))
                    return;
                if (!File.Exists(full))
                    return;
                var md5 = ComputeMd5(full);
                ctx.md5Map[info.relativePath] = md5;
                Interlocked.Increment(ref ctx.md5Done);
            });
            ctx.swMd5.Stop();
            ctx.md5Completed = true;

            for (int i = 0; i < ctx.results.Count; i++)
            {
                var a = ctx.results[i];
                string v;
                a.md5 = ctx.md5Map.TryGetValue(a.relativePath, out v) ? v : string.Empty;
                ctx.results[i] = a;
            }
        }

        private void CopyPhase(SyncContext ctx, SynchronizerConfig cfg)
        {
            ctx.swCopy.Start();
            foreach (var pair in cfg.targetProejcts)
            {
                ctx.totalCopied = 0;
                var targetRoot = pair.Key;
                switch (pair.Value)
                {
                    case SynchronizerConfig.ESyncOperationType.不操作:
                        Debug.Log($"{pair.Key} Skip");
                        continue;

                    case SynchronizerConfig.ESyncOperationType.自动替换:
                        ctx.targetsCount++;
                        Parallel.ForEach(ctx.results, info =>
                        {
                            if (!ctx.absMap.TryGetValue(info.relativePath, out var src))
                                return;

                            var dst = Path.Combine(targetRoot, info.relativePath);
                            var dstDir = Path.GetDirectoryName(dst);
                            IOHelper.InsureExist(dstDir, false);

                            try
                            {
                                if (File.Exists(src))
                                    File.Copy(src, dst, true);
                                else
                                    Debug.LogWarning(ZString.Format("Skip copy: source file missing {0}", src));

                                var srcMeta = src + ".meta";
                                if (File.Exists(srcMeta))
                                    File.Copy(srcMeta, dst + ".meta", true);
                            }
                            catch (Exception)
                            {
                                Debug.LogError(ZString.Format("Copy failed: {0} to {1}", src, dst));
                            }
                            finally
                            {
                                Interlocked.Increment(ref ctx.totalCopied);
                                Interlocked.Increment(ref ctx.copyProgress.done);
                            }
                        });
                        break;

                    default:
                        var assetStateMap = new ConcurrentDictionary<string, AssetInfos.EAssetState>();
                        var targetGuidMap = BuildTargetGuidMap(targetRoot);
                        Parallel.ForEach(ctx.results, info =>
                        {
                            if (!ctx.absMap.TryGetValue(info.relativePath, out var src))
                                return;

                            var dst = TryResolveTargetPath(info, targetRoot, targetGuidMap);
                            if (!File.Exists(dst))
                            {
                                assetStateMap[info.relativePath] = AssetInfos.EAssetState.New;
                                return;
                            }

                            var dstMd5 = ComputeMd5(dst);
                            var fileSame = string.Equals(dstMd5, info.md5, StringComparison.OrdinalIgnoreCase);

                            var srcMeta = src + ".meta";
                            var dstMeta = dst + ".meta";
                            var metaDiff = false;
                            if (File.Exists(srcMeta) && File.Exists(dstMeta))
                            {
                                var srcMetaMd5 = ComputeMd5(srcMeta);
                                var dstMetaMd5 = ComputeMd5(dstMeta);
                                metaDiff = !string.Equals(srcMetaMd5, dstMetaMd5, StringComparison.OrdinalIgnoreCase);
                            }

                            if (fileSame && !metaDiff)
                                assetStateMap[info.relativePath] = AssetInfos.EAssetState.Same;
                            else
                                assetStateMap[info.relativePath] = AssetInfos.EAssetState.Replace;
                        });

                        bool confirm = false;
                        bool cancel = false;
                        var waitEvent = new ManualResetEventSlim(false);
                        List<string> selectedPaths = null;
                        EditorTools.Add2MainThread(() =>
                        {
                            var projectName = Path.GetFileNameWithoutExtension(targetRoot);
                            AssetsTreeWindow.ShowWindow(projectName, assetStateMap,
                                sp =>
                                {
                                    selectedPaths = sp;
                                    confirm = true;
                                    waitEvent.Set();
                                },
                                () =>
                                {
                                    cancel = true;
                                    waitEvent.Set();
                                    Debug.Log($"取消 {projectName} 复制");
                                }
                            );
                        });

                        waitEvent.Wait();
                        if (cancel)
                            break;

                        if (confirm && selectedPaths != null && selectedPaths.Count > 0)
                        {
                            ctx.targetsCount++;
                            var selectedSet = new HashSet<string>(selectedPaths, StringComparer.OrdinalIgnoreCase);
                            ctx.totalCopied = 0;
                            ctx.copyProgress.done = 0;
                            ctx.copyTotal = selectedSet.Count;
                            ctx.showCopyProgress = true;
                            var targetGuidMap2 = targetGuidMap;
                            Parallel.ForEach(ctx.results, info =>
                            {
                                if (!selectedSet.Contains(info.relativePath))
                                    return;
                                if (!ctx.absMap.TryGetValue(info.relativePath, out var src))
                                    return;

                                var dst = TryResolveTargetPath(info, targetRoot, targetGuidMap2);
                                CleanupMovedTarget(info, targetRoot, dst);
                                var dstDir = Path.GetDirectoryName(dst);
                                IOHelper.InsureExist(dstDir, false);

                                try
                                {
                                    if (File.Exists(src))
                                        File.Copy(src, dst, true);
                                    else
                                        Debug.LogWarning(ZString.Format("Skip copy: source file missing {0}", src));

                                    var srcMeta = src + ".meta";
                                    if (File.Exists(srcMeta))
                                        File.Copy(srcMeta, dst + ".meta", true);
                                }
                                catch (Exception)
                                {
                                    Debug.LogError(ZString.Format("Copy failed: {0} to {1}", src, dst));
                                }
                                finally
                                {
                                    Interlocked.Increment(ref ctx.totalCopied);
                                    Interlocked.Increment(ref ctx.copyProgress.done);
                                }
                            });
                            ctx.showCopyProgress = false;
                        }
                        break;
                }
            }
            ctx.swCopy.Stop();
        }

        private void AttachProgressUpdater(SyncContext ctx)
        {
            EditorApplication.update += ProgressUpdate;
            void ProgressUpdate()
            {
                try
                {
                    if (showGuidScanProgress)
                    {
                        var p0 = guidScanTotal > 0 ? (float)guidScanDone / guidScanTotal : 1f;
                        EditorUtility.DisplayProgressBar("框架同步", ZString.Format("建立 GUID 映射 {0}/{1}", guidScanDone, guidScanTotal), p0);
                        return;
                    }

                    var mdDone = ctx.md5Done;
                    if (!ctx.md5Completed)
                    {
                        var p = ctx.results.Count > 0 ? (float)mdDone / ctx.results.Count : 1f;
                        EditorUtility.DisplayProgressBar("框架同步", ZString.Format("计算 MD5 {0}/{1}", mdDone, ctx.results.Count), p);
                        return;
                    }

                    if (ctx.showCopyProgress)
                    {
                        var done = ctx.copyProgress.done;
                        var p2 = ctx.copyTotal > 0 ? (float)done / ctx.copyTotal : 1f;
                        EditorUtility.DisplayProgressBar("框架同步", ZString.Format("复制资源 {0}/{1}", done, ctx.copyTotal), p2);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                    EditorUtility.ClearProgressBar();
                    EditorApplication.update -= ProgressUpdate;
                }
                finally
                {
                    if (ctx.overallTask.IsCompleted)
                    {
                        EditorUtility.ClearProgressBar();
                        EditorApplication.update -= ProgressUpdate;
                        Debug.Log(ZString.Format("MD5 computed for {0} assets in {1} ms", ctx.results.Count, ctx.swMd5.ElapsedMilliseconds));
                        if (ctx.targetsCount > 0)
                            Debug.Log(ZString.Format("Copied {0} assets to {1} targets in {2} ms", ctx.totalCopied, ctx.targetsCount, ctx.swCopy.ElapsedMilliseconds));
                    }
                }
            }
        }

        private class SyncContext
        {
            public List<AssetInfos> results;
            public Dictionary<string, string> absMap;
            public ConcurrentDictionary<string, string> md5Map;
            public Stopwatch swMd5;
            public int md5Done;
            public bool md5Completed;
            public Stopwatch swCopy;
            public int totalCopied;
            public int targetsCount;
            public int copyTotal;
            public CopyProgress copyProgress;
            public bool showCopyProgress;
            public Task overallTask;
        }

        // 收集路径下的所有资源（文件或目录）；记录相对路径与 GUID
        private void CollectFromPath(string path, List<AssetInfos> output, Dictionary<string, string> absMap)
        {
            if (string.IsNullOrEmpty(path))
                return;

            foreach (var extension in ignoreExtensions)
                if (path.ToLower().EndsWith(extension))
                    return;

            foreach (var ignore in SynchronizerConfig.GetInstance().ignoreAssets.AsValueEnumerable())
                if (path.Contains(ignore))
                    return;

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;

            if (Directory.Exists(path))
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    AddSingleAsset(file, projectRoot, output, absMap);
            else if (File.Exists(path))
                AddSingleAsset(path, projectRoot, output, absMap);
        }

        private void AddSingleAsset(string fullPath, string projectRoot, List<AssetInfos> output, Dictionary<string, string> absMap)
        {
            if (fullPath.EndsWith(".meta"))
                return;

            var relative = ToUnityRelativePath(fullPath, projectRoot);
            if (string.IsNullOrEmpty(relative))
                return;

            if (absMap.ContainsKey(relative))
                return;

            var guid = string.Empty;
            var meta = fullPath + ".meta";
            if (File.Exists(meta))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(meta))
                    {
                        if (line.StartsWith("guid:"))
                        {
                            var v = line.Substring(5).Trim();
                            guid = v;
                            break;
                        }
                    }
                }
                catch { }
            }
            if (string.IsNullOrEmpty(guid))
                guid = AssetDatabase.AssetPathToGUID(relative);

            var info = new AssetInfos { relativePath = relative, md5 = string.Empty, guid = guid };
            output.Add(info);
            absMap[relative] = Path.GetFullPath(fullPath);

            if (relative.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                CollectPrefabDependencies(relative, projectRoot, output, absMap);
        }

        private void AddAssetElement(string assetPath, HashSet<string> addedSet, HashSet<string> cfgSet, ListView listView)
        {
            if (assetPath.EndsWith(".meta"))
            {
                var withoutMeta = assetPath.Substring(0, assetPath.Length - 5);
                var guid = AssetDatabase.AssetPathToGUID(withoutMeta);
                var pathFromGuid = AssetDatabase.GUIDToAssetPath(guid);
                assetPath = string.IsNullOrEmpty(pathFromGuid) ? withoutMeta : pathFromGuid;
            }

            if (addedSet.Contains(assetPath))
                return;
            addedSet.Add(assetPath);

            if (!cfgSet.Contains(assetPath))
                cfgSet.Add(assetPath);

            var container = assetContainerTemplate.Instantiate();
            var content = listView.Q<VisualElement>("unity-content-container");

            var objectField = container.Q<ObjectField>();
            {
                objectField.value = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                objectField.RegisterValueChangedCallback(callback =>
                {
                    SetDirty();

                    var oldPath = AssetDatabase.GetAssetPath(callback.previousValue);
                    cfgSet.Remove(oldPath);

                    if (callback.newValue == null)
                    {
                        content.Remove(container);
                        return;
                    }

                    var newPath = AssetDatabase.GetAssetPath(callback.newValue);
                    cfgSet.Add(newPath);
                });
            }
            container.Q<Button>().clicked += () =>
            {
                cfgSet.Remove(assetPath);
                content.Remove(container);
                SetDirty();
            };

            content.Add(container);
            SetDirty();
        }

        private void CollectPrefabDependencies(string prefabRelativePath, string projectRoot, List<AssetInfos> output, Dictionary<string, string> absMap)
        {
            var deps = AssetDatabase.GetDependencies(prefabRelativePath, true);
            if (deps == null)
                return;

            foreach (var dep in deps)
            {
                if (string.IsNullOrEmpty(dep))
                    continue;

                var depFull = Path.Combine(projectRoot, dep);
                AddSingleAsset(depFull, projectRoot, output, absMap);
            }
        }

        // 收集 Unity 相对路径资产及其依赖项，并加入输出与绝对路径映射
        private void CollectAssetWithDependencies(string unityRelativePath, List<AssetInfos> output, Dictionary<string, string> absMap)
        {
            if (string.IsNullOrEmpty(unityRelativePath))
                return;

            if (unityRelativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                unityRelativePath = unityRelativePath.Substring(0, unityRelativePath.Length - 5);

            var obj = AssetDatabase.LoadAssetAtPath<Object>(unityRelativePath);
            if (obj == null)
                return;

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;

            var baseFull = ResolveFullPathFromUnityRelative(unityRelativePath, projectRoot);
            if (!string.IsNullOrEmpty(baseFull))
                AddSingleAsset(baseFull, projectRoot, output, absMap);

            var deps = AssetDatabase.GetDependencies(unityRelativePath, true);
            if (deps == null)
                return;

            foreach (var dep in deps)
            {
                if (string.IsNullOrEmpty(dep))
                    continue;

                var depFull = ResolveFullPathFromUnityRelative(dep, projectRoot);
                if (string.IsNullOrEmpty(depFull))
                    continue;

                AddSingleAsset(depFull, projectRoot, output, absMap);
            }
        }

        // 将 Unity 相对路径转换为绝对路径（支持 Assets/、Packages/、ToolProjects/）
        private string ResolveFullPathFromUnityRelative(string relativePath, string projectRoot)
        {
            var norm = relativePath.Replace('\\', '/');
            if (norm.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(Path.Combine(projectRoot, norm));
            if (norm.StartsWith("Packages", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(Path.Combine(projectRoot, norm));
            if (norm.StartsWith("ToolProjects", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(Path.Combine(projectRoot, norm));
            return string.Empty;
        }

        // 将绝对路径转换为 Unity 相对路径（Assets/、Packages/ 或 ToolProjects/ 开头）
        private string ToUnityRelativePath(string fullPath, string projectRoot)
        {
            var full = Path.GetFullPath(fullPath).Replace('\\', '/');
            var data = Application.dataPath.Replace('\\', '/');
            var pkg = Path.Combine(projectRoot, "Packages").Replace('\\', '/');
            var tool = Path.Combine(projectRoot, "ToolProjects").Replace('\\', '/');

            if (full.StartsWith(data, StringComparison.OrdinalIgnoreCase))
                return "Assets" + full.Substring(data.Length);

            if (full.StartsWith(pkg, StringComparison.OrdinalIgnoreCase))
                return "Packages" + full.Substring(pkg.Length);

            if (full.StartsWith(tool, StringComparison.OrdinalIgnoreCase))
                return "ToolProjects" + full.Substring(tool.Length);

            return string.Empty;
        }

        // 计算文件 MD5
        private string ComputeMd5(string fullPath)
        {
            using (var stream = File.OpenRead(fullPath))
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        // 并行复制所有资产（含 .meta）到指定目标工程根目录
        private class CopyProgress { public int done; }

        private Dictionary<string, string> BuildTargetGuidMap(string targetRoot)
        {
            var concurrent = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] metas;
            try
            {
                metas = Directory.GetFiles(targetRoot, "*.meta", SearchOption.AllDirectories);
            }
            catch
            {
                return new Dictionary<string, string>(0, StringComparer.OrdinalIgnoreCase);
            }
            showGuidScanProgress = true;
            guidScanDone = 0;
            guidScanTotal = metas.Length;

            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.ForEach(metas, options, meta =>
            {
                try
                {
                    foreach (var line in File.ReadLines(meta))
                    {
                        if (line.StartsWith("guid:"))
                        {
                            var guid = line.Substring(5).Trim();
                            if (!string.IsNullOrEmpty(guid))
                            {
                                var assetPath = meta.Substring(0, meta.Length - 5);
                                concurrent.TryAdd(guid, assetPath);
                            }
                            break;
                        }
                    }
                }
                catch { }
                finally { Interlocked.Increment(ref guidScanDone); }
            });

            showGuidScanProgress = false;
            return new Dictionary<string, string>(concurrent);
        }

        private string TryResolveTargetPath(AssetInfos info, string targetRoot, Dictionary<string, string> guidMap)
        {
            if (!string.IsNullOrEmpty(info.guid) && guidMap != null && guidMap.TryGetValue(info.guid, out var p))
                return p;
                
            return Path.Combine(targetRoot, info.relativePath);
        }

        private void CleanupMovedTarget(AssetInfos info, string targetRoot, string resolvedPath)
        {
            try
            {
                var defaultPath = Path.Combine(targetRoot, info.relativePath);
                if (string.Equals(defaultPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
                    return;

                if (File.Exists(defaultPath))
                    File.Delete(defaultPath);

                var meta = defaultPath + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);
            }
            catch (Exception)
            {
                Debug.LogError(ZString.Format("Cleanup moved target failed: {0}", Path.Combine(targetRoot, info.relativePath)));
            }
        }
    }
}
