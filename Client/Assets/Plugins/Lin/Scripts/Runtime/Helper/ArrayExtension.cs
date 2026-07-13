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
#if UNITY_2019_1_OR_NEWER
using UnityEngine;
#endif

namespace Lin.Runtime.Helper
{
    public static class ArrayExtension
    {
        public static bool Contains<T>(this T[] array, T item) => array.IndexOf(item) >= 0;

        public static int IndexOf<T>(this T[] array, T item, int startIndex = 0) => array.IndexOf(item, startIndex, array.Length - startIndex - 1);

        public static int IndexOf<T>(this T[] array, T item, int startIndex, int count)
        {
            if (!array.Operatable())
                return -1;

            if (startIndex >= array.Length || startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex");

            if (startIndex + count >= array.Length)
                throw new ArgumentOutOfRangeException("count");

            for (int i = startIndex; i < startIndex + count; i++)
            {
                var current = array[i];
                if (item.Equals(current))
                    return i;
            }

            return -1;
        }

        /// <summary> Fisher-Yates洗牌算法, 用于随机排序方向 </summary>
        public static void Shuffle<T>(this T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }
        }

        /// <summary>
        /// 查找满足条件(相等）的单个元素(有多个时返回第一个）
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="array">数组</param>
        /// <param name="condition">比较方法（委托）</param>
        /// <returns>返回目标对象</returns>
        public static T Find<T>(this T[] array, Predicate<T> match)
        {
            if (!array.Operatable())
                return default;

            for (int i = 0; i < array.Length; i++)
            {
                T item = array[i];
                if (match(item))
                    return item;
            }

            return default;
        }

        public static int FindIndex<T>(this T[] array, Predicate<T> match)
        {
            if (!array.Operatable())
                return -1;

            for (int i = 0; i < array.Length; i++)
            {
                T item = array[i];
                if (match(item))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 查找满足条件(相等）的多个元素
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="array">数组</param>
        /// <param name="condition">比较方法（委托）</param>
        /// <returns>返回目标对象</returns>
        public static List<T> FindAll<T>(this T[] array, Predicate<T> match)
        {
            if (!array.Operatable())
                return default;

            List<T> list = new List<T>(array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                if (match(array[i]))
                    list.Add(array[i]);
            }
            return list;
        }

        /// <summary>
        /// 查找数组中满足条件的最大值
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <typeparam name="Q">比较依据的数据类型</typeparam>
        /// <param name="array">数组</param>
        /// <param name="condition">比较依据方法</param>
        /// <returns></returns>
        public static T Max<T, Q>(this T[] array, Func<T, Q> condition) where Q : IComparable
        {
            if (!array.Operatable())
                return default;

            T maxT = array[0];
            for (int i = 1; i < array.Length; i++)
            {
                if (condition(array[i]).CompareTo(condition(maxT)) > 0)
                    maxT = array[i];
            }

            return maxT;
        }

        /// <summary>
        /// 查找数组中满足条件的最小值
        /// </summary>
        /// <typeparam name="T">数组类型</typeparam>
        /// <typeparam name="Q">比较依据的数据类型</typeparam>
        /// <param name="array">数组</param>
        /// <param name="condition">比较依据方法</param>
        /// <returns></returns>
        public static T Min<T, Q>(this T[] array, Func<T, Q> condition) where Q : IComparable
        {
            if (!array.Operatable())
                return default;

            T minT = array[0];
            for (int i = 1; i < array.Length; i++)
            {
                if (condition(array[i]).CompareTo(condition(minT)) < 0)
                    minT = array[i];
            }

            return minT;
        }

        /// <summary>
        /// 数组升序排序
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <typeparam name="Q">比较依据</typeparam>
        /// <param name="array">待排序数组</param>
        /// <param name="condition">排序依据方法</param>
        public static void Sort_ascending<T, Q>(this T[] array, Func<T, Q> condition) where Q : IComparable
        {
            if (!array.Operatable())
                return;

            //冒泡排序
            for (int i = 0; i < array.Length - 1; i++)
                for (int j = i + 1; j < array.Length; j++)
                    if (condition(array[j]).CompareTo(condition(array[i])) > 0)
                    {
                        T temp = array[i];
                        array[j] = array[i];
                        array[i] = temp;
                    }
        }

        /// <summary>
        /// 数组降序排序
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <typeparam name="Q">比较依据</typeparam>
        /// <param name="array">待排序数组</param>
        /// <param name="condition">排序依据方法</param>
        public static void Sort_descending<T, Q>(this T[] array, Func<T, Q> condition) where Q : IComparable
        {
            if (!array.Operatable())
                return;

            //冒泡排序
            for (int i = 0; i < array.Length - 1; i++)
                for (int j = i + 1; j < array.Length; j++)
                    if (condition(array[j]).CompareTo(condition(array[i])) < 0)
                    {
                        T temp = array[i];
                        array[j] = array[i];
                        array[i] = temp;
                    }
        }
    }
}