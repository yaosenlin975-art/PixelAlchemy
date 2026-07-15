// 职责：法术能力集合，管理玩家当前装备的法术能力组。
// Responsibility: Spell ability set managing the player's currently equipped ability group.
using System.Collections.Generic;
using UnityEngine;

namespace AOT
{
    public sealed class PixelAbilitySet : ScriptableObject
    {
        [SerializeField] private List<PixelAbility> _abilities;

        public IReadOnlyList<PixelAbility> Abilities
        {
            get { return _abilities; }
        }

        public PixelAbility GetAbility(int index)
        {
            if (_abilities != null && index >= 0 && index < _abilities.Count)
            {
                return _abilities[index];
            }

            return null;
        }

        public int AbilityCount
        {
            get { return _abilities != null ? _abilities.Count : 0; }
        }
    }
}
