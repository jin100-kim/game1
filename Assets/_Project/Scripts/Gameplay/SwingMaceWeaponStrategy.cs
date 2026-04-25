using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class SwingMaceWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.SwingMace;

        private const float SwingMaceHandleMinorStunDuration = 0.05f;
        private const float SwingMaceLengthRangeBonusShare = 0.7f;
        private const float SwingMaceHeadRangeBonusShare = 0.3f;
        private const float SwingMaceVisualForwardOffset = 0f;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            if (weapon.IsSwingMaceSwingActive)
            {
                var duration = Mathf.Max(0.01f, system.Config.swingMaceSwingDuration);
                weapon.SwingMaceSwingElapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(weapon.SwingMaceSwingElapsed / duration);

                UpdateSwingMaceState(weapon, system, progress);

                if (progress >= 1f)
                {
                    weapon.IsSwingMaceSwingActive = false;
                    weapon.SwingMaceHitEnemies.Clear();
                    weapon.SwingMaceStunnedEnemies.Clear();
                    if (weapon.SwingMaceVisualRoot != null)
                    {
                        weapon.SwingMaceVisualRoot.gameObject.SetActive(false);
                    }
                    weapon.Cooldown = GetAttackInterval(weapon, system);
                }
            }
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var targetDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : system.LastAimDirection;
            weapon.SwingMaceSwingDirection = targetDirection;
            weapon.IsSwingMaceSwingActive = true;
            weapon.SwingMaceSwingElapsed = 0f;
            weapon.SwingMaceHitEnemies.Clear();
            weapon.SwingMaceStunnedEnemies.Clear();
            
            UpdateSwingMaceState(weapon, system, 0f);
            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, system.GetOwnerSoundPosition());
        }

        private void UpdateSwingMaceState(WeaponRuntime weapon, AutoWeaponSystem system, float progress)
        {
            if (system.Owner == null) return;

            EnsureSwingMaceVisual(weapon, system);
            if (weapon.SwingMaceVisualRoot == null) return;

            weapon.SwingMaceVisualRoot.gameObject.SetActive(true);
            var halfArc = Mathf.Max(5f, system.Config.swingMaceArcAngle) * 0.5f;
            var currentSwingDir = AutoWeaponSystem.RotateDirection(weapon.SwingMaceSwingDirection, Mathf.Lerp(-halfArc, halfArc, progress));
            
            weapon.SwingMaceVisualRoot.position = system.Owner.position + (Vector3)(currentSwingDir * SwingMaceVisualForwardOffset);
            weapon.SwingMaceVisualRoot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(currentSwingDir.y, currentSwingDir.x) * Mathf.Rad2Deg);

            var range = GetSwingMaceLength(weapon, system);
            var head = weapon.SwingMaceVisualRoot.Find("Head");
            var headVisualSize = GetSwingMaceVisualHeadSize(weapon, system);
            var hitRadius = GetSwingMaceHeadHitRadius(weapon, system);

            if (head != null)
            {
                head.localPosition = new Vector3(range, 0f, 0f);
                head.localScale = Vector3.one * headVisualSize;
                
                var handle = weapon.SwingMaceVisualRoot.Find("Handle");
                if (handle != null)
                {
                    var visualLength = range - (headVisualSize * 0.4f);
                    var visualWidth = system.Config.swingMaceVisualHandleWidth;
                    ApplySwingMaceVisualScale(handle, visualLength, visualWidth);
                }
            }

            var handleHitRadius = Mathf.Max(system.Config.swingMaceVisualHandleWidth * 0.5f, hitRadius * 0.3f);
            var damage = system.GetWeaponBaseDamage(weapon) * Mathf.Clamp(system.Config.swingMaceDamageMultiplier, 0.05f, 5f);
            var stunDuration = Mathf.Max(0.05f, system.Config.swingMaceStunDuration) * (1f + (0.25f * (system.Build != null ? system.Build.GetWeaponMilestoneCount(weapon.WeaponId) : 0)));

            var handleStart = (Vector2)system.Owner.position;
            var handleEnd = (Vector2)system.Owner.position + (currentSwingDir * range);
            var headPos = handleEnd;

            for (int i = system.Registry.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = system.Registry.Enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy) || weapon.SwingMaceHitEnemies.Contains(enemy)) continue;

                var enemyPos = (Vector2)enemy.transform.position;
                
                // Head Hit
                if ((enemyPos - headPos).sqrMagnitude <= (hitRadius + 0.2f) * (hitRadius + 0.2f))
                {
                    weapon.SwingMaceHitEnemies.Add(enemy);
                    system.DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                    system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Hit, enemy.transform.position);
                    continue;
                }

                // Handle Hit
                var line = handleEnd - handleStart;
                var lenSq = line.sqrMagnitude;
                var t = Mathf.Clamp01(Vector2.Dot(enemyPos - handleStart, line) / Mathf.Max(0.0001f, lenSq));
                var projection = handleStart + (t * line);
                if ((enemyPos - projection).sqrMagnitude <= (handleHitRadius + 0.15f) * (handleHitRadius + 0.15f))
                {
                    weapon.SwingMaceHitEnemies.Add(enemy);
                    system.DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                }
            }
        }

        private void EnsureSwingMaceVisual(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            if (weapon == null || weapon.SwingMaceVisualRoot != null) return;

            var root = new GameObject("SwingMaceVisual");
            root.transform.SetParent(system.transform, false);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(root.transform, false);
            var handleRenderer = handle.AddComponent<SpriteRenderer>();
            handleRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            handleRenderer.color = new Color(0.6f, 0.4f, 0.2f, 0.95f);
            handleRenderer.sortingOrder = 30;

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            var headRenderer = head.AddComponent<SpriteRenderer>();
            headRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            headRenderer.color = new Color(0.85f, 0.85f, 0.85f, 0.95f);
            headRenderer.sortingOrder = 31;

            weapon.SwingMaceVisualRoot = root.transform;
            weapon.SwingMaceVisualRoot.gameObject.SetActive(false);
        }

        private static void ApplySwingMaceVisualScale(Transform visualTransform, float desiredLength, float desiredWidth)
        {
            visualTransform.localScale = new Vector3(desiredLength, desiredWidth, 1f);
            visualTransform.localPosition = new Vector3(desiredLength * 0.5f, 0f, 0f);
        }

        private float GetSwingMaceLength(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var baseLength = system.Config != null ? Mathf.Max(0.25f, system.Config.swingMaceMeleeRange) : 1f;
            var scale = GetSwingMaceRangeComponentScale(weapon, system, SwingMaceLengthRangeBonusShare);
            return baseLength * scale;
        }

        private float GetSwingMaceHeadHitRadius(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var baseRadius = system.Config != null ? Mathf.Max(0.05f, system.Config.swingMaceHitRadius) : 0.5f;
            var scale = GetSwingMaceRangeComponentScale(weapon, system, SwingMaceHeadRangeBonusShare);
            return baseRadius * scale;
        }

        private float GetSwingMaceVisualHeadSize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var baseSize = system.Config != null ? Mathf.Max(0.05f, system.Config.swingMaceVisualHeadSize) : 0.38f;
            var scale = GetSwingMaceRangeComponentScale(weapon, system, SwingMaceHeadRangeBonusShare);
            return baseSize * scale;
        }

        private float GetSwingMaceRangeComponentScale(WeaponRuntime weapon, AutoWeaponSystem system, float bonusShare)
        {
            var totalRangeBonus = system.GetWeaponRangeBonusPercent(weapon);
            return 1f + (totalRangeBonus * bonusShare);
        }

                public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.Config.swingMaceAttackInterval);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.swingMaceBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.25f, system.Config.swingMaceMeleeRange);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.95f, 0.95f, 0.45f, 0.95f);
        }
    }
}



