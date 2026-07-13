/*
┌────────────────────────────┐
│　Description：地图加载
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：MapLoader
└──────────────┘
*/
using UnityEngine;
using Lin.Runtime.Helper;

namespace Lin.Runtime.Map
{
    class OctreeNode : TreeNodeBase<OctreeNode>
    {
        public OctreeNode(Bounds bounds) : base(bounds) { }

        public OctreeNode(Bounds bounds, int depth) : base(bounds, depth) { }

        protected override OctreeNode CreateChild(int index, int targetDepth) => new OctreeNode(bounds.GetChildByOctree(index), targetDepth);

        protected override OctreeNode[] CreateChildren() => new OctreeNode[8];
    }
}
