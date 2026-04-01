using System;
using System.Collections.Generic;

namespace EJR.Game.Core
{
    [Serializable]
    public sealed class MetaProfileData
    {
        public int saveVersion;
        public int currentCredits;
        public int totalCreditsEarned;
        public List<int> unlockedCharacterIds = new();
        public List<MetaUpgradeProgressEntry> upgradeLevels = new();
        public List<string> clearedMapIds = new();
        public int lastSingleCharacterId;
        public int runsPlayed;
        public int runsCleared;
        public int bestLevel = 1;
        public float bestTimeSeconds;
        public int totalEnemiesDefeated;
        public List<string> completedAchievementIds = new();
        public List<string> unseenAchievementIds = new();
        public RunRewardSummary pendingRunSummary;
    }
}
