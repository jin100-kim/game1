using EJR.Game.Core;
using System.Collections.Generic;

namespace EJR.Game.Gameplay
{
    public static class WeaponStrategyFactory
    {
        private static readonly Dictionary<WeaponUpgradeId, IWeaponStrategy> _strategies = new()
        {
            { WeaponUpgradeId.Fireball, new FireballWeaponStrategy() },
            { WeaponUpgradeId.Slash, new SlashWeaponStrategy() },
            { WeaponUpgradeId.LightningBolt, new LightningBoltWeaponStrategy() },
            { WeaponUpgradeId.IceSpike, new IceSpikeWeaponStrategy() },
            { WeaponUpgradeId.WindBlade, new WindBladeWeaponStrategy() },
            { WeaponUpgradeId.ChaosBurst, new ChaosBurstWeaponStrategy() },
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
