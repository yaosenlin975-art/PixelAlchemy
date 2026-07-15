// 职责：二维定点数向量，用于帧同步下的确定性位置/速度计算。
// Responsibility: 2D fixed-point vector for deterministic position/velocity calculations under lockstep.
using System.Runtime.CompilerServices;
using Unity.Burst;

namespace AOT
{
    [BurstCompile]
    public struct Fix64Vec2
    {
        public Fix64 X;
        public Fix64 Y;

        public Fix64Vec2(Fix64 x, Fix64 y)
        {
            X = x;
            Y = y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64Vec2 FromFloat(float x, float y)
        {
            return new Fix64Vec2(Fix64.FromFloat(x), Fix64.FromFloat(y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64Vec2 operator +(Fix64Vec2 a, Fix64Vec2 b)
        {
            return new Fix64Vec2(a.X + b.X, a.Y + b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64Vec2 operator -(Fix64Vec2 a, Fix64Vec2 b)
        {
            return new Fix64Vec2(a.X - b.X, a.Y - b.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fix64Vec2 operator *(Fix64Vec2 a, Fix64 s)
        {
            return new Fix64Vec2(a.X * s, a.Y * s);
        }
    }
}
