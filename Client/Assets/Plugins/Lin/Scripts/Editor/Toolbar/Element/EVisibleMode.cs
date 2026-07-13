/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using System;

namespace Lin.Editor.Toolbar.Element
{
    [Flags]
    public enum EVisibleMode
    {
        None = 0,
        Editor = 1,
        Runtime = 2,
        Both = Editor | Runtime
    }
}