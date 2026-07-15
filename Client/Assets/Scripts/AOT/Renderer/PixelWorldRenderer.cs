// 职责：像素世界渲染器，主线程更新 Texture2D，从 RenderBridgeSystem 读取 Color32 缓冲区。
// Responsibility: Main-thread pixel world renderer updating Texture2D from RenderBridgeSystem's Color32 buffer.
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace AOT
{
    public sealed class PixelWorldRenderer : MonoBehaviour
    {
        [SerializeField] private int _pixelsPerUnit = 1;

        private Texture2D _texture;
        private SpriteRenderer _spriteRenderer;
        private RenderBridgeSystem _bridgeSystem;
        private NativeArray<Color32> _colorBuffer;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        private void Start()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return;

            if (world.EntityManager.HasComponent<GridSize>(
                world.EntityManager.CreateEntityQuery(typeof(GridSize)).GetSingletonEntity()))
            {
                GridSize gridSize = world.EntityManager.GetComponentData<GridSize>(
                    world.EntityManager.CreateEntityQuery(typeof(GridSize)).GetSingletonEntity());

                InitializeTexture(gridSize.Width, gridSize.Height);
            }
        }

        public void Initialize(int width, int height)
        {
            InitializeTexture(width, height);
        }

        private void InitializeTexture(int width, int height)
        {
            if (_texture != null)
            {
                Destroy(_texture);
            }

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _texture.filterMode = FilterMode.Point;
            _texture.wrapMode = TextureWrapMode.Clamp;

            _spriteRenderer.sprite = Sprite.Create(
                _texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                _pixelsPerUnit);
        }

        private void Update()
        {
            if (_texture == null)
                return;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
                return;

            if (!world.EntityManager.HasComponent<GridSize>(
                world.EntityManager.CreateEntityQuery(typeof(GridSize)).GetSingletonEntity()))
                return;

            GridSize gridSize = world.EntityManager.GetComponentData<GridSize>(
                world.EntityManager.CreateEntityQuery(typeof(GridSize)).GetSingletonEntity());

            if (_texture.width != gridSize.Width || _texture.height != gridSize.Height)
            {
                InitializeTexture(gridSize.Width, gridSize.Height);
            }

            _texture.SetPixels32(0, 0, _texture.width, _texture.height,
                GetColorArray(gridSize.Width, gridSize.Height));

            _texture.Apply();
        }

        private Color32[] GetColorArray(int width, int height)
        {
            Color32[] colors = new Color32[width * height];
            World world = World.DefaultGameObjectInjectionWorld;

            if (world != null)
            {
                EntityQuery pixelQuery = world.EntityManager.CreateEntityQuery(typeof(PixelData));
                NativeArray<PixelData> pixels = pixelQuery.ToComponentDataArray<PixelData>(Allocator.Temp);

                for (int i = 0; i < pixels.Length && i < colors.Length; i++)
                {
                    ushort rgb565 = pixels[i].Color;
                    byte r = (byte)((rgb565 >> 11) & 0x1F);
                    byte g = (byte)((rgb565 >> 5) & 0x3F);
                    byte b = (byte)(rgb565 & 0x1F);
                    r = (byte)((r << 3) | (r >> 2));
                    g = (byte)((g << 2) | (g >> 4));
                    b = (byte)((b << 3) | (b >> 2));
                    colors[i] = new Color32(r, g, b, 255);
                }

                pixels.Dispose();
            }

            return colors;
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }
    }
}
