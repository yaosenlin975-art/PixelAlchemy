/*
┌────────────────────────────┐
│　Description: 编辑器窗口基类完善
│　Remark: 
└────────────────────────────┘
*/

using Cysharp.Text;
using Lin.Runtime.Helper;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor
{
    public abstract class EditorWindowBase : EditorWindow
    {
        public new void SetDirty()
        {
            if (!titleContent.text.EndsWith('*'))
                titleContent.text = ZString.Concat(titleContent.text, '*');

            this.SetDirty<EditorWindowBase>();
        }

        public void Save()
        {
            if (titleContent.text.EndsWith('*'))
                titleContent.text = titleContent.text.TrimEnd('*');

            OnSave();
        }

        protected abstract void OnSave();

        protected abstract void Refresh();

        protected virtual void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.control && Event.current.keyCode == KeyCode.S)
            {
                Save();
                Event.current.Use();
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptReload()
        {
            var windows = FindObjectsByType<EditorWindowBase>(FindObjectsSortMode.None);

            foreach (var window in windows)
                window.Refresh();
        }
    }
}

