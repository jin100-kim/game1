using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class OrbitWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.OrbitWeapon;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            UpdateSatellite(weapon, system);
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
        }

        private void UpdateSatellite(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var satelliteCount = GetSatelliteCount(weapon, system);
            EnsureSatelliteVisuals(weapon, system, satelliteCount);
            if (weapon.SatelliteVisuals.Count <= 0) return;

            var attackSpeedScale = system.Stats != null ? system.Stats.AttackIntervalMultiplier : 1f;
            var weaponAttackSpeedScale = 1f / (1f + (system.Build != null ? system.Build.GetWeaponAttackSpeedBonusPercentTotal(weapon.WeaponId) / 100f : 0f));
            var attackRangeMultiplier = system.Stats != null ? system.Stats.AttackRangeMultiplier : 1f;
            var weaponRangeScale = 1f + (system.Build != null ? system.Build.GetWeaponRangeBonusPercentTotal(weapon.WeaponId) / 100f : 0f);

            var orbitSpeed = Mathf.Max(30f, system.Config.droneAngularSpeed) * (1f / attackSpeedScale) * (1f / weaponAttackSpeedScale);
            weapon.OrbitAngleDegrees += orbitSpeed * Time.deltaTime;
            if (weapon.OrbitAngleDegrees > 360f) weapon.OrbitAngleDegrees -= 360f;

            var orbitRadius = Mathf.Max(0.2f, system.Config.droneOrbitRadius) * weaponRangeScale * attackRangeMultiplier;
            var hitRadius = Mathf.Max(0.05f, system.Config.droneHitRadius) * weaponRangeScale * attackRangeMultiplier;
            var damage = system.GetWeaponBaseDamage(weapon) * Mathf.Clamp(system.Config.droneDamageMultiplier, 0.05f, 5f);
            var hitCooldown = GetSatelliteHitCooldown(weapon, system);

            system.PruneEnemyCooldownMap(weapon.SatelliteHitCooldownUntil);

            var worldPos = system.Owner != null ? (Vector2)system.Owner.position : Vector2.zero;
            var orbitCenterLocal = ResolveWeaponOrbitCenterLocal(system);
            worldPos += orbitCenterLocal;

            for (var satelliteIndex = 0; satelliteIndex < weapon.SatelliteVisuals.Count; satelliteIndex++)
            {
                var satelliteVisual = weapon.SatelliteVisuals[satelliteIndex];
                if (satelliteVisual == null) continue;

                var phase = (360f / Mathf.Max(1, satelliteCount)) * satelliteIndex;
                var angle = (weapon.OrbitAngleDegrees + phase) * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;

                satelliteVisual.position = new Vector3(worldPos.x + offset.x, worldPos.y + offset.y, 0f);

                // ??Šë“ƒ ?ë¨? ™
                foreach (var enemy in system.Registry.Enemies)
                {
                    if (enemy == null || !system.IsEnemyUsable(enemy)) continue;

                    var enemyPos = (Vector2)enemy.transform.position;
                    if ((enemyPos - (Vector2)satelliteVisual.position).sqrMagnitude > (hitRadius + 0.2f) * (hitRadius + 0.2f)) continue;

                    if (weapon.SatelliteHitCooldownUntil.TryGetValue(enemy, out var nextHitAt) && Time.time < nextHitAt) continue;

                    system.DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                    weapon.SatelliteHitCooldownUntil[enemy] = Time.time + hitCooldown;

                    // FX
                    system.SpawnRingFx(enemyPos, hitRadius * 0.9f, new Color(0.45f, 1f, 0.75f, 0.75f), 0.032f, 0.06f, "SatelliteHitFx");
                }
            }
        }

        private void EnsureSatelliteVisuals(WeaponRuntime weapon, AutoWeaponSystem system, int desiredCount)
        {
            var clampedCount = Mathf.Clamp(desiredCount, 0, 12);
            
            while (weapon.SatelliteVisuals.Count < clampedCount)
            {
                weapon.SatelliteVisuals.Add(CreateSatelliteVisual(system));
            }
            while (weapon.SatelliteVisuals.Count > clampedCount)
            {
                var lastIndex = weapon.SatelliteVisuals.Count - 1;
                var visual = weapon.SatelliteVisuals[lastIndex];
                if (visual != null) UnityEngine.Object.Destroy(visual.gameObject);
                weapon.SatelliteVisuals.RemoveAt(lastIndex);
            }
        }

        private Transform CreateSatelliteVisual(AutoWeaponSystem system)
        {
            var satelliteRoot = new GameObject("SatelliteVisual");
            satelliteRoot.transform.SetParent(system.transform, false);

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(satelliteRoot.transform, false);
            var renderer = visualObject.AddComponent<SpriteRenderer>();
            
            var frames = RuntimeSpriteFactory.GetSexyDroneAnimationFrames();
            if (frames != null && frames.Length > 0)
            {
                renderer.sprite = frames[0];
                var animator = visualObject.AddComponent<RuntimeSpriteAnimator>();
                animator.Initialize(renderer, frames, 12f, loop: true, destroyOnComplete: false);
            }
            else
            {
                renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
                renderer.color = new Color(0.45f, 1f, 0.95f, 0.95f);
            }
            renderer.sortingOrder = 33;

            return satelliteRoot.transform;
        }

        private int GetSatelliteCount(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var baseCount = system.Config != null ? Mathf.Max(1, system.Config.droneBaseCount) : 1;
            return Mathf.Max(1, baseCount + system.GetWeaponExtraCount(weapon));
        }

        private float GetSatelliteHitCooldown(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var baseInterval = system.Config != null ? Mathf.Max(0.05f, system.Config.droneHitCooldownPerEnemy) : 0.6f;
            return baseInterval * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        private Vector2 ResolveWeaponOrbitCenterLocal(AutoWeaponSystem system)
        {
            return Vector2.zero; // Simplified for now
        }

                public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return 1f; // Satellite is continuous
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.droneBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.2f, system.Config.droneRange);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.45f, 1f, 0.9f, 0.95f);
        }
    }
}



