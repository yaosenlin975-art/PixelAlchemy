/*
┌────────────────────────────┐
│　Description：多叉树基类
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：TreeNodeBase
└──────────────┘
*/

using Lin.Runtime.Helper;
using System;
using System.Text;
using UnityEngine;

namespace Lin.Runtime.DesignPattern.TreeNode
{
    public abstract class TreeNodeBase<T> : IDisposable where T : TreeNodeBase<T>
    {
        /// <summary> 节点大小、位置 </summary>
        public Bounds bounds { get; }

        /// <summary> 节点深度 </summary>
        public int depth { get; }

        protected T parent;
        protected int index = 0;
        protected T[] children;
        public int childrenCount => children?.Length ?? 0;

        protected TreeNodeBase(Bounds bounds, int depth)
        {
            this.bounds = bounds;
            this.depth = depth;
        }

        protected TreeNodeBase(Bounds bounds, int depth, int childrenCount, int targetDepth) : this(bounds, depth)
        {
            if (depth < targetDepth)
                CreateChildren(targetDepth, childrenCount);
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= children.Length)
                    throw new IndexOutOfRangeException();

                var child = children[index];
                if (child is null)
                {
                    child = CreateChild(index);
                    children[index] = child;
                }
                return child;
            }
        }

        protected void CreateChildren(int targetDepth, int childrenCount)
        {
            children = new T[childrenCount];
            for (int i = 0; i < childrenCount; i++)
                children[i] = CreateChild(i, targetDepth);
        }

        protected T CreateChild(int index) => CreateChild(index, depth + 1);

        protected abstract T CreateChild(int index, int targetDepth);

        protected void SetParent(T parent, int index)
        {
            this.parent = parent;
            this.index = index;
        }

        public bool Contains(Bounds other) => bounds.Contains(other);

        public bool Intersects(Bounds other) => bounds.Intersects(other);

        public override string ToString()
        {
            if (parent is null)
                return "0";

            return $"{parent}-{index}";
        }

        public virtual void OnGizemos() { }

        public void Dispose()
        {
            if (children is not null)
                foreach (var child in children)
                    child?.Dispose();

            Disposing();
            GC.SuppressFinalize(this);
        }

        protected abstract void Disposing();
    }
}