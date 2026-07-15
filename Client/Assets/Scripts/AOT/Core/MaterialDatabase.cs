// 职责：材料数据库，提供材料定义查询和像素创建方法。
// Responsibility: Material database providing material definition queries and pixel creation methods.
using Unity.Collections;

namespace AOT
{
    public static class MaterialDatabase
    {
        private static MaterialDefinition[] _definitions;

        public static void Initialize()
        {
            _definitions = new MaterialDefinition[12];

            // Air
            _definitions[0] = CreateDef(0, 0, 0, 0, 0, 0, 0);
            // Sand
            _definitions[1] = CreateDef(1, 3, 2000, 3000, 1500, 0xDCB3, 0x01);
            // Water
            _definitions[2] = CreateDef(2, 2, 0, 1000, 0, 0x04DF, 0x02);
            // Smoke
            _definitions[3] = CreateDef(3, 1, 0, 0, 0, 0xAD55, 0x04);
            // Fire
            _definitions[4] = CreateDef(4, 1, 500, 1000, 0, 0xFA60, 0x04);
            // Stone
            _definitions[5] = CreateDef(5, 4, 3000, 5000, 0, 0x8C92, 0x08);
            // Wood
            _definitions[6] = CreateDef(6, 3, 0, 0, 300, 0x8B6C, 0x08);
            // Ash
            _definitions[7] = CreateDef(7, 2, 0, 0, 0, 0xAD55, 0x01);
            // Poison
            _definitions[8] = CreateDef(8, 2, 0, 1000, 0, 0x3E03, 0x02);
            // Ice
            _definitions[9] = CreateDef(9, 2, 0, 0, 0, 0xDEFB, 0x08);
            // Lava
            _definitions[10] = CreateDef(10, 2, 3000, 5000, 0, 0xF800, 0x02);
            // Debris
            _definitions[11] = CreateDef(11, 3, 2000, 3000, 1500, 0x8C92, 0x01);
        }

        private static MaterialDefinition CreateDef(
            byte type, byte density, short melt, short boil, short ignite, ushort color, byte flags)
        {
            MaterialDefinition def = new MaterialDefinition();
            def.MaterialType = type;
            def.Density = density;
            def.MeltingPoint = melt;
            def.BoilingPoint = boil;
            def.IgnitionPoint = ignite;
            def.DefaultColor = color;
            def.Flags = flags;
            return def;
        }

        public static MaterialDefinition GetDefinition(byte materialType)
        {
            if (_definitions == null)
                Initialize();

            if (materialType >= 0 && materialType < _definitions.Length)
                return _definitions[materialType];

            return _definitions[0];
        }

        public static MaterialDefinition GetDefinition(MaterialType type)
        {
            return GetDefinition((byte)type);
        }

        public static NativeArray<MaterialDefinition> ToNativeArray(Allocator allocator)
        {
            if (_definitions == null)
                Initialize();

            NativeArray<MaterialDefinition> result = new NativeArray<MaterialDefinition>(
                _definitions.Length, allocator);

            for (int i = 0; i < _definitions.Length; i++)
            {
                result[i] = _definitions[i];
            }

            return result;
        }

        public static PixelData CreatePixel(MaterialType type)
        {
            MaterialDefinition def = GetDefinition(type);
            PixelData pixel = new PixelData();
            pixel.MaterialType = (byte)type;
            pixel.Density = def.Density;
            pixel.Temperature = 200;
            pixel.Lifetime = 0;
            pixel.Color = def.DefaultColor;
            pixel.Flags = 0x04;
            pixel.VelocityX = 0;
            pixel.VelocityY = 0;
            pixel.FallingFrames = 0;
            return pixel;
        }
    }
}
