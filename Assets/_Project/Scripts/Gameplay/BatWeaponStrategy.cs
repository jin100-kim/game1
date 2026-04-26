using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class BatWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.Bat;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var hadBatInstances = weapon.BatInstances.Count > 0;
            UpdateBatInstances(weapon, system);

            if (hadBatInstances && weapon.BatInstances.Count <= 0)
            {
                weapon.Cooldown = system.GetAttackInterval(weapon) * system.GetCombinedAttackIntervalMultiplier(weapon);
            }

            if (weapon.BatInstances.Count > 0)
            {
                return;
            }

            if (weapon.Cooldown > 0f)
            {
                return;
            }

            var batCount = Mathf.Max(1, 1 + system.GetWeaponExtraCount(weapon));
            var spawnedAny = false;
            for (var i = 0; i < batCount; i++)
            {
                SpawnBatInstance(weapon, system);
                spawnedAny = true;
            }

            var interval = system.GetAttackInterval(weapon) * system.GetCombinedAttackIntervalMultiplier(weapon); 
            weapon.Cooldown = spawnedAny ? interval : Mathf.Max(0.15f, interval * 0.25f);
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            // 박쥐는 Update에서 스스로 스폰되므로 OnFire에서는 아무것도 하지 않습니다.
        }

        private void UpdateBatInstances(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            if (system.Owner == null || weapon == null || weapon.BatInstances.Count <= 0)
            {
                return;
            }

            var attackIntervalMultiplier = Mathf.Max(0.05f, system.GetCombinedAttackIntervalMultiplier(weapon));
            var attackSpeedFactor = 1f / attackIntervalMultiplier;
            var moveSpeed = Mathf.Max(0.1f, system.Config.batMoveSpeed * attackSpeedFactor);
            var orbitRadius = Mathf.Max(0.1f, system.Config.batOrbitRadius);
            var launchDuration = Mathf.Max(0f, system.Config.batOrbitDuration);
            var latchRange = system.GetWeaponRange(weapon);
            var hitInterval = Mathf.Max(0.05f, system.Config.batHitInterval * attackIntervalMultiplier);
            var damage = system.GetWeaponBaseDamage(weapon) * Mathf.Clamp(system.Config.batDamageMultiplier, 0.05f, 5f);
            var hitsBeforeReturn = Mathf.Max(1, system.Config.batHitsBeforeReturn);

            for (var i = weapon.BatInstances.Count - 1; i >= 0; i--)
            {
                var bat = weapon.BatInstances[i];
                if (bat?.Root == null)
                {
                    weapon.BatInstances.RemoveAt(i);
                    continue;
                }

                if (bat.ReturningToOwner)
                {
                    var toOwner = (Vector2)system.Owner.position - (Vector2)bat.Root.position;
                    var distance = toOwner.magnitude;
                    if (distance <= 0.18f)
                    {
                        if (bat.PendingHealAmount > 0.001f)
                        {
                            ApplyBatHealing(weapon, system, bat.PendingHealAmount);
                        }

                        UnityEngine.Object.Destroy(bat.Root.gameObject);
                        weapon.BatInstances.RemoveAt(i);
                        continue;
                    }

                    bat.Root.position += (Vector3)(toOwner / Mathf.Max(0.0001f, distance)) * (moveSpeed * 1.35f * Time.deltaTime);
                    continue;
                }

                if (bat.HitsLanded >= hitsBeforeReturn)
                {
                    BeginBatReturn(weapon, system, bat);
                    continue;
                }

                if (bat.LatchedTarget == null || !system.IsEnemyUsable(bat.LatchedTarget))
                {
                    var previousTarget = bat.LatchedTarget;
                    bat.LatchedTarget = Time.time >= bat.SeekAt
                        ? system.FindNearestUsableFrom((Vector2)bat.Root.position, latchRange)
                        : null;
                    if (previousTarget == null && bat.LatchedTarget != null)
                    {
                        bat.HitCooldown = Mathf.Max(bat.HitCooldown, Time.time + hitInterval);
                        system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Latch, bat.Root.position);
                    }
                }

                if (bat.LatchedTarget == null)
                {
                    if (Time.time < bat.SpawnedAt + launchDuration)
                    {
                        bat.Root.position += (Vector3)(bat.LaunchDirection * moveSpeed * Time.deltaTime);
                    }
                    else
                    {
                        var orbitAngle = (bat.OrbitSeedDegrees + ((Time.time - bat.SeekAt) * 180f)) * Mathf.Deg2Rad;
                        var orbitTarget = (Vector2)system.Owner.position + (new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle)) * orbitRadius);
                        var toOrbit = orbitTarget - (Vector2)bat.Root.position;
                        var orbitDistance = toOrbit.magnitude;
                        if (orbitDistance > 0.02f)
                        {
                            bat.Root.position += (Vector3)(toOrbit / Mathf.Max(0.0001f, orbitDistance)) * (moveSpeed * Time.deltaTime);
                        }
                    }

                    continue;
                }

                var latchTargetPosition = (Vector2)bat.LatchedTarget.transform.position;
                var toTarget = latchTargetPosition - (Vector2)bat.Root.position;
                var targetDistance = toTarget.magnitude;
                if (targetDistance > 0.18f)
                {
                    bat.Root.position += (Vector3)(toTarget / Mathf.Max(0.0001f, targetDistance)) * (moveSpeed * Time.deltaTime);
                    continue;
                }

                bat.Root.position = new Vector3(latchTargetPosition.x, latchTargetPosition.y, 0f);
                if (Time.time >= bat.HitCooldown)
                {
                    system.DealDirectWeaponDamage(bat.LatchedTarget, damage, weapon.WeaponId);
                    bat.PendingHealAmount += Mathf.Max(system.Config.batMinimumHealPerHit, damage * Mathf.Clamp01(system.Config.batHealPerDamageMultiplier));
                    bat.HitsLanded++;
                    bat.HitCooldown = Time.time + hitInterval;
                    bat.LatchedTarget = null;
                    if (bat.HitsLanded >= hitsBeforeReturn)
                    {
                        BeginBatReturn(weapon, system, bat);
                    }
                }
            }
        }

        private void SpawnBatInstance(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var batObject = new GameObject("Bat");
            batObject.transform.SetParent(system.transform, false);
            batObject.transform.position = system.Owner != null ? system.Owner.position : Vector3.zero;
            batObject.transform.localScale = Vector3.one * Mathf.Max(0.05f, system.Config.batVisualScale);

            var renderer = batObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(0.88f, 0.24f, 0.72f, 0.95f);
            renderer.sortingOrder = 35;

            weapon.BatInstances.Add(new BatRuntime
            {
                Root = batObject.transform,
                Renderer = renderer,
                LatchedTarget = null,
                SpawnedAt = Time.time,
                SeekAt = Time.time + Mathf.Max(0f, system.Config.batOrbitDuration),
                HitCooldown = Time.time,
                OrbitSeedDegrees = UnityEngine.Random.Range(0f, 360f),
                LaunchDirection = AutoWeaponSystem.RotateDirection(Vector2.right, UnityEngine.Random.Range(0f, 360f)).normalized,
                PendingHealAmount = 0f,
                HitsLanded = 0,
                ReturningToOwner = false,
            });
            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Spawn, batObject.transform.position);
        }

        private void BeginBatReturn(WeaponRuntime weapon, AutoWeaponSystem system, BatRuntime bat)
        {
            if (bat == null || bat.ReturningToOwner)
            {
                return;
            }

            bat.ReturningToOwner = true;
            bat.LatchedTarget = null;
            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Return, bat.Root != null ? bat.Root.position : system.GetOwnerSoundPosition());
            if (bat.Renderer != null)
            {
                bat.Renderer.color = new Color(0.52f, 1f, 0.72f, 0.95f);
            }
        }

        private void ApplyBatHealing(WeaponRuntime weapon, AutoWeaponSystem system, float healAmount)
        {
            if (system.PlayerHealth == null || healAmount <= 0f)
            {
                return;
            }

            var missingHealth = Mathf.Max(0f, system.PlayerHealth.MaxHealth - system.PlayerHealth.CurrentHealth);
            system.PlayerHealth.Heal(healAmount);
            var overflow = Mathf.Max(0f, healAmount - missingHealth);

            if (overflow <= 0.0001f)
            {
                return;
            }

            weapon.BatOverflowMaxHealthProgress += overflow;
            while (weapon.BatOverflowMaxHealthProgress >= 20f)
            {
                system.Build?.AddRuntimeMaxHealthFlat(1f);
                system.PlayerHealth.SetMaxHealth(system.PlayerHealth.MaxHealth + 1f, healDelta: true);
                weapon.BatOverflowMaxHealthProgress -= 20f;
            }
        }

        public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.05f, system.Config.batAttackInterval);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.batBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.5f, system.Config.batLatchRange);
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(1f, 0.65f, 0.35f, 0.95f);
        }
    }
}
