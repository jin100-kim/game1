using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class SlashWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.Slash;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            system.StartCoroutine(SlashRoutine(weapon, system, direction));
        }

        private IEnumerator SlashRoutine(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var slashCount = Mathf.Max(1, system.Config.slashBaseCount + system.GetWeaponExtraCount(weapon));
            var comboInterval = Mathf.Max(0.01f, system.Config.slashComboInterval);
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;

            for (var i = 0; i < slashCount; i++)
            {
                ExecuteSlash(weapon, system, baseDirection, i);
                if (i < slashCount - 1)
                {
                    yield return new WaitForSeconds(comboInterval);
                }
            }

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private void ExecuteSlash(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 baseDirection, int slashIndex)
        {
            var damage = system.GetWeaponBaseDamage(weapon);
            var range = system.GetWeaponRange(weapon);
            var coneHalfAngle = Mathf.Max(2f, system.Config.slashConeAngle) * 0.5f;
            var origin = (Vector2)system.Owner.position;
            
            var slashDirection = baseDirection;
            if (slashIndex > 0)
            {
                var offsetAngle = (slashIndex % 2 == 0 ? 1 : -1) * (15f * ((slashIndex + 1) / 2));
                slashDirection = AutoWeaponSystem.RotateDirection(baseDirection, offsetAngle);
            }

            SpawnSlashSpriteFx(system, origin, slashDirection, range, slashIndex);
            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, origin);

            for (int i = system.Registry.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = system.Registry.Enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy)) continue;

                var toEnemy = (Vector2)enemy.transform.position - origin;
                var centerDistance = toEnemy.magnitude;
                if (centerDistance > range + 0.25f) continue;

                var angle = Vector2.Angle(slashDirection, toEnemy / Mathf.Max(0.0001f, centerDistance));
                if (angle <= coneHalfAngle + 5f)
                {
                    system.DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                }
            }
        }

        private void SpawnSlashSpriteFx(AutoWeaponSystem system, Vector2 origin, Vector2 direction, float range, int slashIndex)
        {
            WeaponFxRenderer.SpawnKatanaSlashFx(
                system.transform,
                origin,
                direction,
                range,
                slashIndex,
                0.72f, 
                new Vector2(-0.22f, -2.0f), 
                6f, 
                18f,
                500
            );
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
            var range = system.GetWeaponRange(weapon);
            var halfAngle = Mathf.Max(2f, system.Config.slashConeAngle) * 0.5f;
            var origin = (Vector2)system.Owner.position;
            WeaponGizmoUtility.DrawConeCollisionGizmo(origin, system.LastAimDirection, range, halfAngle, color);
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.Config.slashAttackInterval) * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.slashBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.25f, system.Config.slashRange);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(1f, 0.9f, 0.95f, 0.95f);
        }
    }
}
