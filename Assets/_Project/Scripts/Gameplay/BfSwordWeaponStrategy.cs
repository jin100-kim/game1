using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class BfSwordWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.BfSword;

        private const float BfSwordAfterimageSnapshotLifetime = 0.15f;
        private readonly List<EnemyController> _cleanupEnemies = new(16);

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            UpdateBfSword(weapon, system);
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
        }

        private void UpdateBfSword(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            GetBfSwordBladeSegment(weapon, system, out var start, out var end, out var bladeRadius);
            
            var damage = system.GetWeaponBaseDamage(weapon);
            var currentTime = Time.time;

            _cleanupEnemies.Clear();

            for (int i = system.Registry.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = system.Registry.Enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy)) continue;

                if (IsInsideBfSwordHitbox(enemy, start, end, bladeRadius))
                {
                    _cleanupEnemies.Add(enemy); // We use _cleanupEnemies to track who is currently inside

                    if (!weapon.BfSwordInsideEnemies.Contains(enemy))
                    {
                        weapon.BfSwordInsideEnemies.Add(enemy);
                        system.DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                        
                        if (currentTime >= weapon.NextBfSwordSoundAt)
                        {
                            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Hit, enemy.transform.position);
                            weapon.NextBfSwordSoundAt = currentTime + 0.08f;
                        }
                    }
                }
            }

            // Remove enemies from the 'Inside' list if they are no longer in the hitbox or are dead
            var toRemove = new List<EnemyController>();
            foreach (var enemy in weapon.BfSwordInsideEnemies)
            {
                if (enemy == null || !_cleanupEnemies.Contains(enemy))
                {
                    toRemove.Add(enemy);
                }
            }

            foreach (var enemy in toRemove)
            {
                weapon.BfSwordInsideEnemies.Remove(enemy);
            }
        }

        private void GetBfSwordBladeSegment(WeaponRuntime weapon, AutoWeaponSystem system, out Vector2 start, out Vector2 end, out float radius)
        {
            var facingDirection = system.FacingDirection;
            var normalizedDirection = facingDirection.sqrMagnitude > 0.000001f ? facingDirection.normalized : Vector2.right;
            var origin = system.Owner != null ? (Vector2)system.Owner.position : Vector2.zero;
            var forwardOffset = system.Config != null ? Mathf.Max(0f, system.Config.bfSwordForwardOffset) : 0.48f;
            var visualOffset = system.Config != null ? system.Config.bfSwordVisualLocalOffset : new Vector2(0f, -0.08f);
            
            var bladeCenter = origin + (normalizedDirection * forwardOffset) + visualOffset;
            
            var baseLength = system.Config != null ? Mathf.Max(0.2f, system.Config.bfSwordLength) : 1.75f;
            var lengthMultiplier = system.Stats != null ? Mathf.Max(0.1f, system.Stats.AttackRangeMultiplier) : 1f;
            if (system.Build != null)
            {
                lengthMultiplier *= 1f + (Mathf.Max(0f, system.Build.GetWeaponRangeBonusPercentTotal(weapon.WeaponId)) / 100f);
                lengthMultiplier *= Mathf.Max(1f, system.Build.GetBfSwordLengthMultiplier());
            }
            var bladeLength = baseLength * lengthMultiplier;
            
            var baseThickness = system.Config != null ? Mathf.Max(0.05f, system.Config.bfSwordThickness) : 0.55f;
            var widthMultiplier = system.Build != null ? Mathf.Max(1f, system.Build.GetBfSwordWidthMultiplier()) : 1f;
            radius = (baseThickness * widthMultiplier) * 0.5f;

            var halfSegment = normalizedDirection * (bladeLength * 0.5f);
            start = bladeCenter - halfSegment;
            end = bladeCenter + halfSegment;
        }

        private bool IsInsideBfSwordHitbox(EnemyController enemy, Vector2 start, Vector2 end, float radius)
        {
            var point = (Vector2)enemy.transform.position;
            var line = end - start;
            var lenSq = line.sqrMagnitude;
            if (lenSq < 0.0001f) return (point - start).sqrMagnitude <= radius * radius;
            
            var t = Mathf.Clamp01(Vector2.Dot(point - start, line) / lenSq);
            var projection = start + (t * line);
            return (point - projection).sqrMagnitude <= radius * radius;
        }



        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
            GetBfSwordBladeSegment(weapon, system, out var start, out var end, out var radius);
            WeaponGizmoUtility.DrawCapsuleCollisionGizmo(start, end, radius, color);
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.01f, system.Config.bfSwordHitInterval);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.bfSwordBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return system.Config.bfSwordLength + system.Config.bfSwordForwardOffset;
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.3f, 1f, 0.3f, 0.95f);
        }
    }
}
