using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class Projectile : MonoBehaviour
    {
        private const float BoundsCullMargin = 8f;

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
        private Action<float, EnemyController> _directHitCallback;
        private bool _isActive;
        private bool _useBoundsCulling;
        private Rect _bounds;
        private Transform _damageSourceTransform;
        private PlayerBuildRuntime _build;
        private bool _isFragment;
        private float _elapsedTime;
        private float _totalLifetime;
        private bool _isReturning;
        private WeaponDefinition _definition;
        private IProjectileHitBehavior _hitBehavior;
        private Func<float, EnemyController, float> _damageResolver;

        private EnemyController _homingTarget;
        private float _homingSearchTimer;
        private float _homingTurnSpeedOverride = -1f;

        private readonly List<EnemyController> _nearbyEnemies = new(16);
        private readonly List<EnemyController> _hitEnemies = new(8);

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
            Action<float, EnemyController> directHitCallback = null,
            bool useBoundsCulling = false,
            Rect bounds = default,
            Transform damageSourceTransform = null,
            PlayerBuildRuntime build = null,
            bool isFragment = false,
            EnemyController initialIgnoreTarget = null,
            WeaponDefinition definition = null,
            IProjectileHitBehavior hitBehavior = null,
            Func<float, EnemyController, float> damageResolver = null)
        {
            _registry = registry;
            _direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.right;
            _speed = Mathf.Max(0.01f, speed);
            _baseDamage = Mathf.Max(0f, damage);
            _currentDamage = _baseDamage;
            _minimumDamage = _baseDamage * Mathf.Clamp(minimumDamageMultiplier, 0.05f, 1f);
            _lifetime = Mathf.Max(0.01f, lifetime);
            _hitRadius = Mathf.Max(0.01f, hitRadius);
            _remainingHits = Mathf.Max(1, maxHits);
            _damageFalloffPerHit = Mathf.Clamp01(damageFalloffPerHit);
            _sourceWeaponId = sourceWeaponId;
            _releaseToPool = releaseToPool;
            _directHitCallback = directHitCallback;
            _useBoundsCulling = useBoundsCulling;
            _bounds = bounds;
            _damageSourceTransform = damageSourceTransform;
            _build = build;
            _isFragment = isFragment;
            _isActive = true;
            _elapsedTime = 0f;
            _totalLifetime = _lifetime;
            _isReturning = false;
            _definition = definition;
            _hitBehavior = hitBehavior ?? WeaponProjectileHitBehaviorFactory.Get(definition);
            _damageResolver = damageResolver ?? ApplyContextualDamageModifiers;

            _hitEnemies.Clear();
            if (initialIgnoreTarget != null)
            {
                _hitEnemies.Add(initialIgnoreTarget);
            }

            _homingTarget = null;
            _homingSearchTimer = 0f;
            _homingTurnSpeedOverride = -1f;
            if (_definition != null && _definition.projectileMotion == ProjectileMotionKind.Homing)
            {
                _homingTarget = _registry != null
                    ? _registry.FindNearest((Vector2)transform.position, Mathf.Max(0.01f, _definition.homingSearchRange))
                    : null;
                _homingSearchTimer = Mathf.Max(0.01f, _definition.homingRetargetInterval);
            }

            UpdateRotation();
        }

        public void SetHoming(EnemyController target, float turnSpeed)
        {
            _homingTarget = target;
            _homingTurnSpeedOverride = Mathf.Max(0f, turnSpeed);
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            var dt = Time.deltaTime;
            _elapsedTime += dt;

            UpdateBoomerangMotion(dt);
            UpdateHomingMotion(dt);

            transform.position += _direction * _speed * dt;

            if (_useBoundsCulling && IsOutOfBounds(transform.position))
            {
                Release();
                return;
            }

            if (!IsBoomerangReturning())
            {
                _lifetime -= dt;
            }

            if (_lifetime <= 0f && !IsBoomerangReturning())
            {
                var overshoot = -_lifetime;
                var correction = Mathf.Max(0f, dt - overshoot);
                if (correction > 0f)
                {
                    transform.position += _direction * _speed * correction;
                }

                Release();
                return;
            }

            ProcessHits();
        }

        private void UpdateBoomerangMotion(float dt)
        {
            if (_definition == null || _definition.projectileMotion != ProjectileMotionKind.Boomerang)
            {
                return;
            }

            if (!_isReturning && _elapsedTime >= _totalLifetime * 0.5f)
            {
                _isReturning = true;
                _hitEnemies.Clear();
            }

            if (!_isReturning || _damageSourceTransform == null)
            {
                return;
            }

            var toPlayer = (_damageSourceTransform.position - transform.position).normalized;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                var sideBias = new Vector3(_direction.y, -_direction.x, 0f);
                var biasedTarget = Vector3.Lerp(toPlayer, sideBias, 0.01f).normalized;
                _direction = Vector3.Lerp(_direction, biasedTarget, dt * Mathf.Max(0.01f, _definition.boomerangReturnLerp)).normalized;
                UpdateRotation();
            }

            if (Vector3.Distance(transform.position, _damageSourceTransform.position) < Mathf.Max(0.01f, _definition.boomerangReturnDistance))
            {
                Release();
            }
        }

        private void UpdateHomingMotion(float dt)
        {
            if (_definition == null || _definition.projectileMotion != ProjectileMotionKind.Homing || _registry == null)
            {
                return;
            }

            _homingSearchTimer -= dt;
            if (_homingSearchTimer <= 0f)
            {
                _homingSearchTimer = Mathf.Max(0.01f, _definition.homingRetargetInterval);
                _homingTarget = _registry.FindNearest(transform.position, Mathf.Max(0.01f, _definition.homingSearchRange));
            }

            if (_homingTarget == null || !_homingTarget.isActiveAndEnabled || _homingTarget.IsDead)
            {
                return;
            }

            var targetDir = (_homingTarget.transform.position - transform.position).normalized;
            if (targetDir.sqrMagnitude <= 0.001f)
            {
                return;
            }

            _direction = Vector3.RotateTowards(
                _direction,
                targetDir,
                GetHomingTurnSpeed() * Mathf.Deg2Rad * dt,
                0f).normalized;
            UpdateRotation();
        }

        private float GetHomingTurnSpeed()
        {
            return _homingTurnSpeedOverride >= 0f
                ? _homingTurnSpeedOverride
                : Mathf.Max(0f, _definition != null ? _definition.homingTurnSpeed : 0f);
        }

        private void ProcessHits()
        {
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

                ProjectileHitResult hitResult;
                try
                {
                    var context = new ProjectileHitContext(
                        _definition,
                        _sourceWeaponId,
                        enemy,
                        transform.position,
                        _direction,
                        _currentDamage,
                        _baseDamage,
                        _isFragment,
                        _registry,
                        transform.parent != null ? transform.parent : transform,
                        _damageResolver,
                        _directHitCallback,
                        _nearbyEnemies);
                    hitResult = (_hitBehavior ?? WeaponProjectileHitBehaviorFactory.Get(_definition)).OnHit(context);
                }
                finally
                {
                    _hitEnemies.Add(enemy);
                    _remainingHits--;
                }

                if (hitResult.ReleaseProjectile || _remainingHits <= 0)
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

                return;
            }
        }

        private bool IsBoomerangReturning()
        {
            return _definition != null
                && _definition.projectileMotion == ProjectileMotionKind.Boomerang
                && _isReturning;
        }

        private void UpdateRotation()
        {
            if (_direction.sqrMagnitude > 0.0001f)
            {
                var angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
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
            _homingTarget = null;
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
            _isFragment = false;
            _definition = null;
            _hitBehavior = null;
            _damageResolver = null;
            _homingTurnSpeedOverride = -1f;
            _releaseToPool?.Invoke(this);
        }

        private bool IsOutOfBounds(Vector3 worldPosition)
        {
            return worldPosition.x < _bounds.xMin - BoundsCullMargin
                || worldPosition.x > _bounds.xMax + BoundsCullMargin
                || worldPosition.y < _bounds.yMin - BoundsCullMargin
                || worldPosition.y > _bounds.yMax + BoundsCullMargin;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_isActive)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _hitRadius);

            if (_definition != null && _definition.impactBehavior == WeaponImpactBehaviorKind.FireballExplosion)
            {
                Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, _definition.explosionRadius);
            }
        }
#endif
    }
}
