// 职责：阶段间未迁移代码（MonoBehaviour）通过此 Adapter 访问 ECS NativeArray 像素数据，Phase 6 完成后删除。
// Responsibility: Bridge for non-migrated MonoBehaviour code to access ECS NativeArray pixel data; removed after Phase 6.
using Unity.Collections;

namespace AOT
{
    public sealed class PixelGridAdapter
    {
        private NativeArray<PixelData> _sourcePixels;
        private int _gridWidth;
        private int _gridHeight;

        public void Initialize(NativeArray<PixelData> sourcePixels, int width, int height)
        {
            _sourcePixels = sourcePixels;
            _gridWidth = width;
            _gridHeight = height;
        }

        public PixelData GetPixel(int x, int y)
        {
            int index = y * _gridWidth + x;
            return _sourcePixels[index];
        }

        public void SetPixel(int x, int y, PixelData pixel)
        {
            int index = y * _gridWidth + x;
            _sourcePixels[index] = pixel;
        }

        public bool IsValid(int x, int y)
        {
            return x >= 0 && x < _gridWidth && y >= 0 && y < _gridHeight;
        }

        public int Width
        {
            get { return _gridWidth; }
        }

        public int Height
        {
            get { return _gridHeight; }
        }

        public NativeArray<PixelData> SourcePixels
        {
            get { return _sourcePixels; }
        }
    }
}
