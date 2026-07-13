/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/

using Cysharp.Text;
using Lin.Editor.Settings;
using Lin.Runtime.Helper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using static Lin.Editor.Tasks.GUIStyles;

namespace Lin.Editor.Tasks
{
    internal class ScriptTodoWindow : EditorWindow
    {
        [MenuItem("Lin/脚本/Script Todo List")]
        static void OpenWindow()
        {
            var window = GetWindow<ScriptTodoWindow>();
            window.titleContent = new GUIContent("Script Todo");
            window.Show();
        }

        [SerializeField] private Vector2 mainScrollPos;
        [SerializeField] private string searchFilter = "";
        [SerializeField] private bool showSettings = false;
        [SerializeField] private Vector2 settingsScrollPos;

        private List<ScriptTodoInfo> todos = new List<ScriptTodoInfo>();
        private bool isCollecting = false;
        private string lastCollectTime = "";

        private struct ScriptTodoInfo
        {
            public string scriptPath;
            public int line;
            public string content;
            public string assemblyName;
        }

        private void OnEnable()
        {
            CollectTodos();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawMainContent();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(
                        new GUIContent("刷新", EditorGUIUtility.FindTexture("RotateTool")),
                        EditorStyles.toolbarButton,
                        GUILayout.ExpandWidth(false)))
                {
                    CollectTodos();
                }

                if (GUILayout.Button(
                        new GUIContent("设置", EditorGUIUtility.FindTexture("Settings")),
                        EditorStyles.toolbarButton,
                        GUILayout.ExpandWidth(false)))
                {
                    showSettings = !showSettings;
                }

                GUILayout.Space(10);

                EditorGUILayout.LabelField("搜索:", GUILayout.Width(40));
                searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(lastCollectTime))
                {
                    EditorGUILayout.LabelField($"上次收集: {lastCollectTime}", EditorStyles.miniLabel);
                }

