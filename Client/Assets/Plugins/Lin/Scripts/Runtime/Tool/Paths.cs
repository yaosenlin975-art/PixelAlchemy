/*
┌────────────────────────────┐
│　Description: 路径点配置
│　Remark: 
└────────────────────────────┘
*/
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Pool;
using ZLinq;
using Lin.Runtime.Helper;
using Lin.Runtime.DesignPattern.Singleton;
using static Lin.Runtime.Helper.TransformExtensions;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;
#endif

namespace Lin.Runtime.Tool
{
    [Serializable]
    public class Paths : MonoBehaviour
    {
        public enum ELocalType
        {
            World,
            Transform,
            TransformValues,
        }

        [Title("Local")]
        [EnumToggleButtons]
        public ELocalType localType;
        private ELocalType calculateType;
        private bool localWithTransform => localType == ELocalType.Transform;
        private bool localWithTransformValues => localType == ELocalType.TransformValues;

        [SerializeField]
        [ShowIf(nameof(localWithTransform))]
        private Transform parent;
        public Transform Parent
        {
            get
            {
                if (parent == null)
                    return transform;

                return parent;
            }
            set => parent = value;
        }

        [ShowIf(nameof(localWithTransformValues))]
        public TransformValues transformValues;

        public float length
        {
            get
            {
                _ = Points;
                return isBezier ? bezierLength : pathsLength;
            }
        }

        public float pathsLength { get; private set; }

        [SerializeField]
        [HideInInspector]
        private List<TransformValues> points;
        private List<TransformValues> bezierPoints;
        private List<TransformValues> computedPoints;
        private bool initialized = false;

        [Title("贝塞尔曲线")]
        [LabelText("曲线拟合")]
        [OnValueChanged(nameof(OnBezierOptionChanged))]
        public bool isBezier;

        [MinValue(1)]
        [ShowIf(nameof(isBezier))]
        [OnValueChanged(nameof(OnBezierOptionChanged))]
        public int bezierSegment = 10;

        public float bezierLength { get; private set; }

        private void Reset()
        {
            transformValues = transform.WorldValues();

            points = new List<TransformValues>()
            {
                new TransformValues
                {
                    position = Vector3.zero,
                    euler = Vector3.zero,
                    scale = Vector3.one
                },
                new TransformValues
                {
                    position = Vector3.forward,
                    euler = Vector3.zero,
                    scale = Vector3.one
                }
            };
        }

        public List<TransformValues> Points
        {
            get
            {
                if (!initialized)
                {
                    pathsLength = 0;
                    bezierLength = 0;

                    var src = points ?? new List<TransformValues>();
                    if (src.Count > 1)
                    {
                        var start = ToWorldPosition(src[0]);
                        for (int i = 1; i < src.Count; i++)
                        {
                            var current = ToWorldPosition(src[i]);
                            pathsLength += Vector3.Distance(current, start);
                            start = current;
                        } 

                        if (isBezier)
                        {
                            Calculate();
                            var curve = bezierPoints ?? src;
                            if (curve.Count > 1)
                            {
                                start = ToWorldPosition(curve[0]);
                                for (int i = 1; i < curve.Count; i++)
                                {
                                    var current = ToWorldPosition(curve[i]);
                                    bezierLength += Vector3.Distance(current, start);
                                    start = current;
                                }
                            }
                        }
                    }
                    initialized = true;
                }

                var baseList = isBezier ? (bezierPoints ?? points) : points;
                if (localType == ELocalType.World) 
                    return baseList;

#if UNITY_EDITOR
                if (computedPoints == null)
                    computedPoints = new List<TransformValues>(baseList.Count);
                else
                    computedPoints.Clear();

                for (int i = 0; i < baseList.Count; i++)
                    computedPoints.Add(ToWorld(baseList[i]));

#else
                if (computedPoints == null || calculateType != localType)
                {
                    computedPoints = computedPoints ?? new List<TransformValues>(baseList.Count);
                    computedPoints.Clear();

                    for (int i = 0; i < baseList.Count; i++)
                        computedPoints.Add(ToWorld(baseList[i]));
                    calculateType = localType;
                }
#endif

                return computedPoints;
            }
        }

