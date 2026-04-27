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
            int fragmentCount = 2 + (extraCount * 2); // 0->2, 1->4, 2->6

            float speed = system.Config.iceSpikeProjectileSpeed * 0.7f;
            float lifetime = system.Config.iceSpikeProjectileLifetime * 0.5f;
            float hitRadius = system.Config.iceSpikeProjectileHitRadius * 0.6f;
            Color color = GetSourceColor(weapon, system);

            float angleStep = 360f / fragmentCount;
            float startAngle = UnityEngine.Random.Range(0f, 360f);

            for (int i = 0; i < fragmentCount; i++)
            {
                float angle = startAngle + (i * angleStep);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

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
                    ignoreTarget); // 처음 맞은 적 무시

                if (fragment != null)
                {
                    // 파편 시각 효과
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
