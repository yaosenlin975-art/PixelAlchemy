// 职责：法术控制器 MonoBehaviour 外壳，负责法术创建/生命周期管理。
// Responsibility: Spell controller MonoBehaviour shell for spell creation and lifecycle management.
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace AOT
{
    public sealed class SpellController : MonoBehaviour
    {
        private EntityManager _entityManager;

        private void Awake()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                _entityManager = world.EntityManager;
            }
        }

        public Entity CastSpell(SpellData spell)
        {
            if (!_entityManager.IsCreated)
                return Entity.Null;

            Entity spellEntity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(spellEntity, spell);
            return spellEntity;
        }

        public void CastProjectile(Vector2 origin, Vector2 direction, byte spellType, byte damage, Entity caster)
        {
            SpellData spell = new SpellData
            {
                Position = Fix64Vec2.FromFloat(origin.x, origin.y),
                Velocity = Fix64Vec2.FromFloat(direction.x * 5f, direction.y * 5f),
                SpellType = spellType,
                Lifetime = 6000,
                Damage = damage,
                CasterEntity = caster
            };

            CastSpell(spell);
        }

        private void Update()
        {
            if (!_entityManager.IsCreated)
                return;

            EntityQuery spellQuery = _entityManager.CreateEntityQuery(typeof(SpellData));
            NativeArray<SpellData> spells = spellQuery.ToComponentDataArray<SpellData>(Allocator.Temp);
            NativeArray<Entity> spellEntities = spellQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < spells.Length; i++)
            {
                SpellData spell = spells[i];
                spell.Lifetime--;

                if (spell.Lifetime <= 0)
                {
                    _entityManager.DestroyEntity(spellEntities[i]);
                }
                else
                {
                    _entityManager.SetComponentData(spellEntities[i], spell);
                }
            }

            spells.Dispose();
            spellEntities.Dispose();
        }
    }
}
