using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EJR.Game.Core
{
    public static class MetaProgressionService
    {
        private const string SaveFileName = "meta-profile.json";
        private const int CurrentSaveVersion = 2;

        private static bool s_loaded;
        private static MetaProfileData s_profile;
        private static MetaProgressionConfig s_config;

        public static MetaProgressionConfig Config
        {
            get
            {
                EnsureLoaded();
                return s_config;
            }
        }

        public static int CurrentCredits
        {
            get
            {
                EnsureLoaded();
                return Mathf.Max(0, s_profile.currentCredits);
            }
        }

        public static int TotalCreditsEarned
        {
            get
            {
                EnsureLoaded();
                return Mathf.Max(0, s_profile.totalCreditsEarned);
            }
        }

        public static int RunsPlayed
        {
            get
            {
                EnsureLoaded();
                return Mathf.Max(0, s_profile.runsPlayed);
            }
        }

        public static int RunsCleared
        {
            get
            {
                EnsureLoaded();
                return Mathf.Max(0, s_profile.runsCleared);
            }
        }

        public static int BestLevel
        {
            get
            {
                EnsureLoaded();
                return Mathf.Max(1, s_profile.bestLevel);
            }
        }

        public static float BestTimeSeconds
        {
            get
            {
                EnsureLoaded();
                return Mathf.Max(0f, s_profile.bestTimeSeconds);
            }
        }

        public static int TotalEnemiesDefeated
        {
            get
            {
                EnsureLoaded();
                return Mathf.Max(0, s_profile.totalEnemiesDefeated);
            }
        }

        public static void EnsureLoaded()
        {
            if (s_loaded)
            {
                return;
            }

            s_config = MetaProgressionConfig.CreateRuntimeDefault();
            var loadedProfile = LoadProfile();
            s_profile = loadedProfile != null && loadedProfile.saveVersion == CurrentSaveVersion
                ? loadedProfile
                : CreateDefaultProfile();
            SanitizeProfile();
            s_loaded = true;
        }

        public static void SaveNow()
        {
            EnsureLoaded();

            try
            {
                var json = JsonUtility.ToJson(s_profile, true);
                File.WriteAllText(GetSavePath(), json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to save meta profile: {exception.Message}");
            }
        }

        public static bool IsCharacterUnlocked(int characterId)
        {
            EnsureLoaded();
            return s_profile.unlockedCharacterIds.Contains(SharedGameCatalog.NormalizeCharacterId(characterId));
        }

        public static bool IsWeaponUnlocked(WeaponUpgradeId weaponId)
        {
            EnsureLoaded();
            return SharedGameCatalog.IsStarterWeaponSelectable(weaponId);
        }

        public static int GetUnlockedCharacterMask()
        {
            EnsureLoaded();
            var mask = 0;
            for (var i = 0; i < s_profile.unlockedCharacterIds.Count; i++)
            {
                mask |= SharedGameCatalog.GetCharacterMask(s_profile.unlockedCharacterIds[i]);
            }

            return mask;
        }

        public static int GetUnlockedWeaponMask()
        {
            EnsureLoaded();
            var mask = 0;
            for (var i = 0; i < SharedGameCatalog.StarterWeaponDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.StarterWeaponDefinitions[i];
                if (!definition.IsSelectable)
                {
                    continue;
                }

                mask |= SharedGameCatalog.GetWeaponMask(definition.Id);
            }

            return mask;
        }

        public static int GetSingleSelectedCharacterId()
        {
            EnsureLoaded();
            return s_profile.lastSingleCharacterId;
        }

        public static WeaponUpgradeId GetSingleSelectedStarterWeapon()
        {
            EnsureLoaded();
            return SharedGameCatalog.GetStarterWeaponForCharacter(s_profile.lastSingleCharacterId);
        }

        public static void SetSingleSelectedCharacterId(int characterId)
        {
            EnsureLoaded();
            characterId = SharedGameCatalog.NormalizeCharacterId(characterId);
            if (!IsCharacterUnlocked(characterId))
            {
                return;
            }

            s_profile.lastSingleCharacterId = characterId;
            SaveNow();
        }

        public static void SetSingleSelectedStarterWeapon(WeaponUpgradeId weaponId)
        {
            // Starter weapon selection was removed. Character choice now determines the starter.
        }

        public static int GetNextUnlockedCharacterId(int currentCharacterId)
        {
            EnsureLoaded();
            var normalizedCurrent = SharedGameCatalog.NormalizeCharacterId(currentCharacterId);
            for (var offset = 1; offset <= SharedGameCatalog.CharacterCount; offset++)
            {
                var candidate = SharedGameCatalog.NormalizeCharacterId(normalizedCurrent + offset);
                if (IsCharacterUnlocked(candidate))
                {
                    return candidate;
                }
            }

            return GetSingleSelectedCharacterId();
        }

        public static WeaponUpgradeId GetNextUnlockedStarterWeapon(WeaponUpgradeId currentWeaponId)
        {
            return currentWeaponId;
        }

        public static WeaponUpgradeId GetCharacterStarterWeapon(int characterId)
        {
            return SharedGameCatalog.GetStarterWeaponForCharacter(characterId);
        }

        public static MetaBonusValues GetCharacterTraitBonuses(int characterId)
        {
            return GetCharacterBaseBonuses(characterId);
        }

        public static MetaBonusValues GetCharacterBaseBonuses(int characterId)
        {
            EnsureLoaded();
            return SharedGameCatalog.GetCharacter(characterId).BaseBonuses;
        }

        public static CharacterPassiveId GetCharacterPassiveId(int characterId)
        {
            EnsureLoaded();
            return SharedGameCatalog.GetCharacter(characterId).PassiveId;
        }

        public static string GetCharacterPassiveDescription(int characterId)
        {
            EnsureLoaded();
            return SharedGameCatalog.GetCharacter(characterId).PassiveDescription;
        }

        public static bool TryPurchaseCharacter(int characterId, out string reason)
        {
            EnsureLoaded();
            var definition = SharedGameCatalog.GetCharacter(characterId);
            if (IsCharacterUnlocked(definition.Id))
            {
                reason = "이미 해금된 캐릭터입니다.";
                return false;
            }

            if (CurrentCredits < definition.UnlockCost)
            {
                reason = "코인이 부족합니다.";
                return false;
            }

            s_profile.currentCredits -= definition.UnlockCost;
            s_profile.unlockedCharacterIds.Add(definition.Id);
            SaveNow();
            reason = string.Empty;
            return true;
        }

        public static int GetUpgradeLevel(MetaUpgradeId upgradeId)
        {
            EnsureLoaded();
            return GetUpgradeLevelInternal(upgradeId);
        }

        public static bool TryPurchaseUpgrade(MetaUpgradeId upgradeId, out string reason)
        {
            EnsureLoaded();
            if (!Config.TryGetUpgradeDefinition(upgradeId, out var definition))
            {
                reason = "강화 정보를 찾을 수 없습니다.";
                return false;
            }

            var currentLevel = GetUpgradeLevelInternal(upgradeId);
            if (currentLevel >= definition.MaxLevel)
            {
                reason = "이미 최대 단계입니다.";
                return false;
            }

            var cost = Config.GetUpgradeCost(upgradeId, currentLevel);
            if (CurrentCredits < cost)
            {
                reason = "코인이 부족합니다.";
                return false;
            }

            s_profile.currentCredits -= cost;
            SetUpgradeLevelInternal(upgradeId, currentLevel + 1);
            SaveNow();
            reason = string.Empty;
            return true;
        }

        public static int GetUpgradeRefundPreview()
        {
            EnsureLoaded();
            var refund = 0;
            for (var i = 0; i < s_profile.upgradeLevels.Count; i++)
            {
                var entry = s_profile.upgradeLevels[i];
                if (!Config.TryGetUpgradeDefinition((MetaUpgradeId)entry.id, out var definition))
                {
                    continue;
                }

                var level = Mathf.Clamp(entry.level, 0, definition.MaxLevel);
                for (var step = 0; step < level; step++)
                {
                    refund += Config.GetUpgradeCost((MetaUpgradeId)entry.id, step);
                }
            }

            return Mathf.Max(0, refund);
        }

        public static bool TryRefundAllUpgrades(out int refundedCredits, out string reason)
        {
            EnsureLoaded();
            refundedCredits = GetUpgradeRefundPreview();
            if (refundedCredits <= 0)
            {
                reason = "환불할 강화가 없습니다.";
                return false;
            }

            s_profile.currentCredits += refundedCredits;
            s_profile.upgradeLevels.Clear();
            SaveNow();
            reason = string.Empty;
            return true;
        }

        public static MetaBonusValues GetPurchasedUpgradeBonuses()
        {
            EnsureLoaded();
            var combined = default(MetaBonusValues);
            for (var i = 0; i < s_profile.upgradeLevels.Count; i++)
            {
                var entry = s_profile.upgradeLevels[i];
                if (!Config.TryGetUpgradeDefinition((MetaUpgradeId)entry.id, out var definition))
                {
                    continue;
                }

                combined += definition.StepBonuses * Mathf.Max(0, entry.level);
            }

            return combined;
        }

        public static MetaBonusValues GetCombinedRunStartBonuses(int characterId)
        {
            EnsureLoaded();
            return GetPurchasedUpgradeBonuses() + GetCharacterBaseBonuses(characterId);
        }

        public static bool IsMapCleared(string mapId)
        {
            EnsureLoaded();
            return !string.IsNullOrWhiteSpace(mapId) && s_profile.clearedMapIds.Contains(mapId);
        }

        public static RunRewardSummary BuildRunRewardSummary(
            string modeLabel,
            bool cleared,
            int finalLevel,
            float survivalTimeSeconds,
            int enemiesDefeated,
            RunCombatStats combatStats,
            int bossThresholdsReached,
            string mapId,
            float creditGainPercent)
        {
            EnsureLoaded();
            var safeMapId = string.IsNullOrWhiteSpace(mapId) ? "Gameplay" : mapId;
            var breakdown = Config.BuildCreditBreakdown(
                safeMapId,
                cleared,
                enemiesDefeated,
                survivalTimeSeconds,
                bossThresholdsReached,
                creditGainPercent,
                IsMapCleared(safeMapId));

            return new RunRewardSummary
            {
                modeLabel = string.IsNullOrWhiteSpace(modeLabel) ? "싱글" : modeLabel,
                mapId = safeMapId,
                cleared = cleared,
                bossReached = bossThresholdsReached > 0,
                finalLevel = Mathf.Max(1, finalLevel),
                survivalTimeSeconds = Mathf.Max(0f, survivalTimeSeconds),
                enemiesDefeated = Mathf.Max(0, enemiesDefeated),
                creditsEarned = breakdown.totalCredits,
                creditBreakdown = breakdown,
                combatStats = combatStats ?? new RunCombatStats(),
            };
        }

        public static RunRewardSummary BuildRunRewardSummary(
            string modeLabel,
            bool cleared,
            int finalLevel,
            float survivalTimeSeconds,
            int enemiesDefeated,
            bool bossReached)
        {
            return BuildRunRewardSummary(
                modeLabel,
                cleared,
                finalLevel,
                survivalTimeSeconds,
                enemiesDefeated,
                new RunCombatStats(),
                bossReached ? 1 : 0,
                "Gameplay",
                0f);
        }

        public static void RecordRunSummary(RunRewardSummary summary)
        {
            EnsureLoaded();
            if (summary == null)
            {
                return;
            }

            var earnedCredits = Mathf.Max(0, summary.creditsEarned);
            s_profile.currentCredits += earnedCredits;
            s_profile.totalCreditsEarned += earnedCredits;
            s_profile.runsPlayed++;
            if (summary.cleared)
            {
                s_profile.runsCleared++;
                if (!string.IsNullOrWhiteSpace(summary.mapId) && !s_profile.clearedMapIds.Contains(summary.mapId))
                {
                    s_profile.clearedMapIds.Add(summary.mapId);
                }
            }

            s_profile.bestLevel = Mathf.Max(s_profile.bestLevel, Mathf.Max(1, summary.finalLevel));
            s_profile.bestTimeSeconds = Mathf.Max(s_profile.bestTimeSeconds, Mathf.Max(0f, summary.survivalTimeSeconds));
            s_profile.totalEnemiesDefeated += Mathf.Max(0, summary.enemiesDefeated);
            s_profile.pendingRunSummary = summary;
            SaveNow();
        }

        public static bool TryPeekPendingRunSummary(out RunRewardSummary summary)
        {
            EnsureLoaded();
            summary = s_profile.pendingRunSummary;
            return summary != null;
        }

        public static void ClearPendingRunSummary()
        {
            EnsureLoaded();
            if (s_profile.pendingRunSummary == null)
            {
                return;
            }

            s_profile.pendingRunSummary = null;
            SaveNow();
        }

        public static IReadOnlyList<MetaUpgradeProgressEntry> GetUpgradeProgressEntries()
        {
            EnsureLoaded();
            return s_profile.upgradeLevels;
        }

        private static MetaProfileData LoadProfile()
        {
            try
            {
                var path = GetSavePath();
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<MetaProfileData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load meta profile: {exception.Message}");
                return null;
            }
        }

        private static MetaProfileData CreateDefaultProfile()
        {
            var profile = new MetaProfileData
            {
                saveVersion = CurrentSaveVersion,
                lastSingleCharacterId = SharedGameCatalog.GetDefaultUnlockedCharacterId(),
            };

            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                if (definition.DefaultUnlocked)
                {
                    profile.unlockedCharacterIds.Add(definition.Id);
                }
            }

            return profile;
        }

        private static void SanitizeProfile()
        {
            s_profile ??= CreateDefaultProfile();
            s_profile.saveVersion = CurrentSaveVersion;
            s_profile.unlockedCharacterIds ??= new List<int>();
            s_profile.upgradeLevels ??= new List<MetaUpgradeProgressEntry>();
            s_profile.clearedMapIds ??= new List<string>();

            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                if (definition.DefaultUnlocked && !s_profile.unlockedCharacterIds.Contains(definition.Id))
                {
                    s_profile.unlockedCharacterIds.Add(definition.Id);
                }
            }

            DeduplicateInts(s_profile.unlockedCharacterIds);
            DeduplicateStrings(s_profile.clearedMapIds);
            SanitizeUpgradeLevels();

            var normalizedCharacterId = SharedGameCatalog.NormalizeCharacterId(s_profile.lastSingleCharacterId);
            if (!s_profile.unlockedCharacterIds.Contains(normalizedCharacterId))
            {
                s_profile.lastSingleCharacterId = SharedGameCatalog.GetDefaultUnlockedCharacterId();
            }
            else
            {
                s_profile.lastSingleCharacterId = normalizedCharacterId;
            }

            s_profile.currentCredits = Mathf.Max(0, s_profile.currentCredits);
            s_profile.totalCreditsEarned = Mathf.Max(0, s_profile.totalCreditsEarned);
            s_profile.runsPlayed = Mathf.Max(0, s_profile.runsPlayed);
            s_profile.runsCleared = Mathf.Max(0, s_profile.runsCleared);
            s_profile.bestLevel = Mathf.Max(1, s_profile.bestLevel);
            s_profile.bestTimeSeconds = Mathf.Max(0f, s_profile.bestTimeSeconds);
            s_profile.totalEnemiesDefeated = Mathf.Max(0, s_profile.totalEnemiesDefeated);
        }

        private static void SanitizeUpgradeLevels()
        {
            var seen = new HashSet<int>();
            for (var i = s_profile.upgradeLevels.Count - 1; i >= 0; i--)
            {
                var entry = s_profile.upgradeLevels[i];
                var upgradeId = (MetaUpgradeId)entry.id;
                if (!Config.TryGetUpgradeDefinition(upgradeId, out var definition) || !seen.Add(entry.id))
                {
                    s_profile.upgradeLevels.RemoveAt(i);
                    continue;
                }

                entry.level = Mathf.Clamp(entry.level, 0, definition.MaxLevel);
                s_profile.upgradeLevels[i] = entry;
            }
        }

        private static int GetUpgradeLevelInternal(MetaUpgradeId upgradeId)
        {
            for (var i = 0; i < s_profile.upgradeLevels.Count; i++)
            {
                if (s_profile.upgradeLevels[i].id == (int)upgradeId)
                {
                    return Mathf.Max(0, s_profile.upgradeLevels[i].level);
                }
            }

            return 0;
        }

        private static void SetUpgradeLevelInternal(MetaUpgradeId upgradeId, int level)
        {
            for (var i = 0; i < s_profile.upgradeLevels.Count; i++)
            {
                if (s_profile.upgradeLevels[i].id != (int)upgradeId)
                {
                    continue;
                }

                s_profile.upgradeLevels[i].level = Mathf.Max(0, level);
                return;
            }

            s_profile.upgradeLevels.Add(new MetaUpgradeProgressEntry
            {
                id = (int)upgradeId,
                level = Mathf.Max(0, level),
            });
        }

        private static void DeduplicateInts(List<int> values)
        {
            var seen = new HashSet<int>();
            for (var i = values.Count - 1; i >= 0; i--)
            {
                if (!seen.Add(values[i]))
                {
                    values.RemoveAt(i);
                }
            }
        }

        private static void DeduplicateStrings(List<string> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = values.Count - 1; i >= 0; i--)
            {
                var value = values[i];
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                {
                    values.RemoveAt(i);
                }
            }
        }

        private static string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, SaveFileName);
        }
    }
}
