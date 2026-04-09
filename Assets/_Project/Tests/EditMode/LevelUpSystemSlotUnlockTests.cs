using System.Linq;
using EJR.Game.Core;
using EJR.Game.Gameplay;
using NUnit.Framework;

namespace EJR.Game.Tests.EditMode
{
    public sealed class LevelUpSystemSlotUnlockTests
    {
        [Test]
        public void Level1_DoesNotOfferNewWeapon_WhenRifleAlreadyFillsUnlockedSlot()
        {
            var system = CreateSystemWithDefaultBuild(out _);
            var firstOptions = OpenChoiceAtLevel(system, 1);

            Assert.That(firstOptions.Length, Is.GreaterThan(0));
            Assert.That(ContainsNewWeaponAcrossRerolls(system, firstOptions, 12), Is.False);
        }

        [Test]
        public void Level4_DoesNotOfferNewWeapon_BeforeSecondSlotUnlocks()
        {
            var system = CreateSystemWithDefaultBuild(out _);
            var firstOptions = OpenChoiceAtLevel(system, 4);

            Assert.That(firstOptions.Length, Is.GreaterThan(0));
            Assert.That(ContainsNewWeaponAcrossRerolls(system, firstOptions, 12), Is.False);
        }

        [Test]
        public void Level5_CanOfferNewWeapon_WhenSecondSlotUnlocks()
        {
            var system = CreateSystemWithDefaultBuild(out _);
            var firstOptions = OpenChoiceAtLevel(system, 5);

            Assert.That(firstOptions.Length, Is.GreaterThan(0));
            Assert.That(ContainsNewWeaponAcrossRerolls(system, firstOptions, 64), Is.True);
        }

        [Test]
        public void Level9_DoesNotOfferThirdWeapon_WhenTwoSlotsAreFull()
        {
            var system = CreateSystemWithDefaultBuild(out var build);
            AcquireWeapon(build, WeaponUpgradeId.Fireball);
            var firstOptions = OpenChoiceAtLevel(system, 9);

            Assert.That(firstOptions.Length, Is.GreaterThan(0));
            Assert.That(ContainsNewWeaponAcrossRerolls(system, firstOptions, 12), Is.False);
        }

        [Test]
        public void Level10_CanOfferThirdWeapon_WhenThirdSlotUnlocks()
        {
            var system = CreateSystemWithDefaultBuild(out var build);
            AcquireWeapon(build, WeaponUpgradeId.Fireball);
            var firstOptions = OpenChoiceAtLevel(system, 10);

            Assert.That(firstOptions.Length, Is.GreaterThan(0));
            Assert.That(ContainsNewWeaponAcrossRerolls(system, firstOptions, 64), Is.True);
        }

        [Test]
        public void Level10Plus_NeverOffersMoreThanThreeWeapons()
        {
            var system = CreateSystemWithDefaultBuild(out var build);
            AcquireWeapon(build, WeaponUpgradeId.Fireball);
            AcquireWeapon(build, WeaponUpgradeId.Shotgun);
            var firstOptions = OpenChoiceAtLevel(system, 15);

            Assert.That(firstOptions.Length, Is.GreaterThan(0));
            Assert.That(ContainsNewWeaponAcrossRerolls(system, firstOptions, 24), Is.False);
        }

        [Test]
        public void WeaponLevel4_PromotesSpecialMilestoneAtLevel5()
        {
            var system = CreateSystemWithDefaultBuild(out var build);
            AdvanceWeaponToLevel(build, WeaponUpgradeId.Rifle, 4);
            var firstOptions = OpenChoiceAtLevel(system, 5);

            Assert.That(firstOptions.Length, Is.GreaterThan(0));
            Assert.That(ContainsSpecialMilestoneAcrossRerolls(system, firstOptions, WeaponUpgradeId.Rifle, 64), Is.True);
        }

        [Test]
        public void GlobalLuckCard_CanAppearInChoicePool()
        {
            var system = CreateSystemWithDefaultBuild(out _);
            var firstOptions = OpenChoiceAtLevel(system, 2);

            Assert.That(firstOptions.Length, Is.GreaterThan(0));
            Assert.That(ContainsGlobalStatAcrossRerolls(system, firstOptions, StatUpgradeId.Luck, 32), Is.True);
        }

