using Lin.Editor.Toolbar.Element;
using Lin.Runtime.Helper;

namespace Lin.Editor.Attribute
{
    public class ToolbarToggleAttribute : ToolbarElementAttribute
    {
        public bool isOn { get; }
        public string key { get; }

        public ToolbarToggleAttribute(EAlign align, EVisibleMode visibleMode, string iconPathOrLabel, string tooltip, string key) : base(align, visibleMode, iconPathOrLabel, tooltip)
        {
            this.key = key;
            isOn = PrefsHelper.Get(key, false);
        }
    }
}
