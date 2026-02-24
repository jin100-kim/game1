using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        public event Action<float, float> Changed;
        public event Action Died;

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }

        public void Initialize(float maxHealth)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            Changed?.Invoke(CurrentHealth, MaxHealth);
        }

        public void TakeDamage(float damage)
        {
            if (CurrentHealth <= 0f)
            {
                return;
            }

            var appliedDamage = Mathf.Max(0f, damage);
            if (appliedDamage <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - appliedDamage);
            CombatTextSpawner.SpawnDamage(transform.position + new Vector3(0f, 0.9f, 0f), appliedDamage, CombatTextSpawner.PlayerDamagedColor);
            Changed?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0f)
            {
                Died?.Invoke();
            }
        }
    }
}
