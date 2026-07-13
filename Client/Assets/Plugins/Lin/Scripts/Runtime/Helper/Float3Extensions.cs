using Unity.Mathematics;

namespace Lin.Runtime.Helper
{
    public static class Float3Extensions
    {
        public static void Normalize(this ref float3 self) => self = math.normalize(self);

        public static float Magnitude(this float3 self) => math.sqrt(self.SqrMagnitude());

        public static float SqrMagnitude(this float3 self) => self.x * self.x + self.y * self.y + self.z * self.z;

        public static float Distance(this float3 self, float3 other) => math.distance(self, other);

        /// <summary>
        /// 两个点之间的距离是否大于目标距离
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="target">目标距离</param>
        /// <returns>True: 大于目标距离</returns>
        public static bool DistanceFasterThan(float3 a, float3 b, float target)
        {
            var sqr = (a - b).SqrMagnitude();
            return sqr > target * target;
        }

        /// <summary>
        /// 两个点之间的距离是否小于目标距离
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="target">目标距离</param>
        /// <returns>True: 小于目标距离</returns>
        public static bool DistanceCloserThan(float3 a, float3 b, float target)
        {
            var sqr = (a - b).SqrMagnitude();
            return sqr < target * target;
        }

        /// <summary>
        /// 两个点之间的距离是否等于目标距离
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="target">目标距离</param>
        /// <returns>True: 等于目标距离</returns>
        public static bool DistanceEqualsTo(float3 a, float3 b, float target)
        {
            var sqr = (a - b).SqrMagnitude();
            return sqr == target * target;
        }
    }
}
