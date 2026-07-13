/*
┌────────────────────────────┐
│　Description: 带参数展示
│　Remark: 
└────────────────────────────┘
*/
namespace Lin.Runtime.Interface
{
    public interface IEvent
    {
        /// <summary> 注册事件监听。 </summary>
        void RegisterEvents();
        /// <summary> 注销事件监听。 </summary>
        void DeregisterEvents();
    }
}