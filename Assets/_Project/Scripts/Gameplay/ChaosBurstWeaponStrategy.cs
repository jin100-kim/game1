using UnityEngine;
using EJR.Game.Core;
using System.Collections.Generic;

namespace EJR.Game.Gameplay
{
    public sealed class ChaosBurstWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.ChaosBurst;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var config = system.Config;
            var damage = GetBaseDamage(weapon, system);
            var speed = config.chaosBurstProjectileSpeed;
            var lifetime = config.chaosBurstProjectileLifetime;
            var hitRadius = config.chaosBurstProjectileHitRadius;
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;

            var projectile = system.SpawnProjectile(
                WeaponId,
                baseDirection,
                damage,
                speed,
                lifetime,
                hitRadius,
                1,
                0f,
                1f,
                GetSourceColor(weapon, system));

            // Find nearest target for homing
            EnemyController target = FindNearestTarget(system);
            if (target != null)
            {
                projectile.SetHoming(target, 180f); // 180 degrees per second turn speed
            }

            ApplyVisual(projectile);

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private EnemyController FindNearestTarget(AutoWeaponSystem system)
        {
            var enemies = system.Registry.Enemies;
            Vector2 ownerPos = system.Owner.position;
            EnemyController nearest = null;
            float minDistanceSq = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy)) continue;

                float distSq = ((Vector2)enemy.transform.position - ownerPos).sqrMagnitude;
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    nearest = enemy;
                }
            }
            return nearest;
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color baseColor)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.Config.chaosBurstAttackInterval) * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.chaosBurstBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.chaosBurstProjectileSpeed * system.Config.chaosBurstProjectileLifetime;
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.6f, 0.2f, 0.9f, 1f); // 보라색 공허 색상
        }

        private void ApplyVisual(Projectile projectile)
        {
            if (projectile == null) return;
            var renderer = projectile.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
                var vfxPrefab = Resources.Load<GameObject>("VFX/ChaosBurst/VFX_2D_Projectile_Burst_01_Color_Loop_Static");
                if (vfxPrefab != null)
                {
                    var vfx = Object.Instantiate(vfxPrefab, projectile.transform);
                    vfx.name = "ChaosBurstVfx";
                    vfx.transform.localPosition = Vector3.zero;
                    vfx.transform.localRotation = Quaternion.identity;
                    vfx.transform.localScale = Vector3.one * 1.5f;

                    var particleRenderers = vfx.GetComponentsInChildren<ParticleSystemRenderer>();
                    foreach (var psr in particleRenderers)
                    {
                        psr.alignment = ParticleSystemRenderSpace.Local;
                    }
                }
            }
        }
    }
}
