/*
┌────────────────────────────┐
│　Description: 带参数展示
│　Remark: 
└────────────────────────────┘
*/
namespace Lin.Runtime.Interface
{
    public interface ICommand
    {
        void Do();

        void Undo();
    }
}