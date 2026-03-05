using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class AutoWeaponSystem : MonoBehaviour
    {
        [SerializeField, Min(0)] private int projectilePoolPrewarmCount = 32;
        [SerializeField, Min(0.01f)] private float targetScanInterval = 0.08f;

        private WeaponConfig _config;
        private Transform _owner;
        private EnemyRegistry _registry;
        private PlayerStatsRuntime _stats;
        private Func<Vector2, Vector3> _projectileSpawnResolver;
        private EnemyController _currentTarget;
        private Vector2 _lastAimDirection = Vector2.right;

        private float _cooldown;
        private float _targetScanCooldown;
        private readonly Queue<Projectile> _projectilePool = new();
        private Transform _projectilePoolRoot;

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
            _targetScanCooldown = 0f;

            EnsureProjectilePool();
        }

        private void Update()
        {
            if (_config == null || _owner == null || _registry == null || _stats == null)
            {
                return;
            }

            var attackCooldown = Mathf.Max(0.05f, _config.attackInterval * _stats.AttackIntervalMultiplier);
            _targetScanCooldown -= Time.deltaTime;

            if (_targetScanCooldown <= 0f || !IsTargetUsable(_currentTarget))
            {
                _targetScanCooldown = Mathf.Max(0.01f, targetScanInterval);
                _currentTarget = _registry.FindNearest(_owner.position, _config.attackRange);
            }

            RefreshAimDirectionFromCurrentTarget();

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f)
            {
                return;
            }

            if (!IsTargetUsable(_currentTarget))
            {
                return;
            }

            FireAt(_currentTarget.transform.position);
            _cooldown = attackCooldown;
        }

        private void RefreshAimDirectionFromCurrentTarget()
        {
            if (_registry == null || _owner == null || _config == null)
            {
                return;
            }

            if (!IsTargetUsable(_currentTarget))
            {
                return;
            }

            var nextDirection = (Vector2)(_currentTarget.transform.position - _owner.position);
            if (nextDirection.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            nextDirection.Normalize();
            if (Vector2.Dot(_lastAimDirection, nextDirection) >= 0.9998f)
            {
                return;
            }

            _lastAimDirection = nextDirection;
            AimUpdated?.Invoke(nextDirection);
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

            var projectileSpawnPosition = _owner.position;
            if (_projectileSpawnResolver != null)
            {
                projectileSpawnPosition = _projectileSpawnResolver(_lastAimDirection);
            }

            var projectile = GetPooledProjectile();
            var projectileObject = projectile.gameObject;
            projectileObject.transform.SetPositionAndRotation(projectileSpawnPosition, Quaternion.identity);
            var damage = _config.projectileDamage * _stats.DamageMultiplier;
            projectile.Initialize(
                _registry,
                _lastAimDirection,
                _config.projectileSpeed,
                damage,
                _config.projectileLifetime,
                _config.projectileHitRadius,
                ReturnProjectileToPool);
            Fired?.Invoke(_lastAimDirection);
        }

        private void EnsureProjectilePool()
        {
            if (_projectilePoolRoot == null)
            {
                var root = new GameObject("ProjectilePool");
                root.transform.SetParent(transform, false);
                _projectilePoolRoot = root.transform;
            }

            var targetCount = Mathf.Max(0, projectilePoolPrewarmCount);
            while (_projectilePool.Count < targetCount)
            {
                var projectile = CreateProjectileInstance();
                ReturnProjectileToPool(projectile);
            }
        }

        private Projectile GetPooledProjectile()
        {
            while (_projectilePool.Count > 0)
            {
                var pooled = _projectilePool.Dequeue();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            var created = CreateProjectileInstance();
            created.gameObject.SetActive(true);
            return created;
        }

        private Projectile CreateProjectileInstance()
        {
            var projectileObject = new GameObject("Projectile");
            projectileObject.transform.SetParent(_projectilePoolRoot, false);
            projectileObject.transform.localScale = Vector3.one * 0.25f;

            var renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(1f, 0.95f, 0.35f);

            var projectile = projectileObject.AddComponent<Projectile>();
            projectileObject.SetActive(false);
            return projectile;
        }

        private void ReturnProjectileToPool(Projectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            var projectileObject = projectile.gameObject;
            projectileObject.SetActive(false);
            projectileObject.transform.SetParent(_projectilePoolRoot, false);
            _projectilePool.Enqueue(projectile);
        }
    }
}
