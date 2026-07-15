// 职责：玩家移动并行 Job，处理输入驱动的移动、像素碰撞检测和场地边界判定。
// Responsibility: Parallel player movement job handling input-driven movement, pixel collision, and arena boundary checks.
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace AOT
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.Standard)]
    public struct PlayerMovementJob : IJobChunk
    {
        public ComponentLookup<PlayerData> PlayerLookup;
        [ReadOnly] public ComponentLookup<PixelData> PixelLookup;
        [ReadOnly] public NativeArray<PixelData> CurrentPixels;
        public NativeArray<PixelData> NextPixels;
        public GridSize GridSize;
        public Fix64 DeltaTime;

        public void Execute(in ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            NativeArray<PlayerData> players = chunk.GetNativeArray<PlayerData>(
                TypeManager.GetTypeIndex<PlayerData>());

            for (int i = 0; i < players.Length; i++)
            {
                PlayerData player = players[i];
                ApplyInput(ref player);
                ApplyPixelCollision(ref player);
                ApplyArenaBoundary(ref player);

                players[i] = player;
            }
        }

        private void ApplyInput(ref PlayerData player)
        {
            ushort flags = player.ActionFlags;
            Fix64 speed = Fix64.FromFloat(3.0f);
            Fix64Vec2 velocity = new Fix64Vec2(Fix64.FromFloat(0), Fix64.FromFloat(0));

            if ((flags & 0x0001) != 0)
                velocity.X = -speed;
            if ((flags & 0x0002) != 0)
                velocity.X = speed;
            if ((flags & 0x0004) != 0)
                velocity.Y = speed;
            if ((flags & 0x0008) != 0)
                velocity.Y = -speed;

            player.Velocity = velocity;
            player.Position = player.Position + velocity * DeltaTime;
        }

        private void ApplyPixelCollision(ref PlayerData player)
        {
            Fix64 px = player.Position.X;
            Fix64 py = player.Position.Y;

            int gridX = px.ToFloat() >= 0 ? (int)px.ToFloat() : 0;
            int gridY = py.ToFloat() >= 0 ? (int)py.ToFloat() : 0;

            if (gridX < 0 || gridX >= GridSize.Width || gridY < 0 || gridY >= GridSize.Height)
                return;

            int index = gridY * GridSize.Width + gridX;
            if (index < CurrentPixels.Length)
            {
                PixelData pixel = CurrentPixels[index];
                if (!PixelReactionUtility.IsAir(pixel.MaterialType))
                {
                    player.Velocity = new Fix64Vec2(Fix64.FromFloat(0), Fix64.FromFloat(0));
                }
            }
        }

        private void ApplyArenaBoundary(ref PlayerData player)
        {
            Fix64 minX = Fix64.FromFloat(0);
            Fix64 maxX = Fix64.FromFloat(GridSize.Width);
            Fix64 minY = Fix64.FromFloat(0);
            Fix64 maxY = Fix64.FromFloat(GridSize.Height);

            if (player.Position.X < minX)
                player.Position.X = minX;
            if (player.Position.X > maxX)
                player.Position.X = maxX;
            if (player.Position.Y < minY)
                player.Position.Y = minY;
            if (player.Position.Y > maxY)
                player.Position.Y = maxY;
        }
    }
}