                EditorGUILayout.LabelField($"共 {todos.Count} 条", EditorStyles.miniLabel);
            }
        }

        private void DrawMainContent()
        {
            if (showSettings)
            {
                DrawSettings();
            }

            using (var scroll = new GUILayout.ScrollViewScope(mainScrollPos))
            {
                mainScrollPos = scroll.scrollPosition;

                if (isCollecting)
                {
                    GUILayout.FlexibleSpace();
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("正在收集...", GetBigLabel());
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.FlexibleSpace();
                    return;
                }

                if (todos.Count == 0)
                {
                    GUILayout.FlexibleSpace();
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        using (new GUILayout.VerticalScope())
                        {
                            GUILayout.Label("没有找到 TODO", GetBigLabel());
                            GUILayout.Space(10);
                            GUILayout.Label("脚本中没有找到 TODO 注释", GetNormalLabel());
                        }
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.FlexibleSpace();
                    return;
                }

                var filteredTodos = GetFilteredTodos();
                DrawTodoList(filteredTodos);
            }
        }

        private void DrawSettings()
        {
            var settings = EditorSettings_SO.GetInstance();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("过滤设置", EditorStyles.boldLabel);

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("忽略的文件夹:");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    for (int i = 0; i < settings.todoIgnoreFolders.Count; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            settings.todoIgnoreFolders[i] = EditorGUILayout.TextField(settings.todoIgnoreFolders[i]);
                            if (GUILayout.Button("×", GUILayout.Width(20)))
                            {
                                settings.todoIgnoreFolders.RemoveAt(i);
                                i--;
                            }
                        }
                    }

                    if (GUILayout.Button("添加文件夹"))
                    {
                        settings.todoIgnoreFolders.Add("");
                    }
                }

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("忽略的程序集:");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    for (int i = 0; i < settings.todoIgnoreAssemblies.Count; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            settings.todoIgnoreAssemblies[i] = EditorGUILayout.TextField(settings.todoIgnoreAssemblies[i]);
                            if (GUILayout.Button("×", GUILayout.Width(20)))
                            {
                                settings.todoIgnoreAssemblies.RemoveAt(i);
                                i--;
                            }
                        }
                    }

                    if (GUILayout.Button("添加程序集"))
                    {
                        settings.todoIgnoreAssemblies.Add("");
                    }
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("保存设置"))
                {
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                    CollectTodos();
                }
            }
        }

        private void DrawTodoList(List<ScriptTodoInfo> filteredTodos)
        {
            string currentPath = "";
            foreach (var todo in filteredTodos)
            {
                if (todo.scriptPath != currentPath)
                {
                    currentPath = todo.scriptPath;
                    EditorGUILayout.Space(5);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(todo.scriptPath, EditorStyles.linkLabel))
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(todo.scriptPath);
                            if (asset != null)
                            {
                                Selection.activeObject = asset;
                                EditorGUIUtility.PingObject(asset);
                            }
                        }

                        if (!string.IsNullOrEmpty(todo.assemblyName))
                        {
                            EditorGUILayout.LabelField($"[{todo.assemblyName}]", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField($"行 {todo.line}:", GUILayout.Width(50));
                    if (GUILayout.Button(todo.content, EditorStyles.label))
                    {
                        OpenScriptAtLine(todo.scriptPath, todo.line);
                    }
                }
            }
        }

        private List<ScriptTodoInfo> GetFilteredTodos()
        {
            if (string.IsNullOrEmpty(searchFilter))
                return todos;

            var filtered = new List<ScriptTodoInfo>();
            foreach (var todo in todos)
            {
                if (todo.content.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
                    todo.scriptPath.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(todo);
                }
            }
            return filtered;
        }

        private void CollectTodos()
        {
            if (isCollecting)
                return;

            isCollecting = true;
            todos.Clear();

            var settings = EditorSettings_SO.GetInstance();
            string projectRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;

            var scriptGuids = AssetDatabase.FindAssets("t:MonoScript");
            var scriptPaths = new List<string>(scriptGuids.Length);
            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith("Assets\\", StringComparison.Ordinal))
                    continue;

                if (ShouldIgnorePath(path, settings))
                    continue;

                scriptPaths.Add(path);
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var bag = new ConcurrentBag<ScriptTodoInfo>();
                    Parallel.For(0, scriptPaths.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
                    {
                        string assetPath = scriptPaths[i];
                        string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
                        try
                        {
                            ScanFileForTodos(fullPath, assetPath, bag);
                        }
                        catch
                        {
                        }
                    });

                    var list = new List<ScriptTodoInfo>(bag);
                    list.Sort((a, b) =>
                    {
                        int c = string.CompareOrdinal(a.scriptPath, b.scriptPath);
                        if (c != 0)
                            return c;
                        return a.line.CompareTo(b.line);
                    });

                    EditorApplication.delayCall += () =>
                    {
                        todos = list;
                        isCollecting = false;
                        lastCollectTime = DateTime.Now.ToString("HH:mm:ss");
                        Repaint();
                    };
                }
                catch (Exception e)
                {
                    EditorApplication.delayCall += () =>
                    {
                        isCollecting = false;
                        Log.Error(nameof(ScriptTodoWindow), e.Message);
                        Repaint();
                    };
                }
            });
        }

        private bool ShouldIgnorePath(string path, EditorSettings_SO settings)
        {
            if (settings.todoIgnoreFolders != null)
            {
                foreach (var folder in settings.todoIgnoreFolders)
                {
                    if (!string.IsNullOrEmpty(folder) && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (settings.todoIgnoreAssemblies != null)
            {
                string assemblyName = GetAssemblyNameFromPath(path);
                if (!string.IsNullOrEmpty(assemblyName))
                {
                    foreach (var assembly in settings.todoIgnoreAssemblies)
                    {
                        if (!string.IsNullOrEmpty(assembly) && assemblyName.Equals(assembly, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }

        private string GetAssemblyNameFromPath(string path)
        {
            string normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.StartsWith("Packages/"))
            {
                int packageEnd = normalizedPath.IndexOf('/', 9);
                if (packageEnd > 0)
                {
                    string packagePath = normalizedPath.Substring(0, packageEnd);
                    string packageJsonPath = Path.Combine(new DirectoryInfo(Application.dataPath).Parent.FullName, packagePath.Replace('/', Path.DirectorySeparatorChar), "package.json");
                    if (File.Exists(packageJsonPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(packageJsonPath);
                            var match = Regex.Match(json, @"""name""\s*:\s*""([^""]+)""");
                            if (match.Success)
                                return match.Groups[1].Value;
                        }
                        catch { }
                    }
                }
            }

            string directory = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(directory))
            {
                string asmdefPath = FindAsmdefInDirectory(directory);
                if (!string.IsNullOrEmpty(asmdefPath))
                {
                    try
                    {
                        string json = File.ReadAllText(asmdefPath);
                        var match = Regex.Match(json, @"""name""\s*:\s*""([^""]+)""");
                        if (match.Success)
                            return match.Groups[1].Value;
                    }
                    catch { }
                    break;
                }
                directory = Path.GetDirectoryName(directory);
            }

            return null;
        }

        private string FindAsmdefInDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                return null;

            var asmdefFiles = Directory.GetFiles(directory, "*.asmdef");
            if (asmdefFiles.Length > 0)
                return asmdefFiles[0];

            return null;
        }

        private void ScanFileForTodos(string fullPath, string assetPath, ConcurrentBag<ScriptTodoInfo> bag)
        {
            bool inBlock = false;
            int lineIndex = 0;
            string assemblyName = GetAssemblyNameFromPath(assetPath);

            foreach (var raw in File.ReadLines(fullPath, Encoding.UTF8))
            {
                lineIndex++;
                string line = raw;
                int idx = 0;
                while (idx < line.Length)
                {
                    if (!inBlock)
                    {
                        int sl = line.IndexOf("//", idx, StringComparison.Ordinal);
                        int bl = line.IndexOf("/*", idx, StringComparison.Ordinal);
                        if (sl < 0 && bl < 0)
                            break;

                        if (sl >= 0 && (bl < 0 || sl < bl))
                        {
                            string comment = line.Substring(sl + 2);
                            string content = ExtractTodoFromComment(comment);
                            if (!string.IsNullOrWhiteSpace(content))
                                bag.Add(new ScriptTodoInfo { scriptPath = assetPath, line = lineIndex, content = content.Trim(), assemblyName = assemblyName });
                            break;
                        }
                        else
                        {
                            int end = line.IndexOf("*/", bl + 2, StringComparison.Ordinal);
                            if (end >= 0)
                            {
                                string comment = line.Substring(bl + 2, end - (bl + 2));
                                string content = ExtractTodoFromComment(comment);
                                if (!string.IsNullOrWhiteSpace(content))
                                    bag.Add(new ScriptTodoInfo { scriptPath = assetPath, line = lineIndex, content = content.Trim(), assemblyName = assemblyName });
                                idx = end + 2;
                                continue;
                            }
                            else
                            {
                                string comment = line.Substring(bl + 2);
                                string content = ExtractTodoFromComment(comment);
                                if (!string.IsNullOrWhiteSpace(content))
                                    bag.Add(new ScriptTodoInfo { scriptPath = assetPath, line = lineIndex, content = content.Trim(), assemblyName = assemblyName });
                                inBlock = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        int end = line.IndexOf("*/", idx, StringComparison.Ordinal);
                        string segment = end >= 0 ? line.Substring(idx, end - idx) : line.Substring(idx);
                        string content = ExtractTodoFromComment(segment);
                        if (!string.IsNullOrWhiteSpace(content))
                            bag.Add(new ScriptTodoInfo { scriptPath = assetPath, line = lineIndex, content = content.Trim(), assemblyName = assemblyName });
                        if (end >= 0)
                        {
                            inBlock = false;
                            idx = end + 2;
                            continue;
                        }
                        break;
                    }
                }
            }
        }

        private string ExtractTodoFromComment(string commentSegment)
        {
            if (string.IsNullOrEmpty(commentSegment))
                return null;

            var m = Regex.Match(commentSegment, @"\bTODO\b\s*[:：]?\s*(.+)$", RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Groups[1].Value;
            return null;
        }

        private void OpenScriptAtLine(string assetPath, int line)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset, line);
            }
        }
    }
}
