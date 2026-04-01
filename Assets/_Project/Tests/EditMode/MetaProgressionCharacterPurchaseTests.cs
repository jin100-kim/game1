using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EJR.Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace EJR.Game.Tests.EditMode
{
    public sealed class MetaProgressionCharacterPurchaseTests
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
        public void TryPurchaseCharacter_UnlocksWithoutChangingSelectedCharacter()
        {
            var unlockCost = SharedGameCatalog.GetCharacter(1).UnlockCost;
            SetProfile(currentCredits: unlockCost + 150, selectedCharacterId: 0, unlockedCharacterIds: 0);

            var purchased = MetaProgressionService.TryPurchaseCharacter(1, out var reason);

            Assert.That(purchased, Is.True);
            Assert.That(reason, Is.Empty);
            Assert.That(MetaProgressionService.CurrentCredits, Is.EqualTo(150));
            Assert.That(MetaProgressionService.IsCharacterUnlocked(1), Is.True);
            Assert.That(MetaProgressionService.GetSingleSelectedCharacterId(), Is.EqualTo(0));
        }

        [Test]
        public void TryPurchaseCharacter_FailurePreservesCreditsUnlocksAndSelection()
        {
            var unlockCost = SharedGameCatalog.GetCharacter(1).UnlockCost;
            SetProfile(currentCredits: unlockCost - 1, selectedCharacterId: 0, unlockedCharacterIds: 0);

            var purchased = MetaProgressionService.TryPurchaseCharacter(1, out var reason);

            Assert.That(purchased, Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(MetaProgressionService.CurrentCredits, Is.EqualTo(unlockCost - 1));
            Assert.That(MetaProgressionService.IsCharacterUnlocked(1), Is.False);
            Assert.That(MetaProgressionService.GetSingleSelectedCharacterId(), Is.EqualTo(0));
        }

        private static void SetProfile(int currentCredits, int selectedCharacterId, params int[] unlockedCharacterIds)
        {
            var profile = new MetaProfileData
            {
                saveVersion = 2,
                currentCredits = currentCredits,
                totalCreditsEarned = currentCredits,
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
