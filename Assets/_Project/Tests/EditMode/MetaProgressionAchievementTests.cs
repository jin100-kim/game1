using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EJR.Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace EJR.Game.Tests.EditMode
{
    public sealed class MetaProgressionAchievementTests
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
        public void EnsureLoaded_MigratesLegacyProfileAndAppliesRetroactiveAchievements()
        {
            var legacyProfile = new MetaProfileData
            {
                saveVersion = 2,
                currentCredits = 321,
                totalCreditsEarned = 321,
                unlockedCharacterIds = new List<int> { 0 },
                upgradeLevels = new List<MetaUpgradeProgressEntry>(),
                clearedMapIds = new List<string> { "forest", "desert" },
                lastSingleCharacterId = 0,
                runsPlayed = 5,
                runsCleared = 2,
                bestLevel = 21,
                totalEnemiesDefeated = 550,
            };

            File.WriteAllText(_saveFilePath, JsonUtility.ToJson(legacyProfile, true));
            SetStaticField("s_loaded", false);
            SetStaticField("s_profile", null);
            SetStaticField("s_config", null);

            MetaProgressionService.EnsureLoaded();

            var profile = (MetaProfileData)GetStaticField("s_profile");
            Assert.That(MetaProgressionService.CurrentCredits, Is.EqualTo(321));
            Assert.That(profile.saveVersion, Is.EqualTo(3));
            Assert.That(profile.completedAchievementIds, Does.Contain("first_sortie"));
            Assert.That(profile.completedAchievementIds, Does.Contain("first_clear"));
            Assert.That(profile.completedAchievementIds, Does.Contain("forest_clear"));
            Assert.That(profile.completedAchievementIds, Does.Contain("desert_clear"));
            Assert.That(profile.completedAchievementIds, Does.Contain("slayer_500"));
            Assert.That(profile.completedAchievementIds, Does.Contain("level_20"));
            Assert.That(profile.completedAchievementIds, Does.Not.Contain("snow_clear"));
            Assert.That(profile.unseenAchievementIds, Does.Contain("desert_clear"));
            Assert.That(MetaProgressionService.HasUnseenAchievements, Is.True);
            Assert.That(MetaProgressionService.IsCharacterUnlocked(4), Is.True);
            Assert.That(MetaProgressionService.IsCharacterUnlocked(5), Is.False);
        }

        [Test]
        public void TryPurchaseCharacter_AchievementUnlockCharacterReturnsFailure()
        {
            SetProfile(currentCredits: 500, selectedCharacterId: 0, unlockedCharacterIds: 0);

            var purchased = MetaProgressionService.TryPurchaseCharacter(4, out var reason);

            Assert.That(purchased, Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(MetaProgressionService.CurrentCredits, Is.EqualTo(500));
            Assert.That(MetaProgressionService.IsCharacterUnlocked(4), Is.False);
        }

        [Test]
        public void RecordRunSummary_DoesNotDuplicateAchievementRewardForAlreadyUnlockedCharacter()
        {
            SetProfile(0, 0, 0, 4);

            var summary = MetaProgressionService.BuildRunRewardSummary(
                "Single",
                true,
                12,
                600f,
                80,
                new RunCombatStats(),
                5,
                "desert",
                "사막",
                "보통",
                0f);

            MetaProgressionService.RecordRunSummary(summary);
            MetaProgressionService.RecordRunSummary(summary);

            var profile = (MetaProfileData)GetStaticField("s_profile");
            Assert.That(profile.completedAchievementIds.FindAll(id => id == "desert_clear").Count, Is.EqualTo(1));
            Assert.That(profile.unlockedCharacterIds.FindAll(id => id == 4).Count, Is.EqualTo(1));
            Assert.That(profile.unseenAchievementIds.FindAll(id => id == "desert_clear").Count, Is.EqualTo(1));
        }

        private static void SetProfile(int currentCredits, int selectedCharacterId, params int[] unlockedCharacterIds)
        {
            var profile = new MetaProfileData
            {
                saveVersion = 3,
                currentCredits = currentCredits,
                totalCreditsEarned = currentCredits,
                unlockedCharacterIds = new List<int>(unlockedCharacterIds),
                upgradeLevels = new List<MetaUpgradeProgressEntry>(),
                clearedMapIds = new List<string>(),
                completedAchievementIds = new List<string>(),
                unseenAchievementIds = new List<string>(),
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
