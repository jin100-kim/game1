using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class LightningBoltWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.LightningBolt;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var enemies = system.Registry.Enemies;
            Vector2 ownerPos = system.Owner.position;
            float rangeSq = GetRange(weapon, system) * GetRange(weapon, system);

            var extraCount = system.GetWeaponExtraCount(weapon);
            int targetCount = 1 + extraCount;
            var damage = GetBaseDamage(weapon, system);
            var reservedTargets = new System.Collections.Generic.HashSet<EnemyController>();

            for (int t = 0; t < targetCount; t++)
            {
                // 1. 유효한 적(사거리 내, 타겟팅 가능, 이미 선택되지 않음) 찾기
                EnemyController bestTarget = null;
                float bestScore = float.MaxValue; // 가까운 적 우선

                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy == null || reservedTargets.Contains(enemy) || !system.IsEnemyUsable(enemy)) continue;

                    float distSq = ((Vector2)enemy.transform.position - ownerPos).sqrMagnitude;
                    if (distSq <= rangeSq)
                    {
                        if (distSq < bestScore)
                        {
                            bestScore = distSq;
                            bestTarget = enemy;
                        }
                    }
                }

                if (bestTarget == null) break;
                reservedTargets.Add(bestTarget);

                // 2. 타격 및 이펙트
                bestTarget.ReceiveWeaponDamage(damage, WeaponId);

                // 번개 줄기 이펙트
                var lightningPrefab = Resources.Load<GameObject>("VFX/LightningBolt/VFX_2D_Lightning_01_Mask_Static");
                if (lightningPrefab != null)
                {
                    var lightning = Object.Instantiate(lightningPrefab, bestTarget.transform.position, Quaternion.identity);
                    lightning.transform.localScale = Vector3.one;
                    Object.Destroy(lightning, 0.5f);
                }

                // 바닥 타격 폭발 이펙트
                var impactPrefab = Resources.Load<GameObject>("VFX/LightningBolt/VFX_2D_Projectile_Lightning_Impact_01_Color_Static");
                if (impactPrefab != null)
                {
                    var impact = Object.Instantiate(impactPrefab, bestTarget.transform.position, Quaternion.identity);
                    impact.transform.localScale = Vector3.one * 2.0f;
                    Object.Destroy(impact, 0.5f);
                }
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
                var vfxPrefab = Resources.Load<GameObject>("VFX/LightningBolt/VFX_2D_Projectile_Lightning_01_Color_Loop_Static");
                if (vfxPrefab != null)
                {
                    var vfx = Object.Instantiate(vfxPrefab, projectile.transform);
                    vfx.name = "LightningBoltVfx";
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
            return system.Config.lightningBoltAttackInterval * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.lightningBoltBaseDamage;
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.lightningBoltProjectileSpeed * system.Config.lightningBoltProjectileLifetime;
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.4f, 0.7f, 1f, 1f);
        }
    }
}
