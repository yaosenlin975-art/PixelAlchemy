using Lin.Runtime.Interface;
using UnityEngine.Pool;

namespace Lin.Runtime
{
    //加锁
    public static class Factory<T> where T : class, new()
    {
        private static ObjectPool<T> pool;
        private static readonly object lockObject = new object();

        public static T Get()
        {
            lock (lockObject)
            {
                if (pool is null)
                    pool = new ObjectPool<T>(() => new T(), OnGet, OnRelease);
            }
            return pool.Get();
        }

        private static void OnRelease(T t)
        {
            if (t is IPoolObject poolObject)
                poolObject.OnRelease();
        }

        private static void OnGet(T t)
        {
            if (t is IPoolObject poolObject)
                poolObject.OnGet();
        }

        public static void Release(T target)
        {
            if (pool != null)
            {
                pool.Release(target);
            }
        }
    }
}
