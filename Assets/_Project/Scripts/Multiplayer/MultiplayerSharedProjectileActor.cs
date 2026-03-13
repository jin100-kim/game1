using System.Collections.Generic;
using EJR.Game.Core;
using EJR.Game.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MultiplayerSharedProjectileActor : NetworkBehaviour
    {
        private readonly List<EnemyController> _nearbyEnemies = new(12);

        private SpriteRenderer _spriteRenderer;
        private EnemyRegistry _enemyRegistry;
        private Vector2 _direction = Vector2.right;
        private float _speed = 10f;
        private float _remainingLifetime = 2f;
        private float _damage = 12f;
        private float _hitRadius = 0.25f;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderer.sprite = RuntimeSpriteFactory.GetWeaponFire1Sprite();
            _spriteRenderer.color = new Color(1f, 0.84f, 0.25f, 0.95f);
            _spriteRenderer.sortingOrder = 22;
            transform.localScale = Vector3.one * 0.35f;
        }

        public void InitializeServer(
            MultiplayerCoopController coopController,
            Vector2 direction,
            float speed,
            float lifetime,
            float damage,
            float hitRadius,
            ulong ownerClientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            _enemyRegistry = coopController != null ? coopController.EnemyRegistry : null;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _speed = Mathf.Max(0.1f, speed);
            _remainingLifetime = Mathf.Max(0.05f, lifetime);
            _damage = Mathf.Max(0f, damage);
            _hitRadius = Mathf.Max(0.05f, hitRadius);
            ApplyOwnerTint(ownerClientId);
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            transform.position += new Vector3(_direction.x, _direction.y, 0f) * (_speed * Time.deltaTime);
            _remainingLifetime -= Time.deltaTime;
            if (_remainingLifetime <= 0f)
            {
                NetworkObject.Despawn(true);
                return;
            }

            if (_enemyRegistry == null || _damage <= 0f)
            {
                return;
            }

            var searchRadius = _hitRadius + _enemyRegistry.GetMaxCollisionRadius();
            _enemyRegistry.GetNearby(transform.position, searchRadius, _nearbyEnemies);
            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                var combinedRadius = _hitRadius + enemy.CollisionRadius;
                if ((enemy.transform.position - transform.position).sqrMagnitude > combinedRadius * combinedRadius)
                {
                    continue;
                }

                enemy.ReceiveWeaponDamage(_damage, WeaponUpgradeId.Rifle, WeaponCoreElement.None, 0);
                NetworkObject.Despawn(true);
                return;
            }
        }

        private void ApplyOwnerTint(ulong ownerClientId)
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            var tint = ((int)(ownerClientId % 4)) switch
            {
                1 => new Color(0.65f, 0.9f, 1f, 0.95f),
                2 => new Color(1f, 0.7f, 0.78f, 0.95f),
                3 => new Color(0.7f, 1f, 0.8f, 0.95f),
                _ => new Color(1f, 0.84f, 0.25f, 0.95f),
            };

            _spriteRenderer.color = tint;
        }
    }
}
