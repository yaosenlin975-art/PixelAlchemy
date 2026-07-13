/*
┌────────────────────────────┐
│　Description: Editor会调用这个属性快速改变GameObject的名字
│　Remark: 
└────────────────────────────┘
*/

namespace Lin.Runtime.Attribute
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class NameAttribute : System.Attribute
    {
        public NameAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
