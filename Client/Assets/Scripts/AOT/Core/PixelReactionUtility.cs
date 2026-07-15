// 职责：像素反应工具方法，处理材料间的化学反应判定。
// Responsibility: Utility methods for determining chemical reactions between materials.
namespace AOT
{
    public static class PixelReactionUtility
    {
        public static bool IsGas(byte materialType)
        {
            return materialType == (byte)MaterialType.Smoke
                || materialType == (byte)MaterialType.Fire;
        }

        public static bool IsLiquid(byte materialType)
        {
            return materialType == (byte)MaterialType.Water
                || materialType == (byte)MaterialType.Poison
                || materialType == (byte)MaterialType.Lava;
        }

        public static bool IsPowder(byte materialType)
        {
            return materialType == (byte)MaterialType.Sand
                || materialType == (byte)MaterialType.Ash
                || materialType == (byte)MaterialType.Debris;
        }

        public static bool IsSolid(byte materialType)
        {
            return materialType == (byte)MaterialType.Stone
                || materialType == (byte)MaterialType.Wood
                || materialType == (byte)MaterialType.Ice;
        }

        public static bool IsFlammable(byte materialType)
        {
            return materialType == (byte)MaterialType.Wood
                || materialType == (byte)MaterialType.Sand
                || materialType == (byte)MaterialType.Debris;
        }

        public static bool IsAir(byte materialType)
        {
            return materialType == (byte)MaterialType.Air
                || materialType == (byte)MaterialType.Empty;
        }

        public static MaterialType GetReactionProduct(MaterialType a, MaterialType b)
        {
            if ((a == MaterialType.Fire || b == MaterialType.Fire)
                && (a == MaterialType.Water || b == MaterialType.Water))
            {
                return MaterialType.Smoke;
            }

            if ((a == MaterialType.Lava || b == MaterialType.Lava)
                && (a == MaterialType.Water || b == MaterialType.Water))
            {
                return MaterialType.Stone;
            }

            return MaterialType.Air;
        }
    }
}
