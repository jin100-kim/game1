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
            var currentTime = Time.time;
            GetBfSwordBladeSegment(weapon, system, out var start, out var end, out var bladeRadius);
            RecordBfSwordSnapshot(weapon, start, end, bladeRadius, currentTime);
            
            CleanupExpiredBfSwordAfterimageHitCooldowns(weapon, currentTime);

            var damage = system.GetWeaponBaseDamage(weapon);
            var hitInterval = system.GetAttackInterval(weapon);

            foreach (var enemy in system.Registry.Enemies)
            {
                if (enemy == null || !system.IsEnemyUsable(enemy)) continue;

                if (IsInsideBfSwordHitbox(enemy, start, end, bladeRadius))
                {
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
                else
                {
                    weapon.BfSwordInsideEnemies.Remove(enemy);
                }

                if (currentTime >= weapon.BfSwordAfterimageHitCooldownUntil.GetValueOrDefault(enemy, 0f))
                {
                    if (TryGetBfSwordAfterimageSnapshot(weapon, currentTime - 0.05f, out var snapshot1))
                    {
                        if (IsInsideBfSwordHitbox(enemy, snapshot1.Start, snapshot1.End, snapshot1.BladeRadius))
                        {
                            system.DealDirectWeaponDamage(enemy, damage * 0.4f, weapon.WeaponId);
                            weapon.BfSwordAfterimageHitCooldownUntil[enemy] = currentTime + hitInterval;
                            continue;
                        }
                    }

                    if (TryGetBfSwordAfterimageSnapshot(weapon, currentTime - 0.10f, out var snapshot2))
                    {
                        if (IsInsideBfSwordHitbox(enemy, snapshot2.Start, snapshot2.End, snapshot2.BladeRadius))
                        {
                            system.DealDirectWeaponDamage(enemy, damage * 0.25f, weapon.WeaponId);
                            weapon.BfSwordAfterimageHitCooldownUntil[enemy] = currentTime + hitInterval;
                        }
                    }
                }
            }

            UpdateBfSwordVisuals(weapon, system, currentTime);
        }

        private void UpdateBfSwordVisuals(WeaponRuntime weapon, AutoWeaponSystem system, float currentTime)
        {
            EnsureBfSwordAfterimageRenderers(weapon, system, 2);
            if (TryGetBfSwordAfterimageSnapshot(weapon, currentTime - 0.05f, out var snapshot1))
            {
                ApplySnapshotToRenderer(weapon.BfSwordAfterimageRenderers[0], snapshot1, 0.45f);
            }
            if (TryGetBfSwordAfterimageSnapshot(weapon, currentTime - 0.10f, out var snapshot2))
            {
                ApplySnapshotToRenderer(weapon.BfSwordAfterimageRenderers[1], snapshot2, 0.22f);
            }
        }

        private void EnsureBfSwordAfterimageRenderers(WeaponRuntime weapon, AutoWeaponSystem system, int count)
        {
            while (weapon.BfSwordAfterimageRenderers.Count < count)
            {
                var obj = new GameObject($"BfSwordAfterimage_{weapon.BfSwordAfterimageRenderers.Count}");
                obj.transform.SetParent(system.transform, false);
                var renderer = obj.AddComponent<SpriteRenderer>();
                renderer.sprite = RuntimeSpriteFactory.GetSexySwordSprite();
                renderer.sortingOrder = 28;
                weapon.BfSwordAfterimageRenderers.Add(renderer);
            }
        }

        private void ApplySnapshotToRenderer(SpriteRenderer renderer, BfSwordBladeSnapshot snapshot, float alpha)
        {
            if (renderer == null) return;
            renderer.gameObject.SetActive(true);
            var direction = snapshot.End - snapshot.Start;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            renderer.transform.position = (Vector3)snapshot.Start;
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            renderer.color = new Color(1f, 1f, 1f, alpha);
        }

        private void GetBfSwordBladeSegment(WeaponRuntime weapon, AutoWeaponSystem system, out Vector2 start, out Vector2 end, out float radius)
        {
            var ownerPos = (Vector2)system.Owner.position;
            var direction = system.LastAimDirection;
            var length = system.Config.bfSwordLength;
            var offset = system.Config.bfSwordForwardOffset;
            start = ownerPos + (direction * offset);
            end = start + (direction * length);
            radius = system.Config.bfSwordThickness;
        }

        private void RecordBfSwordSnapshot(WeaponRuntime weapon, Vector2 start, Vector2 end, float radius, float time)
        {
            weapon.BfSwordBladeHistory.Add(new BfSwordBladeSnapshot(start, end, radius, time));
            if (weapon.BfSwordBladeHistory.Count > 30)
            {
                weapon.BfSwordBladeHistory.RemoveAt(0);
            }
        }

        private bool TryGetBfSwordAfterimageSnapshot(WeaponRuntime weapon, float targetTime, out BfSwordBladeSnapshot snapshot)
        {
            snapshot = default;
            if (weapon.BfSwordBladeHistory.Count == 0) return false;
            
            for (var i = weapon.BfSwordBladeHistory.Count - 1; i >= 0; i--)
            {
                if (weapon.BfSwordBladeHistory[i].RecordedAt <= targetTime)
                {
                    snapshot = weapon.BfSwordBladeHistory[i];
                    return true;
                }
            }
            return false;
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

        private void CleanupExpiredBfSwordAfterimageHitCooldowns(WeaponRuntime weapon, float currentTime)
        {
            _cleanupEnemies.Clear();
            foreach (var kvp in weapon.BfSwordAfterimageHitCooldownUntil)
            {
                if (currentTime > kvp.Value) _cleanupEnemies.Add(kvp.Key);
            }
            foreach (var enemy in _cleanupEnemies) weapon.BfSwordAfterimageHitCooldownUntil.Remove(enemy);
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
