using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        public event Action<float, float> Changed;
        public event Action Died;

        private PlayerSpriteAnimator _spriteAnimator;
        private float _damageInvulnerabilitySeconds;
        private float _invulnerableUntil = -1f;

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsInvulnerable => Time.time < _invulnerableUntil;

        public void Initialize(float maxHealth, float damageInvulnerabilitySeconds = 0f)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            _damageInvulnerabilitySeconds = Mathf.Max(0f, damageInvulnerabilitySeconds);
            _invulnerableUntil = -1f;
            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetMaxHealth(float newMaxHealth, bool healDelta)
        {
            var clampedMax = Mathf.Max(1f, newMaxHealth);
            if (Mathf.Abs(clampedMax - MaxHealth) <= 0.0001f)
            {
                return;
            }

            var previousMax = MaxHealth;
            MaxHealth = clampedMax;

            if (healDelta)
            {
                CurrentHealth = Mathf.Clamp(CurrentHealth + (MaxHealth - previousMax), 0f, MaxHealth);
            }
            else
            {
                CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);
            }

            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || CurrentHealth <= 0f || CurrentHealth >= MaxHealth)
            {
                return;
            }

            var nextHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            if (nextHealth <= CurrentHealth + 0.0001f)
            {
                return;
            }

            CurrentHealth = nextHealth;
            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public void TakeDamage(float damage)
        {
            if (CurrentHealth <= 0f || IsInvulnerable)
            {
                return;
            }

            var appliedDamage = Mathf.Max(0f, damage);
            if (appliedDamage <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - appliedDamage);
            if (_spriteAnimator == null)
            {
                _spriteAnimator = GetComponent<PlayerSpriteAnimator>();
            }

            if (_damageInvulnerabilitySeconds > 0f)
            {
                _invulnerableUntil = Time.time + _damageInvulnerabilitySeconds;
            }

            _spriteAnimator?.PlayHurt();
            CombatTextSpawner.SpawnDamage(transform.position + new Vector3(0f, 0.9f, 0f), appliedDamage, CombatTextSpawner.PlayerDamagedColor);
            Changed?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0f)
            {
                Died?.Invoke();
            }
        }
    }
}
