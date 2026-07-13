/*
┌────────────────────────────┐
│　Description: 移动
│　Remark: 
└────────────────────────────┘
*/

namespace Lin.Runtime.Interface
{
    public interface IUpdate
    {
        /// <summary> 每帧调用。 </summary>
        void OnUpdate();
    }
}
