using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class RunCombatTracker
    {
        private readonly Dictionary<WeaponUpgradeId, float> _weaponDamage = new();

        public float TotalDamageDealt { get; private set; }
        public float DamageTaken { get; private set; }
        public float HealingDone { get; private set; }
        public int BossThresholdsReached { get; private set; }

        public void Reset()
        {
            _weaponDamage.Clear();
            TotalDamageDealt = 0f;
            DamageTaken = 0f;
            HealingDone = 0f;
            BossThresholdsReached = 0;
        }

        public void RecordDamageDealt(WeaponUpgradeId weaponId, float amount, bool targetWasBoss, float currentHealth, float maxHealth)
        {
            var clampedAmount = Mathf.Max(0f, amount);
            if (clampedAmount <= 0.0001f)
            {
                return;
            }

            TotalDamageDealt += clampedAmount;
            _weaponDamage[weaponId] = _weaponDamage.TryGetValue(weaponId, out var accumulated)
                ? accumulated + clampedAmount
                : clampedAmount;

            if (!targetWasBoss || maxHealth <= 0.0001f)
            {
                return;
            }

            var healthRatio = Mathf.Clamp01(currentHealth / maxHealth);
            var thresholdsReached = Mathf.Clamp(Mathf.FloorToInt((1f - healthRatio) * 10f + 0.0001f), 0, 10);
            BossThresholdsReached = Mathf.Max(BossThresholdsReached, thresholdsReached);
        }

        public void RecordDamageTaken(float amount)
        {
            DamageTaken += Mathf.Max(0f, amount);
        }

        public void RecordHealing(float amount)
        {
            HealingDone += Mathf.Max(0f, amount);
        }

        public RunCombatStats BuildSummary()
        {
            var summary = new RunCombatStats
            {
                totalDamageDealt = TotalDamageDealt,
                damageTaken = DamageTaken,
                healingDone = HealingDone,
            };

            var entries = new List<RunWeaponDamageEntry>(_weaponDamage.Count);
            foreach (var pair in _weaponDamage)
            {
                entries.Add(new RunWeaponDamageEntry
                {
                    weaponId = (int)pair.Key,
                    damage = pair.Value,
                });
            }

            entries.Sort((a, b) => b.damage.CompareTo(a.damage));
            summary.weaponDamages = entries;
            return summary;
        }
    }
}
