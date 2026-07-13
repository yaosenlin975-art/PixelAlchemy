/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using UnityEngine.UIElements;

namespace Lin.Editor.Toolbar.Element
{
    public interface IToolbarElement
    {
        EAlign align { get; }
        EVisibleMode visibleMode { get; }
        float width { get; }
        VisualElement Create();
    }
}