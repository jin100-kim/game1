using EJR.Game.Core;
using System;
using System.Collections.Generic;

namespace EJR.Game.Gameplay
{
    public static class WeaponStrategyFactory
    {
        private static readonly Dictionary<WeaponUpgradeId, IWeaponStrategy> _strategies = new()
        {
            { WeaponUpgradeId.Bat, new BatWeaponStrategy() },
            { WeaponUpgradeId.Rifle, new RifleWeaponStrategy() },
            { WeaponUpgradeId.Fireball, new FireballWeaponStrategy() },
            { WeaponUpgradeId.BfSword, new BfSwordWeaponStrategy() },
            { WeaponUpgradeId.SwingMace, new SwingMaceWeaponStrategy() },
            { WeaponUpgradeId.OrbitWeapon, new OrbitWeaponStrategy() },
            { WeaponUpgradeId.Turret, new TurretWeaponStrategy() },
            { WeaponUpgradeId.Aura, new AuraWeaponStrategy() },
            { WeaponUpgradeId.Slash, new SlashWeaponStrategy() },
            { WeaponUpgradeId.Shotgun, new ShotgunWeaponStrategy() },
            { WeaponUpgradeId.ChainLightning, new ChainLightningWeaponStrategy() }
        };

        public static IWeaponStrategy GetStrategy(WeaponUpgradeId id)
        {
            if (_strategies.TryGetValue(id, out var strategy))
            {
                return strategy;
            }
            return null;
        }
    }
}

