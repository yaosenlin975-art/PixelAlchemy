// 职责：法术移动并行 Job，处理弹道运动、命中检测和像素范围效果。
// Responsibility: Parallel spell movement job handling projectile motion, hit detection, and pixel area effects.
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace AOT
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    public struct SpellMovementJob : IJobChunk
    {
        public ComponentLookup<SpellData> SpellLookup;
        [ReadOnly] public NativeArray<PixelData> CurrentPixels;
        public NativeArray<PixelData> NextPixels;
        public GridSize GridSize;
        public Fix64 DeltaTime;

        public void Execute(in ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            NativeArray<SpellData> spells = chunk.GetNativeArray<SpellData>(
                TypeManager.GetTypeIndex<SpellData>());

            for (int i = 0; i < spells.Length; i++)
            {
                SpellData spell = spells[i];

                spell.Position = spell.Position + spell.Velocity * DeltaTime;
                spell.Lifetime--;

                int gridX = (int)spell.Position.X.ToFloat();
                int gridY = (int)spell.Position.Y.ToFloat();

                if (gridX >= 0 && gridX < GridSize.Width && gridY >= 0 && gridY < GridSize.Height)
                {
                    int index = gridY * GridSize.Width + gridX;
                    if (index < CurrentPixels.Length)
                    {
                        PixelData hitPixel = CurrentPixels[index];
                        if (!PixelReactionUtility.IsAir(hitPixel.MaterialType))
                        {
                            spell.Lifetime = 0;

                            for (int dy = -1; dy <= 1; dy++)
                            {
                                for (int dx = -1; dx <= 1; dx++)
                                {
                                    int impactX = gridX + dx;
                                    int impactY = gridY + dy;
                                    if (impactX >= 0 && impactX < GridSize.Width
                                        && impactY >= 0 && impactY < GridSize.Height)
                                    {
                                        int impactIndex = impactY * GridSize.Width + impactX;
                                        if (impactIndex < NextPixels.Length)
                                        {
                                            PixelData impact = NextPixels[impactIndex];
                                            impact.MaterialType = (byte)MaterialType.Debris;
                                            impact.Color = 0x8C92;
                                            impact.Flags = 0x04;
                                            NextPixels[impactIndex] = impact;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                spells[i] = spell;
            }
        }
    }
}
