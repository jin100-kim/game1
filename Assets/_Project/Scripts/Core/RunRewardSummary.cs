using System;

namespace EJR.Game.Core
{
    [Serializable]
    public sealed class RunRewardSummary
    {
        public string modeLabel = "싱글";
        public bool cleared;
        public bool bossReached;
        public int finalLevel = 1;
        public int enemiesDefeated;
        public float survivalTimeSeconds;
        public int creditsEarned;

        public string BuildDisplayText()
        {
            var clearLabel = cleared ? "클리어" : "실패";
            return $"{modeLabel} | {clearLabel}\n" +
                   $"시간 {survivalTimeSeconds:0.0}초 | 레벨 {finalLevel}\n" +
                   $"처치 {enemiesDefeated} | 크레딧 +{creditsEarned}";
        }
    }
}
