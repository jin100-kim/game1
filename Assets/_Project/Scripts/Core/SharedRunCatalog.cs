using System.Collections.Generic;
using EJR.Game.Gameplay;
using UnityEngine;

namespace EJR.Game.Core
{
    public sealed class RunMapDefinition
    {
        public RunMapDefinition(
            string id,
            string displayName,
            Rect arenaBounds,
            Color cameraBackgroundColor,
            Color boundaryColor,
            string requiredAchievementId,
            RuntimeSpriteFactory.EnemyVisualKind bossVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Boss,
            BossArchetypeId bossArchetype = BossArchetypeId.Final)
        {
            Id = string.IsNullOrWhiteSpace(id) ? SharedRunCatalog.DefaultMapId : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            ArenaBounds = arenaBounds;
            CameraBackgroundColor = cameraBackgroundColor;
            BoundaryColor = boundaryColor;
            RequiredAchievementId = requiredAchievementId ?? string.Empty;
            BossVisualKind = bossVisualKind;
            BossArchetype = bossArchetype;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public Rect ArenaBounds { get; }
        public Color CameraBackgroundColor { get; }
        public Color BoundaryColor { get; }
        public string RequiredAchievementId { get; }
        public RuntimeSpriteFactory.EnemyVisualKind BossVisualKind { get; }
        public BossArchetypeId BossArchetype { get; }

        public float InitialSpawnInterval { get; set; }
        public float MinimumSpawnInterval { get; set; }
        public float MinSpawnRadius { get; set; }
        public float MaxSpawnRadius { get; set; }
        public int TargetAliveStart { get; set; }
        public int TargetAliveEnd { get; set; }
        public int HardAliveCap { get; set; }
        public float MushroomPhaseStartSeconds { get; set; }
        public float MushroomRatioAtPhaseStart { get; set; }
        public float MushroomRatioBeforeBoss { get; set; }
        public float Wave1TimeSeconds { get; set; }
        public float Wave2TimeSeconds { get; set; }
        public float Wave3TimeSeconds { get; set; }
        public int Wave1SlimeCount { get; set; }
        public int Wave1MushroomCount { get; set; }
        public int Wave1SkeletonCount { get; set; }
        public int Wave2SlimeCount { get; set; }
        public int Wave2MushroomCount { get; set; }
        public int Wave2SkeletonCount { get; set; }
        public int Wave3SlimeCount { get; set; }
        public int Wave3MushroomCount { get; set; }
        public int Wave3SkeletonCount { get; set; }
        public float BossWaveStartSeconds { get; set; }
    }

    public sealed class RunDifficultyDefinition
    {
        public RunDifficultyDefinition(
            string id,
            string displayName,
            float enemyHealthScale,
            float enemyMoveScale,
            float enemyDamageScale,
            float spawnIntervalScale,
            float minimumSpawnIntervalScale,
            float aliveTargetScale,
            float hardCapScale,
            float waveCountScale,
            float bossTelegraphScale,
            float bossCooldownScale,
            float bossActionCountScale,
            float bossProjectileSpeedScale,
            float bossDashSpeedScale,
            float bossPullSpeedScale)
        {
            Id = string.IsNullOrWhiteSpace(id) ? SharedRunCatalog.DefaultDifficultyId : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            EnemyHealthScale = enemyHealthScale;
            EnemyMoveScale = enemyMoveScale;
            EnemyDamageScale = enemyDamageScale;
            SpawnIntervalScale = spawnIntervalScale;
            MinimumSpawnIntervalScale = minimumSpawnIntervalScale;
            AliveTargetScale = aliveTargetScale;
            HardCapScale = hardCapScale;
            WaveCountScale = waveCountScale;
            BossTelegraphScale = bossTelegraphScale;
            BossCooldownScale = bossCooldownScale;
            BossActionCountScale = bossActionCountScale;
            BossProjectileSpeedScale = bossProjectileSpeedScale;
            BossDashSpeedScale = bossDashSpeedScale;
            BossPullSpeedScale = bossPullSpeedScale;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public float EnemyHealthScale { get; }
        public float EnemyMoveScale { get; }
        public float EnemyDamageScale { get; }
        public float SpawnIntervalScale { get; }
        public float MinimumSpawnIntervalScale { get; }
        public float AliveTargetScale { get; }
        public float HardCapScale { get; }
        public float WaveCountScale { get; }
        public float BossTelegraphScale { get; }
        public float BossCooldownScale { get; }
        public float BossActionCountScale { get; }
        public float BossProjectileSpeedScale { get; }
        public float BossDashSpeedScale { get; }
        public float BossPullSpeedScale { get; }
    }

    public static class SharedRunCatalog
    {
        public const string DefaultMapId = "forest";
        public const string DefaultDifficultyId = "normal";

