using System.Linq;
using EJR.Game.Core;
using EJR.Game.Gameplay;
using NUnit.Framework;

namespace EJR.Game.Tests.EditMode
{
    public sealed class RunAugmentTests
    {
        [Test]
        public void BuildRandomOptions_ExcludesOwnedAugments()
        {
            var ownedAugments = SharedAugmentCatalog.Definitions
                .Select(definition => definition.Id)
                .Where(id => id != RunAugmentId.CloseQuarters)
                .ToArray();

            var options = SharedAugmentCatalog.BuildRandomOptions(ownedAugments);

            Assert.That(options, Has.Length.EqualTo(1));
            Assert.That(options[0].Domain, Is.EqualTo(LevelUpOptionDomain.Augment));
            Assert.That(options[0].AugmentId, Is.EqualTo(RunAugmentId.CloseQuarters));
        }

        [Test]
        public void ApplyAugment_IgnoresDuplicateSelection()
        {
            var buildRuntime = new PlayerBuildRuntime();
            buildRuntime.InitializeDefaults(grantStarterRifle: false);

            var option = LevelUpOption.CreateAugment(
                RunAugmentId.Berserk,
                "광전사",
                "피해량 +15%",
                "광전사");

            buildRuntime.Apply(option);
            buildRuntime.Apply(option);

            Assert.That(buildRuntime.ActiveAugments.Count, Is.EqualTo(1));
            Assert.That(buildRuntime.ActiveAugments.First(), Is.EqualTo(RunAugmentId.Berserk));
            Assert.That(buildRuntime.GlobalAttackPowerPercentTotal, Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void Ambidextrous_AddsFourthWeaponSlot()
        {
            var buildRuntime = new PlayerBuildRuntime();
            buildRuntime.InitializeDefaults(grantStarterRifle: false);

            Assert.That(buildRuntime.GetUnlockedWeaponSlots(1), Is.EqualTo(1));
            Assert.That(buildRuntime.GetUnlockedWeaponSlots(PlayerBuildRuntime.ThirdWeaponUnlockLevel), Is.EqualTo(3));

            buildRuntime.Apply(LevelUpOption.CreateAugment(
                RunAugmentId.Ambidextrous,
                "양손잡이",
                "무기 슬롯 +1",
                "양손잡이"));

            Assert.That(buildRuntime.GetUnlockedWeaponSlots(1), Is.EqualTo(2));
            Assert.That(buildRuntime.GetUnlockedWeaponSlots(PlayerBuildRuntime.ThirdWeaponUnlockLevel), Is.EqualTo(4));
        }

        [Test]
        public void SpecialAugments_ModifyStatsRuntime()
        {
            var buildRuntime = new PlayerBuildRuntime();
            buildRuntime.InitializeDefaults(grantStarterRifle: false);
            buildRuntime.ApplyMetaBonuses(new MetaBonusValues { healthRegenPerSecond = 2f });
            buildRuntime.Apply(LevelUpOption.CreateAugment(RunAugmentId.GlassCannon, "유리대포", string.Empty, string.Empty));
            buildRuntime.Apply(LevelUpOption.CreateAugment(RunAugmentId.CautiousAttack, "신중한 공격", string.Empty, string.Empty));
            buildRuntime.Apply(LevelUpOption.CreateAugment(RunAugmentId.Vampirism, "흡혈", string.Empty, string.Empty));

            var stats = new PlayerStatsRuntime();
            stats.RecalculateFromBuild(buildRuntime);

            Assert.That(stats.DamageMultiplier, Is.EqualTo(2f).Within(0.001f));
            Assert.That(stats.AttackIntervalMultiplier, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(stats.MaxHealthScale, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(stats.HealthRegenPerSecond, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void BerserkerHeart_ScalesBonusesByCurrentHealthRatio()
        {
            var buildRuntime = new PlayerBuildRuntime();
            buildRuntime.InitializeDefaults(grantStarterRifle: false);
            buildRuntime.Apply(LevelUpOption.CreateAugment(RunAugmentId.BerserkerHeart, "광폭화", string.Empty, string.Empty));

            var fullHealthBonuses = buildRuntime.GetLowHealthDynamicBonuses(1f);
            var midHealthBonuses = buildRuntime.GetLowHealthDynamicBonuses(0.65f);
            var lowHealthBonuses = buildRuntime.GetLowHealthDynamicBonuses(0.2f);

            Assert.That(fullHealthBonuses.attackPowerPercent, Is.EqualTo(0f).Within(0.001f));
            Assert.That(fullHealthBonuses.moveSpeedPercent, Is.EqualTo(0f).Within(0.001f));
            Assert.That(midHealthBonuses.attackPowerPercent, Is.EqualTo(15f).Within(0.01f));
            Assert.That(midHealthBonuses.moveSpeedPercent, Is.EqualTo(15f).Within(0.01f));
            Assert.That(lowHealthBonuses.attackPowerPercent, Is.EqualTo(30f).Within(0.001f));
            Assert.That(lowHealthBonuses.moveSpeedPercent, Is.EqualTo(30f).Within(0.001f));
        }
    }
}
