using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class Projectile : MonoBehaviour
    {
        private const float FireballExplosionRadius = 1.05f;
        private const float FireballExplosionDamageMultiplier = 0.4f;
        private const float FireballBurnDuration = 2.5f;
        private const float FireballBurnTickInterval = 0.5f;
        private const float FireballBurnDamageMultiplier = 0.32f;
        private const float FireballExplosionMinorStunDuration = 0.04f;
        private const float FireballExplosionFxScaleMultiplier = 6f;
        private const float FireballExplosionFxDuration = 0.4f;
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
        private Action<Projectile> _releaseToPool;
        private Action _directHitCallback;
        private bool _isActive;
        private bool _useBoundsCulling;
        private Rect _bounds;
        private Transform _damageSourceTransform;
        private PlayerBuildRuntime _build;
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
            Action<Projectile> releaseToPool,
            Action directHitCallback = null,
            bool useBoundsCulling = false,
            Rect bounds = default,
            Transform damageSourceTransform = null,
            PlayerBuildRuntime build = null)
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
            _releaseToPool = releaseToPool;
            _directHitCallback = directHitCallback;
            _useBoundsCulling = useBoundsCulling;
            _bounds = bounds;
            _damageSourceTransform = damageSourceTransform;
            _build = build;
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
                    if (_sourceWeaponId == WeaponUpgradeId.FireCharm)
                    {
                        TriggerFireballExplosion(transform.position);
                    }
                    else
                    {
                        enemy.ReceiveWeaponDamage(ApplyContextualDamageModifiers(_currentDamage, enemy), _sourceWeaponId);
                        _directHitCallback?.Invoke();
                    }
                }
                finally
                {
                    _hitEnemies.Add(enemy);
                    _remainingHits--;
                }

                if (_sourceWeaponId == WeaponUpgradeId.FireCharm)
                {
                    Release();
                    return;
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

        private void TriggerFireballExplosion(Vector3 center)
        {
            var fxParent = transform.parent != null ? transform.parent : transform;
            WeaponFxRenderer.SpawnFireBurstFx(
                fxParent,
                center,
                Mathf.Max(0.1f, FireballExplosionRadius * FireballExplosionFxScaleMultiplier),
                FireballExplosionFxDuration,
                530,
                "FireballExplosionFx");

            if (_registry == null)
            {
                return;
            }

            var searchRadius = FireballExplosionRadius + _registry.GetMaxCollisionRadius();
            _registry.GetNearby(center, searchRadius, _nearbyEnemies);
            var burnDamage = _baseDamage * FireballBurnDamageMultiplier;
            var explosionDamage = _baseDamage * FireballExplosionDamageMultiplier;
            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                var limit = FireballExplosionRadius + enemy.CollisionRadius;
                if ((enemy.transform.position - center).sqrMagnitude > limit * limit)
                {
                    continue;
                }

                enemy.ReceiveWeaponDamage(ApplyContextualDamageModifiers(explosionDamage, enemy), _sourceWeaponId);
                _directHitCallback?.Invoke();
                enemy.ApplyMinorStun(FireballExplosionMinorStunDuration);
                enemy.ApplyBurn(burnDamage, FireballBurnDuration, FireballBurnTickInterval);
            }
        }

        private float ApplyContextualDamageModifiers(float damage, EnemyController enemy)
        {
            if (_build == null || enemy == null)
            {
                return Mathf.Max(0f, damage);
            }

            var attackerPosition = _damageSourceTransform != null
                ? _damageSourceTransform.position
                : transform.position;
            return Mathf.Max(0f, damage) * _build.GetContextualDamageMultiplier(enemy, attackerPosition);
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
