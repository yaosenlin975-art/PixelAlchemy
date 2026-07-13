/*
┌────────────────────────────┐
│　Description: 物理检测拓展
│　Remark: 
└────────────────────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.Helper
{
    public static class PhysicsHelper
    {
        /// <summary>
        /// 物理检测辅助：统一封装 2D/3D 的 NonAlloc 检测。
        /// 数组池为静态复用，避免 GC；如果要跨帧或异步使用结果，请自行拷贝数组。
        /// </summary>
        #region - 2D -

        // 因为物理操作都是在主线程上执行, 有实际的先后顺序, 所以可以直接用List来做对象池 (但得到的数组不能用于异步操作, 因为数组内部可能会发生变化)
        private static RaycastHit2D[] raycast2dNonAlloc = new RaycastHit2D[5];
        private static Collider2D[] overlap2dNonAlloc = new Collider2D[5];

        /// <summary>
        /// 2d物理检测
        /// </summary>
        /// <param name="ori">检测起点</param>
        /// <param name="direction">检测方向</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <returns>检测获得的信息</returns>
        public static RaycastHit2D Raycast(Vector2 ori, Vector2 direction, float distance, int layerMask) => RayCast(ori, direction, distance, layerMask, Color.green, Color.red);

        // 不带 layerMask 的 2D 射线检测（使用默认层）
        public static RaycastHit2D Raycast(Vector2 ori, Vector2 direction, float distance) => RayCast(ori, direction, distance, Physics2D.DefaultRaycastLayers, Color.green, Color.red);

        /// <summary>
        /// 2D 物理检测（Ray2D 便捷重载）
        /// </summary>
        /// <param name="ray">包含起点与方向的 Ray2D</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <returns>检测获得的信息</returns>
        public static RaycastHit2D Raycast(Ray2D ray, float distance, int layerMask) => RayCast(ray.origin, ray.direction, distance, layerMask, Color.green, Color.red);
        
        // 不带 layerMask 的 2D Ray2D 检测（使用默认层）
        public static RaycastHit2D Raycast(Ray2D ray, float distance) => RayCast(ray.origin, ray.direction, distance, Physics2D.DefaultRaycastLayers, Color.green, Color.red);

        /// <summary>
        /// 2d物理检测
        /// </summary>
        /// <param name="ori">检测起点</param>
        /// <param name="direction">检测方向</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="hitted">检测中物体时的颜色</param>
        /// <param name="empty">未检测中物体时的颜色</param>
        /// <returns>检测获得的信息</returns>
        public static RaycastHit2D RayCast(Vector2 ori, Vector2 direction, float distance, int layerMask, Color hitted, Color empty)
        {
            var result = Physics2D.Raycast(ori, direction, distance, layerMask);
            Debug.DrawLine(ori, result ? result.point : (ori + direction.normalized * distance), result ? hitted : empty);
            return result;
        }

        /// <summary>
        /// 2d物理检测 results若要用在异步操作上, 请自行拷贝一份
        /// </summary>
        /// <param name="ori">检测起点</param>
        /// <param name="direction">检测方向</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="results">被检测到的物体</param>
        /// <param name="maxCastCount">最大检测物体数量</param>
        /// <returns>实际检测到的物体数量</returns>
        public static int RaycastNonAlloc(Vector2 ori, Vector2 direction, float distance, int layerMask, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            if (raycast2dNonAlloc.Length < maxCastCount)
            {
                raycast2dNonAlloc = new RaycastHit2D[maxCastCount];
            }
            int count = Physics2D.RaycastNonAlloc(ori, direction, raycast2dNonAlloc, distance, layerMask);
            results = raycast2dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 2D 射线 NonAlloc（使用默认层）
        public static int RaycastNonAlloc(Vector2 ori, Vector2 direction, float distance, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            return RaycastNonAlloc(ori, direction, distance, Physics2D.DefaultRaycastLayers, out results, maxCastCount);
        }

        /// <summary>
        /// 2D 物理检测（Ray2D，NonAlloc）
        /// </summary>
        /// <param name="ray">包含起点与方向的 Ray2D</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="results">被检测到的物体</param>
        /// <param name="maxCastCount">最大检测物体数量</param>
        /// <returns>实际检测到的物体数量</returns>
        public static int RaycastNonAlloc(Ray2D ray, float distance, int layerMask, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            if (raycast2dNonAlloc.Length < maxCastCount)
            {
                raycast2dNonAlloc = new RaycastHit2D[maxCastCount];
            }
            int count = Physics2D.RaycastNonAlloc(ray.origin, ray.direction, raycast2dNonAlloc, distance, layerMask);
            results = raycast2dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 2D Ray2D NonAlloc（使用默认层）
        public static int RaycastNonAlloc(Ray2D ray, float distance, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            return RaycastNonAlloc(ray.origin, ray.direction, distance, Physics2D.DefaultRaycastLayers, out results, maxCastCount);
        }

        public static int OverlapCircleNonAlloc(Vector2 ori, float radius, int layerMask, out Collider2D[] results, int maxCount = 5)
        {
            if (overlap2dNonAlloc.Length < maxCount)
                overlap2dNonAlloc = new Collider2D[maxCount];

            int count = Physics2D.OverlapCircleNonAlloc(ori, radius, overlap2dNonAlloc, layerMask);
            results = overlap2dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 2D 圆形重叠 NonAlloc（使用默认层）
        public static int OverlapCircleNonAlloc(Vector2 ori, float radius, out Collider2D[] results, int maxCount = 5)
        {
            return OverlapCircleNonAlloc(ori, radius, Physics2D.DefaultRaycastLayers, out results, maxCount);
        }

        /// <summary>
        /// 2D 重叠盒 (NonAlloc)
        /// </summary>
        /// <param name="ori">盒中心</param>
        /// <param name="size">盒尺寸</param>
        /// <param name="angle">旋转角度</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">检测到的碰撞体</param>
        /// <param name="maxCount">最大返回数量</param>
        /// <returns>实际返回数量</returns>
        public static int OverlapBoxNonAlloc(Vector2 ori, Vector2 size, float angle, int layerMask, out Collider2D[] results, int maxCount = 5)
        {
            if (overlap2dNonAlloc.Length < maxCount)
                overlap2dNonAlloc = new Collider2D[maxCount];

            int count = Physics2D.OverlapBoxNonAlloc(ori, size, angle, overlap2dNonAlloc, layerMask);
            results = overlap2dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 2D 盒体重叠 NonAlloc（使用默认层）
        public static int OverlapBoxNonAlloc(Vector2 ori, Vector2 size, float angle, out Collider2D[] results, int maxCount = 5)
        {
            return OverlapBoxNonAlloc(ori, size, angle, Physics2D.DefaultRaycastLayers, out results, maxCount);
        }

        public static int OverlapAreaNonAlloc(Vector2 pointA, Vector2 pointB, int layerMask, out Collider2D[] results, int maxCount = 5)
        {
            if (overlap2dNonAlloc.Length < maxCount)
                overlap2dNonAlloc = new Collider2D[maxCount];

            int count = Physics2D.OverlapAreaNonAlloc(pointA, pointB, overlap2dNonAlloc, layerMask);
            results = overlap2dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 2D 区域重叠 NonAlloc（使用默认层）
        public static int OverlapAreaNonAlloc(Vector2 pointA, Vector2 pointB, out Collider2D[] results, int maxCount = 5)
        {
            return OverlapAreaNonAlloc(pointA, pointB, Physics2D.DefaultRaycastLayers, out results, maxCount);
        }

        /// <summary>
        /// 2D 盒体投射 (NonAlloc)
        /// </summary>
        /// <param name="origin">起点（盒体中心）</param>
        /// <param name="size">盒尺寸</param>
        /// <param name="angle">旋转角度</param>
        /// <param name="direction">投射方向</param>
        /// <param name="distance">最大距离</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">命中信息</param>
        /// <param name="maxCastCount">最大返回数量</param>
        /// <returns>实际命中数量</returns>
        public static int BoxCastNonAlloc(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            if (raycast2dNonAlloc.Length < maxCastCount)
                raycast2dNonAlloc = new RaycastHit2D[maxCastCount];

            int count = Physics2D.BoxCastNonAlloc(origin, size, angle, direction, raycast2dNonAlloc, distance, layerMask);
            results = raycast2dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 2D 盒体投射 NonAlloc（使用默认层）
        public static int BoxCastNonAlloc(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            return BoxCastNonAlloc(origin, size, angle, direction, distance, Physics2D.DefaultRaycastLayers, out results, maxCastCount);
        }

        /// <summary>
        /// 2D 圆形投射 (NonAlloc)
        /// </summary>
        /// <param name="origin">起点（圆心）</param>
        /// <param name="radius">半径</param>
        /// <param name="direction">投射方向</param>
        /// <param name="distance">最大距离</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">命中信息</param>
        /// <param name="maxCastCount">最大返回数量</param>
        /// <returns>实际命中数量</returns>
        public static int CircleCastNonAlloc(Vector2 origin, float radius, Vector2 direction, float distance, int layerMask, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            if (raycast2dNonAlloc.Length < maxCastCount)
                raycast2dNonAlloc = new RaycastHit2D[maxCastCount];

            int count = Physics2D.CircleCastNonAlloc(origin, radius, direction, raycast2dNonAlloc, distance, layerMask);
            results = raycast2dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 2D 圆形投射 NonAlloc（使用默认层）
        public static int CircleCastNonAlloc(Vector2 origin, float radius, Vector2 direction, float distance, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            return CircleCastNonAlloc(origin, radius, direction, distance, Physics2D.DefaultRaycastLayers, out results, maxCastCount);
        }

        /// <summary>
        /// 2D 胶囊体投射 (NonAlloc)
        /// </summary>
        /// <param name="origin">起点（胶囊中心）</param>
        /// <param name="size">胶囊尺寸</param>
        /// <param name="capsuleDirection">胶囊主轴方向</param>
        /// <param name="angle">旋转角度</param>
        /// <param name="direction">投射方向</param>
        /// <param name="distance">最大距离</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">命中信息</param>
        /// <param name="maxCastCount">最大返回数量</param>
        /// <returns>实际命中数量</returns>
        public static int CapsuleCastNonAlloc(Vector2 origin, Vector2 size, CapsuleDirection2D capsuleDirection, float angle, Vector2 direction, float distance, int layerMask, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            if (raycast2dNonAlloc.Length < maxCastCount)
                raycast2dNonAlloc = new RaycastHit2D[maxCastCount];

            int count = Physics2D.CapsuleCastNonAlloc(origin, size, capsuleDirection, angle, direction, raycast2dNonAlloc, distance, layerMask);
            results = raycast2dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 2D 胶囊体投射 NonAlloc（使用默认层）
        public static int CapsuleCastNonAlloc(Vector2 origin, Vector2 size, CapsuleDirection2D capsuleDirection, float angle, Vector2 direction, float distance, out RaycastHit2D[] results, int maxCastCount = 5)
        {
            return CapsuleCastNonAlloc(origin, size, capsuleDirection, angle, direction, distance, Physics2D.DefaultRaycastLayers, out results, maxCastCount);
        }

        #endregion

        #region - 3D -

        // 因为物理操作都是在主线程上执行, 有实际的先后顺序, 所以可以直接用List来做对象池 (但得到的数组不能用于异步操作, 因为数组内部可能会发生变化)
        private static RaycastHit[] raycast3dNonAlloc = new RaycastHit[5];
        private static Collider[] overlap3dNonAlloc = new Collider[5];

        /// <summary>
        /// 2d物理检测
        /// </summary>
        /// <param name="ori">检测起点</param>
        /// <param name="direction">检测方向</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <returns>检测获得的信息</returns>
        public static bool RayCast(Vector3 ori, Vector3 direction, float distance, int layerMask, out RaycastHit info) => RayCast(ori, direction, distance, layerMask, out info, Color.green, Color.red);

        // 不带 layerMask 的 3D 射线检测（使用默认层）
        public static bool RayCast(Vector3 ori, Vector3 direction, float distance, out RaycastHit info) => RayCast(ori, direction, distance, Physics.DefaultRaycastLayers, out info, Color.green, Color.red);

        /// <summary>
        /// 3D 物理检测（Ray 便捷重载）
        /// </summary>
        /// <param name="ray">包含起点与方向的 Ray</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="info">命中信息</param>
        /// <returns>是否命中</returns>
        public static bool RayCast(Ray ray, float distance, int layerMask, out RaycastHit info) => RayCast(ray.origin, ray.direction, distance, layerMask, out info, Color.green, Color.red);
        
        // 不带 layerMask 的 3D Ray 检测（使用默认层）
        public static bool RayCast(Ray ray, float distance, out RaycastHit info) => RayCast(ray.origin, ray.direction, distance, Physics.DefaultRaycastLayers, out info, Color.green, Color.red);

        /// <summary>
        /// 2d物理检测
        /// </summary>
        /// <param name="ori">检测起点</param>
        /// <param name="direction">检测方向</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="hitted">检测中物体时的颜色</param>
        /// <param name="empty">未检测中物体时的颜色</param>
        /// <returns>检测获得的信息</returns>
        public static bool RayCast(Vector3 ori, Vector3 direction, float distance, int layerMask, out RaycastHit info, Color hitted, Color empty)
        {

            var result = Physics.Raycast(ori, direction, out info, distance, layerMask);
            Debug.DrawLine(ori, result ? info.point : (ori + direction.normalized * distance), result ? hitted : empty);
            return result;
        }

        /// <summary>
        /// 3D物理检测 (NonAlloc)
        /// </summary>
        /// <param name="ori">检测起点</param>
        /// <param name="direction">检测方向</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="results">被检测到的物体信息</param>
        /// <param name="maxCastCount">最大检测物体数量</param>
        /// <returns>实际检测到的数量</returns>
        public static int RaycastNonAlloc(Vector3 ori, Vector3 direction, float distance, int layerMask, out RaycastHit[] results, int maxCastCount = 5)
        {
            if (raycast3dNonAlloc.Length < maxCastCount)
                raycast3dNonAlloc = new RaycastHit[maxCastCount];

            int count = Physics.RaycastNonAlloc(ori, direction, raycast3dNonAlloc, distance, layerMask);
            results = raycast3dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 3D 射线 NonAlloc（使用默认层）
        public static int RaycastNonAlloc(Vector3 ori, Vector3 direction, float distance, out RaycastHit[] results, int maxCastCount = 5)
        {
            return RaycastNonAlloc(ori, direction, distance, Physics.DefaultRaycastLayers, out results, maxCastCount);
        }

        /// <summary>
        /// 3D 物理检测（Ray，NonAlloc）
        /// </summary>
        /// <param name="ray">包含起点与方向的 Ray</param>
        /// <param name="distance">检测最大距离</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="results">被检测到的物体信息</param>
        /// <param name="maxCastCount">最大检测物体数量</param>
        /// <returns>实际检测到的数量</returns>
        public static int RaycastNonAlloc(Ray ray, float distance, int layerMask, out RaycastHit[] results, int maxCastCount = 5)
        {
            if (raycast3dNonAlloc.Length < maxCastCount)
                raycast3dNonAlloc = new RaycastHit[maxCastCount];

            int count = Physics.RaycastNonAlloc(ray, raycast3dNonAlloc, distance, layerMask);
            results = raycast3dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 3D Ray NonAlloc（使用默认层）
        public static int RaycastNonAlloc(Ray ray, float distance, out RaycastHit[] results, int maxCastCount = 5) => RaycastNonAlloc(ray, distance, Physics.DefaultRaycastLayers, out results, maxCastCount);

        /// <summary>
        /// 3D物理重叠球 (NonAlloc)
        /// </summary>
        /// <param name="ori">球心</param>
        /// <param name="radius">半径</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="results">被检测到的碰撞体</param>
        /// <param name="maxCount">最大检测物体数量</param>
        /// <returns>实际检测到的数量</returns>
        /// <summary>
        /// 3D 重叠球 (NonAlloc)
        /// </summary>
        /// <param name="ori">球心</param>
        /// <param name="radius">半径</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">碰撞体</param>
        /// <param name="maxCount">最大返回数量</param>
        /// <returns>实际返回数量</returns>
        public static int OverlapSphereNonAlloc(Vector3 ori, float radius, int layerMask, out Collider[] results, int maxCount = 5)
        {
            if (overlap3dNonAlloc.Length < maxCount)
                overlap3dNonAlloc = new Collider[maxCount];

            int count = Physics.OverlapSphereNonAlloc(ori, radius, overlap3dNonAlloc, layerMask);
            results = overlap3dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 3D 圆形重叠 NonAlloc（使用默认层）
        public static int OverlapSphereNonAlloc(Vector3 ori, float radius, out Collider[] results, int maxCount = 5)
        {
            return OverlapSphereNonAlloc(ori, radius, Physics.DefaultRaycastLayers, out results, maxCount);
        }

        /// <summary>
        /// 3D物理重叠盒 (NonAlloc)
        /// </summary>
        /// <param name="ori">盒中心</param>
        /// <param name="size">盒尺寸(长宽高), 内部按半尺寸传入</param>
        /// <param name="orientation">旋转</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="results">被检测到的碰撞体</param>
        /// <param name="maxCount">最大检测物体数量</param>
        /// <returns>实际检测到的数量</returns>
        /// <summary>
        /// 3D 重叠盒 (NonAlloc)
        /// </summary>
        /// <param name="ori">盒中心</param>
        /// <param name="size">盒尺寸，内部转半尺寸</param>
        /// <param name="orientation">旋转</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">碰撞体</param>
        /// <param name="maxCount">最大返回数量</param>
        /// <returns>实际返回数量</returns>
        public static int OverlapBoxNonAlloc(Vector3 ori, Vector3 size, Quaternion orientation, int layerMask, out Collider[] results, int maxCount = 5)
        {
            if (overlap3dNonAlloc.Length < maxCount)
                overlap3dNonAlloc = new Collider[maxCount];

            int count = Physics.OverlapBoxNonAlloc(ori, size * 0.5f, overlap3dNonAlloc, orientation, layerMask);
            results = overlap3dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 3D 盒体重叠 NonAlloc（使用默认层）
        public static int OverlapBoxNonAlloc(Vector3 ori, Vector3 size, Quaternion orientation, out Collider[] results, int maxCount = 5)
        {
            return OverlapBoxNonAlloc(ori, size, orientation, Physics.DefaultRaycastLayers, out results, maxCount);
        }

        /// <summary>
        /// 3D物理重叠胶囊体 (NonAlloc)
        /// </summary>
        /// <param name="point0">胶囊一端中心</param>
        /// <param name="point1">胶囊另一端中心</param>
        /// <param name="radius">半径</param>
        /// <param name="layerMask">检测层级</param>
        /// <param name="results">被检测到的碰撞体</param>
        /// <param name="maxCount">最大检测物体数量</param>
        /// <returns>实际检测到的数量</returns>
        /// <summary>
        /// 3D 重叠胶囊体 (NonAlloc)
        /// </summary>
        /// <param name="point0">胶囊一端中心</param>
        /// <param name="point1">胶囊另一端中心</param>
        /// <param name="radius">半径</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">碰撞体</param>
        /// <param name="maxCount">最大返回数量</param>
        /// <returns>实际返回数量</returns>
        public static int OverlapCapsuleNonAlloc(Vector3 point0, Vector3 point1, float radius, int layerMask, out Collider[] results, int maxCount = 5)
        {
            if (overlap3dNonAlloc.Length < maxCount)
                overlap3dNonAlloc = new Collider[maxCount];

            int count = Physics.OverlapCapsuleNonAlloc(point0, point1, radius, overlap3dNonAlloc, layerMask);
            results = overlap3dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 3D 胶囊体重叠 NonAlloc（使用默认层）
        public static int OverlapCapsuleNonAlloc(Vector3 point0, Vector3 point1, float radius, out Collider[] results, int maxCount = 5)
        {
            return OverlapCapsuleNonAlloc(point0, point1, radius, Physics.DefaultRaycastLayers, out results, maxCount);
        }

        /// <summary>
        /// 3D 球体投射 (NonAlloc)
        /// </summary>
        /// <param name="origin">起点（球心）</param>
        /// <param name="radius">半径</param>
        /// <param name="direction">投射方向</param>
        /// <param name="distance">最大距离</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">命中信息</param>
        /// <param name="maxCastCount">最大返回数量</param>
        /// <returns>实际命中数量</returns>
        public static int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, float distance, int layerMask, out RaycastHit[] results, int maxCastCount = 5)
        {
            if (raycast3dNonAlloc.Length < maxCastCount)
                raycast3dNonAlloc = new RaycastHit[maxCastCount];

            int count = Physics.SphereCastNonAlloc(origin, radius, direction, raycast3dNonAlloc, distance, layerMask);
            results = raycast3dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 3D 球体投射 NonAlloc（使用默认层）
        public static int SphereCastNonAlloc(Vector3 origin, float radius, Vector3 direction, float distance, out RaycastHit[] results, int maxCastCount = 5)
        {
            return SphereCastNonAlloc(origin, radius, direction, distance, Physics.DefaultRaycastLayers, out results, maxCastCount);
        }

        /// <summary>
        /// 3D 盒体投射 (NonAlloc)
        /// </summary>
        /// <param name="center">起点（盒体中心）</param>
        /// <param name="size">盒尺寸</param>
        /// <param name="direction">投射方向</param>
        /// <param name="orientation">旋转</param>
        /// <param name="distance">最大距离</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">命中信息</param>
        /// <param name="maxCastCount">最大返回数量</param>
        /// <returns>实际命中数量</returns>
        public static int BoxCastNonAlloc(Vector3 center, Vector3 size, Vector3 direction, Quaternion orientation, float distance, int layerMask, out RaycastHit[] results, int maxCastCount = 5)
        {
            if (raycast3dNonAlloc.Length < maxCastCount)
                raycast3dNonAlloc = new RaycastHit[maxCastCount];

            int count = Physics.BoxCastNonAlloc(center, size * 0.5f, direction, raycast3dNonAlloc, orientation, distance, layerMask);
            results = raycast3dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 3D 盒体投射 NonAlloc（使用默认层）
        public static int BoxCastNonAlloc(Vector3 center, Vector3 size, Vector3 direction, Quaternion orientation, float distance, out RaycastHit[] results, int maxCastCount = 5)
        {
            return BoxCastNonAlloc(center, size, direction, orientation, distance, Physics.DefaultRaycastLayers, out results, maxCastCount);
        }

        /// <summary>
        /// 3D 胶囊体投射 (NonAlloc)
        /// </summary>
        /// <param name="point1">胶囊一端中心</param>
        /// <param name="point2">胶囊另一端中心</param>
        /// <param name="radius">半径</param>
        /// <param name="direction">投射方向</param>
        /// <param name="distance">最大距离</param>
        /// <param name="layerMask">层级</param>
        /// <param name="results">命中信息</param>
        /// <param name="maxCastCount">最大返回数量</param>
        /// <returns>实际命中数量</returns>
        public static int CapsuleCastNonAlloc(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float distance, int layerMask, out RaycastHit[] results, int maxCastCount = 5)
        {
            if (raycast3dNonAlloc.Length < maxCastCount)
                raycast3dNonAlloc = new RaycastHit[maxCastCount];

            int count = Physics.CapsuleCastNonAlloc(point1, point2, radius, direction, raycast3dNonAlloc, distance, layerMask);
            results = raycast3dNonAlloc;
            return count;
        }

        // 不带 layerMask 的 3D 胶囊体投射 NonAlloc（使用默认层）
        public static int CapsuleCastNonAlloc(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float distance, out RaycastHit[] results, int maxCastCount = 5)
        {
            return CapsuleCastNonAlloc(point1, point2, radius, direction, distance, Physics.DefaultRaycastLayers, out results, maxCastCount);
        }

        #endregion
    }
}