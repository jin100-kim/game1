using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public enum ProjectileMotionKind
    {
        Linear = 0,
        Homing = 1,
        Boomerang = 2,
    }

    public enum WeaponImpactBehaviorKind
    {
        Default = 0,
        FireballExplosion = 1,
        LightningImpact = 2,
        IceSlow = 3,
        WindKnockback = 4,
    }

    [Serializable]
    public sealed class WeaponDefinition
    {
        public WeaponUpgradeId id;
        public bool canAcquire = true;

        [Min(0.01f)] public float baseDamage = 1f;
        [Min(0.01f)] public float attackInterval = 1f;
        [Min(0.01f)] public float range = 5f;
        [Min(0.01f)] public float projectileSpeed = 8f;
        [Min(0.01f)] public float projectileLifetime = 1f;
        [Min(0.01f)] public float projectileHitRadius = 0.25f;
        [Min(0.01f)] public float projectileVisualScale = 0.25f;
        [Min(0.01f)] public float projectileVfxScaleMultiplier = 1f;
        public Color sourceColor = Color.white;

        public GameObject projectilePrefab;
        public GameObject projectileUpPrefab;
        public GameObject projectileDownPrefab;
        public GameObject impactVfxPrefab;
        public GameObject slashVfxPrefab;

        public string projectileVfxResourcePath;
        public string impactVfxResourcePath;
        public string directVfxResourcePath;

        public ProjectileMotionKind projectileMotion = ProjectileMotionKind.Linear;
        public WeaponImpactBehaviorKind impactBehavior = WeaponImpactBehaviorKind.Default;
        public bool releaseProjectileOnHit;

        [Header("Pattern")]
        [Min(1)] public int slashBaseCount = 1;
        [Min(0.01f)] public float slashComboInterval = 0.15f;
        [Min(0.01f)] public float slashConeAngle = 90f;

        [Header("Impact")]
        [Min(0.01f)] public float explosionRadius = 0.8f;
        [Range(0f, 3f)] public float explosionDamageMultiplier = 0.4f;
        [Min(0.01f)] public float explosionFxScaleMultiplier = 3f;
        [Min(0.01f)] public float explosionFxDuration = 0.4f;
        [Range(0f, 1f)] public float slowMultiplier = 0.5f;
        [Min(0f)] public float slowDuration = 1.5f;
        [Min(0f)] public float knockbackStrength = 5f;

        [Header("Motion")]
        [Min(0.01f)] public float homingSearchRange = 10f;
        [Min(0f)] public float homingTurnSpeed = 180f;
        [Min(0.01f)] public float homingRetargetInterval = 0.2f;
        [Min(0.01f)] public float boomerangReturnDistance = 0.4f;
        [Min(0.01f)] public float boomerangReturnLerp = 25f;

        [Header("Level Milestone")]
        public WeaponMilestoneKind milestoneKind = WeaponMilestoneKind.ExtraProjectile;
        public string milestoneDescription = "Special upgrade";
        public float milestoneValue = 1f;
    }

    [CreateAssetMenu(menuName = "EJR/Config/Weapon Catalog", fileName = "WeaponCatalog")]
    public sealed class WeaponCatalog : ScriptableObject
    {
        private static readonly WeaponUpgradeId[] s_defaultWeaponIds =
        {
            WeaponUpgradeId.Fireball,
            WeaponUpgradeId.Slash,
            WeaponUpgradeId.LightningBolt,
            WeaponUpgradeId.IceSpike,
            WeaponUpgradeId.WindBlade,
            WeaponUpgradeId.Bubble,
        };

        [SerializeField, Min(0.5f)] private float defaultAttackRange = 5f;
        [SerializeField] private List<WeaponDefinition> weapons = new();

        public float DefaultAttackRange => Mathf.Max(0.5f, defaultAttackRange);
        public IReadOnlyList<WeaponDefinition> Weapons => weapons;
        public static IReadOnlyList<WeaponUpgradeId> DefaultWeaponIds => s_defaultWeaponIds;

        public static WeaponCatalog CreateRuntimeDefault(WeaponConfig legacyConfig = null)
        {
            var catalog = CreateInstance<WeaponCatalog>();
            catalog.name = "RuntimeWeaponCatalog";
            catalog.defaultAttackRange = legacyConfig != null ? Mathf.Max(0.5f, legacyConfig.attackRange) : 5f;
            catalog.weapons = CreateDefaultDefinitions(legacyConfig);
            return catalog;
        }

        public WeaponDefinition GetDefinition(WeaponUpgradeId id)
        {
            for (var i = 0; i < weapons.Count; i++)
            {
                var definition = weapons[i];
                if (definition != null && definition.id == id)
                {
                    return definition;
                }
            }

            return CreateDefaultDefinition(id, null);
        }

        public IEnumerable<WeaponUpgradeId> GetAcquireWeaponIds()
        {
            for (var i = 0; i < weapons.Count; i++)
            {
                var definition = weapons[i];
                if (definition != null && definition.canAcquire)
                {
                    yield return definition.id;
                }
            }
        }

        public WeaponMilestoneKind GetMilestoneKind(WeaponUpgradeId id, int nextLevel)
        {
            return GetDefinition(id).milestoneKind;
        }

        public float GetMilestoneValue(WeaponUpgradeId id, int nextLevel)
        {
            return GetDefinition(id).milestoneValue;
        }

        public string GetMilestoneDescription(WeaponUpgradeId id, int nextLevel)
        {
            return GetDefinition(id).milestoneDescription;
        }

        public static int GetExtraCountBonus(WeaponUpgradeId id, int milestoneCount)
        {
            var milestones = Mathf.Max(0, milestoneCount);
            return id switch
            {
                WeaponUpgradeId.WindBlade => milestones,
                WeaponUpgradeId.Fireball => milestones,
                WeaponUpgradeId.Slash => milestones,
                WeaponUpgradeId.LightningBolt => milestones,
                WeaponUpgradeId.IceSpike => milestones,
                WeaponUpgradeId.Bubble => milestones,
                _ => 0,
            };
        }

        private static List<WeaponDefinition> CreateDefaultDefinitions(WeaponConfig legacyConfig)
        {
            var definitions = new List<WeaponDefinition>(s_defaultWeaponIds.Length);
            for (var i = 0; i < s_defaultWeaponIds.Length; i++)
            {
                definitions.Add(CreateDefaultDefinition(s_defaultWeaponIds[i], legacyConfig));
            }

            return definitions;
        }

        private static WeaponDefinition CreateDefaultDefinition(WeaponUpgradeId id, WeaponConfig legacy)
        {
            var commonProjectilePrefab = legacy != null ? legacy.projectilePrefab : null;
            var commonImpactPrefab = legacy != null ? legacy.impactVfxPrefab : null;
            var commonVisualScale = legacy != null ? Mathf.Max(0.01f, legacy.projectileVisualScale) : 0.25f;

            return id switch
            {
                WeaponUpgradeId.Fireball => new WeaponDefinition
                {
                    id = id,
                    baseDamage = legacy != null ? legacy.fireballBaseDamage : 14.5f,
                    attackInterval = legacy != null ? legacy.fireballAttackInterval : 0.95f,
                    range = legacy != null ? legacy.fireballRange : 5f,
                    projectileSpeed = legacy != null ? legacy.fireballProjectileSpeed : 6f,
                    projectileLifetime = legacy != null ? legacy.fireballProjectileLifetime : 1.333f,
                    projectileHitRadius = legacy != null ? legacy.fireballProjectileHitRadius : 0.28f,
                    projectileVisualScale = 1f,
                    projectileVfxScaleMultiplier = 1f,
                    projectilePrefab = legacy != null ? legacy.fireballProjectilePrefab : null,
                    projectileUpPrefab = legacy != null ? legacy.fireballUpProjectilePrefab : null,
                    projectileDownPrefab = legacy != null ? legacy.fireballDownProjectilePrefab : null,
                    impactVfxPrefab = legacy != null ? legacy.fireballImpactVfxPrefab : null,
                    projectileVfxResourcePath = "VFX/Fireball/VFX_2D_Fireball_Projectile_01_Color_Loop_Static",
                    impactVfxResourcePath = "VFX/Fireball/VFX_2D_Projectile_Fire_Impact_01_Color_Static",
                    sourceColor = new Color(0.9f, 0.95f, 0.35f, 0.95f),
                    impactBehavior = WeaponImpactBehaviorKind.FireballExplosion,
                    releaseProjectileOnHit = true,
                    explosionRadius = 0.8f,
                    explosionDamageMultiplier = 0.4f,
                    explosionFxScaleMultiplier = 3f,
                    explosionFxDuration = 0.4f,
                    knockbackStrength = 3.5f,
                    milestoneKind = WeaponMilestoneKind.ExtraProjectile,
                    milestoneDescription = "Fireball +1",
                },
                WeaponUpgradeId.Slash => new WeaponDefinition
                {
                    id = id,
                    baseDamage = legacy != null ? legacy.slashBaseDamage : 15f,
                    attackInterval = legacy != null ? legacy.slashAttackInterval : 0.5f,
                    range = legacy != null ? legacy.slashRange : 2f,
                    sourceColor = Color.white,
                    slashVfxPrefab = legacy != null ? legacy.slashVfxPrefab : null,
                    directVfxResourcePath = "VFX/Slash/VFX_2D_Sword_Slash_01_Mask_Static",
                    slashBaseCount = legacy != null ? legacy.slashBaseCount : 1,
                    slashComboInterval = legacy != null ? legacy.slashComboInterval : 0.15f,
                    slashConeAngle = legacy != null ? legacy.slashConeAngle : 90f,
                    knockbackStrength = 4f,
                    milestoneKind = WeaponMilestoneKind.ExtraSlashes,
                    milestoneDescription = "Slash count +1",
                },
                WeaponUpgradeId.LightningBolt => new WeaponDefinition
                {
                    id = id,
                    baseDamage = legacy != null ? legacy.lightningBoltBaseDamage : 12f,
                    attackInterval = legacy != null ? legacy.lightningBoltAttackInterval : 0.8f,
                    range = (legacy != null ? legacy.lightningBoltProjectileSpeed : 12f) * (legacy != null ? legacy.lightningBoltProjectileLifetime : 0.5f),
                    projectileSpeed = legacy != null ? legacy.lightningBoltProjectileSpeed : 12f,
                    projectileLifetime = legacy != null ? legacy.lightningBoltProjectileLifetime : 0.5f,
                    projectileHitRadius = legacy != null ? legacy.lightningBoltProjectileHitRadius : 0.25f,
                    projectileVfxScaleMultiplier = 1f,
                    sourceColor = new Color(0.4f, 0.7f, 1f, 1f),
                    directVfxResourcePath = "VFX/LightningBolt/VFX_2D_Lightning_01_Mask_Static",
                    impactVfxResourcePath = "VFX/LightningBolt/VFX_2D_Projectile_Lightning_Impact_01_Color_Static",
                    impactBehavior = WeaponImpactBehaviorKind.LightningImpact,
                    milestoneKind = WeaponMilestoneKind.ExtraTargets,
                    milestoneDescription = "Lightning target +1",
                },
                WeaponUpgradeId.IceSpike => new WeaponDefinition
                {
                    id = id,
                    baseDamage = legacy != null ? legacy.iceSpikeBaseDamage : 18f,
                    attackInterval = legacy != null ? legacy.iceSpikeAttackInterval : 1.2f,
                    range = (legacy != null ? legacy.iceSpikeProjectileSpeed : 8f) * (legacy != null ? legacy.iceSpikeProjectileLifetime : 0.75f),
                    projectileSpeed = legacy != null ? legacy.iceSpikeProjectileSpeed : 8f,
                    projectileLifetime = legacy != null ? legacy.iceSpikeProjectileLifetime : 0.75f,
                    projectileHitRadius = legacy != null ? legacy.iceSpikeProjectileHitRadius : 0.3f,
                    projectileVisualScale = commonVisualScale,
                    projectileVfxScaleMultiplier = 2f,
                    projectilePrefab = commonProjectilePrefab,
                    impactVfxPrefab = commonImpactPrefab,
                    projectileVfxResourcePath = "VFX/IceSpike/VFX_2D_Projectile_Ice_01_Color_Loop_Static",
                    impactVfxResourcePath = "VFX/IceSpike/VFX_2D_Projectile_Ice_Impact_01_Color_Static",
                    sourceColor = new Color(0.7f, 0.9f, 1f, 1f),
                    impactBehavior = WeaponImpactBehaviorKind.IceSlow,
                    slowMultiplier = 0.5f,
                    slowDuration = 1.5f,
                    milestoneKind = WeaponMilestoneKind.ExtraPierce,
                    milestoneDescription = "Fragments +2 on hit",
                },
                WeaponUpgradeId.WindBlade => new WeaponDefinition
                {
                    id = id,
                    baseDamage = legacy != null ? legacy.windBladeBaseDamage : 8f,
                    attackInterval = legacy != null ? legacy.windBladeAttackInterval : 0.4f,
                    range = (legacy != null ? legacy.windBladeProjectileSpeed : 10f) * (legacy != null ? legacy.windBladeProjectileLifetime : 0.7f),
                    projectileSpeed = legacy != null ? legacy.windBladeProjectileSpeed : 10f,
                    projectileLifetime = legacy != null ? legacy.windBladeProjectileLifetime : 0.7f,
                    projectileHitRadius = legacy != null ? legacy.windBladeProjectileHitRadius : 0.26f,
                    projectileVisualScale = commonVisualScale,
                    projectileVfxScaleMultiplier = 2f,
                    projectilePrefab = commonProjectilePrefab,
                    impactVfxPrefab = commonImpactPrefab,
                    projectileVfxResourcePath = "VFX/WindBlade/VFX_2D_Projectile_Wind_01_Color_Loop_Static",
                    impactVfxResourcePath = "VFX/WindBlade/VFX_2D_Projectile_Wind_Impact_01_Color_Static",
                    sourceColor = new Color(0.6f, 1f, 0.8f, 1f),
                    projectileMotion = ProjectileMotionKind.Boomerang,
                    impactBehavior = WeaponImpactBehaviorKind.Default,
                    milestoneKind = WeaponMilestoneKind.ExtraProjectile,
                    milestoneDescription = "Projectile +1, Pierce +1",
                },
                WeaponUpgradeId.Bubble => new WeaponDefinition
                {
                    id = id,
                    baseDamage = legacy != null ? legacy.bubbleBaseDamage : 12f,
                    attackInterval = legacy != null ? legacy.bubbleAttackInterval : 1.3f,
                    range = (legacy != null ? legacy.bubbleProjectileSpeed : 3f) * (legacy != null ? legacy.bubbleProjectileLifetime : 2.75f),
                    projectileSpeed = legacy != null ? legacy.bubbleProjectileSpeed : 3f,
                    projectileLifetime = legacy != null ? legacy.bubbleProjectileLifetime : 2.75f,
                    projectileHitRadius = legacy != null ? legacy.bubbleProjectileHitRadius : 0.4f,
                    projectileVisualScale = commonVisualScale,
                    projectileVfxScaleMultiplier = 3f,
                    projectilePrefab = commonProjectilePrefab,
                    impactVfxPrefab = commonImpactPrefab,
                    projectileVfxResourcePath = "VFX/Bubble/VFX_2D_Bubble_01_Color_Loop_Static",
                    impactVfxResourcePath = "VFX/Bubble/VFX_2D_Projectile_Burst_Impact_01_Color_Static",
                    sourceColor = new Color(0.4f, 0.8f, 1f, 1f),
                    projectileMotion = ProjectileMotionKind.Homing,
                    milestoneKind = WeaponMilestoneKind.ExtraProjectile,
                    milestoneDescription = "Bubble +1",
                },
                _ => new WeaponDefinition
                {
                    id = id,
                    baseDamage = legacy != null ? legacy.projectileDamage : 12f,
                    attackInterval = legacy != null ? legacy.attackInterval : 0.8f,
                    range = legacy != null ? legacy.attackRange : 5f,
                    projectileSpeed = legacy != null ? legacy.projectileSpeed : 10f,
                    projectileLifetime = legacy != null ? legacy.projectileLifetime : 2f,
                    projectileHitRadius = legacy != null ? legacy.projectileHitRadius : 0.25f,
                    projectileVisualScale = commonVisualScale,
                    projectileVfxScaleMultiplier = 1f,
                    projectilePrefab = commonProjectilePrefab,
                    impactVfxPrefab = commonImpactPrefab,
                },
            };
        }
    }
}
