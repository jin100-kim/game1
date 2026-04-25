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
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var projectileSpeed = system.Config.fireballProjectileSpeed;
            var projectileLifetime = system.GetLifetimeCappedByRange(weapon, projectileSpeed, system.Config.fireballProjectileLifetime);
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
                    // Fixed fan spread instead of random
                    var sign = (i % 2 == 0) ? 1f : -1f;
                    var spreadStep = 15f;
                    var angle = sign * spreadStep * ((i / 2) + 1);
                    var spreadDir = AutoWeaponSystem.RotateDirection(baseDirection, angle);
                    var extraProjectile = system.SpawnProjectile(weapon.WeaponId, spreadDir, damage, projectileSpeed, projectileLifetime, hitRadius, 1, 0f, 1f, GetSourceColor(weapon, system));
                    ApplyFireballVisual(extraProjectile);
                }
            }

            weapon.Cooldown = GetAttackInterval(weapon, system);
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

                    // 파티클이 부모(투사체)의 회전을 무시하지 않고 똑같이 따라 돌도록 강제 설정
                    var particleRenderers = vfx.GetComponentsInChildren<ParticleSystemRenderer>();
                    foreach (var psr in particleRenderers)
                    {
                        psr.alignment = ParticleSystemRenderSpace.Local;
                    }
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

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
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
