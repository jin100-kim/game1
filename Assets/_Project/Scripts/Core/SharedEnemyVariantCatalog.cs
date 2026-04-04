using System;
using System.Collections.Generic;
using EJR.Game.Gameplay;
using UnityEngine;
using Random = UnityEngine.Random;

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
        private const string ForestMapId = "forest";
        private const string DesertMapId = "desert";
        private const string SnowMapId = "snow";

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

        public static EnemyStatProfile CreateVariantStatProfile(EnemyConfig config, EnemyVariantDefinition definition)
        {
            if (config == null || definition == null)
            {
                return null;
            }

            var baseProfile = config.GetStatProfile(definition.BaseVisualKind);
            return new EnemyStatProfile
            {
                healthMultiplier = Mathf.Max(0.1f, (baseProfile != null ? baseProfile.healthMultiplier : 1f) * Mathf.Max(0.1f, definition.HealthMultiplier)),
                moveSpeedMultiplier = Mathf.Max(0.1f, (baseProfile != null ? baseProfile.moveSpeedMultiplier : 1f) * Mathf.Max(0.1f, definition.MoveSpeedMultiplier)),
                contactDamageMultiplier = Mathf.Max(0.1f, (baseProfile != null ? baseProfile.contactDamageMultiplier : 1f) * Mathf.Max(0.1f, definition.ContactDamageMultiplier)),
                experienceMultiplier = Mathf.Max(0.1f, baseProfile != null ? baseProfile.experienceMultiplier : 1f),
                visualScaleMultiplier = Mathf.Max(0.1f, (baseProfile != null ? baseProfile.visualScaleMultiplier : 1f) * Mathf.Max(0.1f, definition.VisualScaleMultiplier)),
                collisionRadiusMultiplier = Mathf.Max(0.1f, (baseProfile != null ? baseProfile.collisionRadiusMultiplier : 1f) * Mathf.Max(0.1f, definition.CollisionRadiusMultiplier)),
            };
        }

        public static RuntimeSpriteFactory.EnemyVisualKind PickDynamicVisualKind(string mapId, EnemyConfig config, float elapsedSeconds)
        {
            if (config == null)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Slime;
            }

            var canSpawnSlime = config.spawnSlime;
            var canSpawnMushroom = config.spawnMushroom;
            var canSpawnSkeleton = config.spawnSkeleton;
            if (!canSpawnSlime && !canSpawnMushroom && !canSpawnSkeleton)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Slime;
            }

            var skeletonChance = canSpawnSkeleton ? GetDynamicSkeletonChance(mapId, elapsedSeconds) : 0f;
            if (skeletonChance > 0f && Random.value < skeletonChance)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Skeleton;
            }

            if (!canSpawnSlime && !canSpawnMushroom)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Skeleton;
            }

            if (IsBeforeMushroomPhase(config, elapsedSeconds))
            {
                return canSpawnSlime
                    ? RuntimeSpriteFactory.EnemyVisualKind.Slime
                    : RuntimeSpriteFactory.EnemyVisualKind.Mushroom;
            }

            if (!canSpawnSlime)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Mushroom;
            }

            if (!canSpawnMushroom)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Slime;
            }

            var mushroomChance = GetDynamicMushroomChance(config, elapsedSeconds);
            return Random.value < mushroomChance
                ? RuntimeSpriteFactory.EnemyVisualKind.Mushroom
                : RuntimeSpriteFactory.EnemyVisualKind.Slime;
        }

        public static EnemyVariantDefinition PickDynamicVariant(string mapId, RuntimeSpriteFactory.EnemyVisualKind visualKind, float elapsedSeconds)
        {
            switch (NormalizeMapId(mapId))
            {
                case ForestMapId:
                    return visualKind switch
                    {
                        RuntimeSpriteFactory.EnemyVisualKind.Slime => PickWeighted(
                            EnemyVariantId.SlimeSplit,
                            elapsedSeconds < 150f ? 0.18f : elapsedSeconds < 300f ? 0.14f : 0.12f,
                            EnemyVariantId.SlimeBomber,
                            elapsedSeconds < 180f ? 0f : elapsedSeconds < 300f ? 0.06f : 0.10f),
                        RuntimeSpriteFactory.EnemyVisualKind.Mushroom => PickWeighted(
                            EnemyVariantId.MushroomShooter,
                            elapsedSeconds < 260f ? 0.10f : 0.12f,
                            EnemyVariantId.MushroomHealer,
                            elapsedSeconds < 320f ? 0.03f : 0.08f),
                        _ => null,
                    };

                case DesertMapId:
                    return visualKind switch
                    {
                        RuntimeSpriteFactory.EnemyVisualKind.Slime => PickWeighted(
                            EnemyVariantId.SlimeBomber,
                            elapsedSeconds < 240f ? 0.16f : 0.20f,
                            EnemyVariantId.SlimeSplit,
                            elapsedSeconds < 240f ? 0.08f : 0.10f),
                        RuntimeSpriteFactory.EnemyVisualKind.Mushroom => PickWeighted(
                            EnemyVariantId.MushroomShooter,
                            elapsedSeconds < 360f ? 0.18f : 0.14f,
                            EnemyVariantId.MushroomHealer,
                            elapsedSeconds < 360f ? 0.04f : 0.10f),
                        RuntimeSpriteFactory.EnemyVisualKind.Skeleton => PickWeighted(
                            EnemyVariantId.SkeletonCharger,
                            0.18f,
                            EnemyVariantId.SkeletonArcher,
                            0.10f),
                        _ => null,
                    };

                case SnowMapId:
                    return visualKind switch
                    {
                        RuntimeSpriteFactory.EnemyVisualKind.Slime => PickWeighted(
                            EnemyVariantId.SlimeSplit,
                            0.12f,
                            EnemyVariantId.SlimeBomber,
                            0.08f),
                        RuntimeSpriteFactory.EnemyVisualKind.Mushroom => PickWeighted(
                            EnemyVariantId.MushroomHealer,
                            0.14f,
                            EnemyVariantId.MushroomShooter,
                            0.12f),
                        RuntimeSpriteFactory.EnemyVisualKind.Skeleton => PickWeighted(
                            EnemyVariantId.SkeletonCharger,
                            elapsedSeconds < 360f ? 0.18f : 0.20f,
                            EnemyVariantId.SkeletonArcher,
                            elapsedSeconds < 360f ? 0.16f : 0.18f),
                        _ => null,
                    };

                default:
                    return null;
            }
        }

        public static EnemyVariantDefinition PickWaveVariant(
            string mapId,
            int waveIndex,
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            int spawnOrdinal,
            int totalCount)
        {
            if (spawnOrdinal < 0 || totalCount <= 0)
            {
                return null;
            }

            switch (NormalizeMapId(mapId))
            {
                case ForestMapId:
                    return visualKind switch
                    {
                        RuntimeSpriteFactory.EnemyVisualKind.Slime => waveIndex <= 1
                            ? PickPattern(spawnOrdinal, EnemyVariantId.SlimeSplit, EnemyVariantId.None, EnemyVariantId.None, EnemyVariantId.SlimeSplit)
                            : PickPattern(spawnOrdinal, EnemyVariantId.SlimeSplit, EnemyVariantId.None, EnemyVariantId.SlimeBomber, EnemyVariantId.None),
                        RuntimeSpriteFactory.EnemyVisualKind.Mushroom => waveIndex <= 1
                            ? PickPattern(spawnOrdinal, EnemyVariantId.MushroomShooter)
                            : PickPattern(spawnOrdinal, EnemyVariantId.MushroomShooter, EnemyVariantId.None, EnemyVariantId.MushroomHealer, EnemyVariantId.None),
                        _ => null,
                    };

                case DesertMapId:
                    return visualKind switch
                    {
                        RuntimeSpriteFactory.EnemyVisualKind.Slime => waveIndex <= 1
                            ? PickPattern(spawnOrdinal, EnemyVariantId.SlimeBomber, EnemyVariantId.None, EnemyVariantId.None, EnemyVariantId.SlimeSplit)
                            : PickPattern(spawnOrdinal, EnemyVariantId.SlimeBomber, EnemyVariantId.None, EnemyVariantId.SlimeBomber, EnemyVariantId.None, EnemyVariantId.SlimeSplit),
                        RuntimeSpriteFactory.EnemyVisualKind.Mushroom => waveIndex <= 1
                            ? null
                            : PickPattern(spawnOrdinal, EnemyVariantId.MushroomShooter, EnemyVariantId.None, EnemyVariantId.MushroomHealer, EnemyVariantId.None),
                        RuntimeSpriteFactory.EnemyVisualKind.Skeleton => PickPattern(spawnOrdinal, EnemyVariantId.SkeletonCharger, EnemyVariantId.None, EnemyVariantId.SkeletonArcher),
                        _ => null,
                    };

                case SnowMapId:
                    return visualKind switch
                    {
                        RuntimeSpriteFactory.EnemyVisualKind.Slime => waveIndex <= 1
                            ? PickPattern(spawnOrdinal, EnemyVariantId.SlimeSplit, EnemyVariantId.None, EnemyVariantId.SlimeBomber)
                            : PickPattern(spawnOrdinal, EnemyVariantId.SlimeSplit, EnemyVariantId.SlimeBomber, EnemyVariantId.None, EnemyVariantId.SlimeSplit),
                        RuntimeSpriteFactory.EnemyVisualKind.Mushroom => waveIndex <= 1
                            ? PickPattern(spawnOrdinal, EnemyVariantId.MushroomHealer, EnemyVariantId.None, EnemyVariantId.MushroomShooter)
                            : PickPattern(spawnOrdinal, EnemyVariantId.MushroomHealer, EnemyVariantId.MushroomShooter, EnemyVariantId.None, EnemyVariantId.MushroomHealer),
                        RuntimeSpriteFactory.EnemyVisualKind.Skeleton => PickPattern(spawnOrdinal, EnemyVariantId.SkeletonCharger, EnemyVariantId.SkeletonArcher, EnemyVariantId.None),
                        _ => null,
                    };

                default:
                    return null;
            }
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

        private static string NormalizeMapId(string mapId)
        {
            return string.IsNullOrWhiteSpace(mapId) ? SharedRunCatalog.DefaultMapId : mapId;
        }

        private static bool IsBeforeMushroomPhase(EnemyConfig config, float elapsedSeconds)
        {
            return elapsedSeconds < Mathf.Max(0f, config.mushroomPhaseStartSeconds);
        }

        private static float GetDynamicMushroomChance(EnemyConfig config, float elapsedSeconds)
        {
            var phaseStart = Mathf.Max(0f, config.mushroomPhaseStartSeconds);
            var phaseEnd = Mathf.Max(phaseStart + 1f, config.wave2TimeSeconds);
            if (elapsedSeconds < phaseStart)
            {
                return 0f;
            }

            if (elapsedSeconds < phaseEnd)
            {
                return Mathf.Clamp01(config.mushroomRatioAtPhaseStart);
            }

            return Mathf.Clamp01(config.mushroomRatioBeforeBoss);
        }

        private static float GetDynamicSkeletonChance(string mapId, float elapsedSeconds)
        {
            return NormalizeMapId(mapId) switch
            {
                DesertMapId => elapsedSeconds >= 420f ? 0.10f : elapsedSeconds >= 300f ? 0.06f : 0f,
                SnowMapId => elapsedSeconds >= 420f ? 0.24f : elapsedSeconds >= 240f ? 0.18f : elapsedSeconds >= 120f ? 0.10f : 0f,
                _ => 0f,
            };
        }

        private static EnemyVariantDefinition PickWeighted(
            EnemyVariantId primary,
            float primaryChance,
            EnemyVariantId secondary = EnemyVariantId.None,
            float secondaryChance = 0f)
        {
            var roll = Random.value;
            var clampedPrimaryChance = Mathf.Clamp01(primaryChance);
            if (primary != EnemyVariantId.None && roll < clampedPrimaryChance)
            {
                return Get(primary);
            }

            roll -= clampedPrimaryChance;
            var clampedSecondaryChance = Mathf.Clamp01(secondaryChance);
            if (secondary != EnemyVariantId.None && roll < clampedSecondaryChance)
            {
                return Get(secondary);
            }

            return null;
        }

        private static EnemyVariantDefinition PickPattern(int spawnOrdinal, params EnemyVariantId[] pattern)
        {
            if (pattern == null || pattern.Length <= 0)
            {
                return null;
            }

            var variantId = pattern[Mathf.Abs(spawnOrdinal) % pattern.Length];
            return variantId == EnemyVariantId.None ? null : Get(variantId);
        }
    }
}
