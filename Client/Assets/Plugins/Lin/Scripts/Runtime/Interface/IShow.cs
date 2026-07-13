/*
┌────────────────────────────┐
│　Description: 带参数展示
│　Remark: 
└────────────────────────────┘
*/
using Cysharp.Threading.Tasks;

namespace Lin.Runtime.Interface
{
    public interface IShow
    {
        void Show(bool immeditely);

        UniTask ShowAsync(bool immeditely);

        void Hide(bool immeditely);

        UniTask HideAsync(bool immeditely);
    }

    public interface IShow<T> : IShow
    {
        void Show(T arg, bool immeditely);

        /// <summary> 以指定参数异步显示。 </summary>
        UniTask ShowAsync(T arg, bool immeditely);
    }
}