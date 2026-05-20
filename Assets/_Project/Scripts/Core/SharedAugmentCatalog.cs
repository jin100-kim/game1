using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Core
{
    public enum RunAugmentId
    {
        None = 0,
        Berserk = 1,
        Overclock = 2,
        Finisher = 3,
        CloseQuarters = 4,
        Ambidextrous = 6,
        GlassCannon = 7,
        CautiousAttack = 8,
        Vampirism = 9,
        BerserkerHeart = 10,
    }

    public sealed class RunAugmentDefinition
    {
        public RunAugmentDefinition(
            RunAugmentId id,
            string displayName,
            string description,
            MetaBonusValues bonuses,
            int extraWeaponSlots = 0,
            float maxHealthScale = 1f,
            float damageTakenScale = 1f,
            bool suppressPassiveRegen = false,
            int lifestealHealPerHit = 0,
            float lifestealDamageRatio = 0f,
            float lifestealMaxHealPerHit = 0f,
            float lifestealBossMultiplier = 1f,
            float lifestealInternalCooldown = 0f,
            float lowHealthDamagePercentMax = 0f,
            float lowHealthMoveSpeedPercentMax = 0f,
            float lowHealthMaxThreshold = 0f,
            float lowEnemyHealthDamagePercent = 0f,
            float lowEnemyHealthThreshold = 0f)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.ToString() : displayName;
            Description = description ?? string.Empty;
            Bonuses = bonuses;
            ExtraWeaponSlots = Mathf.Max(0, extraWeaponSlots);
            MaxHealthScale = Mathf.Max(0.05f, maxHealthScale);
            DamageTakenScale = Mathf.Max(0.1f, damageTakenScale);
            SuppressPassiveRegen = suppressPassiveRegen;
            LifestealHealPerHit = Mathf.Max(0, lifestealHealPerHit);
            LifestealDamageRatio = Mathf.Max(0f, lifestealDamageRatio);
            LifestealMaxHealPerHit = Mathf.Max(0f, lifestealMaxHealPerHit);
            LifestealBossMultiplier = Mathf.Clamp(lifestealBossMultiplier, 0f, 1f);
            LifestealInternalCooldown = Mathf.Max(0f, lifestealInternalCooldown);
            LowHealthDamagePercentMax = Mathf.Max(0f, lowHealthDamagePercentMax);
            LowHealthMoveSpeedPercentMax = Mathf.Max(0f, lowHealthMoveSpeedPercentMax);
            LowHealthMaxThreshold = Mathf.Clamp(lowHealthMaxThreshold, 0f, 1f);
            LowEnemyHealthDamagePercent = Mathf.Max(0f, lowEnemyHealthDamagePercent);
            LowEnemyHealthThreshold = Mathf.Clamp(lowEnemyHealthThreshold, 0f, 1f);
        }

        public RunAugmentId Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public MetaBonusValues Bonuses { get; }
        public int ExtraWeaponSlots { get; }
        public float MaxHealthScale { get; }
        public float DamageTakenScale { get; }
        public bool SuppressPassiveRegen { get; }
        public int LifestealHealPerHit { get; }
        public float LifestealDamageRatio { get; }
        public float LifestealMaxHealPerHit { get; }
        public float LifestealBossMultiplier { get; }
        public float LifestealInternalCooldown { get; }
        public float LowHealthDamagePercentMax { get; }
        public float LowHealthMoveSpeedPercentMax { get; }
        public float LowHealthMaxThreshold { get; }
        public float LowEnemyHealthDamagePercent { get; }
        public float LowEnemyHealthThreshold { get; }
    }

    public static class SharedAugmentCatalog
    {
        private static readonly RunAugmentDefinition[] s_definitions =
        {
            new(
                RunAugmentId.Berserk,
                "광전사",
                "피해량 +18%",
                new MetaBonusValues { attackPowerPercent = 18f }),
            new(
                RunAugmentId.Overclock,
                "과부하",
                "공격 속도 +18%",
                new MetaBonusValues { attackSpeedPercent = 18f }),
            new(
                RunAugmentId.Finisher,
                "마무리타격",
                "체력 30% 이하 적 대상 피해 2배",
                default,
                lowEnemyHealthDamagePercent: 100f,
                lowEnemyHealthThreshold: 0.3f),
            new(
                RunAugmentId.CloseQuarters,
                "백병전",
                "공격 범위 -35%, 피해량 +60%",
                new MetaBonusValues
                {
                    attackPowerPercent = 60f,
                    attackRangePercent = -35f,
                }),
            new(
                RunAugmentId.Ambidextrous,
                "양손잡이",
                "무기 슬롯 +1",
                default,
                extraWeaponSlots: 1),
            new(
                RunAugmentId.GlassCannon,
                "유리대포",
                "피해량 +55%, 받는 피해 2배",
                new MetaBonusValues { attackPowerPercent = 55f },
                damageTakenScale: 2f),
            new(
                RunAugmentId.CautiousAttack,
                "신중한 공격",
                "공격력 +45%, 공격 속도 -18%",
                new MetaBonusValues
                {
                    attackPowerPercent = 45f,
                    attackSpeedPercent = -18f,
                }),
            new(
                RunAugmentId.Vampirism,
                "흡혈",
                "체력 재생 불가, 직접 타격 피해의 4% 흡혈 (최소 1, 최대 4, 보스 60%)",
                default,
                suppressPassiveRegen: true,
                lifestealDamageRatio: 0.04f,
                lifestealMaxHealPerHit: 4f,
                lifestealBossMultiplier: 0.6f,
                lifestealInternalCooldown: 0.10f),
            new(
                RunAugmentId.BerserkerHeart,
                "광폭화",
                "체력이 낮을수록 공격력/이동 속도 증가 (40% 이하 최대)",
                default,
                lowHealthDamagePercentMax: 35f,
                lowHealthMoveSpeedPercentMax: 35f,
                lowHealthMaxThreshold: 0.4f),
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

