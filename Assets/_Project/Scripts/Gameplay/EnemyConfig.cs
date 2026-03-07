using System;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [Serializable]
    public struct EnemyAnimationClipRange
    {
        public string clipName;
        [Min(0)] public int startFrame;
        [Min(0)] public int endFrame;
        public bool loop;

        public EnemyAnimationClipRange(string clipName, int startFrame, int endFrame, bool loop = true)
        {
            this.clipName = clipName;
            this.startFrame = Mathf.Max(0, startFrame);
            this.endFrame = Mathf.Max(0, endFrame);
            this.loop = loop;
        }
    }

    [Serializable]
    public sealed class EnemyAnimationProfile
    {
        public RuntimeSpriteFactory.EnemyVisualKind visualKind = RuntimeSpriteFactory.EnemyVisualKind.Slime;
        [Min(1f)] public float animationFps = 9f;
        public bool flipByMoveDirection = true;
        [Min(0)] public int idleStartFrame = 0;
        [Min(0)] public int idleEndFrame = 3;
        [Min(0)] public int moveStartFrame = 4;
        [Min(0)] public int moveEndFrame = 8;
        public bool useHurtAnimation;
        [Min(0)] public int hurtStartFrame;
        [Min(0)] public int hurtEndFrame;
        [Min(0)] public int dieStartFrame = 9;
        [Min(0)] public int dieEndFrame = 12;
        [Header("Extracted Clip Ranges (Aseprite Tags)")]
        public EnemyAnimationClipRange[] clipRanges = Array.Empty<EnemyAnimationClipRange>();

        public void EnsureClipRangesInitialized()
        {
            var defaults = GetDefaultClipRanges(visualKind);
            if (defaults.Length <= 0)
            {
                clipRanges ??= Array.Empty<EnemyAnimationClipRange>();
                return;
            }

            if (clipRanges == null || clipRanges.Length == 0)
            {
                clipRanges = defaults;
                return;
            }

            var merged = new EnemyAnimationClipRange[clipRanges.Length + defaults.Length];
            var mergedCount = 0;

            for (var i = 0; i < clipRanges.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(clipRanges[i].clipName))
                {
                    continue;
                }

                merged[mergedCount++] = clipRanges[i];
            }

            for (var i = 0; i < defaults.Length; i++)
            {
                if (ContainsClipName(merged, mergedCount, defaults[i].clipName))
                {
                    continue;
                }

                merged[mergedCount++] = defaults[i];
            }

            if (mergedCount == merged.Length)
            {
                clipRanges = merged;
                return;
            }

            var compact = new EnemyAnimationClipRange[mergedCount];
            Array.Copy(merged, compact, mergedCount);
            clipRanges = compact;
        }

        public bool TryGetClipRange(string clipName, out EnemyAnimationClipRange range)
        {
            EnsureClipRangesInitialized();
            if (string.IsNullOrWhiteSpace(clipName) || clipRanges == null)
            {
                range = default;
                return false;
            }

            for (var i = 0; i < clipRanges.Length; i++)
            {
                var candidate = clipRanges[i];
                if (!string.Equals(candidate.clipName, clipName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                range = candidate;
                return true;
            }

            range = default;
            return false;
        }

        private static bool ContainsClipName(EnemyAnimationClipRange[] ranges, int count, string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName))
            {
                return false;
            }

            for (var i = 0; i < count; i++)
            {
                if (string.Equals(ranges[i].clipName, clipName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static EnemyAnimationClipRange[] GetDefaultClipRanges(RuntimeSpriteFactory.EnemyVisualKind kind)
        {
            return kind switch
            {
                RuntimeSpriteFactory.EnemyVisualKind.Mushroom => new[]
                {
                    new EnemyAnimationClipRange("Jump", 0, 6, true),
                    new EnemyAnimationClipRange("Idle", 7, 12, true),
                    new EnemyAnimationClipRange("Attack", 13, 19, false),
                    new EnemyAnimationClipRange("Death", 20, 24, false),
                },
                RuntimeSpriteFactory.EnemyVisualKind.Skeleton => new[]
                {
                    new EnemyAnimationClipRange("Idle", 0, 9, true),
                    new EnemyAnimationClipRange("Walk", 10, 19, true),
                    new EnemyAnimationClipRange("Defense", 20, 25, false),
                    new EnemyAnimationClipRange("Attack", 26, 31, false),
                    new EnemyAnimationClipRange("Hurt", 32, 37, false),
                    new EnemyAnimationClipRange("Death", 38, 45, false),
                },
                RuntimeSpriteFactory.EnemyVisualKind.Boss => new[]
                {
                    new EnemyAnimationClipRange("Idle", 0, 6, true),
                    new EnemyAnimationClipRange("Attack", 7, 14, false),
                    new EnemyAnimationClipRange("Fly", 15, 20, true),
                    new EnemyAnimationClipRange("Hurt", 21, 26, false),
                    new EnemyAnimationClipRange("Death", 27, 34, false),
                },
                _ => new[]
                {
                    new EnemyAnimationClipRange("Idle", 0, 3, true),
                    new EnemyAnimationClipRange("Walk", 4, 7, true),
                    new EnemyAnimationClipRange("Death", 8, 12, false),
                },
            };
        }
    }

    [Serializable]
    public sealed class EnemyStatProfile
    {
        public RuntimeSpriteFactory.EnemyVisualKind visualKind = RuntimeSpriteFactory.EnemyVisualKind.Slime;
        [Min(0.1f)] public float healthMultiplier = 1f;
        [Min(0.1f)] public float moveSpeedMultiplier = 1f;
        [Min(0.1f)] public float contactDamageMultiplier = 1f;
        [Min(0.1f)] public float experienceMultiplier = 1f;
        [Min(0.1f)] public float visualScaleMultiplier = 1f;
        [Min(0.1f)] public float collisionRadiusMultiplier = 1f;
    }

    [CreateAssetMenu(menuName = "EJR/Config/Enemy", fileName = "EnemyConfig")]
    public sealed class EnemyConfig : ScriptableObject
    {
        [Min(1f)] public float maxHealth = 24f;
        [Min(0.1f)] public float moveSpeed = 1.55f;
        [Min(1f)] public float contactDamage = 8f;
        [Min(0.1f)] public float contactDamageCooldown = 0.85f;
        [Min(1)] public int experienceOnDeath = 1;

        [Header("Size")]
        [Min(0.1f)] public float visualScale = 0.95f;
        [Min(0.05f)] public float collisionRadius = 0.24f;
        public float visualYOffset = -0.12f;

        [Header("Variant Spawn")]
        public bool spawnSlime = true;
        public bool spawnMushroom = true;
        public bool spawnSkeleton = true;
        public bool spawnBoss = true;

        [Header("Progression")]
        [Min(30f)] public float mushroomPhaseStartSeconds = 180f;
        [Min(60f)] public float bossWaveStartSeconds = 600f;
        [Range(0f, 1f)] public float mushroomRatioAtPhaseStart = 0.5f;
        [Range(0f, 1f)] public float mushroomRatioBeforeBoss = 1f;
        [Min(1)] public int bossWaveSkeletonCount = 15;
        [Min(0.1f)] public float bossSpawnRadius = 9f;
        [Min(0.1f)] public float skeletonWaveMinRadius = 7.5f;
        [Min(0.1f)] public float skeletonWaveMaxRadius = 11f;

        [Header("Timed Waves")]
        public bool enableTimedWaves = true;
        [Min(1f)] public float wave1TimeSeconds = 180f;
        [Min(1f)] public float wave2TimeSeconds = 360f;
        [Min(0)] public int wave1SlimeCount = 20;
        [Min(0)] public int wave1MushroomCount = 0;
        [Min(0)] public int wave1SkeletonCount = 0;
        [Min(0)] public int wave2SlimeCount = 0;
        [Min(0)] public int wave2MushroomCount = 30;
        [Min(0)] public int wave2SkeletonCount = 0;
        [Min(0.1f)] public float timedWaveMinRadius = 9.5f;
        [Min(0.1f)] public float timedWaveMaxRadius = 13f;

        [Header("Animation Profiles")]
        [SerializeField]
        private EnemyAnimationProfile[] animationProfiles =
        {
            new EnemyAnimationProfile
            {
                visualKind = RuntimeSpriteFactory.EnemyVisualKind.Slime,
                animationFps = 9f,
                flipByMoveDirection = true,
                idleStartFrame = 0,
                idleEndFrame = 3,
                moveStartFrame = 4,
                moveEndFrame = 7,
                useHurtAnimation = false,
                hurtStartFrame = 0,
                hurtEndFrame = 0,
                dieStartFrame = 8,
                dieEndFrame = 12,
                clipRanges = new[]
                {
                    new EnemyAnimationClipRange("Idle", 0, 3, true),
                    new EnemyAnimationClipRange("Walk", 4, 7, true),
                    new EnemyAnimationClipRange("Death", 8, 12, false),
                },
            },
            new EnemyAnimationProfile
            {
                visualKind = RuntimeSpriteFactory.EnemyVisualKind.Mushroom,
                animationFps = 9f,
                flipByMoveDirection = true,
                idleStartFrame = 7,
                idleEndFrame = 12,
                moveStartFrame = 0,
                moveEndFrame = 6,
                useHurtAnimation = false,
                hurtStartFrame = 0,
                hurtEndFrame = 0,
                dieStartFrame = 20,
                dieEndFrame = 24,
                clipRanges = new[]
                {
                    new EnemyAnimationClipRange("Jump", 0, 6, true),
                    new EnemyAnimationClipRange("Idle", 7, 12, true),
                    new EnemyAnimationClipRange("Attack", 13, 19, false),
                    new EnemyAnimationClipRange("Death", 20, 24, false),
                },
            },
            new EnemyAnimationProfile
            {
                visualKind = RuntimeSpriteFactory.EnemyVisualKind.Skeleton,
                animationFps = 10f,
                flipByMoveDirection = true,
                idleStartFrame = 0,
                idleEndFrame = 9,
                moveStartFrame = 10,
                moveEndFrame = 19,
                useHurtAnimation = true,
                hurtStartFrame = 32,
                hurtEndFrame = 37,
                dieStartFrame = 38,
                dieEndFrame = 45,
                clipRanges = new[]
                {
                    new EnemyAnimationClipRange("Idle", 0, 9, true),
                    new EnemyAnimationClipRange("Walk", 10, 19, true),
                    new EnemyAnimationClipRange("Defense", 20, 25, false),
                    new EnemyAnimationClipRange("Attack", 26, 31, false),
                    new EnemyAnimationClipRange("Hurt", 32, 37, false),
                    new EnemyAnimationClipRange("Death", 38, 45, false),
                },
            },
            new EnemyAnimationProfile
            {
                visualKind = RuntimeSpriteFactory.EnemyVisualKind.Boss,
                animationFps = 8f,
                flipByMoveDirection = true,
                idleStartFrame = 0,
                idleEndFrame = 6,
                moveStartFrame = 15,
                moveEndFrame = 20,
                useHurtAnimation = true,
                hurtStartFrame = 21,
                hurtEndFrame = 26,
                dieStartFrame = 27,
                dieEndFrame = 34,
                clipRanges = new[]
                {
                    new EnemyAnimationClipRange("Idle", 0, 6, true),
                    new EnemyAnimationClipRange("Attack", 7, 14, false),
                    new EnemyAnimationClipRange("Fly", 15, 20, true),
                    new EnemyAnimationClipRange("Hurt", 21, 26, false),
                    new EnemyAnimationClipRange("Death", 27, 34, false),
                },
            },
        };

        [Header("Type Stat Profiles")]
        [SerializeField]
        private EnemyStatProfile[] statProfiles =
        {
            new EnemyStatProfile
            {
                visualKind = RuntimeSpriteFactory.EnemyVisualKind.Slime,
                healthMultiplier = 1f,
                moveSpeedMultiplier = 1f,
                contactDamageMultiplier = 1f,
                experienceMultiplier = 1f,
                visualScaleMultiplier = 0.8f,
                collisionRadiusMultiplier = 1f,
            },
            new EnemyStatProfile
            {
                visualKind = RuntimeSpriteFactory.EnemyVisualKind.Mushroom,
                healthMultiplier = 1.5f,
                moveSpeedMultiplier = 1.02f,
                contactDamageMultiplier = 1.25f,
                experienceMultiplier = 1.5f,
                visualScaleMultiplier = 0.8f,
                collisionRadiusMultiplier = 1f,
            },
            new EnemyStatProfile
            {
                visualKind = RuntimeSpriteFactory.EnemyVisualKind.Skeleton,
                healthMultiplier = 44f,
                moveSpeedMultiplier = 1.22f,
                contactDamageMultiplier = 1.45f,
                experienceMultiplier = 2.2f,
                visualScaleMultiplier = 1f,
                collisionRadiusMultiplier = 1f,
            },
            new EnemyStatProfile
            {
                visualKind = RuntimeSpriteFactory.EnemyVisualKind.Boss,
                healthMultiplier = 287.5f,
                moveSpeedMultiplier = 1.2f,
                contactDamageMultiplier = 2.4f,
                experienceMultiplier = 25f,
                visualScaleMultiplier = 1.5f,
                collisionRadiusMultiplier = 1.8f,
            },
        };

        [Header("Crowd")]
        [Min(0f)] public float separationWeight = 1f;
        [Min(1f)] public float separationRangeMultiplier = 1f;
        [Min(0f)] public float overlapResolvePadding = 0f;

        [Header("Spawner")]
        [Min(0.1f)] public float initialSpawnInterval = 1.2f;
        [Min(0.05f)] public float minimumSpawnInterval = 0.25f;
        [Min(1f)] public float spawnRampSeconds = 480f;
        [Min(0.1f)] public float minSpawnRadius = 8f;
        [Min(0.1f)] public float maxSpawnRadius = 12f;

        public EnemyAnimationProfile GetAnimationProfile(RuntimeSpriteFactory.EnemyVisualKind kind)
        {
            if (animationProfiles != null)
            {
                for (var i = 0; i < animationProfiles.Length; i++)
                {
                    var profile = animationProfiles[i];
                    if (profile != null && profile.visualKind == kind)
                    {
                        profile.EnsureClipRangesInitialized();
                        return profile;
                    }
                }
            }

            var fallback = new EnemyAnimationProfile { visualKind = kind };
            fallback.EnsureClipRangesInitialized();
            return fallback;
        }

        public EnemyStatProfile GetStatProfile(RuntimeSpriteFactory.EnemyVisualKind kind)
        {
            if (statProfiles != null)
            {
                for (var i = 0; i < statProfiles.Length; i++)
                {
                    var profile = statProfiles[i];
                    if (profile != null && profile.visualKind == kind)
                    {
                        return profile;
                    }
                }
            }

            return new EnemyStatProfile { visualKind = kind };
        }

    }
}
