using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class ChainLightningWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.ChainLightning;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            if (weapon.ActiveChainCoroutine != null) return;
            weapon.ActiveChainCoroutine = system.StartCoroutine(ChainLightningRoutine(weapon, system));
        }

        private IEnumerator ChainLightningRoutine(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var range = system.GetWeaponRange(weapon);
            var target = system.FindNearestUsableFrom(system.Owner.position, range);
            if (target != null)
            {
                var damage = system.GetWeaponBaseDamage(weapon);
                var maxJumps = Mathf.Max(1, system.Config.chainLightningBaseJumps + system.GetWeaponExtraCount(weapon));
                var jumpRange = range * 1.25f;
                var hopDelay = Mathf.Max(0.01f, system.Config.chainLightningHopDelay);
                var decay = Mathf.Clamp(system.Config.chainLightningDamageDecayPerJump, 0f, 0.9f);

                var currentPos = (Vector2)system.Owner.position;
                var currentTarget = target;
                var jumpCount = 0;
                var hitEnemies = new HashSet<EnemyController>();

                while (currentTarget != null && jumpCount < maxJumps)
                {
                    var targetPos = (Vector2)currentTarget.transform.position;
                    system.DealDirectWeaponDamage(currentTarget, damage, weapon.WeaponId);
                    system.SpawnTracerFx(currentPos, targetPos);
                    system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, targetPos);
                    
                    hitEnemies.Add(currentTarget);
                    jumpCount++;
                    damage *= (1f - decay);
                    currentPos = targetPos;

                    if (jumpCount < maxJumps)
                    {
                        yield return new WaitForSeconds(hopDelay);
                        currentTarget = FindNextChainTarget(system, currentPos, jumpRange, hitEnemies);
                    }
                }
            }

            weapon.Cooldown = GetAttackInterval(weapon, system);
            weapon.ActiveChainCoroutine = null;
        }

        private EnemyController FindNextChainTarget(AutoWeaponSystem system, Vector2 position, float range, HashSet<EnemyController> hitEnemies)
        {
            EnemyController best = null;
            var bestDistSq = range * range;
            for (int i = system.Registry.Enemies.Count - 1; i >= 0; i--)
            {
                var enemy = system.Registry.Enemies[i];
                if (enemy == null || hitEnemies.Contains(enemy) || !system.IsEnemyUsable(enemy)) continue;
                var distSq = ((Vector2)enemy.transform.position - position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = enemy;
                }
            }
            return best;
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
            var range = system.GetWeaponRange(weapon);
            WeaponGizmoUtility.DrawCircleCollisionGizmo(system.Owner.position, range, color);
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.Config.chainLightningAttackInterval) * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.chainLightningBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.5f, system.Config.chainLightningRange);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(0.45f, 1f, 1f, 0.95f);
        }
    }
}
