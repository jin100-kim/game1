using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [CreateAssetMenu(menuName = "EJR/Config/Weapon", fileName = "WeaponConfig")]
    public sealed class WeaponConfig : ScriptableObject
    {
        public enum WeaponType
        {
            Fireball = 0,
            Slash = 1,
            LightningBolt = 2,
            IceSpike = 3,
            WindBlade = 4,
        }

        [Header("General")]
        public WeaponType weaponType = WeaponType.Fireball;
        [Min(0.05f)] public float attackInterval = 0.8f;
        [Min(0.5f)] public float attackRange = 5f;
        [Min(0.1f)] public float fireballRange = 5f;
        [Min(1f)] public float projectileDamage = 12f;

        [Header("Projectile Base")]
        public GameObject projectilePrefab;
        public GameObject impactVfxPrefab;
        [Min(0.5f)] public float projectileSpeed = 10f;
        [Min(0.1f)] public float projectileLifetime = 2f;
        [Min(0.05f)] public float projectileHitRadius = 0.25f;
        [Min(0.05f)] public float projectileVisualScale = 0.25f;

        [Header("Fireball")]
        public GameObject fireballProjectilePrefab;
        public GameObject fireballUpProjectilePrefab;
        public GameObject fireballDownProjectilePrefab;
        public GameObject fireballImpactVfxPrefab;
        [Min(1)] public int fireballBurstCount = 4;
        [Min(0.01f)] public float fireballBurstShotInterval = 0.06f;
        [Range(0.05f, 2f)] public float fireballBurstDamageMultiplier = 0.5f;
        [Range(0f, 25f)] public float fireballBurstSpreadAngle = 6f;
        [Min(0.1f)] public float fireballBaseDamage = 12f;
        [Min(0.1f)] public float fireballAttackInterval = 0.95f;
        [Min(0.1f)] public float fireballProjectileSpeed = 4f;
        [Min(0.1f)] public float fireballProjectileLifetime = 1.5f;
        [Min(0.05f)] public float fireballProjectileHitRadius = 0.28f;
        [Range(0f, 120f)] public float fireballSpreadAngle = 20f;
        [Min(0.1f)] public float fireballExplosionRadius = 1.05f;
        [Range(0.05f, 3f)] public float fireballExplosionDamageMultiplier = 0.4f;
        [Min(0.1f)] public float fireballBurnDuration = 2.5f;
        [Min(0.05f)] public float fireballBurnTickInterval = 0.5f;
        [Range(0.01f, 3f)] public float fireballBurnDamageMultiplier = 0.32f;
        [Range(0f, 1f)] public float fireballBurstChance = 0.35f;

        [Header("Slash")]
        public GameObject slashVfxPrefab;
        [Min(0.1f)] public float slashBaseDamage = 15f;
        [Min(0.1f)] public float slashAttackInterval = 0.5f;
        [Min(0.1f)] public float slashRange = 2.0f;
        [Min(0.1f)] public float slashConeAngle = 90f;
        [Min(1)] public int slashBaseCount = 1;
        [Min(0.01f)] public float slashComboInterval = 0.15f;

        [Header("Lightning Bolt")]
        public float lightningBoltBaseDamage = 10f;
        public float lightningBoltAttackInterval = 0.8f;
        public float lightningBoltProjectileSpeed = 12f;
        public float lightningBoltProjectileLifetime = 1.5f;
        public float lightningBoltProjectileHitRadius = 0.25f;

        [Header("Ice Spike")]
        public float iceSpikeBaseDamage = 15f;
        public float iceSpikeAttackInterval = 1.2f;
        public float iceSpikeProjectileSpeed = 8f;
        public float iceSpikeProjectileLifetime = 2.0f;
        public float iceSpikeProjectileHitRadius = 0.3f;

        [Header("Wind Blade")]
        public float windBladeBaseDamage = 8f;
        public float windBladeAttackInterval = 0.4f;
        public float windBladeProjectileSpeed = 15f;
        public float windBladeProjectileLifetime = 1.0f;
        public float windBladeProjectileHitRadius = 0.22f;

        [Header("Chain Lightning (To be removed)")]
        public List<GameObject> chainLightningBeamPrefabs;
    }
}
