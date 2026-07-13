using Cysharp.Text;
using Lin.Editor.Settings;
using Lin.Runtime.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ZLinq;
using Object = UnityEngine.Object;

namespace Lin.Editor.Attribute
{
    public static class OnSceneGUIAttribute
    {
        class SceneGUIClassInfo
        {
            public bool isExpanded = true;
            public ValueEnumerable<ZLinq.Linq.ArrayWhere<MethodInfo>, MethodInfo> showMethods;
            public ValueEnumerable<ZLinq.Linq.ArrayWhere<FieldInfo>, FieldInfo> fields; 
            public ValueEnumerable<ZLinq.Linq.ArrayWhere<PropertyInfo>, PropertyInfo> properties;
            public ValueEnumerable<ZLinq.Linq.ArrayWhere<MethodInfo>, MethodInfo> drawMethods;

            public bool isEmpty => !showMethods.Any() && !fields.Any() && !properties.Any() && !drawMethods.Any();
        }

        private static Dictionary<Object, SceneGUIClassInfo> selectedMethods;
        private static List<SceneGUIClassInfo> staticMethods;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            selectedMethods = new Dictionary<Object, SceneGUIClassInfo>();
            staticMethods = new List<SceneGUIClassInfo>();

            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies.AsValueEnumerable())
            {
                foreach (var type in assembly.GetTypes().AsValueEnumerable())
                {
                    var show = type
                        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .AsValueEnumerable()
                        .Where(m => m.GetCustomAttributes(typeof(Runtime.Attribute.ShowInSceneGUIAttribute), false).Length > 0 && m.GetParameters().Length == 0);

                    var fields = type
                        .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .AsValueEnumerable()
                        .Where(f => f.GetCustomAttributes(typeof(Runtime.Attribute.ShowInSceneGUIAttribute), false).Length > 0);

                    var properties = type
                        .GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .AsValueEnumerable()
                        .Where(p => p.GetCustomAttributes(typeof(Runtime.Attribute.ShowInSceneGUIAttribute), false).Length > 0);

                    var draw = type
                        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .AsValueEnumerable()
                        .Where(m => m.GetCustomAttributes(typeof(Runtime.Attribute.DrawInSceneGUIAttribute), false).Length > 0 && m.GetParameters().Length == 0);

                    var info = new SceneGUIClassInfo
                    {
                        showMethods = show,
                        fields = fields,
                        properties = properties,
                        drawMethods = draw
                    };

                    if (!info.isEmpty) 
                        staticMethods.Add(info);
                }
            }
        }

        private static void OnSelectionChanged()
        {
            selectedMethods.Clear();

            if (Selection.activeGameObject != null)
            {
                CollectAttributes();
                CalculateBounds();
            }

            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            DrawStaticMethods();
            DrawSelectedObject();
            DrawObjectBounds();
        }

        private static void CollectAttributes()
        {
            var gameObject = Selection.activeGameObject;
            // Components
            foreach (var component in gameObject.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || selectedMethods.ContainsKey(component))
                    continue;

                var classInfo = new SceneGUIClassInfo();
                classInfo.showMethods = component.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .AsValueEnumerable()
                    .Where(m => m.GetCustomAttributes(typeof(Runtime.Attribute.ShowInSceneGUIAttribute), false).Length > 0 && m.GetParameters().Length == 0);

                classInfo.fields = component.GetType()
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .AsValueEnumerable()
                    .Where(f => f.GetCustomAttributes(typeof(Runtime.Attribute.ShowInSceneGUIAttribute), false).Length > 0);

                classInfo.properties = component.GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .AsValueEnumerable()
                    .Where(p => p.GetCustomAttributes(typeof(Runtime.Attribute.ShowInSceneGUIAttribute), false).Length > 0);

                classInfo.drawMethods = component.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .AsValueEnumerable()
                    .Where(m => m.GetCustomAttributes(typeof(Runtime.Attribute.DrawInSceneGUIAttribute), false).Length > 0 && m.GetParameters().Length == 0);

                if (classInfo.isEmpty)
                    continue;

                selectedMethods.Add(component, classInfo);
            }

        }

        private static void DrawStaticMethods()
        {
            if (staticMethods == null || staticMethods.Count == 0)
                return;

            Handles.BeginGUI();
            {
                GUILayout.BeginArea(new Rect(10, 10, 200, 500));
                {
                    foreach (var info in staticMethods.AsValueEnumerable())
                    {
                        info.isExpanded = EditorGUILayout.Foldout(info.isExpanded, "Static");
                        if (info.isExpanded)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                GUILayout.Space(10);
                                GUILayout.BeginVertical("box");
                                {
                                    foreach (var m in info.showMethods)
                                        if (GUILayout.Button(m.Name)) 
                                            m.Invoke(null, null);
                                    foreach (var m in info.drawMethods)
                                        m.Invoke(null, null);

                                    foreach (var f in info.fields)
                                        GUILayout.Label(ZString.Concat(f.Name, ": ", f.GetValue(null)));

                                    foreach (var p in info.properties)
                                        GUILayout.Label(ZString.Concat(p.Name, ": ", p.GetValue(null)));
                                }
                                GUILayout.EndVertical();
                            }
                            GUILayout.EndHorizontal();
                            EditorGUILayout.Space();
                        }
                    }
                }
                GUILayout.EndArea();
            }
            Handles.EndGUI();
        }

        private static void DrawSelectedObject()
        {
            if (selectedMethods.Empty())
                return;

            Handles.BeginGUI();
            {
                GUILayout.BeginArea(new Rect(50, 10, 200, 500));
                {
                    GUILayout.Label(Selection.activeGameObject.name);
                    foreach (var pair in selectedMethods)
                    {
                        var component = pair.Key;
                        var classInfo = pair.Value;

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Space(20);
                            GUILayout.BeginVertical();
                            {
                                classInfo.isExpanded = EditorGUILayout.Foldout(classInfo.isExpanded, component.GetType().Name);

                                if (classInfo.isExpanded)
                                {
                                    GUILayout.BeginHorizontal();
                                    {
                                        GUILayout.Space(20);
                                        GUILayout.BeginVertical("box");
                                        {

                                            // 渲染方法
                                            foreach (var method in classInfo.showMethods)
                                                if (GUILayout.Button(method.Name))
                                                    method.Invoke(component, null);
                                            foreach (var method in classInfo.drawMethods)
                                                method.Invoke(component, null);

                                            // 渲染字段
                                            foreach (var field in classInfo.fields)
                                                GUILayout.Label($"{field.Name}: {field.GetValue(component)}");

                                            // 渲染属性
                                            foreach (var property in classInfo.properties)
                                                GUILayout.Label($"{property.Name}: {property.GetValue(component)}");

                                        }
                                        GUILayout.EndVertical();
                                    }
                                    GUILayout.EndHorizontal();
                                }
                            }
                            GUILayout.EndVertical();
                        }
                        GUILayout.EndHorizontal();
                        EditorGUILayout.Space();
                    }
                }
                GUILayout.EndArea();
            }
            Handles.EndGUI();
        }

        #region - ObjectBounds -

        class RendererInfos
        {
            public Bounds bounds;
            public int vertexsCount;
            public int trianglesCount;
        }

        private static RendererInfos infos;

        private static void CalculateBounds()
        {
            var target = Selection.activeObject;
            if (target == null)
            {
                infos = null;
                return;
            }

            if (target is not GameObject go || !go.scene.IsValid())
            {
                infos = null;
                return;
            }

            var gameObject = target as GameObject;
            if (!gameObject.TryGetComponentInChildren<Renderer>(out var renderer))
            {
                infos = null;
                return;
            }

            infos = new RendererInfos();
            {
                infos.bounds = gameObject.CalculateObjectBounds(false);

                void AddMeshInfo(Mesh mesh)
                {
                    if (mesh == null)
                        return;

                    infos.vertexsCount += mesh.vertexCount;
                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        if (mesh.GetTopology(i) == MeshTopology.Triangles)
                            infos.trianglesCount += (int)mesh.GetIndexCount(i) / 3;
                    }
                }

                var filters = gameObject.GetComponentsInChildren<MeshFilter>();
                foreach (var filter in filters)
                    AddMeshInfo(filter.sharedMesh);

                var skinners = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skinner in skinners)
                    AddMeshInfo(skinner.sharedMesh);
            }
        }

        private static void DrawObjectBounds()
        {
            var settings = EditorSettings_SO.GetInstance();
            if (infos == null || !settings.drawRendererInfos_SV)
                return;

            Handles.color = settings.infosColor_SV;
            Handles.DrawWireCube(infos.bounds.center, infos.bounds.size);

            GUIStyle style = new GUIStyle();
            style.normal.textColor = settings.infosColor_SV;
            style.fontSize = 15;
            style.alignment = TextAnchor.MiddleCenter;

            // 在包围盒的屏幕右下角位置绘制标签
            var sceneView = SceneView.currentDrawingSceneView ?? SceneView.lastActiveSceneView;
            var camera = sceneView != null ? sceneView.camera : null;

            var center = infos.bounds.center;
            var extents = infos.bounds.extents;

            // 计算 AABB 的 8 个角点
            var corners = new Vector3[8];
            corners[0] = center + new Vector3( extents.x,  extents.y,  extents.z);
            corners[1] = center + new Vector3( extents.x,  extents.y, -extents.z);
            corners[2] = center + new Vector3( extents.x, -extents.y,  extents.z);
            corners[3] = center + new Vector3( extents.x, -extents.y, -extents.z);
            corners[4] = center + new Vector3(-extents.x,  extents.y,  extents.z);
            corners[5] = center + new Vector3(-extents.x,  extents.y, -extents.z);
            corners[6] = center + new Vector3(-extents.x, -extents.y,  extents.z);
            corners[7] = center + new Vector3(-extents.x, -extents.y, -extents.z);

            // 选择屏幕坐标 (x 最大, y 最大) 的角点作为右下角
            var bestWorld = corners[0];
            var bestScore = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                var gui = HandleUtility.WorldToGUIPoint(corners[i]);
                var score = gui.x + gui.y;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestWorld = corners[i];
                }
            }

            // 稍微沿相机的右下方向偏移，避免与边线重叠
            if (camera != null)
            {
                var size = HandleUtility.GetHandleSize(bestWorld) * 0.2f;
                bestWorld += camera.transform.right * (size * 0.3f) - camera.transform.up * (size * 0.3f);
            }

            Handles.Label(bestWorld, $"顶点数: {infos.vertexsCount}\n面数: {infos.trianglesCount}", style);
        }

        #endregion

    }
}
