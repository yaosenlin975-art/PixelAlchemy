/*
┌────────────────────────────┐
│　Description：四叉树
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：QuadtreeNode
└──────────────┘
*/

using UnityEngine;

namespace Lin.Runtime.DesignPattern.TreeNode
{
    //包围盒四叉树
    public abstract class QuadtreeNode<T> : TreeNodeBase<T> where T : QuadtreeNode<T>
    {
        protected QuadtreeNode(Bounds bounds, int depth, int targetDepth) : base(bounds, depth, 4, targetDepth) { }
    }
}