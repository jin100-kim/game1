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
            var extraCount = system.GetWeaponExtraCount(weapon);
            int targetCount = 1 + extraCount;
            var damage = GetBaseDamage(weapon, system);

            system.StartCoroutine(FireLightningSequence(weapon, system, targetCount, damage));
            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private System.Collections.IEnumerator FireLightningSequence(WeaponRuntime weapon, AutoWeaponSystem system, int remainingStrikes, float damage)
        {
            while (remainingStrikes > 0)
            {
                var enemies = system.Registry.Enemies;
                Vector2 ownerPos = system.Owner.position;
                float rangeSq = GetRange(weapon, system) * GetRange(weapon, system);
                
                var validEnemies = new System.Collections.Generic.List<EnemyController>();
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy != null && system.IsEnemyUsable(enemy))
                    {
                        float distSq = ((Vector2)enemy.transform.position - ownerPos).sqrMagnitude;
                        if (distSq <= rangeSq)
                        {
                            validEnemies.Add(enemy);
                        }
                    }
                }
                
                // 사거리 내에 적이 아예 없으면 남은 횟수는 포기
                if (validEnemies.Count == 0) break;
                
                // 가까운 순으로 정렬
                validEnemies.Sort((a, b) => 
                    ((Vector2)a.transform.position - ownerPos).sqrMagnitude.CompareTo(((Vector2)b.transform.position - ownerPos).sqrMagnitude));
                
                // 현재 웨이브에서 타격할 수 있는 횟수 (남은 횟수와 적의 수 중 작은 값)
                int strikesThisWave = Mathf.Min(remainingStrikes, validEnemies.Count);
                for (int i = 0; i < strikesThisWave; i++)
                {
                    StrikeTarget(validEnemies[i], damage);
                }
                
                remainingStrikes -= strikesThisWave;
                
                // 횟수가 남았다면 0.1초 대기 후 다음 웨이브 진행 (재타겟팅)
                if (remainingStrikes > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        private void StrikeTarget(EnemyController target, float damage)
        {
            if (target == null) return;
            
            target.ReceiveWeaponDamage(damage, WeaponId);

            var lightningPrefab = Resources.Load<GameObject>("VFX/LightningBolt/VFX_2D_Lightning_01_Mask_Static");
            if (lightningPrefab != null)
            {
                var lightning = Object.Instantiate(lightningPrefab, target.transform.position, Quaternion.identity);
                lightning.transform.localScale = Vector3.one;
                Object.Destroy(lightning, 0.5f);
            }

            var impactPrefab = Resources.Load<GameObject>("VFX/LightningBolt/VFX_2D_Projectile_Lightning_Impact_01_Color_Static");
            if (impactPrefab != null)
            {
                var impact = Object.Instantiate(impactPrefab, target.transform.position, Quaternion.identity);
                impact.transform.localScale = Vector3.one * 2.0f;
                Object.Destroy(impact, 0.5f);
            }
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
