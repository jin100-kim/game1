using System.Collections.Generic;

namespace EJR.Game.Core
{
    public enum AchievementRewardKind
    {
        None,
        UnlockCharacter,
    }

    public enum AchievementMetricKind
    {
        RunsPlayed,
        RunsCleared,
        MapCleared,
        TotalEnemiesDefeated,
        BestLevel,
    }

    public sealed class AchievementRewardDefinition
    {
        public static readonly AchievementRewardDefinition None = new(AchievementRewardKind.None);

        public AchievementRewardDefinition(AchievementRewardKind kind, int characterId = -1)
        {
            Kind = kind;
            CharacterId = characterId;
        }

        public AchievementRewardKind Kind { get; }
        public int CharacterId { get; }
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
            AchievementRewardDefinition reward = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "achievement" : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            Description = string.IsNullOrWhiteSpace(description) ? DisplayName : description;
            MetricKind = metricKind;
            TargetValue = targetValue <= 0 ? 1 : targetValue;
            TargetMapId = targetMapId ?? string.Empty;
            Reward = reward ?? AchievementRewardDefinition.None;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public AchievementMetricKind MetricKind { get; }
        public int TargetValue { get; }
        public string TargetMapId { get; }
        public AchievementRewardDefinition Reward { get; }
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
                1),
            new(
                "first_clear",
                "첫 클리어",
                "누적 1회 클리어",
                AchievementMetricKind.RunsCleared,
                1),
            new(
                "forest_clear",
                "숲 돌파",
                "숲 맵 클리어",
                AchievementMetricKind.MapCleared,
                1,
                "forest"),
            new(
                "desert_clear",
                "사막 돌파",
                "사막 맵 클리어",
                AchievementMetricKind.MapCleared,
                1,
                "desert",
                new AchievementRewardDefinition(AchievementRewardKind.UnlockCharacter, 4)),
            new(
                "snow_clear",
                "설원 돌파",
                "설원 맵 클리어",
                AchievementMetricKind.MapCleared,
                1,
                "snow",
                new AchievementRewardDefinition(AchievementRewardKind.UnlockCharacter, 5)),
            new(
                "slayer_500",
                "사냥꾼 I",
                "누적 500 처치",
                AchievementMetricKind.TotalEnemiesDefeated,
                500),
            new(
                "slayer_2000",
                "사냥꾼 II",
                "누적 2000 처치",
                AchievementMetricKind.TotalEnemiesDefeated,
                2000),
            new(
                "level_20",
                "숙련자",
                "최고 레벨 20 달성",
                AchievementMetricKind.BestLevel,
                20),
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
