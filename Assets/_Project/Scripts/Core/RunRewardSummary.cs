using System;

namespace EJR.Game.Core
{
    [Serializable]
    public sealed class RunRewardSummary
    {
        public string modeLabel = "Single";
        public bool cleared;
        public bool bossReached;
        public int finalLevel = 1;
        public int enemiesDefeated;
        public float survivalTimeSeconds;
        public int creditsEarned;

        public string BuildDisplayText()
        {
            var clearLabel = cleared ? "Cleared" : "Defeated";
            return $"{modeLabel} | {clearLabel}\n" +
                   $"Time {survivalTimeSeconds:0.0}s | Lv {finalLevel}\n" +
                   $"Kills {enemiesDefeated} | Credits +{creditsEarned}";
        }
    }
}
