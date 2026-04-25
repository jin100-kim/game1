using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using System.Collections.Generic;
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
            if (weapon.BurstShotsRemaining > 0)
            {
                weapon.BurstShotCooldown -= Time.deltaTime;
                if (weapon.BurstShotCooldown <= 0f)
                {
                    FireFireballBurstShot(weapon, system, weapon.BurstDirection);
                }
            }
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var projectileSpeed = system.Config.projectileSpeed;
            var projectileLifetime = system.GetLifetimeCappedByRange(weapon, projectileSpeed, system.Config.projectileLifetime);
            var hitRadius = Mathf.Max(0.05f, system.Config.fireballProjectileHitRadius);
            var damage = system.GetWeaponBaseDamage(weapon);
            var range = system.GetWeaponRange(weapon);
            var ownerPosition = (Vector2)system.Owner.position;
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;

            var extraShotCount = system.GetWeaponExtraCount(weapon);
            var reservedTargets = new HashSet<EnemyController>();

            var projectile = system.SpawnProjectile(weapon.WeaponId, baseDirection, damage, projectileSpeed, projectileLifetime, hitRadius, 1, 0f, 1f, GetSourceColor(weapon, system));
            ApplyFireballVisual(projectile);

            for (var i = 0; i < extraShotCount; i++)
            {
                var target = FindPreferredAdditionalFireballTarget(system, ownerPosition, range, baseDirection, reservedTargets);
                if (target != null)
                {
                    reservedTargets.Add(target);
                    var toTarget = ((Vector2)target.transform.position - ownerPosition).normalized;
                    var extraProjectile = system.SpawnProjectile(weapon.WeaponId, toTarget, damage, projectileSpeed, projectileLifetime, hitRadius, 1, 0f, 1f, GetSourceColor(weapon, system));
                    ApplyFireballVisual(extraProjectile);
                }
                else
                {
                    var randomAngle = UnityEngine.Random.Range(-30f, 30f);
                    var randomDir = AutoWeaponSystem.RotateDirection(baseDirection, randomAngle);
                    var extraProjectile = system.SpawnProjectile(weapon.WeaponId, randomDir, damage, projectileSpeed, projectileLifetime, hitRadius, 1, 0f, 1f, GetSourceColor(weapon, system));
                    ApplyFireballVisual(extraProjectile);
                }
            }

            if (UnityEngine.Random.value <= system.Config.fireballBurstChance)
            {
                StartFireballBurst(weapon, system, baseDirection);
            }
            else
            {
                weapon.Cooldown = GetAttackInterval(weapon, system);
            }
        }

        private void StartFireballBurst(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var count = Mathf.Max(1, system.Config.fireballBurstCount + system.GetWeaponExtraCount(weapon));
            weapon.BurstShotsRemaining = count;
            weapon.BurstDirection = direction;
            FireFireballBurstShot(weapon, system, direction);
        }

        private void FireFireballBurstShot(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var spread = UnityEngine.Random.Range(-system.Config.fireballBurstSpreadAngle, system.Config.fireballBurstSpreadAngle);
            var burstDirection = AutoWeaponSystem.RotateDirection(direction, spread);
            var damage = system.GetWeaponBaseDamage(weapon) * Mathf.Clamp(system.Config.fireballBurstDamageMultiplier, 0.05f, 2f);
            var projectileSpeed = system.Config.projectileSpeed * 1.35f;
            var projectileLifetime = system.GetLifetimeCappedByRange(weapon, projectileSpeed, system.Config.projectileLifetime * 0.85f);

            var projectile = system.SpawnProjectile(
                weapon.WeaponId,
                burstDirection,
                damage,
                projectileSpeed,
                projectileLifetime,
                system.Config.projectileHitRadius * 0.9f,
                1,
                0f,
                1f,
                GetSourceColor(weapon, system));
            
            ApplyFireballVisual(projectile);
            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Secondary, projectile != null ? projectile.transform.position : system.GetOwnerSoundPosition());

            weapon.BurstShotsRemaining--;
            weapon.BurstShotCooldown = Mathf.Max(0.01f, Mathf.Max(0.01f, system.Config.fireballBurstShotInterval) * system.GetCombinedAttackIntervalMultiplier(weapon));
            if (weapon.BurstShotsRemaining <= 0)
            {
                weapon.Cooldown = GetAttackInterval(weapon, system);
            }
        }

        private void ApplyFireballVisual(Projectile projectile)
        {
            if (projectile == null) return;
            var renderer = projectile.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
                var vfxPrefab = Resources.Load<GameObject>("VFX/Fireball/VFX_2D_Fireball_Projectile_01_Color_Loop_Static");
                if (vfxPrefab != null)
                {
                    var vfx = UnityEngine.Object.Instantiate(vfxPrefab, projectile.transform);
                    vfx.name = "FireballVfx";
                    vfx.transform.localPosition = Vector3.zero;
                    vfx.transform.localRotation = Quaternion.identity;
                    vfx.transform.localScale = Vector3.one;
                }
            }
        }

        private EnemyController FindPreferredAdditionalFireballTarget(
            AutoWeaponSystem system,
            Vector2 ownerPosition,
            float range,
            Vector2 baseDirection,
            HashSet<EnemyController> reservedTargets)
        {
            var enemies = system.Registry.Enemies;
            EnemyController best = null;
            var bestScore = float.MinValue;

            foreach (var enemy in enemies)
            {
                if (enemy == null || reservedTargets.Contains(enemy) || !system.IsEnemyUsable(enemy)) continue;

                var toEnemy = (Vector2)enemy.transform.position - ownerPosition;
                var distSq = toEnemy.sqrMagnitude;
                if (distSq > range * range) continue;

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
            return Mathf.Max(0.05f, system.Config.fireballAttackInterval) * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.fireballBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.5f, system.Config.fireballRange);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.9f, 0.95f, 0.35f, 0.95f);
        }
    }
}
