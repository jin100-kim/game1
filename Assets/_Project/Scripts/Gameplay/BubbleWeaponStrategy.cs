using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class BubbleWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.Bubble;

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
            var lifetime = definition.projectileLifetime * (system.Stats != null ? system.Stats.AttackRangeMultiplier : 1f);
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;
            var totalProjectiles = 2 + system.GetWeaponExtraCount(weapon);
            var angleStep = 360f / totalProjectiles;
            var startAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;

            for (var i = 0; i < totalProjectiles; i++)
            {
                var angle = startAngle + (i * angleStep);
                var spawnDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                system.SpawnProjectile(
                    WeaponId,
                    spawnDir,
                    damage,
                    definition.projectileSpeed,
                    lifetime,
                    definition.projectileHitRadius,
                    1,
                    0f,
                    1f,
                    GetSourceColor(weapon, system));
            }

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color baseColor)
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