        private static readonly RunMapDefinition[] s_mapDefinitions =
        {
            new RunMapDefinition(
                "forest",
                "\uC232",
                new Rect(-12f, -7f, 24f, 14f),
                new Color(0.10f, 0.16f, 0.12f, 1f),
                new Color(0.40f, 0.58f, 0.36f, 1f),
                string.Empty,
                RuntimeSpriteFactory.EnemyVisualKind.Boss,
                BossArchetypeId.Final)
            {
                InitialSpawnInterval = 2.50f,
                MinimumSpawnInterval = 0.55f,
                MinSpawnRadius = 8f,
                MaxSpawnRadius = 12f,
                TargetAliveStart = 3,
                TargetAliveEnd = 48,
                HardAliveCap = 90,
                MushroomPhaseStartSeconds = 220f,
                MushroomRatioAtPhaseStart = 0.20f,
                MushroomRatioBeforeBoss = 0.45f,
                Wave1TimeSeconds = 240f,
                Wave2TimeSeconds = 480f,
                Wave3TimeSeconds = 720f,
                Wave1SlimeCount = 12,
                Wave1MushroomCount = 2,
                Wave1SkeletonCount = 0,
                Wave2SlimeCount = 8,
                Wave2MushroomCount = 8,
                Wave2SkeletonCount = 0,
                Wave3SlimeCount = 0,
                Wave3MushroomCount = 0,
                Wave3SkeletonCount = 22,
                BossWaveStartSeconds = 900f,
            },
            new RunMapDefinition(
                "desert",
                "\uC0AC\uB9C9",
                new Rect(-15f, -9f, 30f, 18f),
                new Color(0.22f, 0.18f, 0.10f, 1f),
                new Color(0.82f, 0.66f, 0.32f, 1f),
                string.Empty,
                RuntimeSpriteFactory.EnemyVisualKind.Boss,
                BossArchetypeId.Final)
            {
                InitialSpawnInterval = 2.30f,
                MinimumSpawnInterval = 0.50f,
                MinSpawnRadius = 8.5f,
                MaxSpawnRadius = 13f,
                TargetAliveStart = 4,
                TargetAliveEnd = 54,
                HardAliveCap = 100,
                MushroomPhaseStartSeconds = 300f,
                MushroomRatioAtPhaseStart = 0.08f,
                MushroomRatioBeforeBoss = 0.22f,
                Wave1TimeSeconds = 240f,
                Wave2TimeSeconds = 480f,
                Wave3TimeSeconds = 720f,
                Wave1SlimeCount = 14,
                Wave1MushroomCount = 0,
                Wave1SkeletonCount = 0,
                Wave2SlimeCount = 14,
                Wave2MushroomCount = 6,
                Wave2SkeletonCount = 0,
                Wave3SlimeCount = 0,
                Wave3MushroomCount = 0,
                Wave3SkeletonCount = 22,
                BossWaveStartSeconds = 900f,
            },
            new RunMapDefinition(
                "snow",
                "\uC124\uC6D0",
                new Rect(-18f, -11f, 36f, 22f),
                new Color(0.12f, 0.18f, 0.24f, 1f),
                new Color(0.72f, 0.86f, 0.95f, 1f),
                string.Empty,
                RuntimeSpriteFactory.EnemyVisualKind.Boss,
                BossArchetypeId.Final)
            {
                InitialSpawnInterval = 2.20f,
                MinimumSpawnInterval = 0.48f,
                MinSpawnRadius = 9f,
                MaxSpawnRadius = 14f,
                TargetAliveStart = 4,
                TargetAliveEnd = 60,
                HardAliveCap = 110,
                MushroomPhaseStartSeconds = 170f,
                MushroomRatioAtPhaseStart = 0.35f,
                MushroomRatioBeforeBoss = 0.60f,
                Wave1TimeSeconds = 240f,
                Wave2TimeSeconds = 480f,
                Wave3TimeSeconds = 720f,
                Wave1SlimeCount = 10,
                Wave1MushroomCount = 6,
                Wave1SkeletonCount = 0,
                Wave2SlimeCount = 10,
                Wave2MushroomCount = 10,
                Wave2SkeletonCount = 0,
                Wave3SlimeCount = 0,
                Wave3MushroomCount = 0,
                Wave3SkeletonCount = 22,
                BossWaveStartSeconds = 900f,
            },
        };

        private static readonly RunDifficultyDefinition[] s_difficultyDefinitions =
        {
            new RunDifficultyDefinition(
                "normal",
                "\uAE30\uBCF8",
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f),
        };

        public static IReadOnlyList<RunMapDefinition> MapDefinitions => s_mapDefinitions;
        public static IReadOnlyList<RunDifficultyDefinition> DifficultyDefinitions => s_difficultyDefinitions;

