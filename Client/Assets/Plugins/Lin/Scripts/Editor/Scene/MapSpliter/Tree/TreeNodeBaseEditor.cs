/*
┌────────────────────────────┐
│　Description：树节点基类, 处理分割 预制体保存 配置文件编写
│　Remark：
└────────────────────────────┘
*/
using Cysharp.Text;
using Lin.Runtime.DesignPattern.TreeNode;
using Lin.Runtime.Helper;
using Sirenix.Utilities.Editor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace Lin.Editor.Scene.Spliter.Tree
{
    abstract class TreeNodeBaseEditor : TreeNodeBase<TreeNodeBaseEditor>
    {
        protected readonly Dictionary<GameObject, EBoundsState> objects;

        protected TreeNodeBaseEditor(Bounds bounds, int depth, int childrenCount, int targetDepth) : base(bounds, depth, childrenCount, targetDepth)
        {
            if (depth == targetDepth)
                objects = new Dictionary<GameObject, EBoundsState>();
        }

        public void Split(MeshRenderer[] renderers, string[] ignoreTags)
        {
            //四叉树分割
            var boundsMap = new Dictionary<GameObject, Bounds>();
            var checkedObjects = new HashSet<GameObject>();
            var toDestories = new HashSet<GameObject>();

            for (int i = 0; i < renderers.Length; i++)
            {
                var target = renderers[i];
                //获取根部物体
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(target.gameObject) ?? target.gameObject;
                if (ignoreTags?.Contains(root.tag) ?? false)
                    continue;

                if (checkedObjects.Contains(root))
                    continue;
                checkedObjects.Add(root);

                if (!root.activeSelf)
                    toDestories.Add(root);
                else
                    Check(root, boundsMap);

                YooAsset.Editor.EditorTools.DisplayProgressBar("收集场景物体", i + 1, renderers.Length);
            }
            YooAsset.Editor.EditorTools.ClearProgressBar();
        }

        private EBoundsState Check(GameObject target, Dictionary<GameObject, Bounds> boundsMap)
        {
            if (!boundsMap.ContainsKey(target))
                boundsMap.Add(target, target.CalculateObjectBounds());

            Bounds bounds = boundsMap[target];
            if (!Intersects(bounds))
                return Return(EBoundsState.NONE);

            for (int i = 0; i < childrenCount; i++)
            {
                var state = this[i].Check(target, boundsMap);
                if (state == EBoundsState.CONTAINS)
                    return Return(EBoundsState.CONTAINS);
            }

            if (!Contains(bounds))
                return Return(EBoundsState.INTERSECTS);

            return Return(EBoundsState.CONTAINS);

            EBoundsState Return(EBoundsState state)
            {
                objects?.Add(target, state);
                return state;
            }
        }

        public void WriteConfig(string outputDir, Dictionary<GameObject, string> pathMap, HashSet<string> paths, bool copyIntersectedObjects)
        {
            if (children is not null)
            {
                for (int i = 0; i < childrenCount; i++)
                    children[i].WriteConfig(outputDir, pathMap, paths, copyIntersectedObjects);
                return;
            }

            using var configBuilder = ZString.CreateStringBuilder();

            bool hasContainsObjects = false;
            bool hasObjects = false;
            GameObject inChunkObjects = new GameObject(ToString());
            inChunkObjects.transform.position = bounds.center;
            foreach (var pair in objects)
            {
                if (pair.Value == EBoundsState.NONE)
                    continue;

                var target = pair.Key;

                switch (pair.Value)
                {
                    case EBoundsState.NONE:
                        continue;

                    case EBoundsState.CONTAINS:
                        hasContainsObjects = true;
                        target.transform.SetParent(inChunkObjects.transform, true);
                        break;

                    case EBoundsState.INTERSECTS:
                    default:
                        hasObjects = true;
                        SavePrefabAndRecordPath(pair.Key);
                        break;
                }
            }

            //包含物体
            if (!hasContainsObjects)
                Object.DestroyImmediate(inChunkObjects);
            else
                SavePrefabAndRecordPath(inChunkObjects);

            //写入配置文件
            if (hasContainsObjects || hasObjects && configBuilder.Length != 0)
                File.WriteAllText($"{outputDir}/{ToString()}.txt", configBuilder.ToString());

            //保存预制体，记录地址
            void SavePrefabAndRecordPath(GameObject target)
            {
                if (pathMap.TryGetValue(target, out var path))
                {
                    paths.Add(path);
                    return;
                }

                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(target);
                bool isSameWithPrefab = prefab is not null && GameObjectExtensions.CompareObjects(target, prefab);
                if (copyIntersectedObjects)
                    pathMap.Add(target, Save2Prefab(isSameWithPrefab ? prefab : target, outputDir));
                else
                {
                    if (isSameWithPrefab)
                        pathMap.Add(target, AssetDatabase.GetAssetPath(prefab));
                    else
                        pathMap.Add(target, Save2Prefab(target, outputDir));
                }
                paths.Add(pathMap[target]);

                //写入信息
                if (configBuilder.Length > 0)
                    configBuilder.AppendLine();
                configBuilder.Append(Translate(target));
            }

            //物体信息转换成字符串
            //PathIndex,posX,posY,posZ,eulerX,eulerY,eulerZ,scaleX,scaleY,scaleZ,boundsCenterX,boundsCenterY,boundsCenterZ,boundsSizeX,boundsSizeY,boundsSizeZ
            string Translate(GameObject target)
            {
                string path = pathMap[target];
                int index = paths.IndexOf(path);
                var trans = target.transform;
                var pos = trans.position;
                var euler = trans.eulerAngles;
                var scale = trans.lossyScale;
                var bounds = target.CalculateObjectBounds();
                var center = bounds.center;
                var size = bounds.size;
                return $"{index},{pos.x},{pos.y},{pos.z},{euler.x},{euler.y},{euler.z},{scale.x},{scale.y},{scale.z},{center.x},{center.y},{center.z},{size.x},{size.y},{size.z}";
            }
        }

        private static string Save2Prefab(GameObject target, string outputDir)
        {
            var container = GetUsablePrefabPath(target, outputDir);
            string path = container.path.Replace(":", string.Empty);
            if (container.shouldSave)
                PrefabUtility.SaveAsPrefabAsset(target, path);

            return path.Replace(outputDir, "..");

            //查找已存在的预制体并进行对比，相同则返回预制体地址，不同则shouldSave为true并返回可用地址
            (bool shouldSave, string path) GetUsablePrefabPath(GameObject toSave, string outputDir)
            {
                string result = $"{outputDir}/{toSave.name}.prefab";
                int index = 0;
                while (File.Exists(result))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result);
                    if (GameObjectExtensions.CompareObjects(toSave, prefab))
                        return (false, result);

                    result = $"{outputDir}/{toSave.name}.{index++}.prefab";
                }

                return (true, result);
            }
        }

        protected override void Disposing() { }
    }
}
