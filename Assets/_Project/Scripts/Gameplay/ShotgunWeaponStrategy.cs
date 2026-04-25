using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class ShotgunWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.Shotgun;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var pelletCount = Mathf.Max(2, system.Config.shotgunPelletCount + system.GetWeaponExtraCount(weapon));
            var spread = Mathf.Max(1f, system.Config.shotgunSpreadAngle);
            var halfSpread = spread * 0.5f;
            var damage = system.GetWeaponBaseDamage(weapon) * Mathf.Clamp(system.Config.shotgunPelletDamageMultiplier, 0.05f, 2f);
            var hitRadius = Mathf.Max(0.05f, system.Config.projectileHitRadius * 0.9f);
            var speed = system.Config.projectileSpeed;
            var lifetime = system.GetLifetimeCappedByRange(weapon, speed, system.Config.projectileLifetime);
            var color = GetSourceColor(weapon, system);
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;

            for (var i = 0; i < pelletCount; i++)
            {
                var t = pelletCount <= 1 ? 0.5f : i / (float)(pelletCount - 1);
                var angle = Mathf.Lerp(-halfSpread, halfSpread, t);
                var shotDirection = AutoWeaponSystem.RotateDirection(baseDirection, angle);
                system.SpawnProjectile(weapon.WeaponId, shotDirection, damage, speed, lifetime, hitRadius, 1, 0f, 1f, color);
            }

            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, system.GetOwnerSoundPosition());
            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
            var pelletCount = Mathf.Max(2, system.Config.shotgunPelletCount + system.GetWeaponExtraCount(weapon));
            var spread = Mathf.Max(1f, system.Config.shotgunSpreadAngle);
            var halfSpread = spread * 0.5f;
            var hitRadius = Mathf.Max(0.02f, system.Config.projectileHitRadius * 0.9f);
            var range = system.GetWeaponRange(weapon);
            var spawnPos = (Vector2)system.Owner.position;
            var aimDir = system.LastAimDirection;

            for (var i = 0; i < pelletCount; i++)
            {
                var t = pelletCount <= 1 ? 0.5f : i / (float)(pelletCount - 1);
                var angle = Mathf.Lerp(-halfSpread, halfSpread, t);
                var shotDir = AutoWeaponSystem.RotateDirection(aimDir, angle);
                WeaponGizmoUtility.DrawProjectilePathGizmo(spawnPos, shotDir, range, hitRadius, color);
            }
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.Config.shotgunAttackInterval) * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.shotgunBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.5f, system.Config.shotgunRange);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.85f, 0.85f, 0.15f, 0.95f);
        }
    }
}
