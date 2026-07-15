// 职责：像素世界渲染设置，控制像素单位比和视觉风格参数。
// Responsibility: Pixel world render settings controlling pixel-per-unit ratio and visual style parameters.
using UnityEngine;

namespace AOT
{
    [CreateAssetMenu(fileName = "PixelWorldRenderSettings", menuName = "PixelAlchemy/Render Settings")]
    public sealed class PixelWorldRenderSettings : ScriptableObject
    {
        [SerializeField] private int _pixelsPerUnit = 1;
        [SerializeField] private Color _backgroundColor = Color.black;
        [SerializeField] private Material _pixelMaterial;

        public int PixelsPerUnit
        {
            get { return _pixelsPerUnit; }
        }

        public Color BackgroundColor
        {
            get { return _backgroundColor; }
        }

        public Material PixelMaterial
        {
            get { return _pixelMaterial; }
        }
    }
}
