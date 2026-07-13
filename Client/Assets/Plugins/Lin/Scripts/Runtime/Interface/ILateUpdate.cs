/*
┌────────────────────────────┐
│　Description: 移动
│　Remark: 
└────────────────────────────┘
*/

namespace Lin.Runtime.Interface
{
    public interface ILateUpdate
    {
        /// <summary> 延迟更新调用。 </summary>
        void OnLateUpdate();
    }
}
