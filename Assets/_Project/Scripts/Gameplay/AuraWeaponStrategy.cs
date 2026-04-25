using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class AuraWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.Aura;

        private LineRenderer _auraLine;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            UpdateAura(weapon, system);
            UpdateAuraVisual(weapon, system);
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
        }

        private void UpdateAura(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            weapon.Cooldown -= Time.deltaTime;
            if (weapon.Cooldown > 0f) return;

            weapon.Cooldown = GetAuraTickInterval(weapon, system);

            var range = GetAuraRange(weapon, system);
            var damage = system.GetWeaponBaseDamage(weapon) * Mathf.Clamp(system.Config.auraDamageMultiplier, 0.01f, 5f);
            var ownerPos = (Vector2)system.Owner.position;

            for (int i = system.Registry.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = system.Registry.Enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy)) continue;

                var enemyPos = (Vector2)enemy.transform.position;
                if ((enemyPos - ownerPos).sqrMagnitude <= (range + 0.2f) * (range + 0.2f))
                {
                    system.DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                }
            }

            // Pulse FX
            system.SpawnRingFx(ownerPos, range, new Color(0.45f, 1f, 0.75f, 0.75f), 0.032f, 0.08f, "AuraPulseFx");
        }

        private void UpdateAuraVisual(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            if (system.Owner == null)
            {
                if (_auraLine != null) _auraLine.enabled = false;
                return;
            }

            EnsureAuraLine(system);
            if (_auraLine == null) return;

            var range = GetAuraRange(weapon, system);
            var color = new Color(0.45f, 1f, 0.75f, 0.75f);
            
            WeaponFxRenderer.ConfigureLineRenderer(_auraLine, color, 0.018f, true, false);
            WeaponFxRenderer.SetCircleLinePositions(_auraLine, system.Owner.position, range, 28, 0f);
            _auraLine.enabled = true;
        }

        private void EnsureAuraLine(AutoWeaponSystem system)
        {
            if (_auraLine != null) return;

            var auraObject = new GameObject("AuraIdleFx");
            auraObject.transform.SetParent(system.transform, false);
            _auraLine = auraObject.AddComponent<LineRenderer>();
            _auraLine.enabled = false;
        }

                public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return GetAuraTickInterval(weapon, system);
        }

        private float GetAuraTickInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var baseInterval = system.Config != null ? Mathf.Max(0.01f, system.Config.auraTickInterval) : 0.5f;
            return Mathf.Max(0.03f, baseInterval * system.GetCombinedAttackIntervalMultiplier(weapon));
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.auraBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return GetAuraRange(weapon, system);
        }

        private float GetAuraRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var baseRadius = system.Config != null ? Mathf.Max(0.2f, system.Config.auraRadius) : 2f;
            var rangeMultiplier = system.Stats != null ? system.Stats.AttackRangeMultiplier : 1f;
            var weaponRangeBonus = 1f + (system.Build != null ? system.Build.GetWeaponRangeBonusPercentTotal(weapon.WeaponId) / 100f : 0f);
            var milestoneMultiplier = system.Build != null ? Mathf.Max(1f, system.Build.GetAuraMilestoneRangeMultiplier()) : 1f;

            return baseRadius * rangeMultiplier * weaponRangeBonus * milestoneMultiplier;
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.45f, 1f, 0.75f, 0.95f);
        }
    }
}



