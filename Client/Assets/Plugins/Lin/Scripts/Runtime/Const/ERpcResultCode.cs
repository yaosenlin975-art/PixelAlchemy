/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：ERpcMessageType
└──────────────┘
*/

namespace Lin.Runtime.Const
{
    /// <summary> 请求响应结果 </summary>
    public enum ERpcResultCode
    {
        SUCCESSED,

        //注册
        FAILED_ACCOUNT_EXIST,
        FAILED_ACCOUNT_INILEAGE,
        FAILED_PASSWORD_INILEAGE,

        //登录
        FAILED_WRONG_PASSWORD,
        FAILED_ACCOUNT_BLOCK,
        FAILED_ACCOUNT_NOT_EXIST,
    }
}
