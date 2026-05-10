using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class IceSpikeWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.IceSpike;

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

            system.SpawnProjectile(
                WeaponId,
                baseDirection,
                damage,
                definition.projectileSpeed,
                definition.projectileLifetime,
                definition.projectileHitRadius,
                1,
                0f,
                1f,
                GetSourceColor(weapon, system),
                null,
                (finalDamage, enemy) => SpawnFragments(system, weapon, enemy.transform.position, finalDamage * 0.5f, enemy));

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private void SpawnFragments(AutoWeaponSystem system, WeaponRuntime weapon, Vector3 position, float fragmentDamage, EnemyController ignoreTarget)
        {
            var definition = system.GetWeaponDefinition(weapon);
            var extraCount = system.GetWeaponExtraCount(weapon);
            var fragmentCount = 2 + (extraCount * 2);
            var speed = definition.projectileSpeed * 0.7f;
            var lifetime = definition.projectileLifetime * 0.5f;
            var hitRadius = definition.projectileHitRadius * 0.6f;
            var nearbyEnemies = new List<EnemyController>();

            system.Registry.GetNearby(position, 6f, nearbyEnemies);
            nearbyEnemies.RemoveAll(e => e == null || ReferenceEquals(e, ignoreTarget) || e.IsDead);
            nearbyEnemies.Sort((a, b) =>
                (a.transform.position - position).sqrMagnitude.CompareTo((b.transform.position - position).sqrMagnitude));

            for (var i = 0; i < fragmentCount; i++)
            {
                Vector2 dir;
                if (i < nearbyEnemies.Count)
                {
                    var target = nearbyEnemies[i];
                    dir = (Vector2)(target.transform.position - position).normalized;
                    dir = AutoWeaponSystem.RotateDirection(dir, Random.Range(-5f, 5f));
                }
                else
                {
                    var angle = (360f / fragmentCount) * i + Random.Range(0f, 30f);
                    dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                }

                system.SpawnProjectile(
                    WeaponId,
                    dir,
                    fragmentDamage,
                    speed,
                    lifetime,
                    hitRadius,
                    1,
                    0f,
                    1f,
                    GetSourceColor(weapon, system),
                    position,
                    null,
                    true,
                    ignoreTarget);
            }
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
