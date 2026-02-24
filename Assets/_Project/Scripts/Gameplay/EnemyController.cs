using System;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemyController : MonoBehaviour
    {
        public event Action<float, float> Changed;

        private EnemyConfig _config;
        private Transform _target;
        private PlayerHealth _playerHealth;
        private EnemyRegistry _registry;
        private ExperienceSystem _experienceSystem;

        private float _health;
        private float _contactCooldown;
        private bool _isDead;

        public float MaxHealth => _config != null ? _config.maxHealth : 0f;
        public float CurrentHealth => _health;

        public void Initialize(
            EnemyConfig config,
            Transform target,
            PlayerHealth playerHealth,
            EnemyRegistry registry,
            ExperienceSystem experienceSystem)
        {
            _config = config;
            _target = target;
            _playerHealth = playerHealth;
            _registry = registry;
            _experienceSystem = experienceSystem;
            _health = config.maxHealth;
            _registry.Register(this);
            Changed?.Invoke(_health, config.maxHealth);
        }

        private void OnDisable()
        {
            if (_registry != null)
            {
                _registry.Unregister(this);
            }
        }

        private void Update()
        {
            if (_isDead || _target == null || _config == null || _playerHealth == null)
            {
                return;
            }

            var toPlayer = _target.position - transform.position;
            var direction = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector3.zero;
            transform.position += direction * _config.moveSpeed * Time.deltaTime;

            _contactCooldown -= Time.deltaTime;
            if (toPlayer.sqrMagnitude <= 0.45f && _contactCooldown <= 0f)
            {
                _contactCooldown = _config.contactDamageCooldown;
                _playerHealth.TakeDamage(_config.contactDamage);
            }
        }

        public void ReceiveDamage(float damage)
        {
            if (_isDead)
            {
                return;
            }

            var appliedDamage = Mathf.Max(0f, damage);
            if (appliedDamage <= 0f)
            {
                return;
            }

            _health = Mathf.Max(0f, _health - appliedDamage);
            CombatTextSpawner.SpawnDamage(transform.position + new Vector3(0f, 0.8f, 0f), appliedDamage, CombatTextSpawner.EnemyDamagedColor);
            Changed?.Invoke(_health, MaxHealth);

            if (_health <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;

            if (_experienceSystem != null)
            {
                _experienceSystem.SpawnOrb(transform.position, _config.experienceOnDeath);
            }

            if (_registry != null)
            {
                _registry.Unregister(this);
            }

            Destroy(gameObject);
        }
    }
}