/*
┌────────────────────────────┐
│　Description: 数组操作辅助类
│　Author: 花球i
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: ArrayHelper
└──────────────┘
*/
using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_2019_1_OR_NEWER
#endif

namespace Lin.Runtime.Helper
{
    public static class IEnumerableExtension
    {
        public static bool Operatable<T>(this IEnumerable<T> array)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            return array.Any();
        }

        public static T Random<T>(this IEnumerable<T> array)
        {
            if (!array.Operatable())
                return default;


            int index =
#if UNITY_2019_1_OR_NEWER
                (int)(UnityEngine.Random.value * array.Count());
#else
                (int)(new Random().NextDouble() * array.Count());
#endif
            return array.ElementAt(index);
        }

        public static bool Empty<T>(this IEnumerable<T> self) => !self.Any();
    }
}