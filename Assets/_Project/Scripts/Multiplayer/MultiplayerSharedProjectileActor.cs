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
        private readonly List<EnemyController> _hitEnemies = new(8);

        private SpriteRenderer _spriteRenderer;
        private EnemyRegistry _enemyRegistry;
        private Vector2 _direction = Vector2.right;
        private float _speed = 10f;
        private float _remainingLifetime = 2f;
        private float _currentDamage = 12f;
        private float _minimumDamage = 12f;
        private float _hitRadius = 0.25f;
        private float _damageFalloffPerHit;
        private int _remainingHits = 1;
        private WeaponUpgradeId _weaponId = WeaponUpgradeId.Rifle;
        private WeaponCoreElement _coreElement = WeaponCoreElement.None;
        private int _coreLevel;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            _spriteRenderer.color = new Color(1f, 0.84f, 0.25f, 0.95f);
            _spriteRenderer.sortingOrder = 22;
            transform.localScale = Vector3.one * 0.35f;
        }

        public void InitializeServer(
            MultiplayerCoopController coopController,
            AutoWeaponSystem.ProjectileSpawnRequest request)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            _enemyRegistry = coopController != null ? coopController.EnemyRegistry : null;
            _direction = request.Direction.sqrMagnitude > 0.0001f ? request.Direction.normalized : Vector2.right;
            _speed = Mathf.Max(0.1f, request.Speed);
            _remainingLifetime = Mathf.Max(0.05f, request.Lifetime);
            _currentDamage = Mathf.Max(0f, request.Damage);
            _minimumDamage = Mathf.Max(0.01f, request.Damage * Mathf.Clamp(request.MinimumDamageMultiplier, 0.05f, 1f));
            _hitRadius = Mathf.Max(0.05f, request.HitRadius);
            _damageFalloffPerHit = Mathf.Clamp(request.DamageFalloffPerHit, 0f, 0.9f);
            _remainingHits = Mathf.Max(1, request.MaxHits);
            _weaponId = request.WeaponId;
            _coreElement = request.CoreElement;
            _coreLevel = Mathf.Max(0, request.CoreLevel);
            _hitEnemies.Clear();
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
                _spriteRenderer.color = request.Color;
            }

            transform.localScale = Vector3.one * Mathf.Max(0.05f, request.VisualScale);
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

            if (_enemyRegistry == null || _currentDamage <= 0f || _remainingHits <= 0)
            {
                return;
            }

            var searchRadius = _hitRadius + _enemyRegistry.GetMaxCollisionRadius();
            _enemyRegistry.GetNearby(transform.position, searchRadius, _nearbyEnemies);
            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null || HasAlreadyHit(enemy))
                {
                    continue;
                }

                var combinedRadius = _hitRadius + enemy.CollisionRadius;
                if ((enemy.transform.position - transform.position).sqrMagnitude > combinedRadius * combinedRadius)
                {
                    continue;
                }

                enemy.ReceiveWeaponDamage(_currentDamage, _weaponId, _coreElement, _coreLevel);
                _hitEnemies.Add(enemy);
                _remainingHits--;

                if (_remainingHits <= 0)
                {
                    NetworkObject.Despawn(true);
                    return;
                }

                if (_damageFalloffPerHit > 0f)
                {
                    _currentDamage = Mathf.Max(_minimumDamage, _currentDamage * (1f - _damageFalloffPerHit));
                }

                if (_currentDamage <= 0f)
                {
                    NetworkObject.Despawn(true);
                    return;
                }

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
    }
}
