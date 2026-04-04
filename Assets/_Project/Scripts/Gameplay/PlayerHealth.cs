using System;
using EJR.Game.Audio;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        public event Action<float, float> Changed;
        public event Action Died;
        public event Action<float> Damaged;
        public event Action<float> Healed;

        private PlayerSpriteAnimator _spriteAnimator;
        private float _damageInvulnerabilitySeconds;
        private float _damageTakenMultiplier = 1f;
        private float _invulnerableUntil = -1f;
        private float _pendingHealPopupAmount;
        private bool _debugInvincible;

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsInvulnerable => Time.time < _invulnerableUntil;
        public bool IsDebugInvincible => _debugInvincible;

        public void Initialize(float maxHealth, float damageInvulnerabilitySeconds = 0f)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            _damageInvulnerabilitySeconds = Mathf.Max(0f, damageInvulnerabilitySeconds);
            _damageTakenMultiplier = 1f;
            _invulnerableUntil = -1f;
            _pendingHealPopupAmount = 0f;
            _debugInvincible = false;
            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public void SetDebugInvincible(bool enabled)
        {
            _debugInvincible = enabled;
        }

        public void SetDamageTakenMultiplier(float multiplier)
        {
            _damageTakenMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public void Restore(float currentHealth, float maxHealth = -1f)
        {
            if (maxHealth > 0f)
            {
                MaxHealth = Mathf.Max(1f, maxHealth);
            }

            CurrentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
            _pendingHealPopupAmount = 0f;
            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public void GrantInvulnerability(float durationSeconds)
        {
            var duration = Mathf.Max(0f, durationSeconds);
            if (duration <= 0f)
            {
                return;
            }

            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + duration);
        }

        public void SetMaxHealth(float newMaxHealth, bool healDelta, bool preserveCurrentRatio = false)
        {
            var clampedMax = Mathf.Max(1f, newMaxHealth);
            if (Mathf.Abs(clampedMax - MaxHealth) <= 0.0001f)
            {
                return;
            }

            var previousMax = MaxHealth;
            var previousRatio = previousMax > 0.0001f ? CurrentHealth / previousMax : 1f;
            MaxHealth = clampedMax;

            if (preserveCurrentRatio)
            {
                CurrentHealth = Mathf.Clamp(MaxHealth * Mathf.Clamp01(previousRatio), 0f, MaxHealth);
            }
            else if (healDelta)
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

            var appliedHealing = nextHealth - CurrentHealth;
            CurrentHealth = nextHealth;
            TrySpawnHealingPopup(appliedHealing);
            Healed?.Invoke(appliedHealing);
            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        private void TrySpawnHealingPopup(float healingAmount)
        {
            _pendingHealPopupAmount += healingAmount;
            var displayAmount = Mathf.FloorToInt(_pendingHealPopupAmount + 0.0001f);
            if (displayAmount <= 0)
            {
                return;
            }

            _pendingHealPopupAmount = Mathf.Max(0f, _pendingHealPopupAmount - displayAmount);
            CombatTextSpawner.SpawnHealing(transform.position + new Vector3(0f, 0.9f, 0f), displayAmount);
        }

        public bool TrySpendHealth(float amount, bool allowFatal = false)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (CurrentHealth <= 0f)
            {
                return false;
            }

            var minimumHealth = allowFatal ? 0f : 1f;
            if (CurrentHealth - amount < minimumHealth - 0.0001f)
            {
                return false;
            }

            CurrentHealth = Mathf.Max(minimumHealth, CurrentHealth - amount);
            Changed?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0f)
            {
                Died?.Invoke();
            }

            return true;
        }

        public void TakeDamage(float damage)
        {
            if (CurrentHealth <= 0f || IsInvulnerable || _debugInvincible)
            {
                return;
            }

            var appliedDamage = Mathf.Max(0f, damage) * _damageTakenMultiplier;
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
            AudioService.Instance.PlaySfx(AudioCueId.PlayerHurt);
            Damaged?.Invoke(appliedDamage);
            Changed?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0f)
            {
                Died?.Invoke();
            }
        }
    }
}
