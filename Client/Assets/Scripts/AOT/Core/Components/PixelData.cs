// 职责：DOTS IComponentData 像素数据，16 字节/像素，Burst 兼容 blittable 结构。
// Responsibility: DOTS IComponentData pixel data, 16 bytes per pixel, Burst-compatible blittable struct.
using Unity.Entities;
using Unity.Mathematics;

namespace AOT
{
    public struct PixelData : IComponentData
    {
        public byte MaterialType;
        public short Temperature;       // 定点数 ×100, [-327.68, 327.67]°C
        public short Lifetime;          // 定点数 ×100
        public byte Density;
        public byte FallingFrames;
        public ushort Color;            // RGB565 打包
        public byte Flags;              // Bit0=生物, Bit1=法术, Bit2=存活, Bit3=已更新
        public short VelocityX;         // 定点数 ×100
        public short VelocityY;         // 定点数 ×100
    }
}
