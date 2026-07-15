// 职责：像素交互并行 Job，处理温度传导、化学反应、相变（熔化/凝固/汽化/凝结）。
// Responsibility: Parallel pixel interaction job handling thermal conduction, chemical reactions, phase changes (melt/freeze/vaporize/condense).
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace AOT
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    public struct InteractionJob : IJobChunk
    {
        [ReadOnly] public NativeArray<PixelData> CurrentPixels;
        public NativeArray<PixelData> NextPixels;
        [ReadOnly] public NativeArray<MaterialDefinition> MaterialDefs;
        public short AmbientTemp;
        public NativeReference<Xorshift128Plus> RngState;

        public void Execute(in ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            Xorshift128Plus rng = RngState.Value;

            NativeArray<PixelData> chunkPixels = chunk.GetNativeArray<PixelData>(
                TypeManager.GetTypeIndex<PixelData>());

            for (int entityIdx = 0; entityIdx < chunkPixels.Length; entityIdx++)
            {
                int globalIndex = firstEntityIndex + entityIdx;
                PixelData pixel = CurrentPixels[globalIndex];

                if (pixel.MaterialType == (byte)MaterialType.Air)
                    continue;

                ApplyThermalConduction(ref pixel, globalIndex);
                ApplyPhaseChange(ref pixel);
                ApplyChemicalReactions(ref pixel, ref rng, globalIndex);

                NextPixels[globalIndex] = pixel;
            }

            RngState.Value = rng;
        }

        private void ApplyThermalConduction(ref PixelData pixel, int index)
        {
            short temp = pixel.Temperature;
            short conducted = temp;

            int[] neighbors = new int[]
            {
                index - 1, index + 1,
                index - GridSize.Width, index + GridSize.Width
            };

            for (int i = 0; i < 4; i++)
            {
                if (neighbors[i] >= 0 && neighbors[i] < CurrentPixels.Length)
                {
                    PixelData neighbor = CurrentPixels[neighbors[i]];
                    if (neighbor.MaterialType != (byte)MaterialType.Air)
                    {
                        short diff = (short)((neighbor.Temperature - temp) / 32);
                        conducted += diff;
                    }
                }
            }

            conducted += (short)((AmbientTemp - temp) / 128);
            pixel.Temperature = conducted;
        }

        private void ApplyPhaseChange(ref PixelData pixel)
        {
            MaterialDefinition def = MaterialDefs[pixel.MaterialType];

            if (pixel.Temperature >= def.MeltingPoint && (def.Flags & 0x08) != 0)
            {
                short newTemp = (short)(pixel.Temperature - 500);
                if (newTemp < 0) newTemp = 0;

                pixel.MaterialType = (byte)GetLiquidFromSolid((MaterialType)pixel.MaterialType);
                pixel.Temperature = newTemp;
                pixel.Flags = (byte)(pixel.Flags & 0xF0);
            }

            if (pixel.Temperature >= def.BoilingPoint && (def.Flags & 0x02) != 0)
            {
                short newTemp = (short)(pixel.Temperature - 1000);
                if (newTemp < 0) newTemp = 0;

                pixel.MaterialType = (byte)MaterialType.Smoke;
                pixel.Temperature = newTemp;
            }

            if (pixel.Temperature <= 0 && pixel.MaterialType == (byte)MaterialType.Water)
            {
                pixel.MaterialType = (byte)MaterialType.Ice;
                pixel.Temperature = 0;
            }

            if (pixel.Temperature > 0 && pixel.MaterialType == (byte)MaterialType.Ice)
            {
                pixel.MaterialType = (byte)MaterialType.Water;
            }
        }

        private MaterialType GetLiquidFromSolid(MaterialType solid)
        {
            if (solid == MaterialType.Ice)
                return MaterialType.Water;
            if (solid == MaterialType.Stone)
                return MaterialType.Lava;
            return solid;
        }

        private void ApplyChemicalReactions(ref PixelData pixel, ref Xorshift128Plus rng, int index)
        {
            if (pixel.MaterialType != (byte)MaterialType.Fire)
                return;

            int[] neighbors = new int[]
            {
                index - 1, index + 1,
                index - GridSize.Width, index + GridSize.Width
            };

            for (int i = 0; i < 4; i++)
            {
                if (neighbors[i] >= 0 && neighbors[i] < CurrentPixels.Length)
                {
                    PixelData neighbor = CurrentPixels[neighbors[i]];

                    if (PixelReactionUtility.IsFlammable(neighbor.MaterialType))
                    {
                        PixelData firePixel = new PixelData();
                        firePixel.MaterialType = (byte)MaterialType.Fire;
                        firePixel.Temperature = 1000;
                        firePixel.Flags = 0x04;
                        firePixel.Color = 0xFA60;
                        NextPixels[neighbors[i]] = firePixel;
                    }
                }
            }
        }
    }
}
