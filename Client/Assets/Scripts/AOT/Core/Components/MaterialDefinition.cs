// 职责：材料定义数据，用于静态查询材质属性（熔点、沸点等）。
// Responsibility: Static material definition data for querying material properties (melting point, boiling point, etc.).
using Unity.Entities;

namespace AOT
{
    public struct MaterialDefinition : IComponentData
    {
        public byte MaterialType;
        public byte Density;
        public short MeltingPoint;      // 定点数 ×100
        public short BoilingPoint;      // 定点数 ×100
        public short IgnitionPoint;     // 定点数 ×100
        public ushort DefaultColor;     // RGB565
        public byte Flags;              // Bit0=粉末, Bit1=液体, Bit2=气体, Bit3=固体
    }
}
