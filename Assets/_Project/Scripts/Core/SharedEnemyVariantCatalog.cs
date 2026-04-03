using System;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Core
{
    public enum EnemyVariantId
    {
        None = 0,
        SlimeSplit = 1,
        SlimeBomber = 2,
        MushroomShooter = 3,
        MushroomHealer = 4,
        SkeletonCharger = 5,
        SkeletonArcher = 6,
    }

    public enum EnemyVariantBehaviorKind
    {
        None = 0,
        SplitOnDeath = 1,
        ProximityBomber = 2,
        Shooter = 3,
        Healer = 4,
        Charger = 5,
        Archer = 6,
    }

    [Serializable]
    public sealed class EnemyVariantDefinition
    {
        public EnemyVariantId Id;
        public string DisplayName;
        public RuntimeSpriteFactory.EnemyVisualKind BaseVisualKind;
        public EnemyVariantBehaviorKind BehaviorKind;
        public Color TintColor = Color.white;
        public float HealthMultiplier = 1f;
        public float MoveSpeedMultiplier = 1f;
        public float ContactDamageMultiplier = 1f;
        public float VisualScaleMultiplier = 1f;
        public float CollisionRadiusMultiplier = 1f;
        public int SplitSpawnCount;
        public int SplitGenerationLimit;
        public float TriggerDamageRatio;
        public float TriggerDistance;
        public float WindupSeconds;
        public float ExplosionRadius;
        public float ExplosionDamageMultiplier = 1f;
        public float AttackCooldown;
        public float ProjectileSpeed;
        public float ProjectileLifetime;
        public float ProjectileDamageMultiplier = 1f;
        public float HealRadius;
        public float HealAmount;
        public float DashTelegraphSeconds;
        public float DashDuration;
        public float DashSpeedMultiplier = 1f;
        public float DesiredMinRange;
        public float DesiredMaxRange;
    }

    public static class SharedEnemyVariantCatalog
    {
        private static readonly EnemyVariantDefinition[] Definitions =
        {
            new()
            {
                Id = EnemyVariantId.SlimeSplit,
                DisplayName = "\uC2AC\uB77C\uC784 \uBD84\uC5F4\uD615",
                BaseVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Slime,
                BehaviorKind = EnemyVariantBehaviorKind.SplitOnDeath,
                TintColor = ParseColor("#62D9FF"),
                HealthMultiplier = 0.9f,
                MoveSpeedMultiplier = 1.1f,
                ContactDamageMultiplier = 0.9f,
                VisualScaleMultiplier = 0.95f,
                CollisionRadiusMultiplier = 0.95f,
                SplitSpawnCount = 2,
                SplitGenerationLimit = 1,
            },
            new()
            {
                Id = EnemyVariantId.SlimeBomber,
                DisplayName = "\uC2AC\uB77C\uC784 \uC790\uD3ED\uD615",
                BaseVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Slime,
                BehaviorKind = EnemyVariantBehaviorKind.ProximityBomber,
                TintColor = ParseColor("#FF8A5B"),
                HealthMultiplier = 1.2f,
                MoveSpeedMultiplier = 1f,
                ContactDamageMultiplier = 1f,
                VisualScaleMultiplier = 1.1f,
                CollisionRadiusMultiplier = 1.1f,
                TriggerDamageRatio = 0.25f,
                TriggerDistance = 1.1f,
                WindupSeconds = 0.9f,
                ExplosionRadius = 1.4f,
                ExplosionDamageMultiplier = 1.6f,
            },
            new()
            {
                Id = EnemyVariantId.MushroomShooter,
                DisplayName = "\uBC84\uC12F \uC0AC\uC218",
                BaseVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Mushroom,
                BehaviorKind = EnemyVariantBehaviorKind.Shooter,
                TintColor = ParseColor("#C58BFF"),
                HealthMultiplier = 1.1f,
                MoveSpeedMultiplier = 0.95f,
                ContactDamageMultiplier = 1f,
                VisualScaleMultiplier = 1f,
                CollisionRadiusMultiplier = 1f,
                AttackCooldown = 2.2f,
                ProjectileSpeed = 5.5f,
                ProjectileLifetime = 2.5f,
                ProjectileDamageMultiplier = 0.9f,
            },
            new()
            {
                Id = EnemyVariantId.MushroomHealer,
                DisplayName = "\uBC84\uC12F \uD68C\uBCF5\uD615",
                BaseVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Mushroom,
                BehaviorKind = EnemyVariantBehaviorKind.Healer,
                TintColor = ParseColor("#A6FF54"),
                HealthMultiplier = 1.15f,
                MoveSpeedMultiplier = 0.9f,
                ContactDamageMultiplier = 1f,
                VisualScaleMultiplier = 1f,
                CollisionRadiusMultiplier = 1f,
                HealRadius = 2.5f,
                AttackCooldown = 2.6f,
                HealAmount = 18f,
            },
            new()
            {
                Id = EnemyVariantId.SkeletonCharger,
                DisplayName = "\uD574\uACE8 \uB3CC\uC9C4",
                BaseVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Skeleton,
                BehaviorKind = EnemyVariantBehaviorKind.Charger,
                TintColor = ParseColor("#FF6B6B"),
                HealthMultiplier = 1.35f,
                MoveSpeedMultiplier = 1f,
                ContactDamageMultiplier = 1f,
                VisualScaleMultiplier = 1f,
                CollisionRadiusMultiplier = 1f,
                DashTelegraphSeconds = 0.5f,
                DashDuration = 0.45f,
                DashSpeedMultiplier = 4.5f,
                AttackCooldown = 2.2f,
            },
            new()
            {
                Id = EnemyVariantId.SkeletonArcher,
                DisplayName = "\uD574\uACE8 \uAD81\uC218",
                BaseVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Skeleton,
                BehaviorKind = EnemyVariantBehaviorKind.Archer,
                TintColor = ParseColor("#7DC6FF"),
                HealthMultiplier = 0.9f,
                MoveSpeedMultiplier = 0.95f,
                ContactDamageMultiplier = 1f,
                VisualScaleMultiplier = 1f,
                CollisionRadiusMultiplier = 1f,
                DesiredMinRange = 4f,
                DesiredMaxRange = 6f,
                AttackCooldown = 2.4f,
                ProjectileSpeed = 7f,
                ProjectileLifetime = 3.2f,
                ProjectileDamageMultiplier = 1f,
            },
        };

        private static readonly Dictionary<EnemyVariantId, EnemyVariantDefinition> Lookup = BuildLookup();

        public static IReadOnlyList<EnemyVariantDefinition> All => Definitions;

        public static EnemyVariantDefinition Get(EnemyVariantId id)
        {
            return Lookup.TryGetValue(id, out var definition) ? definition : null;
        }

        public static IReadOnlyList<string> GetDisplayNames()
        {
            var names = new string[Definitions.Length];
            for (var i = 0; i < Definitions.Length; i++)
            {
                names[i] = Definitions[i].DisplayName;
            }

            return names;
        }

        public static EnemyVariantId GetByIndex(int index)
        {
            if (index < 0 || index >= Definitions.Length)
            {
                return Definitions[0].Id;
            }

            return Definitions[index].Id;
        }

        public static int GetIndex(EnemyVariantId id)
        {
            for (var i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].Id == id)
                {
                    return i;
                }
            }

            return 0;
        }

        private static Dictionary<EnemyVariantId, EnemyVariantDefinition> BuildLookup()
        {
            var dictionary = new Dictionary<EnemyVariantId, EnemyVariantDefinition>(Definitions.Length);
            for (var i = 0; i < Definitions.Length; i++)
            {
                dictionary[Definitions[i].Id] = Definitions[i];
            }

            return dictionary;
        }

        private static Color ParseColor(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.white;
        }
    }
}
