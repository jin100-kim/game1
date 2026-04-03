using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemyVariantProjectile : MonoBehaviour
    {
        private Vector2 _direction;
        private float _speed;
        private float _lifetime;
        private float _damage;
        private float _hitRadius;
        private float _playerCollisionRadius;
        private PlayerHealth _targetPlayer;

        public void Initialize(
            Vector2 direction,
            float speed,
            float lifetime,
            float damage,
            float hitRadius,
            PlayerHealth targetPlayer,
            float playerCollisionRadius)
        {
            _direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            _speed = Mathf.Max(0.1f, speed);
            _lifetime = Mathf.Max(0.05f, lifetime);
            _damage = Mathf.Max(0f, damage);
            _hitRadius = Mathf.Max(0.02f, hitRadius);
            _targetPlayer = targetPlayer;
            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
        }

        private void Update()
        {
            if (DebugSessionService.IsMonsterLabTimePaused)
            {
                return;
            }

            transform.position += new Vector3(_direction.x, _direction.y, 0f) * (_speed * Time.deltaTime);
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_targetPlayer == null || _damage <= 0f)
            {
                return;
            }

            var hitLimit = _hitRadius + _playerCollisionRadius;
            var playerPos = (Vector2)_targetPlayer.transform.position;
            var projectilePos = (Vector2)transform.position;
            if ((playerPos - projectilePos).sqrMagnitude > hitLimit * hitLimit)
            {
                return;
            }

            _targetPlayer.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
}
