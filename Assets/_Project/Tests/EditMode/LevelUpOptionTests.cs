using EJR.Game.Core;
using NUnit.Framework;

namespace EJR.Game.Tests.EditMode
{
    public sealed class LevelUpOptionTests
    {
        [Test]
        public void ApplyUpgrade_UpdatesExpectedStat()
        {
            var stats = new PlayerStatsRuntime();
            var originalDamage = stats.DamageMultiplier;
            var originalAttackInterval = stats.AttackIntervalMultiplier;
            var originalMoveSpeed = stats.MoveSpeedMultiplier;

            stats.ApplyUpgrade(new LevelUpOption(LevelUpUpgradeType.Damage, 0.2f, "Damage +20%"));
            stats.ApplyUpgrade(new LevelUpOption(LevelUpUpgradeType.AttackSpeed, 0.1f, "Attack Speed +10%"));
            stats.ApplyUpgrade(new LevelUpOption(LevelUpUpgradeType.MoveSpeed, 0.15f, "Move Speed +15%"));

            Assert.That(stats.DamageMultiplier, Is.EqualTo(originalDamage + 0.2f).Within(0.0001f));
            Assert.That(stats.AttackIntervalMultiplier, Is.LessThan(originalAttackInterval));
            Assert.That(stats.MoveSpeedMultiplier, Is.EqualTo(originalMoveSpeed + 0.15f).Within(0.0001f));
        }
    }
}
