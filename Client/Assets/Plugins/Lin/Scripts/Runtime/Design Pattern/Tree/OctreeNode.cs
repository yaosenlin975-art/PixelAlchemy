/*
┌────────────────────────────┐
│　Description：包围盒八叉树
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：OctreeNode
└──────────────┘
*/

using UnityEngine;

namespace Lin.Runtime.DesignPattern.TreeNode
{
    public abstract class OctreeNode<T> : TreeNodeBase<T> where T : OctreeNode<T>
    {
        protected OctreeNode(Bounds bounds, int depth, int targetDepth) : base(bounds, depth, 8, targetDepth){ }
    }
}