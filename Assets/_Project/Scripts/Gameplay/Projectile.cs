using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class Projectile : MonoBehaviour
    {
        private const float FireballExplosionRadius = 0.8f;
        private const float FireballExplosionDamageMultiplier = 0.4f;
        private const float FireballExplosionMinorStunDuration = 0.04f;
        private const float FireballExplosionFxScaleMultiplier = 3.0f;
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
        private Action<float, EnemyController> _directHitCallback;
        private bool _isActive;
        private bool _useBoundsCulling;
        private Rect _bounds;
        private Transform _damageSourceTransform;
        private PlayerBuildRuntime _build;
        private readonly List<EnemyController> _nearbyEnemies = new(16);
        private readonly List<EnemyController> _hitEnemies = new(8);
        private const float BoundsCullMargin = 8.0f;

        // Homing properties
        private EnemyController _homingTarget;
        private float _homingTurnSpeed;

        // Boomerang properties
        private bool _isBoomerang;
        private bool _isReturning;
        private float _elapsedTime;
        private float _totalLifetime;

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
            _elapsedTime = 0f;
            _totalLifetime = lifetime;
            _isReturning = false;
            _isBoomerang = (sourceWeaponId == WeaponUpgradeId.WindBlade);

            _hitEnemies.Clear();
            _homingTarget = null;
            _homingTurnSpeed = 0f;

            UpdateRotation();
        }

        public void SetHoming(EnemyController target, float turnSpeed)
        {
            _homingTarget = target;
            _homingTurnSpeed = turnSpeed;
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            float dt = Time.deltaTime;
            _elapsedTime += dt;

            // Boomerang Logic
            if (_isBoomerang)
            {
                if (!_isReturning && _elapsedTime >= _totalLifetime * 0.5f)
                {
                    _isReturning = true;
                    _hitEnemies.Clear(); // 돌아올 때 재타격 가능하도록 리스트 비우기
                }

                if (_isReturning && _damageSourceTransform != null)
                {
                    Vector3 toPlayer = (_damageSourceTransform.position - transform.position).normalized;
                    if (toPlayer.sqrMagnitude > 0.001f)
                    {
                        // 시계방향 회전을 위한 편향 벡터 계산 (진행 방향의 오른쪽)
                        Vector3 sideBias = new Vector3(_direction.y, -_direction.x, 0f);
                        Vector3 biasedTarget = Vector3.Lerp(toPlayer, sideBias, 0.01f).normalized;

                        _direction = Vector3.Lerp(_direction, biasedTarget, dt * 25f).normalized;
                        UpdateRotation();
                    }

                    // 플레이어 근처에 오면 회수
                    if (Vector3.Distance(transform.position, _damageSourceTransform.position) < 0.4f)
                    {
                        Release();
                        return;
                    }
                }
            }
            else if (_homingTarget != null && _homingTarget.isActiveAndEnabled && !_homingTarget.IsDead)
            {
                Vector3 targetPos = _homingTarget.transform.position;
                Vector3 targetDir = (targetPos - transform.position).normalized;
                
                if (targetDir.sqrMagnitude > 0.001f)
                {
                    _direction = Vector3.RotateTowards(_direction, targetDir, _homingTurnSpeed * Mathf.Deg2Rad * dt, 0f).normalized;
                    UpdateRotation();
                }
            }

            transform.position += _direction * _speed * dt;

            if (_useBoundsCulling && IsOutOfBounds(transform.position))
            {
                Release();
                return;
            }


            if (!_isReturning)
            {
                _lifetime -= dt;
            }

            if (_lifetime <= 0f && !_isReturning)
            {
                float overshoot = -_lifetime;
                float correction = Mathf.Max(0f, dt - overshoot);
                if (correction > 0)
                {
                    transform.position += _direction * _speed * correction;
                }
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
                    if (_sourceWeaponId == WeaponUpgradeId.Fireball)
                    {
                        var appliedDamage = ApplyContextualDamageModifiers(_currentDamage, enemy);
                        enemy.ReceiveWeaponDamage(appliedDamage, _sourceWeaponId);
                        _directHitCallback?.Invoke(appliedDamage, enemy);
                        TriggerFireballExplosion(transform.position, enemy);
                    }
                    else if (_sourceWeaponId == WeaponUpgradeId.LightningBolt)
                    {
                        var appliedDamage = ApplyContextualDamageModifiers(_currentDamage, enemy);
                        enemy.ReceiveWeaponDamage(appliedDamage, _sourceWeaponId);
                        _directHitCallback?.Invoke(appliedDamage, enemy);
                        WeaponFxRenderer.SpawnPrefabFx(
                            "VFX/LightningBolt/VFX_2D_Projectile_Lightning_Impact_01_Color_Static",
                            transform.position,
                            Quaternion.identity,
                            Vector3.one * 2.0f,
                            0.5f,
                            550);
                    }
                    else if (_sourceWeaponId == WeaponUpgradeId.IceSpike)
                    {
                        var appliedDamage = ApplyContextualDamageModifiers(_currentDamage, enemy);
                        enemy.ReceiveWeaponDamage(appliedDamage, _sourceWeaponId);
                        enemy.ApplySlow(0.5f, 1.5f); // 슬로우 효과 추가
                        _directHitCallback?.Invoke(appliedDamage, enemy);
                        WeaponFxRenderer.SpawnPrefabFx(
                            "VFX/IceSpike/VFX_2D_Projectile_Ice_Impact_01_Color_Static",
                            transform.position,
                            Quaternion.identity,
                            Vector3.one * 2.0f,
                            0.5f,
                            550);
                    }
                    else if (_sourceWeaponId == WeaponUpgradeId.WindBlade)
                    {
                        var appliedDamage = ApplyContextualDamageModifiers(_currentDamage, enemy);
                        enemy.ReceiveWeaponDamage(appliedDamage, _sourceWeaponId);
                        enemy.ApplyKnockback(_direction, 5.0f); // 넉백 효과 추가
                        _directHitCallback?.Invoke(appliedDamage, enemy);
                        WeaponFxRenderer.SpawnPrefabFx(
                            "VFX/WindBlade/VFX_2D_Projectile_Wind_Impact_01_Color_Static",
                            transform.position,
                            Quaternion.identity,
                            Vector3.one * 2.0f,
                            0.5f,
                            550);
                    }
                    else
                    {
                        var appliedDamage = ApplyContextualDamageModifiers(_currentDamage, enemy);
                        enemy.ReceiveWeaponDamage(appliedDamage, _sourceWeaponId);
                        _directHitCallback?.Invoke(appliedDamage, enemy);
                    }
                }
                finally
                {
                    _hitEnemies.Add(enemy);
                    _remainingHits--;
                }

                if (_sourceWeaponId == WeaponUpgradeId.Fireball)
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

                return;
            }
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

        private void TriggerFireballExplosion(Vector3 center, EnemyController directTarget)
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
            var explosionDamage = _baseDamage * FireballExplosionDamageMultiplier;
            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null || enemy == directTarget)
                {
                    continue;
                }

                var limit = FireballExplosionRadius + enemy.CollisionRadius;
                if ((enemy.transform.position - center).sqrMagnitude > limit * limit)
                {
                    continue;
                }

                enemy.ReceiveWeaponDamage(ApplyContextualDamageModifiers(explosionDamage, enemy), _sourceWeaponId);
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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_isActive) return;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _hitRadius);

            if (_sourceWeaponId == WeaponUpgradeId.Fireball)
            {
                Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.3f);
                Gizmos.DrawWireSphere(transform.position, FireballExplosionRadius);
            }
        }
#endif
    }
}
