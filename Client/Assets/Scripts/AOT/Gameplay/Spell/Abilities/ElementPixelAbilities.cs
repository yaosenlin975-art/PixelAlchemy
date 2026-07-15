// 职责：元素法术具体实现，包含火/冰/毒三种基本元素法术。
// Responsibility: Elemental spell implementations for fire, frost, and poison spell types.
using Unity.Entities;
using UnityEngine;

namespace AOT
{
    public sealed class ElementPixelAbilities : PixelAbility
    {
        public enum ElementType
        {
            Fire,
            Frost,
            Poison
        }

        [SerializeField] private ElementType _elementType;
        [SerializeField] private byte _damage = 1;
        [SerializeField] private GameObject _projectilePrefab;

        public override void Activate(SpellController controller, Vector2 origin, Vector2 direction, Entity caster)
        {
            byte spellType = (byte)_elementType;
            controller.CastProjectile(origin, direction, spellType, DamageValue, caster);
        }

        public ElementType SpellElement
        {
            get { return _elementType; }
        }

        public byte DamageValue
        {
            get { return _damage; }
        }
    }
}