        public static RunMapDefinition GetMap(string mapId)
        {
            if (TryGetMap(mapId, out var map))
            {
                return map;
            }

            return s_mapDefinitions[0];
        }

        public static bool TryGetMap(string mapId, out RunMapDefinition map)
        {
            for (var i = 0; i < s_mapDefinitions.Length; i++)
            {
                if (!string.Equals(s_mapDefinitions[i].Id, mapId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                map = s_mapDefinitions[i];
                return true;
            }

            map = s_mapDefinitions[0];
            return false;
        }

        public static RunMapDefinition GetMapByIndex(int mapIndex)
        {
            return s_mapDefinitions[Mathf.Clamp(mapIndex, 0, s_mapDefinitions.Length - 1)];
        }

        public static int GetMapIndex(string mapId)
        {
            for (var i = 0; i < s_mapDefinitions.Length; i++)
            {
                if (string.Equals(s_mapDefinitions[i].Id, mapId, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        public static RunDifficultyDefinition GetDifficulty(string difficultyId)
        {
            if (TryGetDifficulty(difficultyId, out var difficulty))
            {
                return difficulty;
            }

            return s_difficultyDefinitions[0];
        }

        public static bool TryGetDifficulty(string difficultyId, out RunDifficultyDefinition difficulty)
        {
            for (var i = 0; i < s_difficultyDefinitions.Length; i++)
            {
                if (!string.Equals(s_difficultyDefinitions[i].Id, difficultyId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                difficulty = s_difficultyDefinitions[i];
                return true;
            }

            difficulty = s_difficultyDefinitions[0];
            return false;
        }

        public static RunDifficultyDefinition GetDifficultyByIndex(int difficultyIndex)
        {
            return s_difficultyDefinitions[Mathf.Clamp(difficultyIndex, 0, s_difficultyDefinitions.Length - 1)];
        }

        public static int GetDifficultyIndex(string difficultyId)
        {
            for (var i = 0; i < s_difficultyDefinitions.Length; i++)
            {
                if (string.Equals(s_difficultyDefinitions[i].Id, difficultyId, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        public static bool IsMapUnlocked(string mapId)
        {
            var map = GetMap(mapId);
            return string.IsNullOrWhiteSpace(map.RequiredAchievementId)
                || MetaProgressionService.HasCompletedAchievement(map.RequiredAchievementId);
        }

        public static string GetMapUnlockRequirementText(string mapId)
        {
            var map = GetMap(mapId);
            if (string.IsNullOrWhiteSpace(map.RequiredAchievementId))
            {
                return string.Empty;
            }

            var requiredAchievement = SharedAchievementCatalog.GetDefinition(map.RequiredAchievementId);
            return requiredAchievement.DisplayName + " 필요";
        }

        public static EnemyConfig CreateRuntimeEnemyConfig(EnemyConfig source, string mapId, string difficultyId)
        {
            var runtimeConfig = ScriptableObject.CreateInstance<EnemyConfig>();
            runtimeConfig.hideFlags = HideFlags.HideAndDontSave;
            ApplySelectionToEnemyConfig(runtimeConfig, source, mapId, difficultyId);
            return runtimeConfig;
        }

        public static void ApplySelectionToEnemyConfig(EnemyConfig target, EnemyConfig source, string mapId, string difficultyId)
        {
            if (target == null)
            {
                return;
            }

            CopyEnemyConfig(target, source);
            ApplyMap(target, GetMap(mapId));
            ApplyDifficulty(target, GetDifficulty(difficultyId));
            NormalizeWaveOrdering(target);
        }

        public static void CopyEnemyConfig(EnemyConfig target, EnemyConfig source)
        {
            if (target == null)
            {
                return;
            }

            var template = source != null ? source : ScriptableObject.CreateInstance<EnemyConfig>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(template), target);
            if (source == null)
            {
                Object.Destroy(template);
            }
        }

        private static void ApplyMap(EnemyConfig config, RunMapDefinition map)
        {
            config.initialSpawnInterval = Mathf.Max(0.1f, map.InitialSpawnInterval);
            config.minimumSpawnInterval = Mathf.Max(0.05f, Mathf.Min(config.initialSpawnInterval, map.MinimumSpawnInterval));
            config.minSpawnRadius = Mathf.Max(0.1f, map.MinSpawnRadius);
            config.maxSpawnRadius = Mathf.Max(config.minSpawnRadius + 0.1f, map.MaxSpawnRadius);
            config.targetAliveStart = Mathf.Max(1, map.TargetAliveStart);
            config.targetAliveEnd = Mathf.Max(1, map.TargetAliveEnd);
            config.hardAliveCap = Mathf.Max(1, map.HardAliveCap);
            config.mushroomPhaseStartSeconds = Mathf.Max(1f, map.MushroomPhaseStartSeconds);
            config.mushroomRatioAtPhaseStart = Mathf.Clamp01(map.MushroomRatioAtPhaseStart);
            config.mushroomRatioBeforeBoss = Mathf.Clamp01(map.MushroomRatioBeforeBoss);
            config.wave1TimeSeconds = Mathf.Max(1f, map.Wave1TimeSeconds);
            config.wave2TimeSeconds = Mathf.Max(config.wave1TimeSeconds + 1f, map.Wave2TimeSeconds);
            config.wave3TimeSeconds = Mathf.Max(config.wave2TimeSeconds + 1f, map.Wave3TimeSeconds);
            config.wave1SlimeCount = Mathf.Max(0, map.Wave1SlimeCount);
            config.wave1MushroomCount = Mathf.Max(0, map.Wave1MushroomCount);
            config.wave1SkeletonCount = Mathf.Max(0, map.Wave1SkeletonCount);
            config.wave2SlimeCount = Mathf.Max(0, map.Wave2SlimeCount);
            config.wave2MushroomCount = Mathf.Max(0, map.Wave2MushroomCount);
            config.wave2SkeletonCount = Mathf.Max(0, map.Wave2SkeletonCount);
            config.wave3SlimeCount = Mathf.Max(0, map.Wave3SlimeCount);
            config.wave3MushroomCount = Mathf.Max(0, map.Wave3MushroomCount);
            config.wave3SkeletonCount = Mathf.Max(0, map.Wave3SkeletonCount);
            config.bossWaveStartSeconds = Mathf.Max(config.wave3TimeSeconds + 1f, map.BossWaveStartSeconds);
        }

        private static void ApplyDifficulty(EnemyConfig config, RunDifficultyDefinition difficulty)
        {
            config.maxHealth = Mathf.Max(1f, config.maxHealth * difficulty.EnemyHealthScale);
            config.moveSpeed = Mathf.Max(0.1f, config.moveSpeed * difficulty.EnemyMoveScale);
            config.contactDamage = Mathf.Max(0.1f, config.contactDamage * difficulty.EnemyDamageScale);
            config.initialSpawnInterval = Mathf.Max(0.1f, config.initialSpawnInterval * difficulty.SpawnIntervalScale);
            config.minimumSpawnInterval = Mathf.Max(0.05f, config.minimumSpawnInterval * difficulty.MinimumSpawnIntervalScale);
            config.minimumSpawnInterval = Mathf.Min(config.initialSpawnInterval, config.minimumSpawnInterval);
            config.targetAliveStart = Mathf.Max(1, Mathf.RoundToInt(config.targetAliveStart * difficulty.AliveTargetScale));
            config.targetAliveEnd = Mathf.Max(1, Mathf.RoundToInt(config.targetAliveEnd * difficulty.AliveTargetScale));
            config.hardAliveCap = Mathf.Max(1, Mathf.RoundToInt(config.hardAliveCap * difficulty.HardCapScale));
            config.wave1SlimeCount = ScaleCount(config.wave1SlimeCount, difficulty.WaveCountScale);
            config.wave1MushroomCount = ScaleCount(config.wave1MushroomCount, difficulty.WaveCountScale);
            config.wave1SkeletonCount = ScaleCount(config.wave1SkeletonCount, difficulty.WaveCountScale);
            config.wave2SlimeCount = ScaleCount(config.wave2SlimeCount, difficulty.WaveCountScale);
            config.wave2MushroomCount = ScaleCount(config.wave2MushroomCount, difficulty.WaveCountScale);
            config.wave2SkeletonCount = ScaleCount(config.wave2SkeletonCount, difficulty.WaveCountScale);
            config.wave3SlimeCount = ScaleCount(config.wave3SlimeCount, difficulty.WaveCountScale);
            config.wave3MushroomCount = ScaleCount(config.wave3MushroomCount, difficulty.WaveCountScale);
            config.wave3SkeletonCount = ScaleCount(config.wave3SkeletonCount, difficulty.WaveCountScale);
        }

        private static void NormalizeWaveOrdering(EnemyConfig config)
        {
            config.targetAliveEnd = Mathf.Max(config.targetAliveStart, config.targetAliveEnd);
            config.hardAliveCap = Mathf.Max(config.targetAliveEnd, config.hardAliveCap);
            config.wave2TimeSeconds = Mathf.Max(config.wave1TimeSeconds + 1f, config.wave2TimeSeconds);
            config.wave3TimeSeconds = Mathf.Max(config.wave2TimeSeconds + 1f, config.wave3TimeSeconds);
            config.bossWaveStartSeconds = Mathf.Max(config.wave3TimeSeconds + 1f, config.bossWaveStartSeconds);
        }

        private static int ScaleCount(int value, float scale)
        {
            return Mathf.Max(0, Mathf.RoundToInt(value * scale));
        }
    }
}
