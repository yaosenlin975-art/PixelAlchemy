/*
┌────────────────────────────┐
│　Description: 
│　Remark: 
└────────────────────────────┘
*/
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lin.Runtime.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextMeshProUGUIMaxWidth : LayoutElement
    {
        [SerializeField] private float m_MaxWidthForTextMeshPro = -1;
        [SerializeField] private TextMeshProUGUI text;

        public override float preferredWidth
        {
            get
            {
                float baseWidth = base.preferredWidth;
                if (text == null)
                    return baseWidth;

                if (m_MaxWidthForTextMeshPro > 0)
                    return text.preferredWidth > m_MaxWidthForTextMeshPro ? m_MaxWidthForTextMeshPro : text.preferredWidth;

                return baseWidth;
            }
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(TextMeshProUGUIMaxWidth))]
    class ClampedLayoutElementEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            TextMeshProUGUIMaxWidth element = (TextMeshProUGUIMaxWidth)target;
            if (element.GetComponent<TextMeshProUGUI>() == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("TextMeshProUGUIMaxWidth requires a TextMeshProUGUI component on the same GameObject.", UnityEditor.MessageType.Warning);
            }
        }
    }
#endif
}