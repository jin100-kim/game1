using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class Projectile : MonoBehaviour
    {
        private EnemyRegistry _registry;
        private Vector3 _direction;
        private float _speed;
        private float _baseDamage;
        private float _currentDamage;
        private float _minimumDamage;
        private float _lifetime;
        private float _hitRadius;
        private float _damageFalloffPerHit;
        private int _remainingHits;
        private WeaponUpgradeId _sourceWeaponId;
        private WeaponCoreElement _sourceCoreElement;
        private int _sourceCoreLevel;
        private Action<Projectile> _releaseToPool;
        private bool _isActive;
        private bool _useBoundsCulling;
        private Rect _bounds;
        private readonly List<EnemyController> _nearbyEnemies = new(16);
        private readonly List<EnemyController> _hitEnemies = new(8);
        private const float BoundsCullMargin = 0.15f;

        public void Initialize(
            EnemyRegistry registry,
            Vector3 direction,
            float speed,
            float damage,
            float lifetime,
            float hitRadius,
            int maxHits,
            float damageFalloffPerHit,
            float minimumDamageMultiplier,
            WeaponUpgradeId sourceWeaponId,
            WeaponCoreElement sourceCoreElement,
            int sourceCoreLevel,
            Action<Projectile> releaseToPool,
            bool useBoundsCulling = false,
            Rect bounds = default)
        {
            _registry = registry;
            _direction = direction.normalized;
            _speed = speed;
            _baseDamage = Mathf.Max(0f, damage);
            _currentDamage = _baseDamage;
            _minimumDamage = _baseDamage * Mathf.Clamp(minimumDamageMultiplier, 0.05f, 1f);
            _lifetime = lifetime;
            _hitRadius = hitRadius;
            _remainingHits = Mathf.Max(1, maxHits);
            _damageFalloffPerHit = Mathf.Clamp01(damageFalloffPerHit);
            _sourceWeaponId = sourceWeaponId;
            _sourceCoreElement = sourceCoreElement;
            _sourceCoreLevel = Mathf.Max(0, sourceCoreLevel);
            _releaseToPool = releaseToPool;
            _useBoundsCulling = useBoundsCulling;
            _bounds = bounds;
            _isActive = true;
            _hitEnemies.Clear();
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            transform.position += _direction * _speed * Time.deltaTime;

            if (_useBoundsCulling && IsOutOfBounds(transform.position))
            {
                Release();
                return;
            }

            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                Release();
                return;
            }

            if (_registry == null || _remainingHits <= 0 || _currentDamage <= 0f)
            {
                return;
            }

            var searchRadius = _hitRadius + _registry.GetMaxCollisionRadius();
            _registry.GetNearby((Vector2)transform.position, searchRadius, _nearbyEnemies);
            for (var i = _nearbyEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null || HasAlreadyHit(enemy))
                {
                    continue;
                }

                var hitDistance = _hitRadius + enemy.CollisionRadius;
                if ((enemy.transform.position - transform.position).sqrMagnitude > hitDistance * hitDistance)
                {
                    continue;
                }

                try
                {
                    enemy.ReceiveWeaponDamage(_currentDamage, _sourceWeaponId, _sourceCoreElement, _sourceCoreLevel);
                }
                finally
                {
                    _hitEnemies.Add(enemy);
                    _remainingHits--;
                }

                if (_remainingHits <= 0)
                {
                    Release();
                    return;
                }

                if (_damageFalloffPerHit > 0f)
                {
                    _currentDamage = Mathf.Max(_minimumDamage, _currentDamage * (1f - _damageFalloffPerHit));
                }

                if (_currentDamage <= 0f)
                {
                    Release();
                    return;
                }

                // Limit to one target per frame so piercing progresses over travel.
                return;
            }
        }

        private bool HasAlreadyHit(EnemyController enemy)
        {
            for (var i = 0; i < _hitEnemies.Count; i++)
            {
                if (ReferenceEquals(_hitEnemies[i], enemy))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDisable()
        {
            _isActive = false;
            _hitEnemies.Clear();
        }

        private void Release()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _releaseToPool?.Invoke(this);
        }

        private bool IsOutOfBounds(Vector3 worldPosition)
        {
            return worldPosition.x < _bounds.xMin - BoundsCullMargin
                || worldPosition.x > _bounds.xMax + BoundsCullMargin
                || worldPosition.y < _bounds.yMin - BoundsCullMargin
                || worldPosition.y > _bounds.yMax + BoundsCullMargin;
        }
    }
}
