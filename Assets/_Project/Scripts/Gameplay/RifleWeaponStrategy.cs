using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class RifleWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.Rifle;

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
                    FireRifleBurstShot(weapon, system);
                    weapon.BurstShotsRemaining--;
                    if (weapon.BurstShotsRemaining <= 0)
                    {
                        weapon.BurstTotalShots = 0;
                        weapon.Cooldown = system.GetAttackInterval(weapon) * system.GetCombinedAttackIntervalMultiplier(weapon);
                    }
                    else
                    {
                        weapon.BurstShotCooldown = Mathf.Max(
                            0.01f,
                            GetRifleBurstShotInterval(system) * system.GetCombinedAttackIntervalMultiplier(weapon));
                    }
                }
            }
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            // 이미 점사 중이면 중복 발사하지 않음
            if (weapon.BurstShotsRemaining > 0) return;

            var baseShotCount = system.Config != null ? Mathf.Max(1, system.Config.rifleBaseShotCount) : 2;
            weapon.BurstTotalShots = Mathf.Max(1, baseShotCount + system.GetWeaponExtraCount(weapon));
            weapon.BurstShotsRemaining = weapon.BurstTotalShots;
            weapon.BurstDirection = direction;
            
            // 발사 시작과 동시에 전체 쿨타임 적용 (중복 호출 방지)
            weapon.Cooldown = system.GetAttackInterval(weapon) * system.GetCombinedAttackIntervalMultiplier(weapon);

            FireRifleBurstShot(weapon, system);
            weapon.BurstShotsRemaining--;

            if (weapon.BurstShotsRemaining > 0)
            {
                weapon.BurstShotCooldown = Mathf.Max(
                    0.01f,
                    GetRifleBurstShotInterval(system) * system.GetCombinedAttackIntervalMultiplier(weapon));
            }
        }

        private void FireRifleBurstShot(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var damage = system.GetWeaponBaseDamage(weapon) * 0.95f;
            var projectileSpeed = system.Config.projectileSpeed * 1.4f;
            var projectileLifetime = system.GetLifetimeCappedByRange(weapon, projectileSpeed, system.Config.projectileLifetime * 0.9f);
            
            system.SpawnProjectile(
                weapon.WeaponId,
                weapon.BurstDirection,
                damage,
                projectileSpeed,
                projectileLifetime,
                system.Config.projectileHitRadius * 0.85f,
                1,
                0f,
                1f,
                new Color(0.45f, 1f, 0.95f, 0.95f));

            var soundPosition = system.GetOwnerSoundPosition();
            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, soundPosition);
        }

        private float GetRifleBurstShotInterval(AutoWeaponSystem system)
        {
            return system.Config != null ? system.Config.rifleBurstShotInterval : 0.08f;
        }

                public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.Config.rifleAttackInterval);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.rifleBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.5f, system.Config.rifleRange);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.45f, 1f, 0.95f, 0.95f);
        }
    }
}



