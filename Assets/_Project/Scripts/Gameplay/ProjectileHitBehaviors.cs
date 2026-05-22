using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public readonly struct ProjectileHitResult
    {
        public ProjectileHitResult(bool releaseProjectile)
        {
            ReleaseProjectile = releaseProjectile;
        }

        public bool ReleaseProjectile { get; }
    }

    public readonly struct ProjectileHitContext
    {
        public ProjectileHitContext(
            WeaponDefinition definition,
            WeaponUpgradeId weaponId,
            EnemyController enemy,
            Vector3 position,
            Vector3 direction,
            float currentDamage,
            float baseDamage,
            bool isFragment,
            EnemyRegistry registry,
            Transform fxParent,
            Func<float, EnemyController, float> damageResolver,
            Action<float, EnemyController> directHitCallback,
            List<EnemyController> nearbyEnemies)
        {
            Definition = definition;
            WeaponId = weaponId;
            Enemy = enemy;
            Position = position;
            Direction = direction;
            CurrentDamage = currentDamage;
            BaseDamage = baseDamage;
            IsFragment = isFragment;
            Registry = registry;
            FxParent = fxParent;
            DamageResolver = damageResolver;
            DirectHitCallback = directHitCallback;
            NearbyEnemies = nearbyEnemies;
        }

        public WeaponDefinition Definition { get; }
        public WeaponUpgradeId WeaponId { get; }
        public EnemyController Enemy { get; }
        public Vector3 Position { get; }
        public Vector3 Direction { get; }
        public float CurrentDamage { get; }
        public float BaseDamage { get; }
        public bool IsFragment { get; }
        public EnemyRegistry Registry { get; }
        public Transform FxParent { get; }
        public Func<float, EnemyController, float> DamageResolver { get; }
        public Action<float, EnemyController> DirectHitCallback { get; }
        public List<EnemyController> NearbyEnemies { get; }

        public float ResolveDamage(float damage, EnemyController enemy)
        {
            return DamageResolver != null ? DamageResolver.Invoke(damage, enemy) : Mathf.Max(0f, damage);
        }

        public float ApplyDamage(float damage, EnemyController enemy)
        {
            if (enemy == null)
            {
                return 0f;
            }

            var appliedDamage = ResolveDamage(damage, enemy);
            enemy.ReceiveWeaponDamage(appliedDamage, WeaponId);
            return appliedDamage;
        }

        public void NotifyDirectHit(float appliedDamage, EnemyController enemy)
        {
            DirectHitCallback?.Invoke(appliedDamage, enemy);
        }
    }

    public interface IProjectileHitBehavior
    {
        ProjectileHitResult OnHit(ProjectileHitContext context);
    }

    public static class WeaponProjectileHitBehaviorFactory
    {
        private static readonly IProjectileHitBehavior s_default = new DefaultProjectileHitBehavior();
        private static readonly IProjectileHitBehavior s_fireball = new FireballProjectileHitBehavior();
        private static readonly IProjectileHitBehavior s_lightning = new LightningProjectileHitBehavior();
        private static readonly IProjectileHitBehavior s_ice = new IceProjectileHitBehavior();
        private static readonly IProjectileHitBehavior s_wind = new WindProjectileHitBehavior();

        public static IProjectileHitBehavior Get(WeaponDefinition definition)
        {
            return definition?.impactBehavior switch
            {
                WeaponImpactBehaviorKind.FireballExplosion => s_fireball,
                WeaponImpactBehaviorKind.LightningImpact => s_lightning,
                WeaponImpactBehaviorKind.IceSlow => s_ice,
                WeaponImpactBehaviorKind.WindKnockback => s_wind,
                _ => s_default,
            };
        }

        internal static void SpawnConfiguredImpactFx(ProjectileHitContext context, float scale = 2f, float duration = 0.5f, int sortingOrder = 550)
        {
            var definition = context.Definition;
            if (definition == null)
            {
                return;
            }

            if (definition.impactVfxPrefab != null)
            {
                var fxObject = UnityEngine.Object.Instantiate(definition.impactVfxPrefab, context.Position, Quaternion.identity);
                VfxAudioRouter.RouteEmbeddedAudio(fxObject);
                fxObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
                UnityEngine.Object.Destroy(fxObject, Mathf.Max(0.1f, duration));
                return;
            }

            if (!string.IsNullOrWhiteSpace(definition.impactVfxResourcePath))
            {
                WeaponFxRenderer.SpawnPrefabFx(
                    definition.impactVfxResourcePath,
                    context.Position,
                    Quaternion.identity,
                    Vector3.one * Mathf.Max(0.01f, scale),
                    duration,
                    sortingOrder);
            }
        }

        private sealed class DefaultProjectileHitBehavior : IProjectileHitBehavior
        {
            public ProjectileHitResult OnHit(ProjectileHitContext context)
            {
                var appliedDamage = context.ApplyDamage(context.CurrentDamage, context.Enemy);
                context.NotifyDirectHit(appliedDamage, context.Enemy);
                SpawnConfiguredImpactFx(context);
                return new ProjectileHitResult(context.Definition != null && context.Definition.releaseProjectileOnHit);
            }
        }

        private sealed class FireballProjectileHitBehavior : IProjectileHitBehavior
        {
            public ProjectileHitResult OnHit(ProjectileHitContext context)
            {
                var appliedDamage = context.ApplyDamage(context.CurrentDamage, context.Enemy);
                ApplyExplosionKnockback(context, context.Enemy, context.Direction);
                context.NotifyDirectHit(appliedDamage, context.Enemy);
                TriggerExplosion(context);
                return new ProjectileHitResult(true);
            }

            private static void ApplyExplosionKnockback(ProjectileHitContext context, EnemyController enemy, Vector3 direction)
            {
                if (context.Definition == null || enemy == null || context.Definition.knockbackStrength <= 0f)
                {
                    return;
                }

                var knockbackDirection = ((Vector2)direction).sqrMagnitude > 0.0001f
                    ? ((Vector2)direction).normalized
                    : Vector2.right;
                enemy.ApplyKnockback(knockbackDirection, context.Definition.knockbackStrength);
            }

            private static void TriggerExplosion(ProjectileHitContext context)
            {
                var definition = context.Definition;
                if (definition == null)
                {
                    return;
                }

                WeaponFxRenderer.SpawnFireBurstFx(
                    context.FxParent,
                    context.Position,
                    Mathf.Max(0.1f, definition.explosionRadius * definition.explosionFxScaleMultiplier),
                    definition.explosionFxDuration,
                    530,
                    "FireballExplosionFx");

                if (context.Registry == null || context.NearbyEnemies == null)
                {
                    return;
                }

                var searchRadius = definition.explosionRadius + context.Registry.GetMaxCollisionRadius();
                context.Registry.GetNearby(context.Position, searchRadius, context.NearbyEnemies);
                var explosionDamage = context.BaseDamage * Mathf.Max(0f, definition.explosionDamageMultiplier);

                for (var i = 0; i < context.NearbyEnemies.Count; i++)
                {
                    var enemy = context.NearbyEnemies[i];
                    if (enemy == null || enemy == context.Enemy)
                    {
                        continue;
                    }

                    var limit = definition.explosionRadius + enemy.CollisionRadius;
                    if ((enemy.transform.position - context.Position).sqrMagnitude > limit * limit)
                    {
                        continue;
                    }

                    context.ApplyDamage(explosionDamage, enemy);
                    var toEnemy = enemy.transform.position - context.Position;
                    ApplyExplosionKnockback(context, enemy, toEnemy);
                }
            }
        }

        private sealed class LightningProjectileHitBehavior : IProjectileHitBehavior
        {
            public ProjectileHitResult OnHit(ProjectileHitContext context)
            {
                var appliedDamage = context.ApplyDamage(context.CurrentDamage, context.Enemy);
                context.NotifyDirectHit(appliedDamage, context.Enemy);
                SpawnConfiguredImpactFx(context);
                return new ProjectileHitResult(context.Definition != null && context.Definition.releaseProjectileOnHit);
            }
        }

        private sealed class IceProjectileHitBehavior : IProjectileHitBehavior
        {
            public ProjectileHitResult OnHit(ProjectileHitContext context)
            {
                var appliedDamage = context.ApplyDamage(context.CurrentDamage, context.Enemy);
                if (context.Enemy != null && context.Definition != null)
                {
                    context.Enemy.ApplySlow(context.Definition.slowMultiplier, context.Definition.slowDuration);
                }

                if (!context.IsFragment)
                {
                    context.NotifyDirectHit(appliedDamage, context.Enemy);
                }

                SpawnConfiguredImpactFx(context);
                return new ProjectileHitResult(context.Definition != null && context.Definition.releaseProjectileOnHit);
            }
        }

        private sealed class WindProjectileHitBehavior : IProjectileHitBehavior
        {
            public ProjectileHitResult OnHit(ProjectileHitContext context)
            {
                var appliedDamage = context.ApplyDamage(context.CurrentDamage, context.Enemy);
                if (context.Enemy != null && context.Definition != null)
                {
                    context.Enemy.ApplyKnockback(context.Direction, context.Definition.knockbackStrength);
                }

                context.NotifyDirectHit(appliedDamage, context.Enemy);
                SpawnConfiguredImpactFx(context);
                return new ProjectileHitResult(context.Definition != null && context.Definition.releaseProjectileOnHit);
            }
        }
    }
}
