using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EJR.Game.Core
{
    public static class MetaProgressionService
    {
        private const string SaveFileName = "meta-profile.json";

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
            s_profile = LoadProfile() ?? CreateDefaultProfile();
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
            return s_profile.unlockedWeaponIds.Contains((int)weaponId);
        }

        public static bool IsNodePurchased(MetaNodeId nodeId)
        {
            EnsureLoaded();
            return s_profile.purchasedNodeIds.Contains((int)nodeId);
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
            for (var i = 0; i < s_profile.unlockedWeaponIds.Count; i++)
            {
                mask |= SharedGameCatalog.GetWeaponMask((WeaponUpgradeId)s_profile.unlockedWeaponIds[i]);
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
            var selectedWeapon = (WeaponUpgradeId)s_profile.lastSingleStarterWeaponId;
            if (!SharedGameCatalog.IsStarterWeaponSelectable(selectedWeapon) || !IsWeaponUnlocked(selectedWeapon))
            {
                return SharedGameCatalog.GetDefaultUnlockedStarterWeapon();
            }

            return selectedWeapon;
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
            EnsureLoaded();
            if (!SharedGameCatalog.IsStarterWeaponSelectable(weaponId) || !IsWeaponUnlocked(weaponId))
            {
                return;
            }

            s_profile.lastSingleStarterWeaponId = (int)weaponId;
            SaveNow();
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
            EnsureLoaded();
            var currentIndex = SharedGameCatalog.GetStarterWeaponIndex(currentWeaponId);
            for (var offset = 1; offset <= SharedGameCatalog.StarterWeaponCount; offset++)
            {
                var candidateIndex = SharedGameCatalog.NormalizeStarterWeaponIndex(currentIndex + offset);
                var candidate = SharedGameCatalog.GetStarterWeaponByIndex(candidateIndex);
                if (SharedGameCatalog.IsStarterWeaponSelectable(candidate) && IsWeaponUnlocked(candidate))
                {
                    return candidate;
                }
            }

            return GetSingleSelectedStarterWeapon();
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
                reason = "크레딧이 부족합니다.";
                return false;
            }

            s_profile.currentCredits -= definition.UnlockCost;
            s_profile.unlockedCharacterIds.Add(definition.Id);
            SaveNow();
            reason = string.Empty;
            return true;
        }

        public static bool TryPurchaseWeapon(WeaponUpgradeId weaponId, out string reason)
        {
            EnsureLoaded();
            if (!SharedGameCatalog.IsStarterWeaponSelectable(weaponId))
            {
                reason = "현재 사용할 수 없는 무기입니다.";
                return false;
            }

            if (IsWeaponUnlocked(weaponId))
            {
                reason = "이미 해금된 무기입니다.";
                return false;
            }

            var definition = SharedGameCatalog.GetStarterWeaponDefinition(SharedGameCatalog.GetStarterWeaponIndex(weaponId));
            if (CurrentCredits < definition.UnlockCost)
            {
                reason = "크레딧이 부족합니다.";
                return false;
            }

            s_profile.currentCredits -= definition.UnlockCost;
            s_profile.unlockedWeaponIds.Add((int)weaponId);
            SaveNow();
            reason = string.Empty;
            return true;
        }

        public static bool TryPurchaseNode(MetaNodeId nodeId, out string reason)
        {
            EnsureLoaded();
            if (!Config.TryGetNodeDefinition(nodeId, out var definition))
            {
                reason = "연구 노드 정보를 찾을 수 없습니다.";
                return false;
            }

            if (IsNodePurchased(nodeId))
            {
                reason = "이미 연구한 노드입니다.";
                return false;
            }

            if (definition.HasPrerequisite && !IsNodePurchased(definition.PrerequisiteId))
            {
                reason = "선행 노드가 필요합니다.";
                return false;
            }

            if (CurrentCredits < definition.Cost)
            {
                reason = "크레딧이 부족합니다.";
                return false;
            }

            s_profile.currentCredits -= definition.Cost;
            s_profile.purchasedNodeIds.Add((int)nodeId);
            SaveNow();
            reason = string.Empty;
            return true;
        }

        public static MetaBonusValues GetCharacterTraitBonuses(int characterId)
        {
            EnsureLoaded();
            return SharedGameCatalog.GetCharacter(characterId).TraitBonuses;
        }

        public static MetaBonusValues GetPurchasedNodeBonuses()
        {
            EnsureLoaded();
            var combined = default(MetaBonusValues);
            for (var i = 0; i < s_profile.purchasedNodeIds.Count; i++)
            {
                if (Config.TryGetNodeDefinition((MetaNodeId)s_profile.purchasedNodeIds[i], out var definition))
                {
                    combined += definition.Bonuses;
                }
            }

            return combined;
        }

        public static MetaBonusValues GetCombinedRunStartBonuses(int characterId)
        {
            EnsureLoaded();
            return GetPurchasedNodeBonuses() + GetCharacterTraitBonuses(characterId);
        }

        public static RunRewardSummary BuildRunRewardSummary(
            string modeLabel,
            bool cleared,
            int finalLevel,
            float survivalTimeSeconds,
            int enemiesDefeated,
            bool bossReached)
        {
            EnsureLoaded();
            return new RunRewardSummary
            {
                modeLabel = string.IsNullOrWhiteSpace(modeLabel) ? "싱글" : modeLabel,
                cleared = cleared,
                bossReached = bossReached,
                finalLevel = Mathf.Max(1, finalLevel),
                survivalTimeSeconds = Mathf.Max(0f, survivalTimeSeconds),
                enemiesDefeated = Mathf.Max(0, enemiesDefeated),
                creditsEarned = Config.CalculateCredits(finalLevel, bossReached, cleared, enemiesDefeated),
            };
        }

        public static void RecordRunSummary(RunRewardSummary summary)
        {
            EnsureLoaded();
            if (summary == null)
            {
                return;
            }

            s_profile.currentCredits += Mathf.Max(0, summary.creditsEarned);
            s_profile.totalCreditsEarned += Mathf.Max(0, summary.creditsEarned);
            s_profile.runsPlayed++;
            if (summary.cleared)
            {
                s_profile.runsCleared++;
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
            var profile = new MetaProfileData();
            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                if (definition.DefaultUnlocked)
                {
                    profile.unlockedCharacterIds.Add(definition.Id);
                }
            }

            for (var i = 0; i < SharedGameCatalog.StarterWeaponDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.StarterWeaponDefinitions[i];
                if (definition.DefaultUnlocked)
                {
                    profile.unlockedWeaponIds.Add((int)definition.Id);
                }
            }

            profile.lastSingleCharacterId = SharedGameCatalog.GetDefaultUnlockedCharacterId();
            profile.lastSingleStarterWeaponId = (int)SharedGameCatalog.GetDefaultUnlockedStarterWeapon();
            return profile;
        }

        private static void SanitizeProfile()
        {
            s_profile ??= CreateDefaultProfile();
            s_profile.unlockedCharacterIds ??= new List<int>();
            s_profile.unlockedWeaponIds ??= new List<int>();
            s_profile.purchasedNodeIds ??= new List<int>();

            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                if (definition.DefaultUnlocked && !s_profile.unlockedCharacterIds.Contains(definition.Id))
                {
                    s_profile.unlockedCharacterIds.Add(definition.Id);
                }
            }

            for (var i = 0; i < SharedGameCatalog.StarterWeaponDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.StarterWeaponDefinitions[i];
                if (definition.DefaultUnlocked && !s_profile.unlockedWeaponIds.Contains((int)definition.Id))
                {
                    s_profile.unlockedWeaponIds.Add((int)definition.Id);
                }
            }

            Deduplicate(s_profile.unlockedCharacterIds);
            Deduplicate(s_profile.unlockedWeaponIds);
            Deduplicate(s_profile.purchasedNodeIds);

            var normalizedCharacterId = SharedGameCatalog.NormalizeCharacterId(s_profile.lastSingleCharacterId);
            if (!s_profile.unlockedCharacterIds.Contains(normalizedCharacterId))
            {
                s_profile.lastSingleCharacterId = SharedGameCatalog.GetDefaultUnlockedCharacterId();
            }
            else
            {
                s_profile.lastSingleCharacterId = normalizedCharacterId;
            }

            var selectedWeaponId = (WeaponUpgradeId)s_profile.lastSingleStarterWeaponId;
            if (!SharedGameCatalog.IsStarterWeaponSelectable(selectedWeaponId)
                || !s_profile.unlockedWeaponIds.Contains(s_profile.lastSingleStarterWeaponId))
            {
                s_profile.lastSingleStarterWeaponId = (int)SharedGameCatalog.GetDefaultUnlockedStarterWeapon();
            }

            s_profile.currentCredits = Mathf.Max(0, s_profile.currentCredits);
            s_profile.totalCreditsEarned = Mathf.Max(0, s_profile.totalCreditsEarned);
            s_profile.runsPlayed = Mathf.Max(0, s_profile.runsPlayed);
            s_profile.runsCleared = Mathf.Max(0, s_profile.runsCleared);
            s_profile.bestLevel = Mathf.Max(1, s_profile.bestLevel);
            s_profile.bestTimeSeconds = Mathf.Max(0f, s_profile.bestTimeSeconds);
            s_profile.totalEnemiesDefeated = Mathf.Max(0, s_profile.totalEnemiesDefeated);
        }

        private static void Deduplicate(List<int> values)
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

        private static string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, SaveFileName);
        }
    }
}
