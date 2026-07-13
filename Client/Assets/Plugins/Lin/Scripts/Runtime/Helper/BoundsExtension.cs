/*
┌────────────────────────────┐
│　Description: UnityEngine.Object辅助
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: BoundsExtension
└──────────────┘
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Pool;
using static UnityEngine.Debug;
using Debug = UnityEngine.Debug;

namespace Lin.Runtime.Helper
{
    public static class BoundsExtension
    {
        [Conditional("UNITY_EDITOR")]
        public static void DrawWireCube(this Bounds bounds, Color color, float duration = 1)
        {
            List<Vector3> corners = ListPool<Vector3>.Get();
            corners.Add(bounds.center + new Vector3(bounds.extents.x, bounds.extents.y, bounds.extents.z));
            corners.Add(bounds.center + new Vector3(bounds.extents.x, bounds.extents.y, -bounds.extents.z));
            corners.Add(bounds.center + new Vector3(bounds.extents.x, -bounds.extents.y, bounds.extents.z));
            corners.Add(bounds.center + new Vector3(bounds.extents.x, -bounds.extents.y, -bounds.extents.z));
            corners.Add(bounds.center + new Vector3(-bounds.extents.x, bounds.extents.y, bounds.extents.z));
            corners.Add(bounds.center + new Vector3(-bounds.extents.x, bounds.extents.y, -bounds.extents.z));
            corners.Add(bounds.center + new Vector3(-bounds.extents.x, -bounds.extents.y, bounds.extents.z));
            corners.Add(bounds.center + new Vector3(-bounds.extents.x, -bounds.extents.y, -bounds.extents.z));

            // 绘制线框
            DrawLine(corners[0], corners[1], color, duration);
            DrawLine(corners[1], corners[3], color, duration);
            DrawLine(corners[3], corners[2], color, duration);
            DrawLine(corners[2], corners[0], color, duration);

            DrawLine(corners[4], corners[5], color, duration);
            DrawLine(corners[5], corners[7], color, duration);
            DrawLine(corners[7], corners[6], color, duration);
            DrawLine(corners[6], corners[4], color, duration);

            DrawLine(corners[0], corners[4], color, duration);
            DrawLine(corners[1], corners[5], color, duration);
            DrawLine(corners[2], corners[6], color, duration);
            DrawLine(corners[3], corners[7], color, duration);
        }

        public static bool Contains(this Bounds self, Bounds other) => self.Contains(other.center) && self.Contains(other.max) && self.Contains(other.min);

        /// <summary>
        /// 八叉树分割包围盒
        /// </summary>
        public static Bounds GetChildByOctree(this Bounds self, int index)
        {
            if (index < 0 || index > 7)
                throw new IndexOutOfRangeException();

            Bounds result = new Bounds();
            result.size = self.size / 2;

            Vector3 offset = result.size / 2;
            // 使用 Convert.ToString 将十进制转换为二进制
            string binary = Convert.ToString(index, 2);
            // 使用 PadLeft 确保字符串长度为4，不足的地方用'0'补齐
            binary = binary.PadLeft(3, '0');
            offset.x *= Sign(0);
            offset.y *= Sign(1);
            offset.z *= Sign(2);
            result.center = self.center + offset;

            return result;

            int Sign(int binaryIndex) => binary[binaryIndex].Equals('1') ? -1 : 1;
        }

        /// <summary>
        /// 四叉树分割包围盒
        /// </summary>
        public static Bounds GetChildByQuadtree(this Bounds self, int index)
        {
            if (index < 0 || index > 3)
                throw new IndexOutOfRangeException();

            Bounds result = new Bounds();
            var childSize = self.size / 2;
            childSize.y = self.size.y;
            result.size = childSize;

            Vector3 offset = result.size / 2;
            offset.y = 0;
            // 使用 Convert.ToString 将十进制转换为二进制
            string binary = Convert.ToString(index, 2);
            // 使用 PadLeft 确保字符串长度为4，不足的地方用'0'补齐
            binary = binary.PadLeft(2, '0');
            offset.x *= Sign(0);
            offset.z *= Sign(1);
            result.center = self.center + offset;

            return result;

            int Sign(int binaryIndex) => binary[binaryIndex].Equals('1') ? -1 : 1;
        }


        public static Bounds CalculateObjectBounds(this GameObject self, bool includeInactive = true)
        {
            var rb = CalculateBounds(self.GetComponentsInChildren<Renderer>(includeInactive));
            var tb = CalculateBounds(self.GetComponentsInChildren<Terrain>(includeInactive));
            return rb.Combinate(tb);
        }

        public static Bounds CalculateBounds(this Renderer[] self)
        {
            Vector3 mapMinBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mapMaxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (Renderer renderer in self)
            {
                if (renderer != null)
                {
                    // 更新边界值
                    mapMinBounds = Vector3.Min(mapMinBounds, renderer.bounds.min);
                    mapMaxBounds = Vector3.Max(mapMaxBounds, renderer.bounds.max);
                }
            }

            return new Bounds((mapMinBounds + mapMaxBounds) / 2f, mapMaxBounds - mapMinBounds);
        }

        public static Bounds CalculateBounds(this LODGroup[] self)
        {
            Vector3 mapMinBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mapMaxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var group in self)
            {
                var bounds = group.gameObject.CalculateObjectBounds();
                // 更新边界值
                mapMinBounds = Vector3.Min(mapMinBounds, bounds.min);
                mapMaxBounds = Vector3.Max(mapMaxBounds, bounds.max);
            }

            return new Bounds((mapMinBounds + mapMaxBounds) / 2f, mapMaxBounds - mapMinBounds);
        }

        public static Bounds CalculateBounds(this Terrain self)
        {
            if (self is null || self.terrainData is null)
                return new Bounds();

            Vector3 mapMinBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mapMaxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var bounds = self.terrainData.bounds;
            bounds.center = self.transform.position + new Vector3(bounds.size.x, 0, bounds.size.z) / 2f;

            mapMinBounds = Vector3.Min(bounds.min, mapMinBounds);
            mapMaxBounds = Vector3.Max(bounds.max, mapMaxBounds);

            return new Bounds((mapMinBounds + mapMaxBounds) / 2f, mapMaxBounds - mapMinBounds);
        }

        public static Bounds CalculateBounds(this Terrain[] self)
        {
            Vector3 mapMinBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mapMaxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var item in self)
            {
                if (item is not null)
                {
                    try
                    {
                        var bounds = item.CalculateBounds();

                        mapMinBounds = Vector3.Min(bounds.min, mapMinBounds);
                        mapMaxBounds = Vector3.Max(bounds.max, mapMaxBounds);
                    }
                    catch (System.Exception)
                    {
                        Debug.LogError(item.name);
                        throw;
                    }
                }
            }
            return new Bounds((mapMinBounds + mapMaxBounds) / 2f, mapMaxBounds - mapMinBounds);
        }

        public static Bounds Combinate(this Bounds[] self)
        {
            Vector3 mapMinBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 mapMaxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var item in self)
            {
                mapMinBounds = Vector3.Min(item.min, mapMinBounds);
                mapMaxBounds = Vector3.Max(item.max, mapMaxBounds);
            }
            return new Bounds((mapMinBounds + mapMaxBounds) / 2f, mapMaxBounds - mapMinBounds);
        }

        public static Bounds Combinate(this Bounds self, Bounds other)
        {
            Vector3 mapMinBounds = Vector3.Min(self.min, other.min);
            Vector3 mapMaxBounds = Vector3.Max(self.max, other.max);
            return new Bounds((mapMinBounds + mapMaxBounds) / 2f, mapMaxBounds - mapMinBounds);
        }

        [Conditional("UNITY_EDITOR")]
        public static void DrawWireCube(Vector3 center, Vector3 size, Color color, float duration = 1) => BoundsExtension.DrawWireCube(new Bounds(center, size), color, duration);
    }
}