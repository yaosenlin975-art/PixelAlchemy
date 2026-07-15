// 职责：法术数据 DOTS 组件，包含位置/速度/类型/寿命/伤害/施法者。
// Responsibility: Spell data DOTS component with position, velocity, type, lifetime, damage, and caster.
using Unity.Entities;

namespace AOT
{
    public struct SpellData : IComponentData
    {
        public Fix64Vec2 Position;
        public Fix64Vec2 Velocity;
        public byte SpellType;
        public short Lifetime;
        public byte Damage;
        public Entity CasterEntity;
    }
}
