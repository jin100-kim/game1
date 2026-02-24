using UnityEngine;

namespace EJR.Game.Core
{
    [System.Serializable]
    public sealed class PlayerStatsRuntime
    {
        public float DamageMultiplier { get; private set; } = 1f;
        public float AttackIntervalMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;

        public void ApplyUpgrade(LevelUpOption option)
        {
            switch (option.UpgradeType)
            {
                case LevelUpUpgradeType.Damage:
                    DamageMultiplier += option.Value;
                    break;
                case LevelUpUpgradeType.AttackSpeed:
                    AttackIntervalMultiplier = Mathf.Clamp(AttackIntervalMultiplier * (1f - option.Value), 0.15f, 4f);
                    break;
                case LevelUpUpgradeType.MoveSpeed:
                    MoveSpeedMultiplier += option.Value;
                    break;
            }
        }
    }
}
