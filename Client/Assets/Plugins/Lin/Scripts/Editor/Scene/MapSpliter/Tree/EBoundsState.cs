/*
┌────────────────────────────┐
│　Description：两个包围盒的关系
│　Remark：
└────────────────────────────┘
*/
namespace Lin.Editor.Scene.Spliter.Tree
{
    /// <summary>
    /// 两个包围盒的关系
    /// </summary>
    public enum EBoundsState
    {
        /// <summary> 无交集 </summary>
        NONE,
        /// <summary> 被包含 </summary>
        CONTAINS,
        /// <summary> 相交 </summary>
        INTERSECTS
    }
}
