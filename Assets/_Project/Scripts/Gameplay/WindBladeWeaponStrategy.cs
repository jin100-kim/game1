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
            var config = system.Config;
            var damage = GetBaseDamage(weapon, system);
            var speed = config.windBladeProjectileSpeed;
            var lifetime = config.windBladeProjectileLifetime;
            var hitRadius = config.windBladeProjectileHitRadius;
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;

            // Wind blade fires in quick bursts or multiple projectiles
            var extraCount = system.GetWeaponExtraCount(weapon);
            
            for (int i = 0; i <= extraCount; i++)
            {
                var spreadAngle = (i - extraCount * 0.5f) * 10f;
                var spawnDir = AutoWeaponSystem.RotateDirection(baseDirection, spreadAngle);
                
                var projectile = system.SpawnProjectile(
                    WeaponId,
                    spawnDir,
                    damage,
                    speed,
                    lifetime,
                    hitRadius,
                    3, // Pierces 2 enemies
                    0.3f, // 30% falloff
                    0.4f,
                    GetSourceColor(weapon, system));

                ApplyVisual(projectile);
            }

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private void ApplyVisual(Projectile projectile)
        {
            if (projectile == null) return;
            var renderer = projectile.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
                var vfxPrefab = Resources.Load<GameObject>("VFX/WindBlade/VFX_2D_Projectile_Wind_01_Color_Loop_Static");
                if (vfxPrefab != null)
                {
                    var vfx = Object.Instantiate(vfxPrefab, projectile.transform);
                    vfx.name = "WindBladeVfx";
                    vfx.transform.localPosition = Vector3.zero;
                    vfx.transform.localRotation = Quaternion.identity;
                    vfx.transform.localScale = Vector3.one * 2.0f; // Set to 2.0x

                    var particleRenderers = vfx.GetComponentsInChildren<ParticleSystemRenderer>();
                    foreach (var psr in particleRenderers)
                    {
                        psr.alignment = ParticleSystemRenderSpace.Local;
                    }
                }
            }
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.windBladeAttackInterval * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.windBladeBaseDamage;
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.windBladeProjectileSpeed * system.Config.windBladeProjectileLifetime;
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.6f, 1f, 0.8f, 1f);
        }
    }
}
