using System;
using System.Collections.Generic;
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
        private Action<Projectile> _releaseToPool;
        private bool _isActive;
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
            Action<Projectile> releaseToPool)
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
            _releaseToPool = releaseToPool;
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
                    enemy.ReceiveDamage(_currentDamage);
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
    }
}
