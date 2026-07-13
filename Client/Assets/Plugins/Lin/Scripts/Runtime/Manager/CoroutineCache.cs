/*
┌────────────────────────────┐
│　Description: 协程缓存管理器
│　Remark: 提供协程等待对象的缓存和十进制分拆等待机制
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: CoroutineCache
└──────────────┘
*/
using System.Collections;
using UnityEngine;

namespace Lin.Runtime.Manager
{
    /// <summary>
    /// 协程缓存管理器
    /// </summary>
    public static class CoroutineCache
    {
        public readonly static WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
        public readonly static WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

        private readonly static WaitForSeconds _1s = new WaitForSeconds(1);
        private readonly static WaitForSeconds _0_1s = new WaitForSeconds(0.1f);
        private readonly static WaitForSeconds _0_01s = new WaitForSeconds(0.01f);

        public static IEnumerator WaitForSeconds(float seconds)
        {
            //整数部分
            for (int i = 0; i < (int)seconds; i++)
                yield return _1s;

            //小数部分
            var decimals = seconds - (int)seconds;
            var wait = (int)(decimals * 10);
            // 0.1s
            if (wait > 0)
                for (int i = 0; i < wait; i++)
                    yield return _0_1s;
            // 0.01s
            decimals -= wait / 10f;
            wait = (int)(decimals * 100);
            if (wait > 0)
                for (int i = 0; i < wait; i++)
                    yield return _0_01s;
        }

        /// <summary>
        /// 等待指定帧数的FixedUpdate
        /// </summary>
        /// <param name="framesCount">帧数</param>
        /// <returns>等待协程</returns>
        public static IEnumerator WaitForFixedUpdate(int framesCount = 1)
        {
            for (int i = 0; i < framesCount; i++)
                yield return waitForFixedUpdate;
        }

        /// <summary>
        /// 等待指定帧数的Update
        /// </summary>
        /// <param name="framesCount">帧数</param>
        /// <returns>等待协程</returns>
        public static IEnumerator WaitForUpdate(int framesCount = 1)
        {
            for (int i = 0; i < framesCount; i++)
                yield return null;
        }
    }
}
