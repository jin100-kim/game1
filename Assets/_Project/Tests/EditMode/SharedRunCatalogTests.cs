using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EJR.Game.Core;
using EJR.Game.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace EJR.Game.Tests.EditMode
{
    public sealed class SharedRunCatalogTests
    {
        private const BindingFlags StaticFlags = BindingFlags.NonPublic | BindingFlags.Static;

        private bool _hadSaveFile;
        private string _saveFilePath;
        private string _originalSaveJson;
        private bool _originalLoaded;
        private MetaProfileData _originalProfile;
        private MetaProgressionConfig _originalConfig;

        [SetUp]
        public void SetUp()
        {
            _saveFilePath = Path.Combine(Application.persistentDataPath, "meta-profile.json");
            _hadSaveFile = File.Exists(_saveFilePath);
            _originalSaveJson = _hadSaveFile ? File.ReadAllText(_saveFilePath) : null;
            _originalLoaded = (bool)GetStaticField("s_loaded");
            _originalProfile = (MetaProfileData)GetStaticField("s_profile");
            _originalConfig = (MetaProgressionConfig)GetStaticField("s_config");
        }

        [TearDown]
        public void TearDown()
        {
            SetStaticField("s_loaded", _originalLoaded);
            SetStaticField("s_profile", _originalProfile);
            SetStaticField("s_config", _originalConfig);

            if (_hadSaveFile)
            {
                File.WriteAllText(_saveFilePath, _originalSaveJson ?? string.Empty);
            }
            else if (File.Exists(_saveFilePath))
            {
                File.Delete(_saveFilePath);
            }
        }

        [Test]
        public void CreateRuntimeEnemyConfig_AppliesMapThenDifficulty()
        {
            var runtimeConfig = SharedRunCatalog.CreateRuntimeEnemyConfig(null, "desert", "hard");

            try
            {
                Assert.That(runtimeConfig.initialSpawnInterval, Is.EqualTo(2.07f).Within(0.001f));
                Assert.That(runtimeConfig.minimumSpawnInterval, Is.EqualTo(0.45f).Within(0.001f));
                Assert.That(runtimeConfig.minSpawnRadius, Is.EqualTo(8.5f).Within(0.001f));
                Assert.That(runtimeConfig.maxSpawnRadius, Is.EqualTo(13f).Within(0.001f));
                Assert.That(runtimeConfig.targetAliveStart, Is.EqualTo(5));
                Assert.That(runtimeConfig.targetAliveEnd, Is.EqualTo(48));
                Assert.That(runtimeConfig.hardAliveCap, Is.EqualTo(115));
                Assert.That(runtimeConfig.wave1TimeSeconds, Is.EqualTo(160f).Within(0.001f));
                Assert.That(runtimeConfig.wave2TimeSeconds, Is.EqualTo(320f).Within(0.001f));
                Assert.That(runtimeConfig.bossWaveStartSeconds, Is.EqualTo(555f).Within(0.001f));
                Assert.That(runtimeConfig.wave1SlimeCount, Is.EqualTo(23));
                Assert.That(runtimeConfig.wave2SlimeCount, Is.EqualTo(25));
                Assert.That(runtimeConfig.bossWaveSkeletonCount, Is.EqualTo(8));
                Assert.That(runtimeConfig.maxHealth, Is.EqualTo(60f).Within(0.001f));
                Assert.That(runtimeConfig.moveSpeed, Is.EqualTo(1.674f).Within(0.001f));
                Assert.That(runtimeConfig.contactDamage, Is.EqualTo(9.2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(runtimeConfig);
            }
        }

        [Test]
        public void RecordRunSummary_TracksFirstClearByMapOnly()
        {
            SetProfile(selectedCharacterId: SharedGameCatalog.GetDefaultUnlockedCharacterId(), unlockedCharacterIds: SharedGameCatalog.GetDefaultUnlockedCharacterId());

            var easySummary = MetaProgressionService.BuildRunRewardSummary(
                "Single",
                true,
                12,
                600f,
                80,
                new RunCombatStats(),
                10,
                "forest",
                "forest",
                "easy",
                0f);

            Assert.That(easySummary.creditBreakdown.firstClearCredits, Is.GreaterThan(0));

            MetaProgressionService.RecordRunSummary(easySummary);

            var hardSummary = MetaProgressionService.BuildRunRewardSummary(
                "Single",
                true,
                12,
                600f,
                80,
                new RunCombatStats(),
                10,
                "forest",
                "forest",
                "hard",
                0f);

            Assert.That(hardSummary.creditBreakdown.firstClearCredits, Is.EqualTo(0));

            MetaProgressionService.RecordRunSummary(hardSummary);

            var profile = (MetaProfileData)GetStaticField("s_profile");
            Assert.That(MetaProgressionService.IsMapCleared("forest"), Is.True);
            Assert.That(profile.clearedMapIds.FindAll(id => id == "forest").Count, Is.EqualTo(1));
        }

        private static void SetProfile(int selectedCharacterId, params int[] unlockedCharacterIds)
        {
            var profile = new MetaProfileData
            {
                saveVersion = 2,
                currentCredits = 0,
                totalCreditsEarned = 0,
                unlockedCharacterIds = new List<int>(unlockedCharacterIds),
                upgradeLevels = new List<MetaUpgradeProgressEntry>(),
                clearedMapIds = new List<string>(),
                lastSingleCharacterId = selectedCharacterId,
                bestLevel = 1,
            };

            SetStaticField("s_config", MetaProgressionConfig.CreateRuntimeDefault());
            SetStaticField("s_profile", profile);
            SetStaticField("s_loaded", true);
        }

        private static object GetStaticField(string fieldName)
        {
            return typeof(MetaProgressionService).GetField(fieldName, StaticFlags)?.GetValue(null);
        }

        private static void SetStaticField(string fieldName, object value)
        {
            typeof(MetaProgressionService).GetField(fieldName, StaticFlags)?.SetValue(null, value);
        }
    }
}
