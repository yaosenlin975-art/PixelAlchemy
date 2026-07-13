/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：OctreeNode
└──────────────┘
*/
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.Scene.Spliter.Tree
{
    interface IMapSpliter
    {
        void Split(MeshRenderer[] renderers, string[] ignoreTags)
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

                if (!root.activeInHierarchy)
                    toDestories.Add(root);
                else
                    Check(target.gameObject, boundsMap);

                YooAsset.Editor.EditorTools.DisplayProgressBar("收集场景物体", i + 1, renderers.Length);
            }
            YooAsset.Editor.EditorTools.ClearProgressBar();
        }

        EBoundsState Check(GameObject target, Dictionary<GameObject, Bounds> boundsMap);
    }
}
