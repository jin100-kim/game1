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
        [SerializeField] private int[] upgradeCostCurve = { 20, 30, 45, 65, 90, 120, 155, 195, 240, 290 };
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

            if (currentLevel < 0 || currentLevel >= definition.MaxLevel || currentLevel >= upgradeCostCurve.Length)
            {
                return int.MaxValue;
            }

            return Mathf.Max(0, upgradeCostCurve[currentLevel]);
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
                "피해량 증폭",
                "영구 피해량 +2%",
                10,
                new MetaBonusValues { attackPowerPercent = 2f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.AttackSpeedPercent,
                "공속 증폭",
                "영구 공격 속도 +2%",
                10,
                new MetaBonusValues { attackSpeedPercent = 2f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.MaxHealthFlat,
                "생존력 보강",
                "영구 최대 체력 +5",
                10,
                new MetaBonusValues { maxHealthFlat = 5f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.HealthRegenPerSecond,
                "재생 회로",
                "영구 체력 재생 +0.1/초",
                10,
                new MetaBonusValues { healthRegenPerSecond = 0.1f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.MoveSpeedPercent,
                "기동 증폭",
                "영구 이동 속도 +2%",
                10,
                new MetaBonusValues { moveSpeedPercent = 2f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.RangePercent,
                "사거리 증폭",
                "영구 범위 +2%",
                10,
                new MetaBonusValues { attackRangePercent = 2f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.Luck,
                "행운 축적",
                "영구 행운 +5",
                10,
                new MetaBonusValues { luck = 5f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.ExperienceGainPercent,
                "경험 회수",
                "영구 XP 획득량 +4%",
                10,
                new MetaBonusValues { experienceGainPercent = 4f }));
            _upgradeDefinitions.Add(new MetaUpgradeDefinition(
                MetaUpgradeId.CreditGainPercent,
                "전리품 정산",
                "영구 코인 획득량 +5%",
                10,
                new MetaBonusValues { creditGainPercent = 5f }));
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
