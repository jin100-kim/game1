using EJR.Game.Gameplay;
using UnityEngine;

namespace EJR.Game.Core
{
    [System.Serializable]
    public sealed class PlayerStatsRuntime
    {
        public float DamageMultiplier { get; private set; } = 1f;
        public float AttackIntervalMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float AttackRangeMultiplier { get; private set; } = 1f;
        public float MaxHealthBonus { get; private set; }
        public float HealthRegenPerSecond { get; private set; }

        public void RecalculateFromBuild(PlayerBuildRuntime build)
        {
            DamageMultiplier = 1f;
            AttackIntervalMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
            AttackRangeMultiplier = 1f;
            MaxHealthBonus = 0f;
            HealthRegenPerSecond = 0f;

            if (build == null)
            {
                return;
            }

            var attackPowerLevel = build.GetStatLevel(StatUpgradeId.AttackPower);
            var attackSpeedLevel = build.GetStatLevel(StatUpgradeId.AttackSpeed);
            var maxHealthLevel = build.GetStatLevel(StatUpgradeId.MaxHealth);
            var healthRegenLevel = build.GetStatLevel(StatUpgradeId.HealthRegen);
            var moveSpeedLevel = build.GetStatLevel(StatUpgradeId.MoveSpeed);
            var attackRangeLevel = build.GetStatLevel(StatUpgradeId.AttackRange);

            DamageMultiplier += attackPowerLevel * 0.10f;
            // Cooldown model: finalCooldown = baseCooldown / (1 + attackSpeedBonus)
            // Keep this as "interval multiplier" so weapon systems can keep multiplying by it.
            var attackSpeedBonus = Mathf.Max(0f, attackSpeedLevel * 0.05f);
            AttackIntervalMultiplier = 1f / (1f + attackSpeedBonus);
            MaxHealthBonus = maxHealthLevel * 12f;
            HealthRegenPerSecond = healthRegenLevel * 0.6f;
            MoveSpeedMultiplier = 1f + (moveSpeedLevel * 0.06f);
            AttackRangeMultiplier = 1f + (attackRangeLevel * 0.05f);
        }
    }
}
