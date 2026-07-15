// 职责：简单像素玩家，调试用最小实现。
// Responsibility: Minimal debug implementation of a simple pixel player.
using UnityEngine;

namespace AOT
{
    public sealed class SimplePixelPlayer : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private Color _playerColor = Color.white;

        private SpriteRenderer _spriteRenderer;
        private Vector2 _position;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            _spriteRenderer.color = _playerColor;
            _position = transform.position;

            Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color32[] colors = new Color32[64];
            for (int i = 0; i < 64; i++)
            {
                colors[i] = _playerColor;
            }
            tex.SetPixels32(colors);
            tex.Apply();
            _spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        }

        private void Update()
        {
            float dx = 0f;
            float dy = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                dx -= _moveSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                dx += _moveSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Space))
                dy += _moveSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                dy -= _moveSpeed * Time.deltaTime;

            _position += new Vector2(dx, dy);
            transform.position = _position;
        }
    }
}
