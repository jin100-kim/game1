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
            var options = CaptureOptionsAtLevel(system, 1);

            Assert.That(options.Length, Is.GreaterThan(0));
            Assert.That(options.Any(IsNewWeaponAcquire), Is.False);
        }

        [Test]
        public void Level9_DoesNotOfferNewWeapon_WhenSecondSlotStillLocked()
        {
            var system = CreateSystemWithDefaultBuild(out _);
            var options = CaptureOptionsAtLevel(system, 9);

            Assert.That(options.Length, Is.GreaterThan(0));
            Assert.That(options.Any(IsNewWeaponAcquire), Is.False);
        }

        [Test]
        public void Level10_CanOfferNewWeapon_WhenSecondSlotUnlocks()
        {
            var system = CreateSystemWithDefaultBuild(out var build);
            MaxOutWeapon(build, WeaponUpgradeId.Rifle);
            FillThreeStatsToMax(build);

            var options = CaptureOptionsAtLevel(system, 10);

            Assert.That(options.Length, Is.GreaterThan(0));
            Assert.That(options.Any(IsNewWeaponAcquire), Is.True);
        }

        [Test]
        public void Level19_DoesNotOfferThirdNewWeapon_WhenTwoSlotsAreFull()
        {
            var system = CreateSystemWithDefaultBuild(out var build);
            AcquireWeapon(build, WeaponUpgradeId.Smg);

            var options = CaptureOptionsAtLevel(system, 19);

            Assert.That(options.Length, Is.GreaterThan(0));
            Assert.That(options.Any(IsNewWeaponAcquire), Is.False);
        }

        [Test]
        public void Level20_CanOfferThirdNewWeapon_WhenThirdSlotUnlocks()
        {
            var system = CreateSystemWithDefaultBuild(out var build);
            AcquireWeapon(build, WeaponUpgradeId.Smg);
            MaxOutWeapon(build, WeaponUpgradeId.Rifle);
            MaxOutWeapon(build, WeaponUpgradeId.Smg);
            FillThreeStatsToMax(build);

            var options = CaptureOptionsAtLevel(system, 20);

            Assert.That(options.Length, Is.GreaterThan(0));
            Assert.That(options.Any(IsNewWeaponAcquire), Is.True);
        }

        [Test]
        public void Level20Plus_NeverOffersMoreThanThreeWeapons()
        {
            var system = CreateSystemWithDefaultBuild(out var build);
            AcquireWeapon(build, WeaponUpgradeId.Smg);
            AcquireWeapon(build, WeaponUpgradeId.Shotgun);

            var options = CaptureOptionsAtLevel(system, 25);

            if (options.Length > 0)
            {
                Assert.That(options.Any(IsNewWeaponAcquire), Is.False);
            }
            else
            {
                Assert.Pass();
            }
        }

        private static LevelUpSystem CreateSystemWithDefaultBuild(out PlayerBuildRuntime build)
        {
            build = new PlayerBuildRuntime();
            build.InitializeDefaults();

            var system = new LevelUpSystem();
            system.Initialize(build);
            return system;
        }

        private static LevelUpOption[] CaptureOptionsAtLevel(LevelUpSystem system, int targetLevel)
        {
            LevelUpOption[] captured = null;
            system.OptionsGenerated += options =>
            {
                if (captured == null)
                {
                    captured = options;
                }
            };

            var experienceToTarget = 0;
            for (var level = 1; level < targetLevel; level++)
            {
                experienceToTarget += ProgressionMath.RequiredExperienceForLevel(level);
            }

            if (experienceToTarget > 0)
            {
                system.AddExperience(experienceToTarget);
            }
            else
            {
                system.AddExperience(system.RequiredExperience);
            }

            return captured ?? new LevelUpOption[0];
        }

        private static bool IsNewWeaponAcquire(LevelUpOption option)
        {
            return option.Category == UpgradeCategory.Weapon && option.IsNewAcquire;
        }

        private static void AcquireWeapon(PlayerBuildRuntime build, WeaponUpgradeId id)
        {
            build.Apply(new LevelUpOption(
                UpgradeCategory.Weapon,
                id,
                default,
                0,
                1,
                isNewAcquire: true,
                isLockedBySlot: false,
                label: string.Empty));
        }

        private static void MaxOutWeapon(PlayerBuildRuntime build, WeaponUpgradeId id)
        {
            if (!build.HasWeapon(id))
            {
                AcquireWeapon(build, id);
            }

            while (build.GetWeaponLevel(id) < PlayerBuildRuntime.MaxWeaponLevel)
            {
                var current = build.GetWeaponLevel(id);
                build.Apply(new LevelUpOption(
                    UpgradeCategory.Weapon,
                    id,
                    default,
                    current,
                    current + 1,
                    isNewAcquire: false,
                    isLockedBySlot: false,
                    label: string.Empty));
            }
        }

        private static void FillThreeStatsToMax(PlayerBuildRuntime build)
        {
            var ids = new[]
            {
                StatUpgradeId.AttackPower,
                StatUpgradeId.AttackSpeed,
                StatUpgradeId.MoveSpeed,
            };

            for (var i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (!build.HasStat(id))
                {
                    build.Apply(new LevelUpOption(
                        UpgradeCategory.Stat,
                        default,
                        id,
                        0,
                        1,
                        isNewAcquire: true,
                        isLockedBySlot: false,
                        label: string.Empty));
                }

                while (build.GetStatLevel(id) < PlayerBuildRuntime.MaxStatLevel)
                {
                    var current = build.GetStatLevel(id);
                    build.Apply(new LevelUpOption(
                        UpgradeCategory.Stat,
                        default,
                        id,
                        current,
                        current + 1,
                        isNewAcquire: false,
                        isLockedBySlot: false,
                        label: string.Empty));
                }
            }
        }
    }
}
