// 职责：定义像素网格尺寸，作为 DOTS 单例组件。
// Responsibility: Defines the pixel grid dimensions as a DOTS singleton component.
using Unity.Entities;

namespace AOT
{
    public struct GridSize : IComponentData
    {
        public int Width;
        public int Height;
    }
}
