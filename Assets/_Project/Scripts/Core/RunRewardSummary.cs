using System;
using System.Text;
using EJR.Game.Gameplay;

namespace EJR.Game.Core
{
    [Serializable]
    public sealed class RunRewardSummary
    {
        public string modeLabel = "\uC2F1\uAE00";
        public string mapId = string.Empty;
        public string mapDisplayName = string.Empty;
        public string difficultyLabel = string.Empty;
        public bool cleared;
        public bool bossReached;
        public int finalLevel = 1;
        public int enemiesDefeated;
        public float survivalTimeSeconds;
        public int creditsEarned;
        public RunCreditBreakdown creditBreakdown = new();
        public RunCombatStats combatStats = new();

        public string BuildDisplayText()
        {
            var builder = new StringBuilder();
            builder.AppendLine(cleared ? "\uD074\uB9AC\uC5B4" : "\uAC8C\uC784 \uC624\uBC84");
            builder.Append("\uBAA8\uB4DC ").Append(modeLabel)
                .Append(" | \uB9F5 ").Append(string.IsNullOrWhiteSpace(mapDisplayName) ? mapId : mapDisplayName)
                .Append(" | \uB09C\uC774\uB3C4 ").Append(string.IsNullOrWhiteSpace(difficultyLabel) ? "-" : difficultyLabel)
                .Append(" | \uB808\uBCA8 ").Append(finalLevel)
                .Append(" | \uC2DC\uAC04 ").Append(survivalTimeSeconds.ToString("0.0"))
                .Append("\uCD08 | \uCC98\uCE58 ").Append(enemiesDefeated)
                .AppendLine();
            builder.AppendLine();
            builder.AppendLine("\uCF54\uC778");
            builder.Append("\uCD1D \uD68D\uB4DD +").Append(creditsEarned);

            if (creditBreakdown != null)
            {
                builder.AppendLine()
                    .Append("\uCC98\uCE58 +").Append(creditBreakdown.killCredits)
                    .Append(" | \uC2DC\uAC04 +").Append(creditBreakdown.timeCredits)
                    .Append(" | \uBCF4\uC2A4 +").Append(creditBreakdown.bossDamageCredits)
                    .Append(" | \uCCAB \uD074\uB9AC\uC5B4 +").Append(creditBreakdown.firstClearCredits)
                    .Append(" | \uBCF4\uB108\uC2A4 +").Append(creditBreakdown.creditBonusApplied);
            }

            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("\uC804\uD22C");
            builder.Append("\uCD1D \uD53C\uD574 ").Append(combatStats != null ? combatStats.totalDamageDealt.ToString("0") : "0")
                .Append(" | \uBC1B\uC740 \uD53C\uD574 ").Append(combatStats != null ? combatStats.damageTaken.ToString("0") : "0")
                .Append(" | \uD68C\uBCF5 ").Append(combatStats != null ? combatStats.healingDone.ToString("0.#") : "0");

            if (combatStats?.weaponDamages != null && combatStats.weaponDamages.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
                builder.AppendLine("\uBB34\uAE30\uBCC4 \uD53C\uD574\uB7C9");
                for (var i = 0; i < combatStats.weaponDamages.Count; i++)
                {
                    var entry = combatStats.weaponDamages[i];
                    builder.Append(SharedGameCatalog.GetWeaponDisplayName((WeaponUpgradeId)entry.weaponId))
                        .Append("  ")
                        .Append(entry.damage.ToString("0"))
                        .AppendLine();
                }
            }

            return builder.ToString();
        }
    }
}