        // 贝塞尔状态发生变化
        private void OnBezierOptionChanged()
        {
            if (isBezier)
                Calculate();
        }

        // 折线换算为贝塞尔曲线点
        private void Calculate()
        {
            if (points == null || points.Count == 0)
            {
                bezierPoints = points;
                return;
            }

            if (points.Count == 1)
            {
                if (bezierPoints == null) 
                    bezierPoints = new List<TransformValues>(1);
                else 
                    bezierPoints.Clear();

                bezierPoints.Add(points[0]);
                return;
            }

            int segCount = points.Count - 1;
            int samplesPerSeg = bezierSegment + 1;
            int totalSamples = segCount * samplesPerSeg;

            var src = new NativeArray<Vector3>(points.Count, Allocator.TempJob);
            for (int i = 0; i < points.Count; i++) src[i] = points[i].position;
            var dst = new NativeArray<Vector3>(totalSamples, Allocator.TempJob);

            new CatmullRomJob
            {
                positions = src,
                output = dst,
                samplesPerSeg = samplesPerSeg,
                pointsCount = points.Count
            }.Schedule(totalSamples, 64).Complete();

            if (bezierPoints == null)
                bezierPoints = new List<TransformValues>(totalSamples);
            else
                bezierPoints.Clear();

            for (int idx = 0; idx < totalSamples; idx++)
            {
                int seg = idx / samplesPerSeg;
                int s = idx % samplesPerSeg;
                float t = samplesPerSeg > 1 ? (float)s / (samplesPerSeg - 1) : 0f;

                int nextSeg = seg + 1;
                if (nextSeg >= points.Count) nextSeg = points.Count - 1;

                var r0 = points[seg].euler;
                var r1 = points[nextSeg].euler;
                var sc0 = points[seg].scale;
                var sc1 = points[nextSeg].scale;

                bezierPoints.Add(new TransformValues
                {
                    position = dst[idx],
                    euler = Quaternion.Slerp(r0.ToVector3().ToQuaternion(), r1.ToVector3().ToQuaternion(), t).eulerAngles,
                    scale = Vector3.Lerp(sc0, sc1, t)
                });
            }

            src.Dispose();
            dst.Dispose();
        }

        public struct CatmullRomJob : IJobParallelFor
        {
            [Unity.Collections.ReadOnly] 
            public NativeArray<Vector3> positions;

            public NativeArray<Vector3> output;
            public int samplesPerSeg;
            public int pointsCount;

            public void Execute(int index)
            {
                int seg = index / samplesPerSeg;
                int s = index % samplesPerSeg;
                float t = samplesPerSeg > 1 ? (float)s / (samplesPerSeg - 1) : 0f;

                Vector3 p0 = seg > 0 ? positions[seg - 1] : positions[seg];
                Vector3 p1 = positions[seg];
                Vector3 p2 = positions[seg + 1];
                Vector3 p3 = (seg + 2) < pointsCount ? positions[seg + 2] : positions[seg + 1];

                float t2 = t * t;
                float t3 = t2 * t;
                Vector3 res = 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                output[index] = res;
            }
        }

        // === LocalType conversions (runtime) ===
        private TransformValues ToWorld(TransformValues values)
        {
            var result = TransformValues.Default;
            result.position = ToWorldPosition(values);
            result.euler = ToWorldRotation(values).eulerAngles;
            result.scale = ToWorldScale(values.scale);
            return result;
        }

        private TransformValues ToLocal(TransformValues values)
        {
            var result = TransformValues.Default;
            result.position = ToLocalPosition(values.position);
            result.euler = ToLocalRotation(values.euler.ToVector3().ToQuaternion()).eulerAngles;
            result.scale = ToLocalScale(values.scale);
            return result;
        }

