/*
┌────────────────────────────┐
│　Description：四叉树分割
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：QuadtreeNode
└──────────────┘
*/
using Lin.Runtime.Helper;
using UnityEngine;

namespace Lin.Editor.Scene.Spliter.Tree
{
    class QuadtreeNode : TreeNodeBaseEditor
    {
        public QuadtreeNode(Bounds bounds, int depth, int targetDepth) : base(bounds, depth, 4, targetDepth) { }

        protected override TreeNodeBaseEditor CreateChild(int index, int targetDepth)
        {
            var childBounds = bounds.GetChildByQuadtree(index);
            var child = new QuadtreeNode(childBounds, depth + 1, targetDepth);
            child.SetParent(this, index);
            return child;
        }

        public static Bounds CalculateRootBounds(Bounds mapBounds)
        {
            var rootBounds = new Bounds();
            var size = mapBounds.size;
            float maxSize = Mathf.Max(size.x, size.y, size.z);
            rootBounds.size = new Vector3(maxSize, mapBounds.size.y + 10, maxSize);
            rootBounds.center = mapBounds.center;
            return rootBounds;
        }
    }
}
