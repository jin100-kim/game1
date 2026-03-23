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
        public float Luck { get; private set; }

        public void RecalculateFromBuild(PlayerBuildRuntime build)
        {
            DamageMultiplier = 1f;
            AttackIntervalMultiplier = 1f;
            MoveSpeedMultiplier = 1f;
            AttackRangeMultiplier = 1f;
            MaxHealthBonus = 0f;
            HealthRegenPerSecond = 0f;
            Luck = 0f;

            if (build == null)
            {
                return;
            }

            DamageMultiplier = 1f + (Mathf.Max(0f, build.GlobalAttackPowerPercentTotal) / 100f);
            var globalAttackSpeedBonus = Mathf.Max(0f, build.GlobalAttackSpeedPercentTotal) / 100f;
            AttackIntervalMultiplier = 1f / (1f + globalAttackSpeedBonus);
            MoveSpeedMultiplier = 1f + (Mathf.Max(0f, build.GlobalMoveSpeedPercentTotal) / 100f);
            AttackRangeMultiplier = 1f + (Mathf.Max(0f, build.GlobalAttackRangePercentTotal) / 100f);
            MaxHealthBonus = Mathf.Max(0f, build.GlobalMaxHealthFlatTotal);
            HealthRegenPerSecond = Mathf.Max(0f, build.GlobalHealthRegenPerSecondTotal);
            Luck = Mathf.Max(0f, build.GlobalLuckTotal);
        }
    }
}
