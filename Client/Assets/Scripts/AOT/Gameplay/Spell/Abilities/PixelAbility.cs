// 职责：法术抽象基类，定义法术行为和效果接口。
// Responsibility: Abstract spell base class defining spell behavior and effect interfaces.
using UnityEngine;

namespace AOT
{
    public abstract class PixelAbility : ScriptableObject
    {
        [SerializeField] private string _abilityName;
        [SerializeField] private Color _abilityColor = Color.white;
        [SerializeField] private float _cooldown = 1f;

        public string AbilityName
        {
            get { return _abilityName; }
        }

        public Color AbilityColor
        {
            get { return _abilityColor; }
        }

        public float Cooldown
        {
            get { return _cooldown; }
        }

        public abstract void Activate(SpellController controller, Vector2 origin, Vector2 direction, Entity caster);

        public virtual void OnUpdate(SpellController controller) { }
    }
}
