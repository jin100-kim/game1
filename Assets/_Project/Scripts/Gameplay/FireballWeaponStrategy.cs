using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class FireballWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.Fireball;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var definition = system.GetWeaponDefinition(weapon);
            var projectileSpeed = definition.projectileSpeed;
            var projectileLifetime = system.GetLifetimeCappedByRange(weapon, projectileSpeed, definition.projectileLifetime);
            var hitRadius = Mathf.Max(0.05f, definition.projectileHitRadius);
            var damage = system.GetWeaponBaseDamage(weapon);
            var range = system.GetWeaponRange(weapon);
            var ownerPosition = (Vector2)system.Owner.position;
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;
            var extraShotCount = system.GetWeaponExtraCount(weapon);
            var reservedTargets = new HashSet<EnemyController>();

            system.SpawnProjectile(weapon.WeaponId, baseDirection, damage, projectileSpeed, projectileLifetime, hitRadius, 1, 0f, 1f, GetSourceColor(weapon, system));

            for (var i = 0; i < extraShotCount; i++)
            {
                var target = FindPreferredAdditionalFireballTarget(system, ownerPosition, range, baseDirection, reservedTargets);
                if (target != null)
                {
                    reservedTargets.Add(target);
                    var toTarget = ((Vector2)target.transform.position - ownerPosition).normalized;
                    system.SpawnProjectile(weapon.WeaponId, toTarget, damage, projectileSpeed, projectileLifetime, hitRadius, 1, 0f, 1f, GetSourceColor(weapon, system));
                    continue;
                }

                var sign = (i % 2 == 0) ? 1f : -1f;
                var angle = sign * 15f * ((i / 2) + 1);
                var spreadDir = AutoWeaponSystem.RotateDirection(baseDirection, angle);
                system.SpawnProjectile(weapon.WeaponId, spreadDir, damage, projectileSpeed, projectileLifetime, hitRadius, 1, 0f, 1f, GetSourceColor(weapon, system));
            }

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private static EnemyController FindPreferredAdditionalFireballTarget(
            AutoWeaponSystem system,
            Vector2 ownerPosition,
            float range,
            Vector2 baseDirection,
            HashSet<EnemyController> reservedTargets)
        {
            var enemies = system.Registry.Enemies;
            EnemyController best = null;
            var bestScore = float.MinValue;

            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (enemy == null || reservedTargets.Contains(enemy) || !system.IsEnemyUsable(enemy))
                {
                    continue;
                }

                var toEnemy = (Vector2)enemy.transform.position - ownerPosition;
                var distSq = toEnemy.sqrMagnitude;
                if (distSq > range * range)
                {
                    continue;
                }

                var dot = Vector2.Dot(baseDirection, toEnemy.normalized);
                var score = dot * 100f - (distSq * 0.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }

            return best;
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
