using System.Collections;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class LightningBoltWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.LightningBolt;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            var targetCount = 1 + system.GetWeaponExtraCount(weapon);
            var damage = system.GetWeaponBaseDamage(weapon);
            system.StartCoroutine(FireLightningSequence(weapon, system, targetCount, damage));
            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private IEnumerator FireLightningSequence(WeaponRuntime weapon, AutoWeaponSystem system, int remainingStrikes, float damage)
        {
            while (remainingStrikes > 0)
            {
                var validEnemies = CollectTargets(weapon, system);
                if (validEnemies.Count == 0)
                {
                    break;
                }

                var strikesThisWave = Mathf.Min(remainingStrikes, validEnemies.Count);
                for (var i = 0; i < strikesThisWave; i++)
                {
                    StrikeTarget(weapon, system, validEnemies[i], damage);
                }

                remainingStrikes -= strikesThisWave;
                if (remainingStrikes > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        private List<EnemyController> CollectTargets(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var enemies = system.Registry.Enemies;
            var ownerPos = (Vector2)system.Owner.position;
            var range = GetRange(weapon, system);
            var rangeSq = range * range;
            var validEnemies = new List<EnemyController>();

            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !system.IsEnemyUsable(enemy))
                {
                    continue;
                }

                var distSq = ((Vector2)enemy.transform.position - ownerPos).sqrMagnitude;
                if (distSq <= rangeSq)
                {
                    validEnemies.Add(enemy);
                }
            }

            validEnemies.Sort((a, b) =>
                ((Vector2)a.transform.position - ownerPos).sqrMagnitude.CompareTo(((Vector2)b.transform.position - ownerPos).sqrMagnitude));
            return validEnemies;
        }

        private static void StrikeTarget(WeaponRuntime weapon, AutoWeaponSystem system, EnemyController target, float damage)
        {
            if (target == null)
            {
                return;
            }

            var definition = system.GetWeaponDefinition(weapon);
            system.DealDirectWeaponDamage(target, damage, weapon.WeaponId);

            if (!string.IsNullOrWhiteSpace(definition.directVfxResourcePath))
            {
                WeaponFxRenderer.SpawnPrefabFx(
                    definition.directVfxResourcePath,
                    target.transform.position,
                    Quaternion.identity,
                    Vector3.one,
                    0.5f,
                    550);
            }

            if (!string.IsNullOrWhiteSpace(definition.impactVfxResourcePath))
            {
                WeaponFxRenderer.SpawnPrefabFx(
                    definition.impactVfxResourcePath,
                    target.transform.position,
                    Quaternion.identity,
                    Vector3.one * 2f,
                    0.5f,
                    550);
            }
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
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