        private Vector3 ToWorldPosition(TransformValues v)
        {
            switch (localType)
            {
                case ELocalType.Transform:
                    return Parent.TransformPoint(v.position);

                case ELocalType.TransformValues:
                    var tv = transformValues;
                    var scaled = new Vector3(v.position.x * tv.scale.x, v.position.y * tv.scale.y, v.position.z * tv.scale.z);
                    return tv.euler.ToVector3().ToQuaternion() * scaled + tv.position.ToVector3();

                case ELocalType.World:
                default:
                    return v.position;
            }
        }

        private Quaternion ToWorldRotation(TransformValues v)
        {
            switch (localType)
            {
                case ELocalType.Transform:
                    return Parent.rotation * v.euler.ToVector3().ToQuaternion();

                case ELocalType.TransformValues:
                    return transformValues.euler.ToVector3().ToQuaternion() * v.euler.ToVector3().ToQuaternion();

                case ELocalType.World:
                default:
                    return v.euler.ToVector3().ToQuaternion();
            }
        }

        private Vector3 ToLocalPosition(Vector3 world)
        {
            switch (localType)
            {
                case ELocalType.Transform:
                    return Parent.InverseTransformPoint(world);

                case ELocalType.TransformValues:
                    var tv = transformValues;
                    var delta = world - tv.position.ToVector3();
                    var invRot = Quaternion.Inverse(tv.euler.ToVector3().ToQuaternion());
                    var rotated = invRot * delta;
                    float ex = Mathf.Approximately(tv.scale.x, 0f) ? 1f : tv.scale.x;
                    float ey = Mathf.Approximately(tv.scale.y, 0f) ? 1f : tv.scale.y;
                    float ez = Mathf.Approximately(tv.scale.z, 0f) ? 1f : tv.scale.z;
                    return new Vector3(rotated.x / ex, rotated.y / ey, rotated.z / ez);

                case ELocalType.World:
                default:
                    return world;
            }
        }

        private Quaternion ToLocalRotation(Quaternion world)
        {
            switch (localType)
            {
                case ELocalType.Transform:
                    return Quaternion.Inverse(Parent.rotation) * world;

                case ELocalType.TransformValues:
                    return Quaternion.Inverse(transformValues.euler.ToVector3().ToQuaternion()) * world;

                case ELocalType.World:
                default:
                    return world;
            }
        }

        private Vector3 ToWorldScale(Vector3 local)
        {
            switch (localType)
            {
                case ELocalType.Transform:
                    var ls = Parent.lossyScale;
                    return new Vector3(local.x * ls.x, local.y * ls.y, local.z * ls.z);

                case ELocalType.TransformValues:
                    var tvs = transformValues.scale;
                    return new Vector3(local.x * tvs.x, local.y * tvs.y, local.z * tvs.z);

                case ELocalType.World:
                default:
                    return local;
            }
        }

        private Vector3 ToLocalScale(Vector3 world)
        {
            switch (localType)
            {
                case ELocalType.Transform:
                    var ls = Parent.lossyScale;
                    float ex = Mathf.Approximately(ls.x, 0f) ? 1f : ls.x;
                    float ey = Mathf.Approximately(ls.y, 0f) ? 1f : ls.y;
                    float ez = Mathf.Approximately(ls.z, 0f) ? 1f : ls.z;
                    return new Vector3(world.x / ex, world.y / ey, world.z / ez);

                case ELocalType.TransformValues:
                    var tvs = transformValues.scale;
                    float ex2 = Mathf.Approximately(tvs.x, 0f) ? 1f : tvs.x;
                    float ey2 = Mathf.Approximately(tvs.y, 0f) ? 1f : tvs.y;
                    float ez2 = Mathf.Approximately(tvs.z, 0f) ? 1f : tvs.z;
                    return new Vector3(world.x / ex2, world.y / ey2, world.z / ez2);

                case ELocalType.World:
                default:
                    return world;
            }
        }

