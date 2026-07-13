/*
┌────────────────────────────┐
│　Description: 无限列表接口
│　Remark: 
└────────────────────────────┘
*/
namespace Lin.Runtime.Interface
{
    /// <summary> 支持放入无限列表的数据 </summary>
    public interface IScrollableData { }

    /// <summary> 可变高度数据: 实现此接口以告知 InfiniteScroller 预估高度 </summary>
    public interface IVariableHeightData : IScrollableData
    {
        /// <summary> 预估高度（像素），用于首次布局；返回 0 表示使用 InfiniteScroller.elementSize.y </summary>
        float EstimatedHeight { get; }
    }

    /// <summary> 可变宽度数据: 实现此接口以告知 InfiniteScroller 预估宽度 </summary>
    public interface IVariableWidthData : IScrollableData
    {
        /// <summary> 预估宽度（像素），用于首次布局；返回 0 表示使用 InfiniteScroller.elementSize.x </summary>
        float EstimatedWidth { get; }
    }

    /// <summary> 支持用无限列表刷新的预制体 </summary>
    public interface IScrollableElement
    {
        /// <summary>
        /// 要实现data为空时的显示
        /// </summary>
        /// <param name="data"></param>
        void Refresh(IScrollableData data, int dataIndex);
    }
}