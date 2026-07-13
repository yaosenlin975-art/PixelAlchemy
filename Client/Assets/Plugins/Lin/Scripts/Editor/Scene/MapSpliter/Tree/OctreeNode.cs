/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：OctreeNode
└──────────────┘
*/
using Lin.Runtime.Helper;
using UnityEngine;

namespace Lin.Editor.Scene.Spliter.Tree
{
    class OctreeNode : TreeNodeBaseEditor
    {
        public OctreeNode(Bounds bounds, int depth, int targetDepth) : base(bounds, depth, 8, targetDepth) { }

        protected override TreeNodeBaseEditor CreateChild(int index, int targetDepth)
        {
            var childBounds = bounds.GetChildByOctree(index);
            var child = new OctreeNode(childBounds, depth + 1, targetDepth);
            child.SetParent(this, index);
            return child;
        }

        public static Bounds CalculateRootBounds(Bounds mapBounds)
        {
            Bounds rootBounds = new Bounds();
            var size = mapBounds.size;
            float maxSize = Mathf.Max(size.x, size.y, size.z) + 10;
            rootBounds.size = Vector3.one * maxSize;
            rootBounds.center = mapBounds.center;
            float yOffset = mapBounds.min.y - rootBounds.min.y;
            rootBounds.center += Vector3.up * yOffset;
            return rootBounds;
        }
    }
}
