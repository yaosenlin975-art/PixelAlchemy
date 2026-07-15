using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace AOT
{
    /// <summary>
    /// IJobChunk for pixel interaction simulation.
    /// Handles temperature conduction, reactions, and phase changes.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    public struct InteractionJob : IJobChunk
    {
        [ReadOnly] public NativeArray<PixelData> CurrentPixels;
        public NativeArray<PixelData> NextPixels;
        public short AmbientTemp;
        public NativeReference<Xorshift128Plus> RngState;

        public void Execute(in ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var pixels = chunk.GetNativeArray<PixelData>(SystemAPI.GetTypeHandle<PixelData>(true));
            if (!pixels.IsCreated || pixels.Length == 0)
                return;

            var rng = RngState.Value;

            // Simple temperature conduction and phase change
            // Expand to full material interaction system in Phase 2+ implementation
            for (int i = 0; i < pixels.Length; i++)
            {
                PixelData current = CurrentPixels[i];
                if (current.MaterialType == 0)
                    continue;

                PixelData result = current;

                // Temperature equilibration toward ambient
                if (result.Temperature != AmbientTemp)
                {
                    short diff = (short)((AmbientTemp - result.Temperature) >> 2);
                    result.Temperature += diff;
                }

                // Simple phase change at temperature thresholds
                if (result.MaterialType == 2 && result.Temperature < -100) // Water → Ice at -1°C
                {
                    result.MaterialType = 9; // Ice
                }
                else if (result.MaterialType == 9 && result.Temperature > 0) // Ice → Water at 0°C
                {
                    result.MaterialType = 2; // Water
                }
                else if (result.MaterialType == 2 && result.Temperature > 10000) // Water → Steam at 100°C
                {
                    result.MaterialType = 3; // Smoke/Steam
                    result.Density = 10;     // Gas density
                }
                else if ((result.MaterialType == 4 || result.MaterialType == 10) && // Fire/Lava
                         SystemAPI.HasComponent<MaterialDefinition>(SystemAPI.GetComponent<Entity>(i)))
                {
                    // Material-based reaction logic (placeholder for full implementation)
                }

                NextPixels[i] = result;
            }

            RngState.Value = rng;
        }
    }
}
