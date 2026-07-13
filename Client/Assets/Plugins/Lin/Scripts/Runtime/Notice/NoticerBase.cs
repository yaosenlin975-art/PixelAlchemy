/*
┌────────────────────────────┐
│　Description：
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：NoticerBase
└──────────────┘
*/

using Cysharp.Threading.Tasks;

namespace Lin.Runtime.Notice
{
    public abstract class NoticerBase
    {
        protected abstract string uri { get; }

        public abstract UniTask Message(string message);
    }
}