        public void SetDirty()
        {
            initialized = false;
            this.SetDirty<Paths>();
        }

#if UNITY_EDITOR

        [Title("Editor")]

        [SerializeField]
        [SceneObjectsOnly]
        [LabelText("示例物体")]
        private Transform sampleTransfrom;

        [ColorPalette]
        [SerializeField]
        [LabelText("折线颜色")]
        private Color lineColor = Color.green;

        [ColorPalette]
        [SerializeField]
        [LabelText("曲线颜色")]
        [ShowIf(nameof(isBezier))]
        private Color bezierColor = Color.gray;

        private Vector2 pointScrolPos;

        private void OnDrawGizmosSelected() => AdvancePathsMonitor.GetInstance().Set(this);

        private void OnDrawGizmos() => AdvancePathsMonitor.GetInstance().Register(this);

        [OnInspectorGUI]
        private void OnInspectorGUI()
        {
            var __ = length;
            GUILayout.Label($"折线 点数: {points.Count} 长度: {pathsLength:0.00}M");
            GUILayout.Label($"曲线 点数: {(points.Count - 1) * (bezierSegment + 1)}长度: {bezierLength:0.00}M");

            GUILayout.Space(10);
            int high = Mathf.Min(points.Count * 100, 500);
            pointScrolPos = GUILayout.BeginScrollView(pointScrolPos, GUILayout.Height(high));
            bool shouldRefresh = false;

            GUILayout.BeginHorizontal();
            {
                GUILayout.Label("点列表");

                if (GUILayout.Button("+", GUILayout.Width(25)))
                {
                    if (points.Count <= 1)
                    {
                        points.Add(new TransformValues
                        {
                            position = Vector3.zero,
                            euler = Vector3.zero,
                            scale = Vector3.one
                        });
                    }
                    else
                    {
                        points.Add(new TransformValues
                        {
                            position = points[points.Count - 1].position.ToVector3() + (points[points.Count - 1].position - points[points.Count - 2].position).ToVector3().normalized,
                            euler = Vector3.zero,
                            scale = Vector3.one
                        });
                    }
                    shouldRefresh = true;
                }
                
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    if (points.Count > 0)
                    {
                        points.RemoveAt(points.Count - 1);
                        shouldRefresh = true;
                    }
                }

                if (GUILayout.Button("R", GUILayout.Width(25))) 
                {
                    points.Clear();
                    points.Add(TransformValues.Default);
                    var value = TransformValues.Default;
                    value.position += Vector3.forward;
                    points.Add(value);
                    shouldRefresh = true;
                }
            }
            GUILayout.EndHorizontal();

