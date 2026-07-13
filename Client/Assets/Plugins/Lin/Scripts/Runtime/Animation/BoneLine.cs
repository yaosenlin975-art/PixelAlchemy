/*
┌────────────────────────────┐
│　Description：画出骨格线
│　Remark：
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName：BoneLine
└──────────────┘
*/
using UnityEngine;

namespace Lin.Runtime.Animation
{
    public class BoneLine : MonoBehaviour
    {
        public Color color;
        private void OnDrawGizmos()
        {
            DrawBones(transform);
        }

        private void DrawBones(Transform transform)
        {
            foreach (Transform child in transform)
            {
                Debug.DrawLine(transform.position, child.position, color);
                DrawBones(child);
            }
        }
    }
}
