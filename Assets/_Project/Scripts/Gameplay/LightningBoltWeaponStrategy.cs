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
            
            // 1. 유효한 적(사거리 내, 타겟팅 가능)의 수 계산 (가비지 컬렉션 방지)
            int validCount = 0;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy)) continue;
                
                if (((Vector2)enemy.transform.position - ownerPos).sqrMagnitude <= rangeSq)
                {
                    validCount++;
                }
            }

            if (validCount == 0) return;

            // 2. 랜덤 인덱스 추첨 후 해당 타겟 찾기
            int randomIndex = UnityEngine.Random.Range(0, validCount);
            EnemyController target = null;
            int currentIndex = 0;

            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy)) continue;
                
                if (((Vector2)enemy.transform.position - ownerPos).sqrMagnitude <= rangeSq)
                {
                    if (currentIndex == randomIndex)
                    {
                        target = enemy;
                        break;
                    }
                    currentIndex++;
                }
            }

            if (target == null) return;

            var damage = GetBaseDamage(weapon, system);
            
            // 즉시 대미지 입힘
            target.ReceiveWeaponDamage(damage, WeaponId);

            // 1. 적 위치에 번개 줄기 이펙트 소환
            var lightningPrefab = Resources.Load<GameObject>("VFX/LightningBolt/VFX_2D_Lightning_01_Mask_Static");
            if (lightningPrefab != null)
            {
                var lightning = Object.Instantiate(lightningPrefab, target.transform.position, Quaternion.identity);
                lightning.name = "LightningBoltStrikeVfx";
                lightning.transform.localScale = Vector3.one;
                Object.Destroy(lightning, 0.5f);
            }

            // 2. 적 위치에 바닥 타격 폭발 이펙트 소환
            var impactPrefab = Resources.Load<GameObject>("VFX/LightningBolt/VFX_2D_Projectile_Lightning_Impact_01_Color_Static");
            if (impactPrefab != null)
            {
                var impact = Object.Instantiate(impactPrefab, target.transform.position, Quaternion.identity);
                impact.name = "LightningBoltImpactVfx";
                impact.transform.localScale = Vector3.one * 2.0f;
                Object.Destroy(impact, 0.5f);
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