            using var _ = ListPool<int>.Get(out var toRemoves);
            int count = points.Count;
            for (int i = 0; i < count; i++)
            {
                var point = points[i];
                int index = i;

                GUILayout.BeginVertical();
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label($"{i + 1}");
                        if (GUILayout.Button("R", GUILayout.Width(25)))
                        {
                            //若是端点  则用前两个/后两个点的朝向, 若是中心点, 则用左右两个点的中点
                            if (i == 0)
                            {
                                if (count > 2)
                                {
                                    point.position = points[1].position - (points[2].position - points[1].position).ToVector3().normalized;
                                    point.euler = Vector3.zero;
                                    point.scale = Vector3.one;
                                }
                                else
                                    point = TransformValues.Default;
                            }
                            else if (i == count - 1)
                            {
                                if (count > 2)
                                {
                                    point.position = points[count - 1].position + (points[count - 1].position - points[count - 2].position).ToVector3().normalized;
                                    point.euler = Vector3.zero;
                                    point.scale = Vector3.one;
                                }
                                else
                                    point = TransformValues.Default;
                            }
                            else
                            {
                                Vector3Extensions.SerializableVector3 dir = points[i + 1].position - points[i - 1].position;
                                point.position = points[i - 1].position + dir / 2f;
                                point.euler = Vector3.zero;
                                point.scale = Vector3.one;
                            }

                            shouldRefresh = true;
                        }

                        if (GUILayout.Button(EditorGUIUtility.IconContent("winbtn_win_close"), GUILayout.Width(25)))
                        {
                            toRemoves.Add(i);
                            shouldRefresh = true;
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Pos", GUILayout.Width(40));
                        point.position = EditorGUILayout.Vector3Field(string.Empty, point.position);
                        if (GUILayout.Button(EditorGUIUtility.IconContent("MoveTool"), GUILayout.Width(25)))
                            AdvancePathsMonitor.GetInstance().Set(this, index, ETransformValueType.Position);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Rot", GUILayout.Width(40));
                        point.euler = EditorGUILayout.Vector3Field(string.Empty, point.euler);
                        if (GUILayout.Button(EditorGUIUtility.IconContent("RotateTool"), GUILayout.Width(25)))
                            AdvancePathsMonitor.GetInstance().Set(this, index, ETransformValueType.Rotation);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Scale", GUILayout.Width(40));
                        point.scale = EditorGUILayout.Vector3Field(string.Empty, point.scale);
                        if (GUILayout.Button(EditorGUIUtility.IconContent("ScaleTool"), GUILayout.Width(25)))
                            AdvancePathsMonitor.GetInstance().Set(this, index, ETransformValueType.Scale);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(5);
                }
                GUILayout.EndVertical();

                points[i] = point;
            }
            GUILayout.EndScrollView();

            if (toRemoves.Count > 0)
            {
                toRemoves.Sort((a, b) => b.CompareTo(a));
                foreach (var index in toRemoves.AsValueEnumerable())
                    points.RemoveAt(index);
            }

            if (shouldRefresh)
                Calculate();
        }

        // SceneView中绘制线路逻辑
        class AdvancePathsMonitor : Singleton<AdvancePathsMonitor>
        {
            private const int INVALUE_INDEX = -1;

            private Paths currentPaths
            {
                get
                {
                    if (pathsIndex == INVALUE_INDEX || pathsIndex >= pathsSet.Count)
                        return null;

                    return pathsSet[pathsIndex];
                }
            }

            private TransformValues currentPoint
            {
                get
                {
                    if (currentPaths is null || pointIndex == INVALUE_INDEX || pointIndex >= currentPaths.points.Count)
                        return TransformValues.Default;

                    return currentPaths.points[pointIndex];
                }
                set
                {
                    if (currentPaths is null || pointIndex == INVALUE_INDEX || pointIndex >= currentPaths.points.Count)
                        return;

                    currentPaths.points[pointIndex] = value;
                }
            } 

            private List<Paths> pathsSet;
            private int pathsIndex = -1;
            private int pointIndex = -1;
            private ETransformValueType operationType = ETransformValueType.Position;

            private bool shouldRefresh;
            private readonly TimeSpan refreshInterval = new TimeSpan(0, 0, 0, 0, 100);
            private DateTime lastRefreshTime;

            public AdvancePathsMonitor()
            {
                pathsSet = new List<Paths>();

                SceneView.duringSceneGui += sceneView =>
                {
                    foreach (var paths in pathsSet.AsValueEnumerable())
                        Draw(paths);
                };
                EditorApplication.update += () =>
                {
                    if (pathsIndex != INVALUE_INDEX && shouldRefresh && DateTime.Now - lastRefreshTime >= refreshInterval)
                    {
                        pathsSet[pathsIndex].Calculate();
                        lastRefreshTime = DateTime.Now;
                        shouldRefresh = false;
                    }
                };
                EditorApplication.playModeStateChanged += state => pathsSet.Clear();
                EditorSceneManager.sceneClosing += (a, b) => pathsSet.Clear();
            }

