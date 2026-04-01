using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Core
{
    public enum RunAugmentId
    {
        None = 0,
        Berserk = 1,
        Overclock = 2,
        LongReach = 3,
        Fleetfoot = 4,
        VitalCore = 5,
    }

    public sealed class RunAugmentDefinition
    {
        public RunAugmentDefinition(
            RunAugmentId id,
            string displayName,
            string description,
            MetaBonusValues bonuses)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName;
            Description = description ?? string.Empty;
            Bonuses = bonuses;
        }

        public RunAugmentId Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public MetaBonusValues Bonuses { get; }
    }

    public static class SharedAugmentCatalog
    {
        private static readonly RunAugmentDefinition[] s_definitions =
        {
            new(
                RunAugmentId.Berserk,
                "Berserk",
                "공격력 +15%",
                new MetaBonusValues { attackPowerPercent = 15f }),
            new(
                RunAugmentId.Overclock,
                "Overclock",
                "공격 속도 +15%",
                new MetaBonusValues { attackSpeedPercent = 15f }),
            new(
                RunAugmentId.LongReach,
                "Long Reach",
                "공격 범위 +20%",
                new MetaBonusValues { attackRangePercent = 20f }),
            new(
                RunAugmentId.Fleetfoot,
                "Fleetfoot",
                "이동 속도 +12%",
                new MetaBonusValues { moveSpeedPercent = 12f }),
            new(
                RunAugmentId.VitalCore,
                "Vital Core",
                "최대 체력 +25",
                new MetaBonusValues { maxHealthFlat = 25f }),
        };

        private static readonly Dictionary<RunAugmentId, RunAugmentDefinition> s_lookup = BuildLookup();

        public static IReadOnlyList<RunAugmentDefinition> Definitions => s_definitions;

        public static RunAugmentDefinition GetDefinition(RunAugmentId id)
        {
            return s_lookup.TryGetValue(NormalizeAugmentId(id), out var definition)
                ? definition
                : s_definitions[0];
        }

        public static RunAugmentId NormalizeAugmentId(RunAugmentId id)
        {
            return s_lookup.ContainsKey(id) ? id : RunAugmentId.Berserk;
        }

        public static LevelUpOption[] BuildRandomOptions(IReadOnlyCollection<RunAugmentId> ownedAugments, int maxCount = 3)
        {
            var owned = ownedAugments ?? System.Array.Empty<RunAugmentId>();
            var candidates = new List<RunAugmentDefinition>(s_definitions.Length);

            for (var i = 0; i < s_definitions.Length; i++)
            {
                var definition = s_definitions[i];
                if (definition == null || ContainsAugment(owned, definition.Id))
                {
                    continue;
                }

                candidates.Add(definition);
            }

            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var swapIndex = Random.Range(0, i + 1);
                (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
            }

            var optionCount = Mathf.Min(Mathf.Max(0, maxCount), candidates.Count);
            var options = new LevelUpOption[optionCount];
            for (var i = 0; i < optionCount; i++)
            {
                options[i] = LevelUpOption.CreateAugment(
                    candidates[i].Id,
                    candidates[i].DisplayName,
                    candidates[i].Description,
                    BuildLabel(candidates[i]));
            }

            return options;
        }

        private static bool ContainsAugment(IReadOnlyCollection<RunAugmentId> ownedAugments, RunAugmentId augmentId)
        {
            if (ownedAugments == null)
            {
                return false;
            }

            foreach (var ownedAugment in ownedAugments)
            {
                if (ownedAugment == augmentId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildLabel(RunAugmentDefinition definition)
        {
            return $"{definition.DisplayName}\n<color=#FFD64D>증강</color>\n{definition.Description}";
        }

        private static Dictionary<RunAugmentId, RunAugmentDefinition> BuildLookup()
        {
            var lookup = new Dictionary<RunAugmentId, RunAugmentDefinition>(s_definitions.Length);
            for (var i = 0; i < s_definitions.Length; i++)
            {
                var definition = s_definitions[i];
                if (definition == null)
                {
                    continue;
                }

                lookup[definition.Id] = definition;
            }

            return lookup;
        }
    }
}

