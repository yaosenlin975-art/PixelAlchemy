/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: GizmosHelper
└──────────────┘
*/
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Lin.Runtime.Helper
{
    /// <summary>
    /// Gizmos Extensions.
    /// </summary>
    public static class GizmosHelper
    {
        /// <summary>
        /// Draw a circular arc in 3D space.
        /// </summary>
        /// <param name="center">The center of the circle.</param>
        /// <param name="normal">The normal of the circle.</param>
        /// <param name="from">The direction of the point on the circle circumference, relative to the center, where the arc begins.</param>
        /// <param name="angle"> The angle of the arc, in degrees.</param>
        /// <param name="radius">The radius of the circle Note: Use HandleUtility.GetHandleSize where you might want to have constant screen-sized handles.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void DrawWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius, Color color)
        {
#if UNITY_EDITOR
            Handles.color = color;
            Handles.DrawWireArc(center, normal, from, angle, radius);
#endif
        }

        /// <summary>
        /// Draw a circular sector (pie piece) in 3D space.
        /// </summary>
        /// <param name="center">The center of the circle.</param>
        /// <param name="normal">The normal of the circle.</param>
        /// <param name="from">The direction of the point on the circle circumference, relative to the center, where the arc begins.</param>
        /// <param name="angle"> The angle of the arc, in degrees.</param>
        /// <param name="radius">The radius of the circle Note: Use HandleUtility.GetHandleSize where you might want to have constant screen-sized handles.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void DrawSolidArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius, Color color)
        {
#if UNITY_EDITOR
            Handles.color = color;
            Handles.DrawSolidArc(center, normal, from, angle, radius);
#endif
        }

        /// <summary>
        /// Draw the outline of a flat disc in 3D space.
        /// </summary>
        /// <param name="center">The center of the disc.</param>
        /// <param name="normal">The normal of the disc.</param>
        /// <param name="radius">The radius of the disc.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void DrawWireDisc(Vector3 center, Vector3 normal, float radius, Color color)
        {
#if UNITY_EDITOR
            Handles.color = color;
            Handles.DrawWireDisc(center, normal, radius);
#endif
        }

        /// <summary>
        /// Draw a solid flat disc in 3D space.
        /// Note: Use HandleUtility.GetHandleSize where you might want to have constant screen-sized handles.
        /// </summary>
        /// <param name="center">The center of the disc.</param>
        /// <param name="normal">The normal of the disc.</param>
        /// <param name="radius">The radius of the disc.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void DrawSolidDisc(Vector3 center, Vector3 normal, float radius, Color color)
        {
#if UNITY_EDITOR
            Handles.color = color;
            Handles.DrawSolidDisc(center, normal, radius);
#endif
        }

        /// <summary>
        /// Make a text label positioned in 3D space.
        /// </summary>
        /// <param name="position">Position in 3D space as seen from the current handle camera.</param>
        /// <param name="text">Text to display on the label.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Label(Vector3 position, string text, Color color)
        {
#if UNITY_EDITOR
            Handles.color = color;
            Handles.Label(position, text);
#endif
        }

        /// <summary>
        /// Make a text label positioned in 3D space.
        ///  Note: Use HandleUtility.GetHandleSize where you might want to have constant screen-sized handles.
        /// </summary>
        /// <param name="position">Position in 3D space as seen from the current handle camera.</param>
        /// <param name="content">Text, image and tooltip for this label.</param>
        /// <param name="style">The style to use. If left out, the label style from the current GUISkin is used.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Label(Vector3 position, GUIContent content, GUIStyle style, Color color)
        {
#if UNITY_EDITOR
            Handles.color = color;
            Handles.Label(position, content, style);
#endif
        }
    }
}
