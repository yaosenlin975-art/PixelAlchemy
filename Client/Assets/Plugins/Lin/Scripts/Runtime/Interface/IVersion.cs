/*
┌────────────────────────────┐
│　Description: 更新信息接口
│　Remark: 
└────────────────────────────┘
*/

namespace Lin.Runtime.Interface
{
    public interface IVersion 
    {
        /// <summary> 获取版本号。 </summary>
        public string GetVersion();
        /// <summary> 获取版本说明。 </summary>
        public string GetInfos();
    }
}
