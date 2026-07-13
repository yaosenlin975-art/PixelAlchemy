/*
┌────────────────────────────┐
│　Description: 对象池
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: ObjectPool
└──────────────┘
*/
using Lin.Runtime.DesignPattern.Singleton;
using Sirenix.OdinInspector;

namespace Lin.Runtime.DesignPattern
{
    public abstract class ObjectPool<TPool, TItem> : MonoSingleton<TPool> where TItem : class where TPool : MonoSingleton<TPool>
    {
        private UnityEngine.Pool.ObjectPool<TItem> objectPool;

        protected override void Init()
        {
            base.Init();

            objectPool = new UnityEngine.Pool.ObjectPool<TItem>(Create, OnGet, OnRelease);
        }

        protected abstract void OnRelease(TItem obj);
        protected abstract void OnGet(TItem obj);
        protected abstract TItem Create();

        public TItem Get() => objectPool.Get();

        public virtual void Recycle(TItem target) => objectPool.Release(target);

        [ReadOnly]
        [ShowInInspector]
        [LabelText("活跃对象数")]
        public int activeCount => objectPool?.CountActive ?? 0;

        [ReadOnly]
        [ShowInInspector]
        [LabelText("池中对象总数")]
        public int totalCount => objectPool?.CountAll ?? 0;
    }
}