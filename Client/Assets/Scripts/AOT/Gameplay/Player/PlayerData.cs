// 职责：玩家数据 DOTS 组件，包含位置/速度/血量/法术槽/操作标志。
// Responsibility: Player data DOTS component with position, velocity, health, spell slots, and action flags.
using Unity.Entities;
using Unity.Mathematics;

namespace AOT
{
    public struct PlayerData : IComponentData
    {
        public Fix64Vec2 Position;
        public Fix64Vec2 Velocity;
        public short Health;
        public short MaxHealth;
        public int4 SpellIds;
        public byte SelectedSpellSlot;
        public ushort ActionFlags;
        public ushort AimAngle;
    }
}
