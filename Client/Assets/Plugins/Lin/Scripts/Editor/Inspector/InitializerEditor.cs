using Lin.Editor.Config;
using Lin.Runtime;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.Inspector
{
    [CustomEditor(typeof(Initializer))]
    public class InitializerEditor : UnityEditor.Editor
    {
        private GlobalConfigDrawer globalConfigDrawer;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            globalConfigDrawer = globalConfigDrawer ?? new GlobalConfigDrawer();
            globalConfigDrawer.Draw();
        }
    }
}