            public void Set(Paths target)
            {
                if (pathsIndex != INVALUE_INDEX && pathsSet[pathsIndex] == target)
                    return;

                pathsIndex = pathsSet.IndexOf(target);
                pointIndex = 0;

                if (SceneView.lastActiveSceneView != null && currentPaths?.points.Count > 0)
                {
                    var sceneView = SceneView.lastActiveSceneView;
                    sceneView.Frame(new Bounds(currentPaths.ToWorldPosition(currentPoint), Vector3.one * 2), false);
                }

                if (Selection.activeObject != target)
                    Selection.activeObject = target;
            }

            public void Register(Paths paths)
            {
                if (pathsSet.Contains(paths))
                    return;

                pathsSet.Add(paths);
            }

            public void Set(Paths paths, int index, ETransformValueType operationType)
            {
                if (currentPaths == paths && index == pointIndex && this.operationType == operationType)
                    return;

                pathsIndex = pathsSet.IndexOf(paths);
                pointIndex = index;
                this.operationType = operationType;

                // 让 SceneView 摄像机看向目标点
                if (SceneView.lastActiveSceneView != null && paths.points.Count > index)
                {
                    var sceneView = SceneView.lastActiveSceneView;
                    var wp = paths.ToWorldPosition(paths.points[index]);
                    sceneView.Frame(new Bounds(wp, Vector3.one * 2), false);
                }

                if (paths.sampleTransfrom != null)
                {
                    switch (paths.localType)
                    {
                        case ELocalType.World:
                            paths.sampleTransfrom.SetWorld(paths.points[index]);
                            break;

                        case ELocalType.Transform:
                            paths.sampleTransfrom.SetWorld(paths.ToWorld(paths.points[index]));
                            break;

                        case ELocalType.TransformValues:
                        default:
                            break;
                    }
                }

                if (Selection.activeObject != paths)
                    Selection.activeObject = paths;
            }

            private void Draw(Paths paths)
            {
                if (paths is null)
                    return;

                if (paths.points == null || paths.points.Count < 2)
                    return;

                if (paths.isBezier)
                {
                    var curve = paths.Points;
                    if (curve != null && curve.Count > 1)
                    {
                        Handles.color = paths.bezierColor;
                        for (int i = 0; i < curve.Count - 1; i++)
                            Handles.DrawLine(curve[i].position, curve[i + 1].position);
                    }
                }

                if (paths.localWithTransformValues)
                {
                    Handles.Label(paths.transformValues.position + Vector3.up * 0.25f, "Base Point");
                    var newPos = Handles.PositionHandle(paths.transformValues.position, paths.transformValues.euler.ToQuaternion());
                    if (EditorGUI.EndChangeCheck() && !newPos.ApproximatelyEqual(paths.transformValues.position))
                    {
                        Undo.RecordObject(paths, "Move BasePoint");
                        paths.transformValues.position = newPos;
                        shouldRefresh = true;
                    }
                }

                Handles.color = paths.lineColor;
                var drawLine = paths.points;
                for (int i = 0; i < drawLine.Count - 1; i++)
                    Handles.DrawLine(paths.ToWorldPosition(drawLine[i]), paths.ToWorldPosition(drawLine[i + 1]));

                // 处理点的编辑
                for (int i = 0; i < paths.points.Count; i++)
                {
                    var point = paths.points[i];
                    var currentPos = paths.ToWorldPosition(point);
                    var currentRot = paths.ToWorldRotation(point);
                    float size = HandleUtility.GetHandleSize(currentPos) * 0.05f;

                    // 绘制序号标签
                    Handles.Label(currentPos + Vector3.up * 0.25f, (i + 1).ToString());
                    Handles.Label(currentPos + Vector3.up * 0.5f, currentPos.ToString());

                    if (pointIndex != i || paths != currentPaths)
                    {
                        Handles.color = Color.white;
                        if (Handles.Button(currentPos, Quaternion.identity, size, size, Handles.DotHandleCap))
                        {
                            Set(paths, i, operationType);
                            Tools.current = UnityEditor.Tool.None;
                        }
                    }
                    // 创建可拖拽的控制点
                    else
                    {
                        EditorGUI.BeginChangeCheck();
                        switch (operationType)
                        {
                            case ETransformValueType.Position:
                                var newPos = Handles.PositionHandle(currentPos, Quaternion.identity);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(paths, "Move Point");
                                    point.position = paths.ToLocalPosition(newPos);
                                    shouldRefresh = true;
                                }
                                break;

                            case ETransformValueType.Rotation:
                                var newRot = Handles.RotationHandle(currentRot, currentPos);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(paths, "Rotate Point");
                                    point.euler = paths.ToLocalRotation(newRot).eulerAngles;
                                    shouldRefresh = true;
                                }
                                break;

                            case ETransformValueType.Scale:
                            default:
                                var worldScale = paths.ToWorldScale(point.scale);
                                var newScale = Handles.ScaleHandle(worldScale, currentPos, Quaternion.identity, 0.5f);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    Undo.RecordObject(paths, "Scale Point");
                                    point.scale = paths.ToLocalScale(newScale);
                                    shouldRefresh = true;
                                }
                                break;
                        }

