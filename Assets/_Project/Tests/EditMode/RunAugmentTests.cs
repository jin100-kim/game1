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
            var options = SharedAugmentCatalog.BuildRandomOptions(new[]
            {
                RunAugmentId.Berserk,
                RunAugmentId.Overclock,
                RunAugmentId.LongReach,
                RunAugmentId.Fleetfoot,
            });

            Assert.That(options, Has.Length.EqualTo(1));
            Assert.That(options[0].Domain, Is.EqualTo(LevelUpOptionDomain.Augment));
            Assert.That(options[0].AugmentId, Is.EqualTo(RunAugmentId.VitalCore));
        }

        [Test]
        public void ApplyAugment_IgnoresDuplicateSelection()
        {
            var buildRuntime = new PlayerBuildRuntime();
            buildRuntime.InitializeDefaults(grantStarterRifle: false);

            var option = LevelUpOption.CreateAugment(
                RunAugmentId.Berserk,
                "Berserk",
                "공격력 +15%",
                "Berserk");

            buildRuntime.Apply(option);
            buildRuntime.Apply(option);

            Assert.That(buildRuntime.ActiveAugments.Count, Is.EqualTo(1));
            Assert.That(buildRuntime.ActiveAugments.First(), Is.EqualTo(RunAugmentId.Berserk));
            Assert.That(buildRuntime.GlobalAttackPowerPercentTotal, Is.EqualTo(15f).Within(0.001f));
        }
    }
}
