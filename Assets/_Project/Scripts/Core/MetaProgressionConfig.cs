using System;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Core
{
    [Serializable]
    public struct MetaBonusValues
    {
        public float attackPowerPercent;
        public float attackSpeedPercent;
        public float maxHealthFlat;
        public float healthRegenPerSecond;
        public float moveSpeedPercent;
        public float attackRangePercent;
        public float luck;
        public float experienceGainPercent;
        public float creditGainPercent;
        public float experiencePickupRadiusPercent;
        public float projectileCountFlat;

        public static MetaBonusValues operator +(MetaBonusValues a, MetaBonusValues b)
        {
            return new MetaBonusValues
            {
                attackPowerPercent = a.attackPowerPercent + b.attackPowerPercent,
                attackSpeedPercent = a.attackSpeedPercent + b.attackSpeedPercent,
                maxHealthFlat = a.maxHealthFlat + b.maxHealthFlat,
                healthRegenPerSecond = a.healthRegenPerSecond + b.healthRegenPerSecond,
                moveSpeedPercent = a.moveSpeedPercent + b.moveSpeedPercent,
                attackRangePercent = a.attackRangePercent + b.attackRangePercent,
                luck = a.luck + b.luck,
                experienceGainPercent = a.experienceGainPercent + b.experienceGainPercent,
                creditGainPercent = a.creditGainPercent + b.creditGainPercent,
                experiencePickupRadiusPercent = a.experiencePickupRadiusPercent + b.experiencePickupRadiusPercent,
                projectileCountFlat = a.projectileCountFlat + b.projectileCountFlat,
            };
        }

        public static MetaBonusValues operator *(MetaBonusValues values, int multiplier)
        {
            return new MetaBonusValues
            {
                attackPowerPercent = values.attackPowerPercent * multiplier,
                attackSpeedPercent = values.attackSpeedPercent * multiplier,
                maxHealthFlat = values.maxHealthFlat * multiplier,
                healthRegenPerSecond = values.healthRegenPerSecond * multiplier,
                moveSpeedPercent = values.moveSpeedPercent * multiplier,
                attackRangePercent = values.attackRangePercent * multiplier,
                luck = values.luck * multiplier,
                experienceGainPercent = values.experienceGainPercent * multiplier,
                creditGainPercent = values.creditGainPercent * multiplier,
                experiencePickupRadiusPercent = values.experiencePickupRadiusPercent * multiplier,
                projectileCountFlat = values.projectileCountFlat * multiplier,
            };
        }
    }

    [Serializable]
    public struct MetaUpgradeDefinition
    {
        public MetaUpgradeDefinition(
            MetaUpgradeId id,
            string title,
            string description,
            int maxLevel,
            MetaBonusValues stepBonuses)
        {
            Id = id;
            Title = title;
            Description = description;
            MaxLevel = maxLevel;
            StepBonuses = stepBonuses;
        }

        public MetaUpgradeId Id;
        public string Title;
        public string Description;
        public int MaxLevel;
        public MetaBonusValues StepBonuses;
    }

    [Serializable]
    public sealed class MetaProgressionConfig : ScriptableObject
    {
        [SerializeField] private int[] upgradeCostCurve = { 60, 120, 220, 350, 500 };
        [SerializeField] private int[] projectileCountCostCurve = { 10000 };
        [SerializeField, Min(0)] private int killsPerCredit = 10;
        [SerializeField, Min(0)] private int creditsPerMinuteSurvived = 5;
        [SerializeField, Min(0)] private int creditsPerBossThreshold = 5;
        [SerializeField, Min(0)] private int firstClearCredits = 100;

        private readonly List<MetaUpgradeDefinition> _upgradeDefinitions = new();
        private readonly Dictionary<MetaUpgradeId, MetaUpgradeDefinition> _upgradeLookup = new();

        public IReadOnlyList<MetaUpgradeDefinition> UpgradeDefinitions => _upgradeDefinitions;
        public IReadOnlyList<int> UpgradeCostCurve => upgradeCostCurve;

        public static MetaProgressionConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<MetaProgressionConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            config.BuildDefaults();
            return config;
        }

        public bool TryGetUpgradeDefinition(MetaUpgradeId id, out MetaUpgradeDefinition definition)
        {
            EnsureLookups();
            return _upgradeLookup.TryGetValue(id, out definition);
        }

        public int GetUpgradeCost(MetaUpgradeId id, int currentLevel)
        {
            if (!TryGetUpgradeDefinition(id, out var definition))
            {
                return int.MaxValue;
            }

            var costCurve = id == MetaUpgradeId.ProjectileCount ? projectileCountCostCurve : upgradeCostCurve;
            if (currentLevel < 0 || currentLevel >= definition.MaxLevel || currentLevel >= costCurve.Length)
            {
                return int.MaxValue;
            }

            return Mathf.Max(0, costCurve[currentLevel]);
        }

        public RunCreditBreakdown BuildCreditBreakdown(
            string mapId,
            bool cleared,
            int enemiesDefeated,
            float survivalTimeSeconds,
            int bossThresholdsReached,
            float creditGainPercent,
            bool alreadyClearedMap)
        {
            var breakdown = new RunCreditBreakdown
            {
                mapId = mapId ?? string.Empty,
                killCredits = Mathf.Max(0, enemiesDefeated) / Mathf.Max(1, killsPerCredit),
                timeCredits = Mathf.FloorToInt(Mathf.Max(0f, survivalTimeSeconds) / 60f) * Mathf.Max(0, creditsPerMinuteSurvived),
                bossThresholdsReached = Mathf.Clamp(bossThresholdsReached, 0, 10),
            };

            breakdown.bossDamageCredits = breakdown.bossThresholdsReached * Mathf.Max(0, creditsPerBossThreshold);
            breakdown.repeatCreditsBase = breakdown.killCredits + breakdown.timeCredits + breakdown.bossDamageCredits;
            breakdown.creditBonusPercent = Mathf.Max(0, Mathf.RoundToInt(creditGainPercent));
            breakdown.creditBonusApplied = Mathf.RoundToInt(breakdown.repeatCreditsBase * (breakdown.creditBonusPercent / 100f));
            breakdown.firstClearCredits = cleared && !alreadyClearedMap ? Mathf.Max(0, firstClearCredits) : 0;
            breakdown.totalCredits = breakdown.repeatCreditsBase + breakdown.creditBonusApplied + breakdown.firstClearCredits;
            return breakdown;
        }

        private void BuildDefaults()
        {
            _upgradeDefinitions.Clear();
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.DamagePercent,
                "기본 피해",
                "단계당 피해량 +4%",
                5,
                new MetaBonusValues { attackPowerPercent = 4f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.AttackSpeedPercent,
                "기본 공속",
                "단계당 공격 속도 +4%",
                5,
                new MetaBonusValues { attackSpeedPercent = 4f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.MaxHealthFlat,
                "기본 체력",
                "단계당 최대 체력 +10",
                5,
                new MetaBonusValues { maxHealthFlat = 10f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.HealthRegenPerSecond,
                "기본 재생",
                "단계당 체력 재생 +0.2/초",
                5,
                new MetaBonusValues { healthRegenPerSecond = 0.2f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.MoveSpeedPercent,
                "기본 이속",
                "단계당 이동 속도 +4%",
                5,
                new MetaBonusValues { moveSpeedPercent = 4f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.RangePercent,
                "기본 범위",
                "단계당 공격 범위 +4%",
                5,
                new MetaBonusValues { attackRangePercent = 4f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.Luck,
                "기본 행운",
                "단계당 행운 +10",
                5,
                new MetaBonusValues { luck = 10f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.ExperienceGainPercent,
                "기본 경험치",
                "단계당 XP 획득량 +8%",
                5,
                new MetaBonusValues { experienceGainPercent = 8f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.ExperiencePickupRadiusPercent,
                "XP 흡입 거리",
                "단계당 XP 흡입 거리 +12%",
                5,
                new MetaBonusValues { experiencePickupRadiusPercent = 12f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.CreditGainPercent,
                "기본 코인",
                "단계당 코인 획득량 +10%",
                5,
                new MetaBonusValues { creditGainPercent = 10f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.ProjectileCount,
                "추가 투사체",
                "모든 무기 발사/타격 수 +1",
                1,
                new MetaBonusValues { projectileCountFlat = 1f }));
            EnsureLookups();
        }

        private void EnsureLookups()
        {
            if (_upgradeLookup.Count == _upgradeDefinitions.Count)
            {
                return;
            }

            _upgradeLookup.Clear();
            for (var i = 0; i < _upgradeDefinitions.Count; i++)
            {
                _upgradeLookup[_upgradeDefinitions[i].Id] = _upgradeDefinitions[i];
            }
        }
    }
}
