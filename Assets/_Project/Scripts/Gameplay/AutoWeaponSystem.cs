using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class AutoWeaponSystem : MonoBehaviour
    {
        private WeaponConfig _config;
        private Transform _owner;
        private EnemyRegistry _registry;
        private PlayerStatsRuntime _stats;

        private float _cooldown;

        public void Initialize(WeaponConfig config, Transform owner, EnemyRegistry registry, PlayerStatsRuntime stats)
        {
            _config = config;
            _owner = owner;
            _registry = registry;
            _stats = stats;
        }

        private void Update()
        {
            if (_config == null || _owner == null || _registry == null || _stats == null)
            {
                return;
            }

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f)
            {
                return;
            }

            var target = _registry.FindNearest(_owner.position, _config.attackRange);
            if (target == null)
            {
                return;
            }

            FireAt(target.transform.position);
            _cooldown = Mathf.Max(0.05f, _config.attackInterval * _stats.AttackIntervalMultiplier);
        }

        private void FireAt(Vector3 targetPosition)
        {
            var projectileObject = new GameObject("Projectile");
            projectileObject.transform.position = _owner.position;

            var renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(1f, 0.95f, 0.35f);
            projectileObject.transform.localScale = Vector3.one * 0.25f;

            var projectile = projectileObject.AddComponent<Projectile>();
            var direction = (targetPosition - _owner.position).normalized;
            var damage = _config.projectileDamage * _stats.DamageMultiplier;
            projectile.Initialize(_registry, direction, _config.projectileSpeed, damage, _config.projectileLifetime, _config.projectileHitRadius);
        }
    }
}
