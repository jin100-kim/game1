using System.Collections.Generic;

namespace EJR.Game.Core
{
    public enum AchievementRewardKind
    {
        None,
        UnlockCharacter,
        UnlockMap,
        GrantCredits,
    }

    public enum AchievementMetricKind
    {
        RunsPlayed,
        RunsCleared,
        MapCleared,
        TotalEnemiesDefeated,
        BestLevel,
        WeaponLevelReached,
    }

    public sealed class AchievementRewardDefinition
    {
        public static readonly AchievementRewardDefinition None = new(AchievementRewardKind.None);

        public AchievementRewardDefinition(AchievementRewardKind kind, int characterId = -1, string mapId = null, int credits = 0)
        {
            Kind = kind;
            CharacterId = characterId;
            MapId = mapId ?? string.Empty;
            Credits = credits;
        }

        public AchievementRewardKind Kind { get; }
        public int CharacterId { get; }
        public string MapId { get; }
        public int Credits { get; }
    }

    public sealed class AchievementDefinition
    {
        public AchievementDefinition(
            string id,
            string displayName,
            string description,
            AchievementMetricKind metricKind,
            int targetValue,
            string targetMapId = null,
            AchievementRewardDefinition reward = null,
            WeaponUpgradeId targetWeaponId = WeaponUpgradeId.Fireball)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "achievement" : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            Description = string.IsNullOrWhiteSpace(description) ? DisplayName : description;
            MetricKind = metricKind;
            TargetValue = targetValue <= 0 ? 1 : targetValue;
            TargetMapId = targetMapId ?? string.Empty;
            Reward = reward ?? AchievementRewardDefinition.None;
            TargetWeaponId = targetWeaponId;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public AchievementMetricKind MetricKind { get; }
        public int TargetValue { get; }
        public string TargetMapId { get; }
        public AchievementRewardDefinition Reward { get; }
        public WeaponUpgradeId TargetWeaponId { get; }
    }

    public readonly struct AchievementEntryView
    {
        public AchievementEntryView(
            string id,
            string displayName,
            string description,
            string progressText,
            string rewardText,
            bool isCompleted,
            bool isNew,
            int currentValue,
            int targetValue)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            ProgressText = progressText ?? string.Empty;
            RewardText = rewardText ?? string.Empty;
            IsCompleted = isCompleted;
            IsNew = isNew;
            CurrentValue = currentValue;
            TargetValue = targetValue <= 0 ? 1 : targetValue;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ProgressText { get; }
        public string RewardText { get; }
        public bool IsCompleted { get; }
        public bool IsNew { get; }
        public int CurrentValue { get; }
        public int TargetValue { get; }
    }

    public static class SharedAchievementCatalog
    {
        private static readonly AchievementDefinition[] s_definitions =
        {
            new(
                "first_sortie",
                "첫 출격",
                "누적 1회 플레이",
                AchievementMetricKind.RunsPlayed,
                1,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 50)),
            new(
                "first_clear",
                "첫 클리어",
                "누적 1회 클리어",
                AchievementMetricKind.RunsCleared,
                1,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 150)),
            new(
                "clear_3",
                "연속 출격",
                "누적 3회 클리어",
                AchievementMetricKind.RunsCleared,
                3,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 300)),
            new(
                "clear_5",
                "숙련 원정",
                "누적 5회 클리어",
                AchievementMetricKind.RunsCleared,
                5,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 500)),
            new(
                "clear_10",
                "원정대장",
                "누적 10회 클리어",
                AchievementMetricKind.RunsCleared,
                10,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 1000)),
            new(
                "slayer_500",
                "사냥꾼 I",
                "누적 500 처치",
                AchievementMetricKind.TotalEnemiesDefeated,
                500,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 150)),
            new(
                "slayer_2000",
                "사냥꾼 II",
                "누적 2000 처치",
                AchievementMetricKind.TotalEnemiesDefeated,
                2000,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 400)),
            new(
                "slayer_5000",
                "사냥꾼 III",
                "누적 5000 처치",
                AchievementMetricKind.TotalEnemiesDefeated,
                5000,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 900)),
            new(
                "level_10",
                "성장 시작",
                "최고 레벨 10 달성",
                AchievementMetricKind.BestLevel,
                10,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 120)),
            new(
                "level_20",
                "숙련자",
                "최고 레벨 20 달성",
                AchievementMetricKind.BestLevel,
                20,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 300)),
            new(
                "level_30",
                "고급 숙련자",
                "최고 레벨 30 달성",
                AchievementMetricKind.BestLevel,
                30,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 600)),
            new(
                "level_40",
                "한계 돌파",
                "최고 레벨 40 달성",
                AchievementMetricKind.BestLevel,
                40,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 1000)),
            new(
                "weapon_fireball_lv10",
                "화염 숙련",
                "화염구 Lv.10 달성",
                AchievementMetricKind.WeaponLevelReached,
                10,
                reward: new AchievementRewardDefinition(AchievementRewardKind.GrantCredits, credits: 150),
                targetWeaponId: WeaponUpgradeId.Fireball),
            new(
                "weapon_slash_lv10",
                "검술 입문",
                "베기 Lv.10 달성",
                AchievementMetricKind.WeaponLevelReached,
                10,
                reward: new AchievementRewardDefinition(AchievementRewardKind.UnlockCharacter, characterId: 1),
                targetWeaponId: WeaponUpgradeId.Slash),
            new(
                "weapon_lightning_lv10",
                "번개 숙련",
                "낙뢰 Lv.10 달성",
                AchievementMetricKind.WeaponLevelReached,
                10,
                reward: new AchievementRewardDefinition(AchievementRewardKind.UnlockCharacter, characterId: 2),
                targetWeaponId: WeaponUpgradeId.LightningBolt),
            new(
                "weapon_ice_lv10",
                "빙결 숙련",
                "얼음 파편 Lv.10 달성",
                AchievementMetricKind.WeaponLevelReached,
                10,
                reward: new AchievementRewardDefinition(AchievementRewardKind.UnlockCharacter, characterId: 3),
                targetWeaponId: WeaponUpgradeId.IceSpike),
            new(
                "weapon_wind_lv10",
                "바람 숙련",
                "칼날 바람 Lv.10 달성",
                AchievementMetricKind.WeaponLevelReached,
                10,
                reward: new AchievementRewardDefinition(AchievementRewardKind.UnlockCharacter, characterId: 4),
                targetWeaponId: WeaponUpgradeId.WindBlade),
            new(
                "weapon_bubble_lv10",
                "거품 숙련",
                "추적 방울 Lv.10 달성",
                AchievementMetricKind.WeaponLevelReached,
                10,
                reward: new AchievementRewardDefinition(AchievementRewardKind.UnlockCharacter, characterId: 5),
                targetWeaponId: WeaponUpgradeId.Bubble),
        };

        public static IReadOnlyList<AchievementDefinition> Definitions => s_definitions;

        public static AchievementDefinition GetDefinition(string achievementId)
        {
            if (TryGetDefinition(achievementId, out var definition))
            {
                return definition;
            }

            return s_definitions[0];
        }

        public static bool TryGetDefinition(string achievementId, out AchievementDefinition definition)
        {
            for (var i = 0; i < s_definitions.Length; i++)
            {
                if (!string.Equals(s_definitions[i].Id, achievementId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                definition = s_definitions[i];
                return true;
            }

            definition = s_definitions[0];
            return false;
        }
    }
}
