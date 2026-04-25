using EJR.Game.Audio;
using EJR.Game.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class TurretWeaponStrategy : IWeaponStrategy
    {
        public WeaponUpgradeId WeaponId => WeaponUpgradeId.Turret;

        public void OnInitialize(WeaponRuntime weapon, AutoWeaponSystem system)
        {
        }

        public void Update(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            UpdateTurrets(weapon, system);
        }

        public void OnFire(WeaponRuntime weapon, AutoWeaponSystem system, Vector2 direction)
        {
            DeployTurret(weapon, system);
            weapon.Cooldown = GetAttackInterval(weapon, system);
        }

        private void UpdateTurrets(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            if (weapon.TurretInstances.Count <= 0) return;

            var damage = system.GetWeaponBaseDamage(weapon);
            var range = GetTurretRange(weapon, system);
            var shotInterval = GetTurretShotInterval(weapon, system);

            for (var i = weapon.TurretInstances.Count - 1; i >= 0; i--)
            {
                var turret = weapon.TurretInstances[i];
                if (turret == null || turret.Root == null)
                {
                    weapon.TurretInstances.RemoveAt(i);
                    continue;
                }

                if (Time.time >= turret.ExpiresAt)
                {
                    DestroyTurret(weapon, system, i);
                    continue;
                }

                turret.ShotCooldown -= Time.deltaTime;
                if (turret.ShotCooldown <= 0f)
                {
                    var target = system.FindNearestUsableFrom(turret.Position, range);
                    if (target != null)
                    {
                        FireTurret(weapon, system, turret, target, damage);
                        turret.ShotCooldown = shotInterval;
                    }
                    else
                    {
                        SetTurretIdle(turret);
                    }
                }
            }
        }

        private void FireTurret(WeaponRuntime weapon, AutoWeaponSystem system, TurretRuntime turret, EnemyController target, float damage)
        {
            var targetPos = (Vector2)target.transform.position;
            system.DealDirectWeaponDamage(target, damage, weapon.WeaponId);
            system.SpawnTracerFx(turret.Position, targetPos);
            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, turret.Position);
            
            PlayTurretFireAnimation(weapon, system, turret);
        }

        private void DeployTurret(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            if (system.Owner == null) return;

            var turretObject = new GameObject("Turret");
            turretObject.transform.SetParent(system.transform, false);
            var deployPos = (Vector2)system.Owner.position;
            turretObject.transform.position = deployPos;

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(turretObject.transform, false);
            var renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 32;
            
            var visualScale = system.Config != null ? system.Config.turretVisualScale : 3f;
            visualObject.transform.localScale = Vector3.one * Mathf.Max(0.05f, visualScale);

            var turretRange = GetTurretRange(weapon, system);
            
            // Range FX
            var rangeFxObject = new GameObject("TurretRangeFx");
            rangeFxObject.transform.SetParent(turretObject.transform, false);
            var rangeRenderer = rangeFxObject.AddComponent<LineRenderer>();
            var rangeColor = new Color(0.55f, 0.9f, 1f, 0.28f);
            WeaponFxRenderer.ConfigureLineRenderer(rangeRenderer, rangeColor, 0.03f, true, false);
            WeaponFxRenderer.SetCircleLinePositions(rangeRenderer, Vector3.zero, turretRange, 28, 0f);

            var turretFrames = RuntimeSpriteFactory.GetSexyTurretAnimationFrames();
            var idleFrame = (turretFrames != null && turretFrames.Length > 0) ? turretFrames[0] : null;
            var fireFrames = turretFrames;

            var runtime = new TurretRuntime
            {
                Root = turretObject.transform,
                Position = deployPos,
                ExpiresAt = Time.time + (system.Config != null ? Mathf.Max(0.1f, system.Config.turretLifetime) : 5f),
                ShotCooldown = 0f,
                Renderer = renderer,
                IdleFrame = idleFrame,
                FireFrames = fireFrames
            };

            renderer.sprite = idleFrame;
            weapon.TurretInstances.Add(runtime);
            
            system.RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Deploy, deployPos);
        }

        private void SetTurretIdle(TurretRuntime turret)
        {
            if (turret == null || turret.Renderer == null || turret.IdleFrame == null) return;
            if (turret.FireAnimationCoroutine == null)
            {
                turret.Renderer.sprite = turret.IdleFrame;
            }
        }

        private void PlayTurretFireAnimation(WeaponRuntime weapon, AutoWeaponSystem system, TurretRuntime turret)
        {
            if (turret == null || turret.Renderer == null || turret.FireFrames == null || turret.FireFrames.Length <= 0) return;

            if (turret.FireAnimationCoroutine != null)
            {
                system.StopCoroutine(turret.FireAnimationCoroutine);
            }
            turret.FireAnimationCoroutine = system.StartCoroutine(PlayTurretFireAnimationRoutine(turret, system));
        }

        private IEnumerator PlayTurretFireAnimationRoutine(TurretRuntime turret, AutoWeaponSystem system)
        {
            if (turret == null || turret.Renderer == null) yield break;

            var fps = system.Config != null ? system.Config.turretVisualAnimationFps : 12f;
            var frameDuration = 1f / Mathf.Max(0.1f, fps);
            
            for (var i = 0; i < turret.FireFrames.Length; i++)
            {
                if (turret.Renderer == null) yield break;
                turret.Renderer.sprite = turret.FireFrames[i];
                yield return new WaitForSeconds(frameDuration);
            }

            if (turret.Renderer != null && turret.IdleFrame != null)
            {
                turret.Renderer.sprite = turret.IdleFrame;
            }
            turret.FireAnimationCoroutine = null;
        }

        private void DestroyTurret(WeaponRuntime weapon, AutoWeaponSystem system, int index)
        {
            var turret = weapon.TurretInstances[index];
            if (turret != null)
            {
                if (turret.FireAnimationCoroutine != null)
                {
                    system.StopCoroutine(turret.FireAnimationCoroutine);
                    turret.FireAnimationCoroutine = null;
                }
                if (turret.Root != null) UnityEngine.Object.Destroy(turret.Root.gameObject);
            }
            weapon.TurretInstances.RemoveAt(index);
        }

                public void OnDrawGizmos(WeaponRuntime weapon, AutoWeaponSystem system, Color color)
        {
        }

        public float GetAttackInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var baseInterval = system.Config != null ? Mathf.Max(0.1f, system.Config.turretDeployInterval) : 2f;
            return baseInterval * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        private float GetTurretShotInterval(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var baseInterval = system.Config != null ? Mathf.Max(0.05f, system.Config.turretShotInterval) : 0.45f;
            return baseInterval * system.GetCombinedAttackIntervalMultiplier(weapon);
        }

        public float GetBaseDamage(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return Mathf.Max(0.1f, system.Config.turretBaseDamage);
        }

        public float GetRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return GetTurretRange(weapon, system);
        }

        private float GetTurretRange(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            var multiplier = system.Config != null ? Mathf.Clamp(system.Config.turretRangeMultiplier, 0.1f, 3f) : 1.25f;
            return system.GetWeaponRange(weapon) * multiplier;
        }

        public Color GetSourceColor(WeaponRuntime weapon, AutoWeaponSystem system)
        {
            return new Color(1f, 0.86f, 0.28f, 0.95f);
        }
    }
}



