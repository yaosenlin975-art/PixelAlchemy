// 职责：像素世界背景层，渲染场景背景图和装饰。
// Responsibility: Pixel world backdrop layer, renders scene background and decorations.
using UnityEngine;

namespace AOT
{
    public sealed class PixelWorldBackdrop : MonoBehaviour
    {
        [SerializeField] private Color _skyColor = new Color(0.1f, 0.1f, 0.15f);
        [SerializeField] private SpriteRenderer _backdropRenderer;

        private void Awake()
        {
            if (_backdropRenderer == null)
            {
                GameObject backdrop = new GameObject("Backdrop");
                backdrop.transform.SetParent(transform);
                _backdropRenderer = backdrop.AddComponent<SpriteRenderer>();
            }

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                float height = mainCamera.orthographicSize * 2f;
                float width = height * mainCamera.aspect;
                transform.localScale = new Vector3(width, height, 1f);
            }

            _backdropRenderer.color = _skyColor;
        }

        public void SetSkyColor(Color color)
        {
            _skyColor = color;
            if (_backdropRenderer != null)
            {
                _backdropRenderer.color = _skyColor;
            }
        }
    }
}
