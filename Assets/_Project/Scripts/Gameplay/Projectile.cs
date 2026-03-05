using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class Projectile : MonoBehaviour
    {
        private EnemyRegistry _registry;
        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private float _lifetime;
        private float _hitRadius;
        private Action<Projectile> _releaseToPool;
        private bool _isActive;
        private readonly System.Collections.Generic.List<EnemyController> _nearbyEnemies = new(16);

        public void Initialize(
            EnemyRegistry registry,
            Vector3 direction,
            float speed,
            float damage,
            float lifetime,
            float hitRadius,
            Action<Projectile> releaseToPool)
        {
            _registry = registry;
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _lifetime = lifetime;
            _hitRadius = hitRadius;
            _releaseToPool = releaseToPool;
            _isActive = true;
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

            if (_registry == null)
            {
                return;
            }

            var searchRadius = _hitRadius + _registry.GetMaxCollisionRadius();
            _registry.GetNearby((Vector2)transform.position, searchRadius, _nearbyEnemies);
            for (var i = _nearbyEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                var hitDistance = _hitRadius + enemy.CollisionRadius;
                if ((enemy.transform.position - transform.position).sqrMagnitude <= hitDistance * hitDistance)
                {
                    try
                    {
                        enemy.ReceiveDamage(_damage);
                    }
                    finally
                    {
                        // Always release projectile on first hit so runtime errors cannot create pseudo-piercing.
                        Release();
                    }

                    return;
                }
            }
        }

        private void OnDisable()
        {
            _isActive = false;
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
