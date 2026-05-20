using System.Collections;
using EJR.Game.Core;
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
            var definition = system.GetWeaponDefinition(weapon);
            var slashCount = Mathf.Max(1, definition.slashBaseCount + system.GetWeaponExtraCount(weapon));
            var comboInterval = Mathf.Max(0.01f, definition.slashComboInterval);
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
            var definition = system.GetWeaponDefinition(weapon);
            var damage = system.GetWeaponBaseDamage(weapon);
            var range = system.GetWeaponRange(weapon);
            var coneHalfAngle = Mathf.Max(2f, definition.slashConeAngle) * 0.5f;
            var origin = (Vector2)system.Owner.position;

            SpawnSlashVfx(definition, origin, snappedDirection, range);

            for (var i = system.Registry.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = system.Registry.Enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy))
                {
                    continue;
                }

                var toEnemy = (Vector2)enemy.transform.position - origin;
                var centerDistance = toEnemy.magnitude;
                if (centerDistance > range + 0.5f)
                {
                    continue;
                }

                var angle = Vector2.Angle(snappedDirection, toEnemy / Mathf.Max(0.0001f, centerDistance));
                if (angle <= coneHalfAngle)
                {
                    var knockbackDirection = centerDistance > 0.0001f ? toEnemy / centerDistance : snappedDirection;
                    enemy.ApplyKnockback(knockbackDirection, definition.knockbackStrength);
                    system.DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                }
            }
        }

        private static void SpawnSlashVfx(WeaponDefinition definition, Vector2 origin, Vector2 direction, float range)
        {
            var prefab = definition.slashVfxPrefab;
            if (prefab == null && !string.IsNullOrWhiteSpace(definition.directVfxResourcePath))
            {
                prefab = Resources.Load<GameObject>(definition.directVfxResourcePath);
            }

            if (prefab == null)
            {
                return;
            }

            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, 0f, angle);
            var spawnPos = (Vector3)origin + (Vector3)(direction * (range * 0.4f)) + Vector3.back * 0.1f;
            var vfx = Object.Instantiate(prefab, spawnPos, rotation);
            vfx.transform.localScale = Vector3.one * (range * 0.5f);

            var particleRenderers = vfx.GetComponentsInChildren<ParticleSystemRenderer>();
            foreach (var psr in particleRenderers)
            {
                psr.alignment = ParticleSystemRenderSpace.Local;
            }

            Object.Destroy(vfx, 0.4f);
        }

        private static Vector2 SnapToFourDirections(Vector2 direction)
        {
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? direction.x > 0 ? Vector2.right : Vector2.left
                : direction.y > 0 ? Vector2.up : Vector2.down;
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
            var definition = system.GetWeaponDefinition(weapon);
            var range = system.GetWeaponRange(weapon);
            var halfAngle = Mathf.Max(2f, definition.slashConeAngle) * 0.5f;
            var origin = (Vector2)system.Owner.position;
            var snappedDir = SnapToFourDirections(system.LastAimDirection);
            WeaponGizmoUtility.DrawConeCollisionGizmo(origin, snappedDir, range, halfAngle, color);
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.GetWeaponDefinition(weapon).attackInterval) * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.GetWeaponDefinition(weapon).baseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.5f, system.GetWeaponDefinition(weapon).range);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.GetWeaponDefinition(weapon).sourceColor;
        }
    }
}
