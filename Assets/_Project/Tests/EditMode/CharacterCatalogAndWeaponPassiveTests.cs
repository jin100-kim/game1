using EJR.Game.Core;
using EJR.Game.Gameplay;
using NUnit.Framework;

namespace EJR.Game.Tests.EditMode
{
    public sealed class CharacterCatalogAndWeaponPassiveTests
    {
        [Test]
        public void NewWeaponCharacters_HaveExpectedBaseHealthAndPassive()
        {
            for (var characterId = 6; characterId <= 10; characterId++)
            {
                var definition = SharedGameCatalog.GetCharacter(characterId);
                Assert.That(definition.BaseBonuses.maxHealthFlat, Is.EqualTo(20f));
                Assert.That(definition.PassiveId, Is.EqualTo(CharacterPassiveId.StarterWeaponSpecialist));
                Assert.That(definition.UnlockSource, Is.EqualTo(CharacterUnlockSource.Shop));
            }
        }

        [Test]
        public void CharacterWeaponBonuses_OnlyApplyToChosenWeapon()
        {
            var build = new PlayerBuildRuntime();
            build.InitializeDefaults(grantStarterRifle: false);
            build.ApplyCharacterWeaponBonuses(WeaponUpgradeId.Shotgun, 10f, 0f, 10f);

            Assert.That(build.GetWeaponDamageBonusPercentTotal(WeaponUpgradeId.Shotgun), Is.EqualTo(10f));
            Assert.That(build.GetWeaponRangeBonusPercentTotal(WeaponUpgradeId.Shotgun), Is.EqualTo(10f));
            Assert.That(build.GetWeaponDamageBonusPercentTotal(WeaponUpgradeId.Rifle), Is.EqualTo(0f));
            Assert.That(build.GetWeaponRangeBonusPercentTotal(WeaponUpgradeId.Rifle), Is.EqualTo(0f));

            build.ClearCharacterWeaponBonuses();

            Assert.That(build.GetWeaponDamageBonusPercentTotal(WeaponUpgradeId.Shotgun), Is.EqualTo(0f));
            Assert.That(build.GetWeaponRangeBonusPercentTotal(WeaponUpgradeId.Shotgun), Is.EqualTo(0f));
        }
    }
}
