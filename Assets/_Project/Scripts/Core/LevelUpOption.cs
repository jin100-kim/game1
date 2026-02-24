using UnityEngine;

namespace EJR.Game.Core
{
    public enum LevelUpUpgradeType
    {
        Damage,
        AttackSpeed,
        MoveSpeed,
    }

    public readonly struct LevelUpOption
    {
        public LevelUpOption(LevelUpUpgradeType type, float value, string label)
        {
            UpgradeType = type;
            Value = value;
            Label = label;
        }

        public LevelUpUpgradeType UpgradeType { get; }
        public float Value { get; }
        public string Label { get; }
    }
}
