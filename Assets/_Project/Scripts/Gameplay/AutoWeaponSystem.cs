using System;
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
        private Func<Vector2, Vector3> _projectileSpawnResolver;
        private EnemyController _currentTarget;
        private Vector2 _lastAimDirection = Vector2.right;

        private float _cooldown;

        public event Action<Vector2> AimUpdated;
        public event Action<Vector2> Fired;

        public void Initialize(
            WeaponConfig config,
            Transform owner,
            EnemyRegistry registry,
            PlayerStatsRuntime stats,
            Func<Vector2, Vector3> projectileSpawnResolver = null)
        {
            _config = config;
            _owner = owner;
            _registry = registry;
            _stats = stats;
            _projectileSpawnResolver = projectileSpawnResolver;
            _currentTarget = null;
            _lastAimDirection = Vector2.right;
            _cooldown = 0f;
        }

        private void Update()
        {
            if (_config == null || _owner == null || _registry == null || _stats == null)
            {
                return;
            }

            var attackCooldown = Mathf.Max(0.05f, _config.attackInterval * _stats.AttackIntervalMultiplier);

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f)
            {
                return;
            }

            RefreshAimTarget();

            if (!IsTargetUsable(_currentTarget))
            {
                return;
            }

            FireAt(_currentTarget.transform.position);
            _cooldown = attackCooldown;
        }

        private void RefreshAimTarget()
        {
            if (_registry == null || _owner == null || _config == null)
            {
                return;
            }

            _currentTarget = _registry.FindNearest(_owner.position, _config.attackRange);
            if (_currentTarget == null)
            {
                return;
            }

            var nextDirection = (Vector2)(_currentTarget.transform.position - _owner.position);
            if (nextDirection.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            _lastAimDirection = nextDirection.normalized;
            AimUpdated?.Invoke(_lastAimDirection);
        }

        private bool IsTargetUsable(EnemyController target)
        {
            if (target == null || _owner == null || _config == null)
            {
                return false;
            }

            var maxDistance = Mathf.Max(0.01f, _config.attackRange);
            return (target.transform.position - _owner.position).sqrMagnitude <= maxDistance * maxDistance;
        }

        private void FireAt(Vector3 targetPosition)
        {
            var nextDirection = (Vector2)(targetPosition - _owner.position);
            if (nextDirection.sqrMagnitude > 0.000001f)
            {
                _lastAimDirection = nextDirection.normalized;
            }

            var projectileObject = new GameObject("Projectile");
            var projectileSpawnPosition = _owner.position;
            if (_projectileSpawnResolver != null)
            {
                projectileSpawnPosition = _projectileSpawnResolver(_lastAimDirection);
            }

            projectileObject.transform.position = projectileSpawnPosition;

            var renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(1f, 0.95f, 0.35f);
            projectileObject.transform.localScale = Vector3.one * 0.25f;

            var projectile = projectileObject.AddComponent<Projectile>();
            var damage = _config.projectileDamage * _stats.DamageMultiplier;
            projectile.Initialize(_registry, _lastAimDirection, _config.projectileSpeed, damage, _config.projectileLifetime, _config.projectileHitRadius);
            Fired?.Invoke(_lastAimDirection);
        }
    }
}