                        if (shouldRefresh)
                        {
                            if (Selection.activeObject != paths)
                                Selection.activeObject = paths;

                            Set(paths, i, operationType);
                            if (paths.sampleTransfrom != null)
                            {
                                var worldTv = paths.ToWorld(paths.points[i]);
                                paths.sampleTransfrom.SetWorld(worldTv);
                            }

                            paths.points[i] = point;
                            paths.SetDirty();
                        }

                        // 按ESC或切换选择时退出编辑模式
                        if (UnityEngine.Event.current.type == EventType.KeyDown && UnityEngine.Event.current.keyCode == KeyCode.Escape)
                        {
                            pointIndex = -1;
                            UnityEngine.Event.current.Use();
                        }
                    }

                    // 处理右键菜单
                    if (UnityEngine.Event.current.type == EventType.MouseDown && UnityEngine.Event.current.button == 1)
                    {
                        var mousePosition = UnityEngine.Event.current.mousePosition;
                        var guiPoint = HandleUtility.WorldToGUIPoint(currentPos);
                        if (Vector2.Distance(mousePosition, guiPoint) < 10f)
                        {
                            int index = i;
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Select Move"), false, () => pointIndex = index);
                            menu.AddItem(new GUIContent("Insert Point After"), false, () => {
                                Undo.RecordObject(paths, "Insert Point");
                                if (index < paths.points.Count - 1)
                                {
                                    var nextIndex = index + 1;
                            var nextWorld = paths.ToWorldPosition(paths.points[nextIndex]);
                            var newWorldPos = (currentPos + nextWorld) * 0.5f;
                            var newWorldRot = Quaternion.Lerp(currentRot, paths.ToWorldRotation(paths.points[nextIndex]), 0.5f);
                            var newPoint = new TransformValues
                            {
                                position = paths.ToLocalPosition(newWorldPos),
                                euler = paths.ToLocalRotation(newWorldRot).eulerAngles,
                                scale = Vector3.one,
                            };
                            paths.points.Insert(index + 1, newPoint);
                        }
                        else
                        {
                            var prePoint = paths.points[index - 1];
                            var preWorld = paths.ToWorldPosition(prePoint);
                            paths.points[index] = point;
                            var newWorldPos = currentPos + (currentPos - preWorld).normalized;
                            var newPoint = new TransformValues
                            {
                                position = paths.ToLocalPosition(newWorldPos),
                                euler = prePoint.euler,
                                scale = Vector3.one,
                            };
                            paths.points.Add(newPoint);
                        }
                        paths.SetDirty();
                    });
                            menu.AddItem(new GUIContent("Delete Point"), false, () => {
                                if (paths.points.Count > 2)
                                {
                                    Undo.RecordObject(paths, "Delete Point");
                                    paths.points.RemoveAt(index);
                                    paths.SetDirty();
                                }
                            });
                            menu.ShowAsContext();
                            UnityEngine.Event.current.Use();
                        }
                    }
                }
            }
        }
#endif
    }
}
