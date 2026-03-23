using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [CreateAssetMenu(menuName = "EJR/Config/Level Up Balance", fileName = "LevelUpBalanceConfig")]
    public sealed class LevelUpBalanceConfig : ScriptableObject
    {
        [System.Serializable]
        public struct FixedRarityValues
        {
            public float common;
            public float rare;
            public float epic;
            public float legendary;

            public readonly float GetValue(OptionRarity rarity)
            {
                return rarity switch
                {
                    OptionRarity.Rare => rare,
                    OptionRarity.Epic => epic,
                    OptionRarity.Legendary => legendary,
                    _ => common,
                };
            }
        }

        [Header("Base Rarity Weights")]
        [Min(0f)] public float commonWeight = 55f;
        [Min(0f)] public float rareWeight = 28f;
        [Min(0f)] public float epicWeight = 12f;
        [Min(0f)] public float legendaryWeight = 5f;

        [Header("Luck Shift Per Point")]
        public float commonShiftPerLuck = -2f;
        public float rareShiftPerLuck = 1f;
        public float epicShiftPerLuck = 0.7f;
        public float legendaryShiftPerLuck = 0.3f;

        [Header("Rarity Clamps")]
        [Min(0f)] public float commonMinimumWeight = 15f;
        [Min(0f)] public float legendaryMaximumWeight = 20f;

        [Header("Weapon Roll Values")]
        public FixedRarityValues weaponDamagePercent = new() { common = 12f, rare = 18f, epic = 24f, legendary = 30f };
        public FixedRarityValues weaponAttackSpeedPercent = new() { common = 6f, rare = 9f, epic = 12f, legendary = 15f };
        public FixedRarityValues weaponRangePercent = new() { common = 12f, rare = 18f, epic = 24f, legendary = 30f };

        [Header("Global Stat Values")]
        public FixedRarityValues globalAttackPowerPercent = new() { common = 8f, rare = 12f, epic = 16f, legendary = 20f };
        public FixedRarityValues globalAttackSpeedPercent = new() { common = 4f, rare = 6f, epic = 8f, legendary = 10f };
        public FixedRarityValues globalMaxHealthFlat = new() { common = 20f, rare = 30f, epic = 40f, legendary = 50f };
        public FixedRarityValues globalHealthRegenPerSecond = new() { common = 0.25f, rare = 0.50f, epic = 0.75f, legendary = 1.00f };
        public FixedRarityValues globalMoveSpeedPercent = new() { common = 4f, rare = 6f, epic = 8f, legendary = 10f };
        public FixedRarityValues globalAttackRangePercent = new() { common = 8f, rare = 12f, epic = 16f, legendary = 20f };
        public FixedRarityValues globalLuck = new() { common = 1f, rare = 2f, epic = 3f, legendary = 4f };

        public static LevelUpBalanceConfig CreateRuntimeDefault()
        {
            return CreateInstance<LevelUpBalanceConfig>();
        }

        public OptionRarity RollRarity(float totalLuck)
        {
            var common = Mathf.Max(commonMinimumWeight, commonWeight + (commonShiftPerLuck * totalLuck));
            var rare = Mathf.Max(0f, rareWeight + (rareShiftPerLuck * totalLuck));
            var epic = Mathf.Max(0f, epicWeight + (epicShiftPerLuck * totalLuck));
            var legendary = Mathf.Clamp(legendaryWeight + (legendaryShiftPerLuck * totalLuck), 0f, legendaryMaximumWeight);

            var total = common + rare + epic + legendary;
            if (total <= 0.001f)
            {
                return OptionRarity.Common;
            }

            var roll = Random.value * total;
            if (roll < common)
            {
                return OptionRarity.Common;
            }

            roll -= common;
            if (roll < rare)
            {
                return OptionRarity.Rare;
            }

            roll -= rare;
            if (roll < epic)
            {
                return OptionRarity.Epic;
            }

            return OptionRarity.Legendary;
        }

        public float GetWeaponRollValue(WeaponRollKind kind, OptionRarity rarity)
        {
            return kind switch
            {
                WeaponRollKind.AttackSpeedPercent => weaponAttackSpeedPercent.GetValue(rarity),
                WeaponRollKind.RangePercent => weaponRangePercent.GetValue(rarity),
                _ => weaponDamagePercent.GetValue(rarity),
            };
        }

        public float GetGlobalRollValue(StatUpgradeId statId, OptionRarity rarity)
        {
            return statId switch
            {
                StatUpgradeId.AttackPower => globalAttackPowerPercent.GetValue(rarity),
                StatUpgradeId.AttackSpeed => globalAttackSpeedPercent.GetValue(rarity),
                StatUpgradeId.MaxHealth => globalMaxHealthFlat.GetValue(rarity),
                StatUpgradeId.HealthRegen => globalHealthRegenPerSecond.GetValue(rarity),
                StatUpgradeId.MoveSpeed => globalMoveSpeedPercent.GetValue(rarity),
                StatUpgradeId.AttackRange => globalAttackRangePercent.GetValue(rarity),
                StatUpgradeId.Luck => globalLuck.GetValue(rarity),
                _ => 0f,
            };
        }
    }
}
