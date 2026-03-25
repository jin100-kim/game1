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
        public float commonShiftPerLuck = -0.2f;
        public float rareShiftPerLuck = 0.1f;
        public float epicShiftPerLuck = 0.07f;
        public float legendaryShiftPerLuck = 0.03f;

        [Header("Rarity Clamps")]
        [Min(0f)] public float commonMinimumWeight = 15f;
        [Min(0f)] public float legendaryMaximumWeight = 20f;

        [Header("Weapon Roll Values")]
        public FixedRarityValues weaponDamagePercent = new() { common = 12f, rare = 14f, epic = 16f, legendary = 18f };
        public FixedRarityValues weaponAttackSpeedPercent = new() { common = 10f, rare = 12f, epic = 14f, legendary = 15f };
        public FixedRarityValues weaponRangePercent = new() { common = 12f, rare = 14f, epic = 16f, legendary = 18f };

        [Header("Global Stat Values")]
        public FixedRarityValues globalAttackPowerPercent = new() { common = 5f, rare = 6f, epic = 7f, legendary = 8f };
        public FixedRarityValues globalAttackSpeedPercent = new() { common = 5f, rare = 6f, epic = 7f, legendary = 8f };
        public FixedRarityValues globalMaxHealthFlat = new() { common = 24f, rare = 28f, epic = 32f, legendary = 36f };
        public FixedRarityValues globalHealthRegenPerSecond = new() { common = 0.70f, rare = 0.80f, epic = 0.90f, legendary = 1.00f };
        public FixedRarityValues globalMoveSpeedPercent = new() { common = 4f, rare = 4.5f, epic = 5f, legendary = 6f };
        public FixedRarityValues globalAttackRangePercent = new() { common = 5f, rare = 6f, epic = 7f, legendary = 8f };
        public FixedRarityValues globalLuck = new() { common = 10f, rare = 12f, epic = 14f, legendary = 15f };

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
