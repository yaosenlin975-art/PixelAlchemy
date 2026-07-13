/*
┌────────────────────────────┐
│　Description: Input window for comment and uri.
└────────────────────────────┘
*/
using System;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.Asset
{
    public class AssetMessageInputWindow : EditorWindow
    {
        private string key;
        private string path;
        private string input;
        private Action onWrited;

        private Action<string> stringSave;
        private Action<string, string> pairSave;

        public static void Show(string path, string key, string defaultMessage, Action<string, string> save, Action onWrited)
        {
            var window = GetWindow<AssetMessageInputWindow>("Uri");
            window.path = path;
            window.key = key;
            window.onWrited = onWrited;
            window.pairSave = save;
            window.stringSave = null;
            window.input = defaultMessage;
            window.minSize = window.maxSize = new Vector2(540, 600);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label($"Path：{path}");
            if (pairSave is not null)
            {
                GUILayout.Label("Title");
                key = EditorGUILayout.TextArea(key);
                GUILayout.Label("URI");
            }
            input = EditorGUILayout.TextArea(input, GUILayout.Height(500));
            if (GUILayout.Button("SaveAsync"))
            {
                stringSave?.Invoke(input);
                pairSave?.Invoke(key, input);
                onWrited?.Invoke();
                AssetSummaryDrawer.Refresh(AssetDatabase.GUIDFromAssetPath(path).ToString());
                Close();
            }
        }
    }
}