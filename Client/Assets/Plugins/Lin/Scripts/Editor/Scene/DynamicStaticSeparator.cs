/*
┌────────────────────────────┐
│　Description: 场景物体动静分离
│　Remark: 
└────────────────────────────┘
*/
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZLinq;

namespace Lin.Editor.Scene
{
    public static class DynamicStaticSeparator
    {
        // 用法: 菜单 Lin/Scene/动态静态分离
        // 根据场景中的对象, 将具有 Renderer 或 LODGroup 的对象分为 动态 与 静态 两类
        // 规则见下:
        // 1. 只对带有Renderer的物体进行检测
        // 2. 带有Rigidbody CharacterController Animation Animator ParticleSystem 以及自定义脚本的物体都视为动态物体
        // 3. 若父体也带有Renderer, 则保持原有父子关系, 以最上层带Renderer的父体为分组根
        // 4. 若物体为预制体的一部分, 则保持原有父子关系, 以最上层预制体分分组根
        // 5. 若为预制体实例, 任意子物体带有Renderer, 则整个预制体视为动态物体
        // 6. 将动态物体都放置在 物体 "Dynamic" 上
        // 7. 将静态物体都放置在 物体 "Static" 上, 将 StaticFlags 全部勾选
        // 8. 移动物体时要留ActiveSelf

        [MenuItem("Lin/Scene/动态静态分离")]
        private static void Separate()
        {
            var activeScene = SceneManager.GetActiveScene();
            if(!activeScene.IsValid())
            {
                Debug.LogError("当前没有有效场景");
                return;
            }

            // 收集场景中所有 Renderer/LODGroup 的对象作为候选
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            var lodGroups = UnityEngine.Object.FindObjectsOfType<LODGroup>(true);
            if((renderers == null || renderers.Length == 0) && (lodGroups == null || lodGroups.Length == 0))
            {
                Debug.Log("场景中没有带 Renderer 或 LODGroup 的对象");
                return;
            }

            var groupRoots = new HashSet<GameObject>();

            // 以分组根策略确定根节点:
            // - 若对象属于预制体实例, 使用最外层预制体实例根
            // - 否则, 使用最上层带 Renderer 的父体
            foreach(var r in renderers.AsValueEnumerable())
            {
                var go = r.gameObject;
                var root = GetGroupRootFor(go);
                groupRoots.Add(root);
            }

            // LODGroup 同样遵循分组根策略
            foreach(var lg in lodGroups.AsValueEnumerable())
            {
                var root = GetGroupRootFor(lg.gameObject);
                groupRoots.Add(root);
            }

            // 目标父物体: Dynamic / Static
            var dynamicRoot = FindOrCreateRoot(activeScene, "Dynamic");
            var staticRoot  = FindOrCreateRoot(activeScene, "Static");

            var dynamicGroups = new List<GameObject>();
            var staticGroups  = new List<GameObject>();

            foreach(var root in groupRoots.AsValueEnumerable())
            {
                if(IsPrefabInstanceRoot(root) && PrefabHasAnyRenderer(root))
                {
                    // 预制体: 只要任意子物体带 Renderer, 整个预制体归为动态
                    dynamicGroups.Add(root);
                    continue;
                }

                if(IsDynamicByComponents(root))
                    dynamicGroups.Add(root);
                else
                    staticGroups.Add(root);
            }

            // 进行重定位
            foreach(var g in dynamicGroups.AsValueEnumerable())
            {
                var wasActive = g.activeSelf; // 留ActiveSelf
                g.transform.SetParent(dynamicRoot.transform, true);
                g.SetActive(wasActive);
            }

            var allFlags = GetAllStaticEditorFlags();
            foreach(var g in staticGroups.AsValueEnumerable())
            {
                var wasActive = g.activeSelf; // 留ActiveSelf
                g.transform.SetParent(staticRoot.transform, true);
                g.SetActive(wasActive);
                ApplyStaticFlagsRecursive(g, allFlags);
            }

            // 标记场景已改变以支持撤销与保存
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"分离完成: 动态 {dynamicGroups.Count}, 静态 {staticGroups.Count}");
        }

        // 获取带有 Renderer 的最顶层父物体作为分组根
        private static GameObject GetTopMostRendererAncestor(GameObject go)
        {
            var current = go;
            var parent = current.transform.parent;
            while(parent != null)
            {
                if(parent.GetComponent<Renderer>() != null)
                {
                    current = parent.gameObject;
                    parent = parent.parent;
                }
                else
                    break;
            }
            return current;
        }

        // 获取分组根: 若属于预制体实例则取最外层预制体根, 否则取最上层带 Renderer 的父体
        private static GameObject GetGroupRootFor(GameObject go)
        {
            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if(status != PrefabInstanceStatus.NotAPrefab)
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                if(root != null) return root;
            }

            return GetTopMostRendererAncestor(go);
        }

        // 判断是否为预制体实例根
        private static bool IsPrefabInstanceRoot(GameObject go)
        {
            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if(status == PrefabInstanceStatus.NotAPrefab)
                return false;
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            return root == go;
        }

        // 预制体是否存在任意 Renderer 子物体
        private static bool PrefabHasAnyRenderer(GameObject prefabRoot)
        {
            var renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
            return renderers != null && renderers.Length > 0;
        }

        // 组件规则判定: 带有下列组件或自定义脚本则视为动态
        private static bool IsDynamicByComponents(GameObject go)
        {
            if(go.GetComponent<Rigidbody>() != null) 
                return true;
            if(go.GetComponent<CharacterController>() != null)
                return true;
            if(go.GetComponent<UnityEngine.Animation>() != null) 
                return true;
            if(go.GetComponent<Animator>() != null)
                return true;
            if(go.GetComponent<ParticleSystem>() != null)
                return true;

            // 自定义脚本: 非 Unity.* 或 UnityEngine.* 程序集的 MonoBehaviour
            var behaviours = go.GetComponents<MonoBehaviour>();
            if(behaviours != null && behaviours.Length > 0)
            {
                foreach(var b in behaviours.AsValueEnumerable())
                {
                    if(b == null) continue;
                    var asm = b.GetType().Assembly.GetName().Name;
                    if(!asm.StartsWith("Unity") && !asm.StartsWith("UnityEngine"))
                    {
                        Debug.Log(b.GetType().FullName);
                        return true;
                    }
                }
            }

            return false;
        }

        // 找到或在当前场景创建指定名称的根物体
        private static GameObject FindOrCreateRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            foreach(var r in roots.AsValueEnumerable())
            {
                if(r.name == name) return r;
            }

            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        // 获取所有静态标记的组合
        private static StaticEditorFlags GetAllStaticEditorFlags()
        {
            var sum = (StaticEditorFlags)0;
            var values = Enum.GetValues(typeof(StaticEditorFlags));
            for(int i = 0; i < values.Length; i++)
            {
                sum |= (StaticEditorFlags)values.GetValue(i);
            }
            return sum;
        }

        // 递归为整个层级设置静态标记
        private static void ApplyStaticFlagsRecursive(GameObject root, StaticEditorFlags flags)
        {
            GameObjectUtility.SetStaticEditorFlags(root, flags);
            var t = root.transform;
            for(int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i).gameObject;
                ApplyStaticFlagsRecursive(child, flags);
            }
        }
    }
}
