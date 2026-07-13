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
    class QuadtreeNode : TreeNodeBase<QuadtreeNode>
    {
        public QuadtreeNode(Bounds bounds) : base(bounds) { }

        protected QuadtreeNode(Bounds bounds, int depth): base(bounds, depth) { }

        public override void OnGizemos()
        {
            if (viewable)
            {
                if (children is not null)
                    foreach (var kid in children)
                        kid?.OnDrawGizmos();
                else
                {
                    Gizmos.color = Color.green;
                    var size = bounds.size;
                    size.y = 5;

                    var center = bounds.center;
                    center.y = 0;
                    Gizmos.DrawWireCube(center, size);
                }
            }
        }

        protected override QuadtreeNode CreateChild(int index, int targetDepth) => new QuadtreeNode(bounds.GetChildByQuadtree(index), targetDepth);

        protected override QuadtreeNode[] CreateChildren() => new QuadtreeNode[4];
    }
}
