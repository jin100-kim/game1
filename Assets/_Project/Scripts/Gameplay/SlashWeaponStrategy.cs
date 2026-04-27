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
            
            var rawDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;
            var snappedDirection = SnapToFourDirections(rawDirection);

            for (var i = 0; i < slashCount; i++)
            {
                ExecuteSlash(weapon, system, snappedDirection, i);
                if (i < slashCount - 1)
                {
                    yield return new WaitForSeconds(comboInterval);
                }
            }

            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private void ExecuteSlash(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 snappedDirection, int slashIndex)
        {
            var damage = system.GetWeaponBaseDamage(weapon);
            var range = system.GetWeaponRange(weapon);
            var coneHalfAngle = Mathf.Max(2f, system.Config.slashConeAngle) * 0.5f;
            var origin = (Vector2)system.Owner.position;
            
            SpawnSlashVfx(system, origin, snappedDirection, range);
            // system.RequestWeaponSound(...) 제거됨 - 이제 프리팹에서 사운드 재생

            for (int i = system.Registry.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = system.Registry.Enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy)) continue;

                var toEnemy = (Vector2)enemy.transform.position - origin;
                var centerDistance = toEnemy.magnitude;
                if (centerDistance > range + 0.5f) continue;

                var angle = Vector2.Angle(snappedDirection, toEnemy / Mathf.Max(0.0001f, centerDistance));
                if (angle <= coneHalfAngle)
                {
                    system.DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                }
            }
        }

        private void SpawnSlashVfx(AutoWeaponSystem system, Vector2 origin, Vector2 direction, float range)
        {
            GameObject prefab = system.Config != null ? system.Config.slashVfxPrefab : null;

            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>("VFX/Slash/VFX_2D_Sword_Slash_01_Mask_Static");
            }

            if (prefab == null) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, 0f, angle);
            
            var spawnPos = (Vector3)origin + (Vector3)(direction * (range * 0.4f)) + Vector3.back * 0.1f;
            var vfx = UnityEngine.Object.Instantiate(prefab, spawnPos, rotation);
            
            vfx.transform.localScale = Vector3.one * (range * 0.5f); 

            var particleRenderers = vfx.GetComponentsInChildren<ParticleSystemRenderer>();
            foreach (var psr in particleRenderers)
            {
                psr.alignment = ParticleSystemRenderSpace.Local;
            }

            UnityEngine.Object.Destroy(vfx, 0.4f);
        }

        private Vector2 SnapToFourDirections(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return direction.x > 0 ? Vector2.right : Vector2.left;
            }
            else
            {
                return direction.y > 0 ? Vector2.up : Vector2.down;
            }
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
            var range = system.GetWeaponRange(weapon);
            var halfAngle = Mathf.Max(2f, system.Config.slashConeAngle) * 0.5f;
            var origin = (Vector2)system.Owner.position;
            var snappedDir = SnapToFourDirections(system.LastAimDirection);
            WeaponGizmoUtility.DrawConeCollisionGizmo(origin, snappedDir, range, halfAngle, color);
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system) => system.Config.slashAttackInterval;
        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system) => system.Config.slashBaseDamage;
        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system) => system.Config.slashRange;
        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system) => Color.white;
    }
}
