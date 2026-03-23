using System;
using System.Collections.Generic;

namespace EJR.Game.Core
{
    [Serializable]
    public sealed class MetaProfileData
    {
        public int currentCredits;
        public int totalCreditsEarned;
        public List<int> unlockedCharacterIds = new();
        public List<int> unlockedWeaponIds = new();
        public List<int> purchasedNodeIds = new();
        public int lastSingleCharacterId;
        public int lastSingleStarterWeaponId;
        public int runsPlayed;
        public int runsCleared;
        public int bestLevel = 1;
        public float bestTimeSeconds;
        public int totalEnemiesDefeated;
        public RunRewardSummary pendingRunSummary;
    }
}
