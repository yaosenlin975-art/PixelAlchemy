/*
┌────────────────────────────┐
│　Description: 在Hierarchy中显示信息
│　Remark: 
└────────────────────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.Tool
{
    public class Description : MonoBehaviour
    {
#if UNITY_EDITOR
        public string title;
        public string description;

        public Color titleColor;

        public (string title, string description, Color color) Get() => (title, description, titleColor);
#endif

        private void OnValidate()
        {
            hideFlags = HideFlags.DontSaveInBuild | HideFlags.HideInInspector;
        }
    }
}