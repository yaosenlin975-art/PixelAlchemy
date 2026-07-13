/*
┌────────────────────────────┐
│　Description: 初始化接口
│　Remark: 
└────────────────────────────┘
*/

using Cysharp.Threading.Tasks;

namespace Lin.Runtime.Interface
{
    public interface IInitialize
    {
        bool IsInitialized { get; }
        /// <summary> 初始化。 </summary>
        void Initialize();
    }

    public interface IInitialize<TArg>
    {
        bool IsInitialized { get; }
        /// <summary> 使用指定参数初始化。 </summary>
        void Initialize(TArg arg);
    }

    public interface IInitialize<TArg1, TArg2>
    {
        bool IsInitialized { get; }
        /// <summary> 使用指定参数初始化。 </summary>
        void Initialize(TArg1 arg1, TArg2 arg2);
    }
}
