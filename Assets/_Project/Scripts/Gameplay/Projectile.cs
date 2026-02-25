using EJR.Game.Core;
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

        public void Initialize(
            EnemyRegistry registry,
            Vector3 direction,
            float speed,
            float damage,
            float lifetime,
            float hitRadius)
        {
            _registry = registry;
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _lifetime = lifetime;
            _hitRadius = hitRadius;
        }

        private void Update()
        {
            transform.position += _direction * _speed * Time.deltaTime;
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_registry == null)
            {
                return;
            }

            var enemies = _registry.Enemies;
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
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
                        // Always remove projectile on first hit so runtime errors cannot create pseudo-piercing.
                        Destroy(gameObject);
                    }

                    return;
                }
            }
        }
    }
}