        private static LevelUpSystem CreateSystemWithDefaultBuild(out PlayerBuildRuntime build)
        {
            build = new PlayerBuildRuntime();
            build.InitializeDefaults();

            var system = new LevelUpSystem();
            system.Initialize(build, LevelUpBalanceConfig.CreateRuntimeDefault());
            return system;
        }

        private static LevelUpOption[] OpenChoiceAtLevel(LevelUpSystem system, int targetLevel)
        {
            LevelUpOption[] captured = null;
            void HandleOptions(LevelUpOption[] options)
            {
                captured ??= options;
            }

            system.OptionsGenerated += HandleOptions;

            var experienceToTarget = 0;
            for (var level = 1; level < targetLevel; level++)
            {
                experienceToTarget += ProgressionMath.RequiredExperienceForLevel(level);
            }

            system.AddExperience(experienceToTarget > 0 ? experienceToTarget : system.RequiredExperience);
            system.OptionsGenerated -= HandleOptions;
            return captured ?? new LevelUpOption[0];
        }

        private static bool ContainsNewWeaponAcrossRerolls(LevelUpSystem system, LevelUpOption[] initialOptions, int rerolls)
        {
            if (ContainsNewWeapon(initialOptions))
            {
                return true;
            }

            for (var i = 0; i < rerolls; i++)
            {
                var rerolled = CaptureReroll(system);
                if (ContainsNewWeapon(rerolled))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSpecialMilestoneAcrossRerolls(LevelUpSystem system, LevelUpOption[] initialOptions, WeaponUpgradeId weaponId, int rerolls)
        {
            if (ContainsSpecialMilestone(initialOptions, weaponId))
            {
                return true;
            }

            for (var i = 0; i < rerolls; i++)
            {
                var rerolled = CaptureReroll(system);
                if (ContainsSpecialMilestone(rerolled, weaponId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsGlobalStatAcrossRerolls(LevelUpSystem system, LevelUpOption[] initialOptions, StatUpgradeId statId, int rerolls)
        {
            if (ContainsGlobalStat(initialOptions, statId))
            {
                return true;
            }

            for (var i = 0; i < rerolls; i++)
            {
                var rerolled = CaptureReroll(system);
                if (ContainsGlobalStat(rerolled, statId))
                {
                    return true;
                }
            }

            return false;
        }

        private static LevelUpOption[] CaptureReroll(LevelUpSystem system)
        {
            return system.TryRerollCurrentChoice(out var captured)
                ? captured
                : new LevelUpOption[0];
        }

        private static bool ContainsNewWeapon(LevelUpOption[] options)
        {
            return options.Any(option => option.Domain == LevelUpOptionDomain.WeaponAcquire && option.IsNewAcquire);
        }

        private static bool ContainsSpecialMilestone(LevelUpOption[] options, WeaponUpgradeId weaponId)
        {
            return options.Any(option =>
                option.Domain == LevelUpOptionDomain.WeaponMilestone &&
                option.IsSpecialMilestone &&
                option.WeaponId == weaponId);
        }

        private static bool ContainsGlobalStat(LevelUpOption[] options, StatUpgradeId statId)
        {
            return options.Any(option => option.Domain == LevelUpOptionDomain.GlobalStatRoll && option.StatId == statId);
        }

        private static void AcquireWeapon(PlayerBuildRuntime build, WeaponUpgradeId id)
        {
            build.Apply(LevelUpOption.CreateWeaponAcquire(id, string.Empty, string.Empty, string.Empty));
        }

        private static void AdvanceWeaponToLevel(PlayerBuildRuntime build, WeaponUpgradeId id, int targetLevel)
        {
            if (!build.HasWeapon(id))
            {
                AcquireWeapon(build, id);
            }

            while (build.GetWeaponLevel(id) < targetLevel)
            {
                var current = build.GetWeaponLevel(id);
                var next = current + 1;
                var option = next == 5 || next == 10
                    ? LevelUpOption.CreateWeaponMilestone(
                        id,
                        WeaponMilestoneKind.ExtraProjectile,
                        1f,
                        current,
                        next,
                        string.Empty,
                        string.Empty,
                        string.Empty)
                    : LevelUpOption.CreateWeaponRoll(
                        id,
                        WeaponRollKind.DamagePercent,
                        OptionRarity.Common,
                        12f,
                        current,
                        next,
                        string.Empty,
                        string.Empty,
                        string.Empty);

                build.Apply(option);
            }
        }
    }
}
