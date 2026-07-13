/*
┌────────────────────────────┐
│　Description: 对象池中对象接口
│　Remark: 
└────────────────────────────┘
*/

namespace Lin.Runtime.Interface
{
    public interface IPoolObject
    {
        /// <summary> 从池中取出时调用。 </summary>
        void OnGet();
        /// <summary> 归还到池中时调用。 </summary>
        void OnRelease();
    }
}
