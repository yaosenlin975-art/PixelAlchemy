/*
┌────────────────────────────┐
│　Description: PrefsHelper 管理界面
│　Remark: 支持增删改查 + 引用追踪（扫描 Assets 下 .cs 文件）
└────────────────────────────┘
┌──────────────┐
│　ClassName: PrefsHelperWindow
└──────────────┘
*/
using Lin.Runtime.Helper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor
{
    public class PrefsHelperWindow : EditorWindow
    {
        private struct RefHit
        {
            public string assetPath;
            public int line;
            public string snippet;
            public string kind;
        }

        // 选中的类型 / Key
        private Type[] archiveTypes;
        private int selectedTypeIndex = -1;
        private string[] currentKeys;
        private int selectedKeyIndex = -1;

        // 编辑态
        private string editingKeyName = string.Empty;
        private object editingValue;
        private bool useJsonEditor;
        private string jsonEditText = string.Empty;

        // 搜索 / 输入
        private string keySearchText = string.Empty;
        private string newKeyInput = string.Empty;
        private Vector2 keyListScroll;
        private Vector2 valuePanelScroll;
        private Vector2 refsScroll;

        // 引用缓存（按 Type 而非 Key）
        private readonly Dictionary<Type, List<RefHit>> referencesCache = new Dictionary<Type, List<RefHit>>();
        private Type referencesLastType;

        // 反射方法缓存（按具体类型）
        private readonly Dictionary<Type, MethodInfo[]> methodCache = new Dictionary<Type, MethodInfo[]>();

        // 嵌套对象的 foldout 状态
        private readonly HashSet<string> openPaths = new HashSet<string>();

        [MenuItem("Lin/Prefs Manager")]
        private static void Open()
        {
            var window = GetWindow<PrefsHelperWindow>();
            window.titleContent = new GUIContent("Prefs Helper");
            window.minSize = new Vector2(760, 420);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshArchiveTypes();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawKeyListPanel();
                DrawValueEditorPanel();
            }

            // Ctrl+S 快捷保存
            if (Event.current.type == EventType.KeyDown && Event.current.control && Event.current.keyCode == KeyCode.S)
            {
                SaveEditing();
                Event.current.Use();
            }
        }

        // ========================== 顶部工具栏 ==========================

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    RefreshArchiveTypes();

                GUILayout.Label("Type:", GUILayout.Width(40));
                if (archiveTypes == null || archiveTypes.Length == 0)
                {
                    GUILayout.Label("(无已使用类型)", EditorStyles.miniLabel);
                }
                else
                {
                    if (selectedTypeIndex >= archiveTypes.Length)
                        selectedTypeIndex = archiveTypes.Length - 1;
                    var labels = archiveTypes.Select(t => $"{t.Name}    [{t.FullName}]").ToArray();
                    int newIndex = EditorGUILayout.Popup(selectedTypeIndex, labels, EditorStyles.toolbarPopup, GUILayout.MinWidth(280));
                    if (newIndex != selectedTypeIndex)
                    {
                        selectedTypeIndex = newIndex;
                        OnTypeChanged();
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("Ctrl+S 保存当前编辑值", EditorStyles.miniLabel);
            }
        }

        // ========================== 左侧 Keys 列表 ==========================

        private void DrawKeyListPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
            {
                EditorGUILayout.LabelField("Keys", EditorStyles.boldLabel);

                // 新增 Key 输入
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    newKeyInput = EditorGUILayout.TextField(newKeyInput, EditorStyles.toolbarTextField);
                    if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24)))
                        TryAddKey();
                }

                // 搜索 + 清空
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    keySearchText = EditorGUILayout.TextField(keySearchText, EditorStyles.toolbarSearchField);
                    if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    {
                        if (EditorUtility.DisplayDialog("确认", $"清空类型 {GetSelectedType()?.Name} 的所有 keys？", "清空", "取消"))
                            ClearAllKeys();
                    }
                }

                // 列表
                using (var scope = new EditorGUILayout.ScrollViewScope(keyListScroll, EditorStyles.helpBox))
                {
                    keyListScroll = scope.scrollPosition;
                    if (currentKeys == null || currentKeys.Length == 0)
                    {
                        GUILayout.Label("(无 Key)", EditorStyles.miniLabel);
                    }
                    else
                    {
                        var filtered = string.IsNullOrEmpty(keySearchText)
                            ? currentKeys
                            : currentKeys.Where(k => k.IndexOf(keySearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                        for (int i = 0; i < filtered.Length; i++)
                            DrawKeyRow(filtered[i], i);
                    }
                }
            }
        }

        private void DrawKeyRow(string key, int displayIndex)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool isSelected = displayIndex == selectedKeyIndex;
                bool newSelected = GUILayout.Toggle(isSelected, key, "ProjectBrowserHeaderLabel");
                if (newSelected && !isSelected)
                {
                    selectedKeyIndex = displayIndex;
                    LoadKeyForEdit(key);
                }
                else if (!newSelected && isSelected)
                {
                    selectedKeyIndex = -1;
                    editingKeyName = string.Empty;
                    editingValue = null;
                }
            }
        }

        // ========================== 右侧 值编辑器 ==========================

        private void DrawValueEditorPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (selectedKeyIndex < 0 || currentKeys == null || selectedKeyIndex >= currentKeys.Length)
                {
                    EditorGUILayout.HelpBox("请在左侧选择一个 Key 进行查看 / 编辑", MessageType.Info);
                    return;
                }

                var type = GetSelectedType();
                EditorGUILayout.LabelField($"类型: {type.FullName}", EditorStyles.miniLabel);

                // 操作按钮
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Key:", GUILayout.Width(40));
                    if (GUILayout.Button(editingKeyName, EditorStyles.linkLabel))
                        EditorGUIUtility.systemCopyBuffer = editingKeyName;
                    GUILayout.FlexibleSpace();
                    var prevColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.6f, 0.85f, 0.6f);
                    if (GUILayout.Button("保存", GUILayout.Width(70))) SaveEditing();
                    GUI.backgroundColor = prevColor;
                    if (GUILayout.Button("重载", GUILayout.Width(60))) LoadKeyForEdit(editingKeyName);
                    prevColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.9f, 0.5f, 0.5f);
                    if (GUILayout.Button("删除", GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("确认", $"删除 Key \"{editingKeyName}\"？", "删除", "取消"))
                            DeleteCurrentKey();
                    }
                    GUI.backgroundColor = prevColor;
                }

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    useJsonEditor = GUILayout.Toggle(useJsonEditor, "JSON 编辑", EditorStyles.toolbarButton, GUILayout.Width(80));
                    GUILayout.FlexibleSpace();
                }

                using (var scope = new EditorGUILayout.ScrollViewScope(valuePanelScroll))
                {
                    valuePanelScroll = scope.scrollPosition;
                    if (useJsonEditor)
                        DrawJsonEditor();
                    else
                        DrawReflectionEditor();
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("引用追踪", EditorStyles.boldLabel);
                DrawReferencesPanel();
            }
        }

        // ---------- 反射值编辑器（返回新值，调用方赋值给 editingValue） ----------

        private void DrawReflectionEditor()
        {
            var type = GetSelectedType();
            if (type == null) return;

            try
            {
                editingValue = DrawValueEditor(editingValue, type, "root", type.Name);
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"反射编辑失败: {ex.Message}\n请切换到 JSON 编辑。", MessageType.Warning);
            }
        }

        private object DrawValueEditor(object value, Type type, string path, string label)
        {
            if (type == null) return value;

            // 可空类型：仅展示 + 让用户创建/置空
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                EditorGUILayout.HelpBox($"Nullable<{underlying.Name}> 暂未支持反射编辑，请用 JSON 编辑。", MessageType.Info);
                return value;
            }

            // 简单值类型
            if (type == typeof(string)) return FieldString(label, value);
            if (type == typeof(bool)) return FieldBool(label, value);
            if (type == typeof(int)) return FieldInt(label, value);
            if (type == typeof(long)) return FieldLong(label, value);
            if (type == typeof(float)) return FieldFloat(label, value);
            if (type == typeof(double)) return FieldDouble(label, value);
            if (type == typeof(byte)) return FieldByte(label, value);
            if (type == typeof(short)) return FieldShort(label, value);

            if (type == typeof(Vector2)) return FieldVector2(label, value);
            if (type == typeof(Vector3)) return FieldVector3(label, value);
            if (type == typeof(Vector4)) return FieldVector4(label, value);
            if (type == typeof(Color)) return FieldColor(label, value);
            if (type == typeof(Color32)) return FieldColor32(label, value);
            if (type == typeof(Rect)) return FieldRect(label, value);
            if (type == typeof(Bounds)) return FieldBounds(label, value);

            if (type.IsEnum) return FieldEnum(label, value, type);

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return FieldUnityObject(label, value, type);

            if (typeof(System.Collections.IList).IsAssignableFrom(type))
                return DrawListEditor(value, type, path, label);

            if (type.IsClass || (type.IsValueType && !type.IsPrimitive))
                return DrawNestedEditor(value, type, path, label);

            EditorGUILayout.HelpBox($"不支持直接编辑类型: {type.FullName}，请使用 JSON 编辑。", MessageType.Warning);
            return value;
        }

        // ---------- 各类型的字段绘制（EditorGUI.BeginChangeCheck 包裹） ----------

        private static object FieldString(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.TextField(label, value as string ?? "");
            return EditorGUI.EndChangeCheck() ? newVal : value;
        }

        private static object FieldBool(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.ToggleLeft(label, value is bool b && b);
            return EditorGUI.EndChangeCheck() ? newVal : value;
        }

        private static object FieldInt(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.IntField(label, value is int i ? i : 0);
            return EditorGUI.EndChangeCheck() ? newVal : value;
        }

        private static object FieldLong(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.LongField(label, value is long l ? l : 0L);
            return EditorGUI.EndChangeCheck() ? newVal : value;
        }

        private static object FieldFloat(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.FloatField(label, value is float f ? f : 0f);
            return EditorGUI.EndChangeCheck() ? newVal : value;
        }

        private static object FieldDouble(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.DoubleField(label, value is double d ? d : 0d);
            return EditorGUI.EndChangeCheck() ? newVal : value;
        }

        private static object FieldByte(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = (byte)EditorGUILayout.IntField(label, value is byte b ? b : (byte)0);
            return EditorGUI.EndChangeCheck() ? newVal : value;
        }

        private static object FieldShort(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = (short)EditorGUILayout.IntField(label, value is short s ? s : (short)0);
            return EditorGUI.EndChangeCheck() ? newVal : value;
        }

        private static object FieldVector2(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.Vector2Field(label, value is Vector2 v ? v : default);
            return EditorGUI.EndChangeCheck() ? (object)newVal : value;
        }

        private static object FieldVector3(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.Vector3Field(label, value is Vector3 v ? v : default);
            return EditorGUI.EndChangeCheck() ? (object)newVal : value;
        }

        private static object FieldVector4(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.Vector4Field(label, value is Vector4 v ? v : default);
            return EditorGUI.EndChangeCheck() ? (object)newVal : value;
        }

        private static object FieldColor(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.ColorField(label, value is Color c ? c : default);
            return EditorGUI.EndChangeCheck() ? (object)newVal : value;
        }

        private static object FieldColor32(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = (Color32)EditorGUILayout.ColorField(label, value is Color32 c ? c : default);
            return EditorGUI.EndChangeCheck() ? (object)newVal : value;
        }

        private static object FieldRect(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.RectField(label, value is Rect r ? r : default);
            return EditorGUI.EndChangeCheck() ? (object)newVal : value;
        }

        private static object FieldBounds(string label, object value)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.BoundsField(label, value is Bounds b ? b : default);
            return EditorGUI.EndChangeCheck() ? (object)newVal : value;
        }

        private static object FieldEnum(string label, object value, Type enumType)
        {
            EditorGUI.BeginChangeCheck();
            Enum newVal = EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Enum.GetValues(enumType).GetValue(0));
            return EditorGUI.EndChangeCheck() ? newVal : value;
        }

        private static object FieldUnityObject(string label, object value, Type type)
        {
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, type, true);
            if (EditorGUI.EndChangeCheck())
                return newVal;
            return value;
        }

        // ---------- 嵌套对象编辑器 ----------

        private object DrawNestedEditor(object value, Type type, string path, string label)
        {
            bool isOpen = openPaths.Contains(path);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool newOpen = EditorGUILayout.Foldout(isOpen, $"{label}    ({type.Name})", true);
                if (newOpen != isOpen)
                {
                    if (newOpen) openPaths.Add(path);
                    else openPaths.Remove(path);
                }
                if (value == null && type.IsClass)
                {
                    GUILayout.Label("null", EditorStyles.miniLabel);
                    if (GUILayout.Button("+ 新建", GUILayout.Width(60)))
                        return Activator.CreateInstance(type);
                }
            }

            if (!isOpen) return value;
            if (value == null)
            {
                if (type.IsClass) return null;
                value = Activator.CreateInstance(type);
            }

            EditorGUI.indentLevel++;
            var fields = GetSerializableFields(type);
            foreach (var field in fields)
            {
                var fieldPath = path + "/" + field.Name;
                object fieldVal;
                try { fieldVal = field.GetValue(value); }
                catch (Exception ex)
                {
                    EditorGUILayout.HelpBox($"读取字段 {field.Name} 失败: {ex.Message}", MessageType.Warning);
                    continue;
                }
                var newFieldVal = DrawValueEditor(fieldVal, field.FieldType, fieldPath, field.Name);
                if (IsChanged(fieldVal, newFieldVal))
                {
                    try { field.SetValue(value, newFieldVal); }
                    catch (Exception ex)
                    {
                        EditorGUILayout.HelpBox($"写入字段 {field.Name} 失败: {ex.Message}", MessageType.Warning);
                    }
                }
            }
            EditorGUI.indentLevel--;
            return value;
        }

        // ---------- 列表/数组编辑器 ----------

        private object DrawListEditor(object listObj, Type listType, string path, string label)
        {
            var elementType = listType.IsArray
                ? listType.GetElementType()
                : listType.GetGenericArguments().FirstOrDefault();
            if (elementType == null)
            {
                EditorGUILayout.HelpBox($"无法解析 List 元素类型: {listType.FullName}", MessageType.Warning);
                return listObj;
            }

            // 确保是 IList 而不是 array
            System.Collections.IList list = listObj as System.Collections.IList;
            if (list == null && listType.IsArray)
            {
                list = ConvertArrayToList((Array)listObj, elementType);
                listObj = list;
            }

            int count = list?.Count ?? 0;
            bool isOpen = openPaths.Contains(path);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool newOpen = EditorGUILayout.Foldout(isOpen, $"{label}    [{count}]  ({elementType.Name})", true);
                if (newOpen != isOpen)
                {
                    if (newOpen) openPaths.Add(path);
                    else openPaths.Remove(path);
                }
                if (GUILayout.Button("+", GUILayout.Width(24)))
                {
                    if (list == null) list = CreateList(elementType);
                    var newElem = elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
                    list.Add(newElem);
                }
            }

            if (!isOpen) return listObj;
            if (list == null) list = CreateList(elementType);

            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                var elemPath = path + "/[" + i + "]";
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"[{i}]", GUILayout.Width(36));
                    var newElem = DrawValueEditor(list[i], elementType, elemPath, elementType.Name);
                    if (IsChanged(list[i], newElem)) list[i] = newElem;
                    if (GUILayout.Button("×", GUILayout.Width(22)))
                    {
                        list.RemoveAt(i);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUI.indentLevel--;
            return list;
        }

        private static System.Collections.IList CreateList(Type elementType)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            return (System.Collections.IList)Activator.CreateInstance(listType);
        }

        private static System.Collections.IList ConvertArrayToList(Array arr, Type elementType)
        {
            var list = CreateList(elementType);
            foreach (var item in arr) list.Add(item);
            return list;
        }

        // ---------- JSON 编辑器 ----------

        private void DrawJsonEditor()
        {
            var type = GetSelectedType();
            if (type == null) return;

            // 初次进入时把当前值序列化为 JSON
            if (string.IsNullOrEmpty(jsonEditText))
            {
                try { jsonEditText = JsonConvert.SerializeObject(editingValue, Formatting.Indented); }
                catch { jsonEditText = "{}"; }
            }

            EditorGUILayout.LabelField("编辑 JSON 后点击「应用到内存」", EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            var newText = EditorGUILayout.TextArea(jsonEditText, GUILayout.MinHeight(180));
            if (EditorGUI.EndChangeCheck()) jsonEditText = newText;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("应用到内存", GUILayout.Width(120)))
                {
                    try
                    {
                        editingValue = JsonConvert.DeserializeObject(jsonEditText, type);
                        ShowNotification(new GUIContent("已解析"));
                    }
                    catch (Exception ex)
                    {
                        EditorUtility.DisplayDialog("JSON 解析失败", ex.Message, "OK");
                    }
                }
                if (GUILayout.Button("从内存重新生成", GUILayout.Width(140)))
                {
                    try { jsonEditText = JsonConvert.SerializeObject(editingValue, Formatting.Indented); }
                    catch (Exception ex) { EditorUtility.DisplayDialog("序列化失败", ex.Message, "OK"); }
                }
            }
        }

        // ---------- 引用追踪（按 Type 搜索） ----------

        private static readonly Dictionary<string, Color> KindColors = new Dictionary<string, Color>
        {
            { "call",     new Color(0.55f, 0.85f, 0.55f) },  // PrefsHelper.Get/Set<T> 调用 - 绿
            { "call-fq",  new Color(0.40f, 0.75f, 0.55f) },  // 全限定调用 - 深绿
            { "const",    new Color(0.45f, 0.75f, 0.95f) },  // const string X = nameof(T) - 蓝
            { "nameof",   new Color(0.95f, 0.80f, 0.45f) },  // nameof(T) - 黄
        };

        private static readonly Dictionary<string, string> KindLabels = new Dictionary<string, string>
        {
            { "call",     "[调用]" },
            { "call-fq",  "[调用.FQ]" },
            { "const",    "[const]" },
            { "nameof",   "[nameof]" },
        };

        private void DrawReferencesPanel()
        {
            var type = GetSelectedType();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(type == null
                    ? "Type: (无)"
                    : $"Type: {type.Name}    [{type.FullName}]  ·  扫描 Assets/**/*.cs", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(type == null))
                {
                    if (GUILayout.Button("扫描引用", EditorStyles.toolbarButton, GUILayout.Width(80)))
                        ScanTypeReferences(type);
                }
            }

            if (type == null) return;

            if (referencesLastType != type)
            {
                EditorGUILayout.HelpBox("支持匹配：①PrefsHelper.Get/Set/ContainsKey<T> 调用  ②const string X = nameof(T)  ③nameof(T)  ④全限定类型名", MessageType.None);
                return;
            }

            if (!referencesCache.TryGetValue(type, out var hits))
            {
                EditorGUILayout.LabelField("(未找到引用)", EditorStyles.miniLabel);
                return;
            }

            GUILayout.Label($"找到 {hits.Count} 处引用", EditorStyles.miniLabel);

            if (hits.Count == 0) return;

            using (var scope = new EditorGUILayout.ScrollViewScope(refsScroll, GUILayout.MaxHeight(180)))
            {
                refsScroll = scope.scrollPosition;
                foreach (var hit in hits)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (KindColors.TryGetValue(hit.kind, out var c))
                        {
                            var prev = GUI.color;
                            GUI.color = c;
                            GUILayout.Label(KindLabels.TryGetValue(hit.kind, out var l) ? l : "[" + hit.kind + "]", EditorStyles.miniBoldLabel, GUILayout.Width(72));
                            GUI.color = prev;
                        }
                        if (GUILayout.Button($"{hit.assetPath}:{hit.line}", EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
                            PingScript(hit.assetPath);
                        GUILayout.Label(TrimSnippet(hit.snippet), EditorStyles.miniLabel);
                    }
                }
            }
        }

        private static string TrimSnippet(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Trim();
            return s.Length > 100 ? s.Substring(0, 100) + "..." : s;
        }

        private static void PingScript(string assetPath)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (script == null) return;
            EditorGUIUtility.PingObject(script);
            AssetDatabase.OpenAsset(script);
        }

        private void ScanTypeReferences(Type type)
        {
            if (type == null) return;
            referencesLastType = type;
            referencesCache[type] = FindTypeReferencesInScripts(type);
            Repaint();
        }

        private static List<RefHit> FindTypeReferencesInScripts(Type type)
        {
            var hits = new List<RefHit>();
            if (type == null) return hits;
            var assetsDir = Application.dataPath;
            if (!Directory.Exists(assetsDir)) return hits;

            var files = Directory.GetFiles(assetsDir, "*.cs", SearchOption.AllDirectories);

            // 同时支持短名和全限定名（namespace 段不同时，靠全限定匹配）
            var typeName = type.Name;
            var fullTypeName = type.FullName ?? typeName;
            var escShort = Regex.Escape(typeName);
            var escFull = Regex.Escape(fullTypeName);

            // PrefsHelper 上的所有泛型方法签名
            const string methodGroup = "(?:Get|Set|ContainsKey|DeleteKey|Clear|GetAllKeys)";

            var patterns = new (Regex regex, string kind)[]
            {
                // ① 短名调用：PrefsHelper.Get<UIGenerationInfo>(...)
                (new Regex(@"\bPrefsHelper\s*\.\s*" + methodGroup + @"\s*<\s*(global\s*::\s*)?" + escShort + @"\s*>"), "call"),
                // ② 全限定调用：PrefsHelper.Get<MyNamespace.MyData>(...)
                (new Regex(@"\bPrefsHelper\s*\.\s*" + methodGroup + @"\s*<\s*(global\s*::\s*)?" + escFull + @"\s*>"), "call-fq"),
                // ③ const 声明（key 定义）：const string X = nameof(T)
                (new Regex(@"\bconst\s+string\s+\w+\s*=\s*nameof\s*\(\s*(global\s*::\s*)?" + escShort + @"\s*\)"), "const"),
                // ④ 单独 nameof(T) 调用
                (new Regex(@"\bnameof\s*\(\s*(global\s*::\s*)?" + escShort + @"\s*\)"), "nameof"),
            };

            foreach (var file in files)
            {
                string content;
                try { content = File.ReadAllText(file); }
                catch { continue; }

                foreach (var (regex, kind) in patterns)
                {
                    foreach (Match m in regex.Matches(content))
                    {
                        int idx = m.Index;
                        int line = 1;
                        for (int i = 0; i < idx; i++) if (content[i] == '\n') line++;
                        int lineStart = content.LastIndexOf('\n', idx);
                        if (lineStart < 0) lineStart = 0;
                        int lineEnd = content.IndexOf('\n', idx);
                        if (lineEnd < 0) lineEnd = content.Length;
                        var snippet = content.Substring(lineStart, lineEnd - lineStart);

                        var assetPath = "Assets" + file.Substring(assetsDir.Length).Replace('\\', '/');
                        hits.Add(new RefHit { assetPath = assetPath, line = line, snippet = snippet, kind = kind });
                    }
                }
            }

            // 同一行可能既命中 call 又命中 const（几乎不可能，但防御一下）+ 按 (path,line) 去重
            return hits
                .GroupBy(h => h.assetPath + ":" + h.line)
                .Select(g => g.First())
                .OrderBy(h => h.assetPath).ThenBy(h => h.line)
                .ToList();
        }

        // ========================== 状态切换 ==========================

        private void RefreshArchiveTypes()
        {
            archiveTypes = PrefsHelper.GetAllArchiveTypes()
                .OrderBy(t => t.FullName)
                .ToArray();
            if (archiveTypes.Length == 0)
            {
                selectedTypeIndex = -1;
                currentKeys = null;
                selectedKeyIndex = -1;
                return;
            }
            if (selectedTypeIndex < 0 || selectedTypeIndex >= archiveTypes.Length)
                selectedTypeIndex = 0;
            OnTypeChanged();
        }

        private void OnTypeChanged()
        {
            currentKeys = null;
            selectedKeyIndex = -1;
            editingKeyName = string.Empty;
            editingValue = null;
            jsonEditText = string.Empty;
            referencesLastType = null;
            openPaths.Clear();

            if (selectedTypeIndex < 0) return;
            try
            {
                var t = archiveTypes[selectedTypeIndex];
                currentKeys = InvokeGetAllKeys(t).OrderBy(k => k).ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PrefsHelperWindow] 加载 Keys 失败: {ex.Message}");
                currentKeys = Array.Empty<string>();
            }
        }

        private void LoadKeyForEdit(string key)
        {
            editingKeyName = key;
            jsonEditText = string.Empty;
            try
            {
                var t = archiveTypes[selectedTypeIndex];
                editingValue = InvokeGet(t, key);
            }
            catch (Exception ex)
            {
                editingValue = null;
                EditorUtility.DisplayDialog("加载失败", ex.Message, "OK");
            }
        }

        private void SaveEditing()
        {
            if (string.IsNullOrEmpty(editingKeyName) || selectedTypeIndex < 0) return;
            try
            {
                var t = archiveTypes[selectedTypeIndex];

                // JSON 模式下，从 jsonEditText 重新解析
                if (useJsonEditor && !string.IsNullOrEmpty(jsonEditText))
                {
                    try { editingValue = JsonConvert.DeserializeObject(jsonEditText, t); }
                    catch (Exception ex)
                    {
                        EditorUtility.DisplayDialog("JSON 解析失败", ex.Message, "OK");
                        return;
                    }
                }

                InvokeSet(t, editingKeyName, editingValue);
                RefreshKeysListOnly();
                ShowNotification(new GUIContent("已保存"));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("保存失败", ex.Message, "OK");
            }
        }

        private void DeleteCurrentKey()
        {
            try
            {
                var t = archiveTypes[selectedTypeIndex];
                InvokeDeleteKey(t, editingKeyName);
                editingKeyName = string.Empty;
                editingValue = null;
                selectedKeyIndex = -1;
                jsonEditText = string.Empty;
                referencesLastType = null;
                RefreshKeysListOnly();
                ShowNotification(new GUIContent("已删除"));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("删除失败", ex.Message, "OK");
            }
        }

        private void ClearAllKeys()
        {
            try
            {
                var t = archiveTypes[selectedTypeIndex];
                InvokeClear(t);
                selectedKeyIndex = -1;
                editingKeyName = string.Empty;
                editingValue = null;
                jsonEditText = string.Empty;
                referencesLastType = null;
                RefreshKeysListOnly();
                ShowNotification(new GUIContent("已清空"));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("清空失败", ex.Message, "OK");
            }
        }

        private void TryAddKey()
        {
            var t = GetSelectedType();
            if (t == null) return;
            var key = newKeyInput?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                ShowNotification(new GUIContent("Key 不能为空"));
                return;
            }
            try
            {
                if (InvokeContainsKey(t, key))
                {
                    ShowNotification(new GUIContent("Key 已存在"));
                    return;
                }
                var defaultValue = t.IsValueType ? Activator.CreateInstance(t) : null;
                InvokeSet(t, key, defaultValue);
                newKeyInput = string.Empty;
                RefreshKeysListOnly();
                selectedKeyIndex = Array.IndexOf(currentKeys, key);
                if (selectedKeyIndex >= 0) LoadKeyForEdit(key);
                ShowNotification(new GUIContent("已新增"));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("新增失败", ex.Message, "OK");
            }
        }

        private void RefreshKeysListOnly()
        {
            try
            {
                var t = archiveTypes[selectedTypeIndex];
                currentKeys = InvokeGetAllKeys(t).OrderBy(k => k).ToArray();
            }
            catch { currentKeys = Array.Empty<string>(); }
        }

        private Type GetSelectedType()
        {
            if (archiveTypes == null || selectedTypeIndex < 0 || selectedTypeIndex >= archiveTypes.Length)
                return null;
            return archiveTypes[selectedTypeIndex];
        }

        // ========================== 工具方法 ==========================

        private static bool IsChanged(object oldVal, object newVal)
        {
            if (ReferenceEquals(oldVal, newVal)) return false;
            if (oldVal == null && newVal == null) return false;
            if (oldVal == null || newVal == null) return true;
            return !oldVal.Equals(newVal);
        }

        private static FieldInfo[] GetSerializableFields(Type type)
        {
            return type
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => !f.IsStatic && !f.IsLiteral && !f.IsInitOnly)
                .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)
                .OrderBy(f => f.IsPublic ? 0 : 1)
                .ToArray();
        }

        // ========================== 反射调用包装 ==========================

        private MethodInfo[] GetMethods(Type t)
        {
            if (methodCache.TryGetValue(t, out var cached)) return cached;
            var flags = BindingFlags.Public | BindingFlags.Static;
            cached = new[]
            {
                // Get<T>(string)
                typeof(PrefsHelper).GetMethods(flags)
                    .First(m => m.Name == "Get" && m.IsGenericMethodDefinition
                                && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string)),
                // Set<T>(string, T)
                typeof(PrefsHelper).GetMethods(flags)
                    .First(m => m.Name == "Set" && m.IsGenericMethodDefinition
                                && m.GetParameters().Length == 2
                                && m.GetParameters()[0].ParameterType == typeof(string)),
                // DeleteKey<T>(string)
                typeof(PrefsHelper).GetMethods(flags)
                    .First(m => m.Name == "DeleteKey" && m.IsGenericMethodDefinition
                                && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string)),
                // ContainsKey<T>(string)
                typeof(PrefsHelper).GetMethods(flags)
                    .First(m => m.Name == "ContainsKey" && m.IsGenericMethodDefinition
                                && m.GetParameters().Length == 1
                                && m.GetParameters()[0].ParameterType == typeof(string)),
                // Clear<T>()
                typeof(PrefsHelper).GetMethods(flags)
                    .First(m => m.Name == "Clear" && m.IsGenericMethodDefinition
                                && m.GetParameters().Length == 0),
                // GetAllKeys<T>()
                typeof(PrefsHelper).GetMethods(flags)
                    .First(m => m.Name == "GetAllKeys" && m.IsGenericMethodDefinition
                                && m.GetParameters().Length == 0
                                && m.ReturnType == typeof(IEnumerable<string>)),
            };
            methodCache[t] = cached;
            return cached;
        }

        private object InvokeGet(Type t, string key)
        {
            var m = GetMethods(t)[0].MakeGenericMethod(t);
            return m.Invoke(null, new object[] { key });
        }

        private void InvokeSet(Type t, string key, object value)
        {
            var m = GetMethods(t)[1].MakeGenericMethod(t);
            m.Invoke(null, new object[] { key, value });
        }

        private void InvokeDeleteKey(Type t, string key)
        {
            var m = GetMethods(t)[2].MakeGenericMethod(t);
            m.Invoke(null, new object[] { key });
        }

        private bool InvokeContainsKey(Type t, string key)
        {
            var m = GetMethods(t)[3].MakeGenericMethod(t);
            return (bool)m.Invoke(null, new object[] { key });
        }

        private void InvokeClear(Type t)
        {
            var m = GetMethods(t)[4].MakeGenericMethod(t);
            m.Invoke(null, null);
        }

        private string[] InvokeGetAllKeys(Type t)
        {
            var m = GetMethods(t)[5].MakeGenericMethod(t);
            return ((System.Collections.IEnumerable)m.Invoke(null, null))
                .Cast<string>()
                .ToArray();
        }
    }
}
