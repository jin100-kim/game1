using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class IceSpikeWeaponStrategy : IWeaponStrategy
    {
        private const int MainProjectileMaxHits = 2;
        private const float FragmentDamageRatio = 0.5f;
        private const float UnusedFragmentDamageRatio = 0.25f;

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
                MainProjectileMaxHits,
                0f,
                1f,
                GetSourceColor(weapon, system),
                null,
                (finalDamage, enemy) => HandleMainProjectileHit(system, weapon, finalDamage, enemy));

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private void HandleMainProjectileHit(AutoWeaponSystem system, WeaponRuntime weapon, float finalDamage, EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            SpawnFragments(system, weapon, enemy.transform.position, finalDamage * FragmentDamageRatio, enemy, finalDamage);
        }

        private void SpawnFragments(
            AutoWeaponSystem system,
            WeaponRuntime weapon,
            Vector3 position,
            float fragmentDamage,
            EnemyController ignoreTarget,
            float mainHitDamage)
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

            var targetedFragmentCount = Mathf.Min(fragmentCount, nearbyEnemies.Count);
            for (var i = 0; i < targetedFragmentCount; i++)
            {
                var target = nearbyEnemies[i];
                var dir = (Vector2)(target.transform.position - position).normalized;
                dir = AutoWeaponSystem.RotateDirection(dir, Random.Range(-5f, 5f));

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

            var unusedFragmentCount = fragmentCount - targetedFragmentCount;
            if (unusedFragmentCount <= 0 || ignoreTarget == null || ignoreTarget.IsDead)
            {
                return;
            }

            var fallbackDamage = mainHitDamage * UnusedFragmentDamageRatio * unusedFragmentCount;
            if (fallbackDamage > 0f)
            {
                ignoreTarget.ReceiveWeaponDamage(fallbackDamage, WeaponId);
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
