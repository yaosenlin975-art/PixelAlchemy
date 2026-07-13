using Lin.Editor.Toolbar.Element;

namespace Lin.Editor.Attribute
{
    public class ToolbarButtonAttribute : ToolbarElementAttribute
    {
        public ToolbarButtonAttribute(EAlign align, EVisibleMode visibleMode, string iconPathOrLabel, string tooltip) : base(align, visibleMode, iconPathOrLabel, tooltip) { }
    }
}
