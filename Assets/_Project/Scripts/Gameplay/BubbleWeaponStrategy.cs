using UnityEngine;
using EJR.Game.Core;
using System.Collections.Generic;

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
            var config = system.Config;
            var damage = system.GetWeaponBaseDamage(weapon);
            var speed = config.bubbleProjectileSpeed;
            var lifetime = config.bubbleProjectileLifetime * (system.Stats != null ? system.Stats.AttackRangeMultiplier : 1f); // 수명에 범위 보너스 적용
            var hitRadius = config.bubbleProjectileHitRadius;
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;

            var extraCount = system.GetWeaponExtraCount(weapon);
            int totalProjectiles = 3 + extraCount; // 기본 3개 + 마일스톤

            float angleStep = 360f / totalProjectiles;
            float startAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;

            for (int i = 0; i < totalProjectiles; i++)
            {
                float angle = startAngle + (i * angleStep);
                Vector2 spawnDir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                var projectile = system.SpawnProjectile(
                    WeaponId,
                    spawnDir,
                    damage,
                    speed,
                    lifetime,
                    hitRadius,
                    1,
                    0f,
                    1f,
                    GetSourceColor(weapon, system));

                ApplyVisual(projectile);
            }

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color baseColor)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.Config.bubbleAttackInterval) * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.bubbleBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.bubbleProjectileSpeed * system.Config.bubbleProjectileLifetime;
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.4f, 0.8f, 1f, 1f); // 비누방울 민트/하늘색
        }

        private void ApplyVisual(Projectile projectile)
        {
            if (projectile == null) return;
            var renderer = projectile.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
                var vfxPrefab = Resources.Load<GameObject>("VFX/Bubble/VFX_2D_Bubble_01_Color_Loop_Static");
                if (vfxPrefab != null)
                {
                    var vfx = Object.Instantiate(vfxPrefab, projectile.transform);
                    vfx.name = "BubbleVfx";
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
