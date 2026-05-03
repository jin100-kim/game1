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
            var config = system.Config;
            var damage = GetBaseDamage(weapon, system);
            var speed = config.iceSpikeProjectileSpeed;
            var lifetime = config.iceSpikeProjectileLifetime;
            var hitRadius = config.iceSpikeProjectileHitRadius;
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;

            var projectile = system.SpawnProjectile(
                WeaponId,
                baseDirection,
                damage,
                speed,
                lifetime,
                hitRadius,
                1, // 파편 생성을 위해 관통 삭제
                0f,
                1f,
                GetSourceColor(weapon, system),
                null,
                (finalDmg, enemy) => SpawnFragments(system, weapon, enemy.transform.position, finalDmg * 0.5f, enemy));

            ApplyVisual(projectile);

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private void SpawnFragments(AutoWeaponSystem system, WeaponRuntime weapon, Vector3 position, float fragmentDamage, EnemyController ignoreTarget)
        {
            var extraCount = system.GetWeaponExtraCount(weapon);
            int fragmentCount = 2 + (extraCount * 2);

            float speed = system.Config.iceSpikeProjectileSpeed * 0.7f;
            float lifetime = system.Config.iceSpikeProjectileLifetime * 0.5f;
            float hitRadius = system.Config.iceSpikeProjectileHitRadius * 0.6f;
            Color color = GetSourceColor(weapon, system);

            // 주변 적 탐색 (최대 6m)
            var nearbyEnemies = new List<EnemyController>();
            system.Registry.GetNearby(position, 6.0f, nearbyEnemies);
            
            // 무효한 타겟 제거 (이미 맞은 적, 죽은 적 등)
            nearbyEnemies.RemoveAll(e => e == null || ReferenceEquals(e, ignoreTarget) || e.IsDead);
            
            // 거리순 정렬
            nearbyEnemies.Sort((a, b) => 
                (a.transform.position - position).sqrMagnitude.CompareTo((b.transform.position - position).sqrMagnitude));

            for (int i = 0; i < fragmentCount; i++)
            {
                Vector2 dir;
                if (nearbyEnemies.Count > 0)
                {
                    // 적이 있는 경우: 적들을 순환하며 타겟팅
                    var target = nearbyEnemies[i % nearbyEnemies.Count];
                    dir = (Vector2)(target.transform.position - position).normalized;
                    
                    // 약간의 무작위 각도 추가 (겹침 방지)
                    float jitter = UnityEngine.Random.Range(-15f, 15f);
                    float rad = jitter * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);
                    dir = new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
                }
                else
                {
                    // 주변에 적이 없는 경우: 기존처럼 원형으로 분산
                    float angle = (360f / fragmentCount) * i + UnityEngine.Random.Range(0f, 30f);
                    dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                }

                var fragment = system.SpawnProjectile(
                    WeaponId,
                    dir,
                    fragmentDamage,
                    speed,
                    lifetime,
                    hitRadius,
                    1, 0f, 1f, color,
                    position,
                    null,
                    true,
                    ignoreTarget);

                if (fragment != null)
                {
                    var renderer = fragment.GetComponent<SpriteRenderer>();
                    if (renderer != null) renderer.enabled = false;

                    var vfxPrefab = Resources.Load<GameObject>("VFX/IceSpike/VFX_2D_Projectile_Ice_01_Color_Loop_Static");
                    if (vfxPrefab != null)
                    {
                        var vfx = Object.Instantiate(vfxPrefab, fragment.transform);
                        vfx.transform.localScale = Vector3.one * 1.2f;
                    }
                }
            }
        }


        private void ApplyVisual(Projectile projectile)
        {
            if (projectile == null) return;
            var renderer = projectile.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
                var vfxPrefab = Resources.Load<GameObject>("VFX/IceSpike/VFX_2D_Projectile_Ice_01_Color_Loop_Static");
                if (vfxPrefab != null)
                {
                    var vfx = Object.Instantiate(vfxPrefab, projectile.transform);
                    vfx.name = "IceSpikeVfx";
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
            return system.Config.iceSpikeAttackInterval * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.iceSpikeBaseDamage;
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.iceSpikeProjectileSpeed * system.Config.iceSpikeProjectileLifetime;
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.7f, 0.9f, 1f, 1f);
        }
    }
}
