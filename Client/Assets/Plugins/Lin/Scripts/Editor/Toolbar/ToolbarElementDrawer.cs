/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using System;
using UnityEditor;
using UnityEngine;

namespace Lin.Editor.Toolbar
{
    public static class ToolbarElementDrawer
    {
        #region - Buttons -

        public readonly static GUIStyle ButtonStyle = new GUIStyle(GUI.skin.button)
        {
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            imagePosition = ImagePosition.ImageOnly,
        };

        public static void Button(Action onClick, Texture2D icon)
        {
            Rect buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(34), GUILayout.Height(20));
            if (GUI.Button(buttonRect, icon, ButtonStyle))
                onClick();
        }

        public static void Button(Action onClick, string label)
        {
            Rect buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(34), GUILayout.Height(20));
            if (GUI.Button(buttonRect, label))
                onClick();
        }

        #endregion

        #region - Toggle -

        public static bool Toggle(bool value, string label)
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(34), GUILayout.Height(20));
            if (GUI.Button(rect, label))
                value = !value;

            return value;
        }

        public static bool Toggle(bool value, Texture2D icon)
        {
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(34), GUILayout.Height(20));
            if (GUI.Button(rect, icon, ButtonStyle))
                value = !value;

            if (icon)
            {
                var texRect = new Rect(rect.x + 8, rect.y + 2, 18, 16);
                GUI.DrawTexture(texRect, icon, ScaleMode.ScaleToFit, true);
            }

            return value;
        }

        #endregion
    }
}
