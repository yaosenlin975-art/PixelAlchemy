/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using UnityEngine;

namespace Lin.Editor.Interface
{
    public interface IHierarchyDrawable
    {
        int drawPriority { get; }

        /// <param name="instanceId">SceneObject Id</param>
        /// <param name="drawablePoint">最右侧可以绘制的位置</param>
        /// <returns>使用掉的宽度</returns>
        float DrawInHierarchy(int instanceId, Rect drawablePoint);
    }
}
