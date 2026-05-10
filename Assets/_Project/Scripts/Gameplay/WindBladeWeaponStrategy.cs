using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class WindBladeWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.WindBlade;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var definition = system.GetWeaponDefinition(weapon);
            var damage = system.GetWeaponBaseDamage(weapon);
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;
            var extraCount = system.GetWeaponExtraCount(weapon);

            for (var i = 0; i <= extraCount; i++)
            {
                var spreadAngle = (i - extraCount * 0.5f) * 10f;
                var spawnDir = AutoWeaponSystem.RotateDirection(baseDirection, spreadAngle);

                system.SpawnProjectile(
                    WeaponId,
                    spawnDir,
                    damage,
                    definition.projectileSpeed,
                    definition.projectileLifetime,
                    definition.projectileHitRadius,
                    2 + extraCount,
                    0.3f,
                    0.4f,
                    GetSourceColor(weapon, system));
            }

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.GetWeaponDefinition(weapon).attackInterval) * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.GetWeaponDefinition(weapon).baseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.5f, system.GetWeaponDefinition(weapon).range);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.GetWeaponDefinition(weapon).sourceColor;
        }
    }
}
