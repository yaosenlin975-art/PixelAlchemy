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
using System.Collections.Generic;
#if UNITY_2019_1_OR_NEWER
#endif

namespace Lin.Runtime.Helper
{
    public static class HashSetExtenstion
    {
        public static int IndexOf<T>(this HashSet<T> self, T element)
        {
            int index = 0;
            foreach (T item in self)
            {
                if (EqualityComparer<T>.Default.Equals(item, element))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }
    }
}