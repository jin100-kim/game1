using EJR.Game.Gameplay;
using UnityEngine;

namespace EJR.Game.Core
{
    [System.Serializable]
    public sealed class PlayerStatsRuntime
    {
        public float DamageMultiplier { get; private set; } = 1f;
        public float DamageTakenMultiplier { get; private set; } = 1f;
        public float AttackIntervalMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float AttackRangeMultiplier { get; private set; } = 1f;
        public float MaxHealthBonus { get; private set; }
        public float MaxHealthScale { get; private set; } = 1f;
        public float HealthRegenPerSecond { get; private set; }
        public float Luck { get; private set; }
        public float ExperienceGainMultiplier { get; private set; } = 1f;
        public float CreditGainPercent { get; private set; }

        public void RecalculateFromBuild(PlayerBuildRuntime build)
        {
            DamageMultiplier = 1f;
            DamageTakenMultiplier = 1f;
            AttackIntervalMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
            AttackRangeMultiplier = 1f;
            MaxHealthBonus = 0f;
            MaxHealthScale = 1f;
            HealthRegenPerSecond = 0f;
            Luck = 0f;
            ExperienceGainMultiplier = 1f;
            CreditGainPercent = 0f;

            if (build == null)
            {
                return;
            }

            DamageMultiplier = 1f + (Mathf.Max(0f, build.GlobalAttackPowerPercentTotal) / 100f);
            DamageTakenMultiplier = Mathf.Max(0.1f, build.GlobalDamageTakenScale);
            var attackSpeedScale = Mathf.Max(0.25f, 1f + (build.GlobalAttackSpeedPercentTotal / 100f));
            AttackIntervalMultiplier = 1f / attackSpeedScale;
            MoveSpeedMultiplier = 1f + (Mathf.Max(0f, build.GlobalMoveSpeedPercentTotal) / 100f);
            AttackRangeMultiplier = Mathf.Max(0.25f, 1f + (build.GlobalAttackRangePercentTotal / 100f));
            MaxHealthBonus = Mathf.Max(0f, build.GlobalMaxHealthFlatTotal);
            MaxHealthScale = Mathf.Max(0.05f, build.GlobalMaxHealthScale);
            HealthRegenPerSecond = build.SuppressesPassiveRegen ? 0f : Mathf.Max(0f, build.GlobalHealthRegenPerSecondTotal);
            Luck = Mathf.Max(0f, build.GlobalLuckTotal);
            ExperienceGainMultiplier = 1f + (Mathf.Max(0f, build.GlobalExperienceGainPercentTotal) / 100f);
            CreditGainPercent = Mathf.Max(0f, build.GlobalCreditGainPercentTotal);
        }
    }
}
