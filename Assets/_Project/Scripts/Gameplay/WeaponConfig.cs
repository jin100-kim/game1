using UnityEngine;

namespace EJR.Game.Gameplay
{
    [CreateAssetMenu(menuName = "EJR/Config/Weapon", fileName = "WeaponConfig")]
    public sealed class WeaponConfig : ScriptableObject
    {
        public enum WeaponType
        {
            Rifle = 0,
            Fireball = 1,
            Bat = 2,
            Shotgun = 3,
            Slash = 4,
            BfSword = 5,
            ChainLightning = 6,
            SwingMace = 7,
            OrbitWeapon = 8,
            Turret = 9,
            Aura = 10,
        }

        [Header("General")]
        public WeaponType weaponType = WeaponType.Rifle;
        [Min(0.05f)] public float attackInterval = 0.8f;
        [Min(0.5f)] public float attackRange = 5f;
        [Min(1f)] public float projectileDamage = 12f;

        [Header("Base Range by Weapon")]
        [Min(0.5f)] public float rifleRange = 5f;
        [Min(0.5f)] public float fireballRange = 4.75f;
        [Min(0.5f)] public float batLatchRange = 4.8f;
        [Min(0.5f)] public float shotgunRange = 3f;
        [Min(0.5f)] public float chainLightningRange = 3f;
        [Min(0.5f)] public float swingMaceRange = 4f;
        [Min(0.2f)] public float droneRange = 1.5f;
        [Min(0.2f)] public float turretRange = 4f;
        [Min(0.25f)] public float slashRange = 1.6f;
        [Min(0.25f)] public float swingMaceMeleeRange = 1.85f;

        [Header("Rifle")]
        [Min(0.05f)] public float rifleAttackInterval = 0.7f;
        [Min(0.1f)] public float rifleBaseDamage = 12f;
        [Min(1)] public int rifleBaseShotCount = 2;
        [Min(0.01f)] public float rifleBurstShotInterval = 0.08f;
        [Min(0f)] public float rifleParallelShotSpacing = 0.32f;

        [Header("Projectile Base")]
        [Min(0.5f)] public float projectileSpeed = 10f;
        [Min(0.1f)] public float projectileLifetime = 2f;
        [Min(0.05f)] public float projectileHitRadius = 0.25f;
        [Min(0.05f)] public float projectileVisualScale = 0.25f;

        [Header("Fireball")]
        [Min(1)] public int fireballBurstCount = 4;
        [Min(0.01f)] public float fireballBurstShotInterval = 0.06f;
        [Range(0.05f, 2f)] public float fireballBurstDamageMultiplier = 0.5f;
        [Range(0f, 25f)] public float fireballBurstSpreadAngle = 6f;
        [Min(0.1f)] public float fireballBaseDamage = 12f;
        [Min(0.1f)] public float fireballAttackInterval = 0.95f;
        [Min(0.1f)] public float fireballProjectileSpeed = 8f;
        [Min(0.1f)] public float fireballProjectileLifetime = 1.5f;
        [Min(0.05f)] public float fireballProjectileHitRadius = 0.28f;
        [Range(0f, 120f)] public float fireballSpreadAngle = 20f;
        [Min(0.1f)] public float fireballExplosionRadius = 1.05f;
        [Range(0.05f, 3f)] public float fireballExplosionDamageMultiplier = 0.4f;
        [Min(0.1f)] public float fireballBurnDuration = 2.5f;
        [Min(0.05f)] public float fireballBurnTickInterval = 0.5f;
        [Range(0.01f, 3f)] public float fireballBurnDamageMultiplier = 0.32f;
        [Range(0f, 1f)] public float fireballBurstChance = 0.35f;

        [Header("Bat")]
        [Min(1)] public int batMaxHits = 4;
        [Range(0f, 0.9f)] public float batDamageFalloffPerHit = 0.2f;
        [Range(0.05f, 1f)] public float batMinimumDamageMultiplier = 0.35f;
        [Min(0.1f)] public float batBaseDamage = 12f;
        [Min(0.1f)] public float batAttackInterval = 1.5f;
        [Min(0f)] public float batHealthCost = 0f;
        [Min(0f)] public float batLifetime = 0f;
        [Min(0f)] public float batOrbitDuration = 0.5f;
        [Min(0.1f)] public float batOrbitRadius = 1.15f;
        [Min(0.1f)] public float batMoveSpeed = 6.8f;
        [Min(0.01f)] public float batHitInterval = 0.4f;
        [Range(0.05f, 5f)] public float batDamageMultiplier = 1.2f;
        [Range(0.01f, 1f)] public float batHealPerDamageMultiplier = 0.06f;
        [Min(0f)] public float batMinimumHealPerHit = 0f;
        [Min(1)] public int batHitsBeforeReturn = 5;
        [Min(0.05f)] public float batVisualScale = 0.32f;

        [Header("Shotgun")]
        [Min(0.1f)] public float shotgunBaseDamage = 12f;
        [Min(2)] public int shotgunPelletCount = 4;
        [Range(1f, 120f)] public float shotgunSpreadAngle = 36f;
        [Range(0.05f, 2f)] public float shotgunPelletDamageMultiplier = 0.5f;
        [Min(0.05f)] public float shotgunAttackInterval = 0.95f;

        [Header("Slash (Melee Cone)")]
        [Range(5f, 180f)] public float slashConeAngle = 80f;
        [Min(0.1f)] public float slashBaseDamage = 12f;
        [Range(0.05f, 3f)] public float slashDamageMultiplier = 1f;
        [Min(1)] public int slashBaseCount = 2;
        [Min(0.05f)] public float slashAttackInterval = 1.05f;
        [Min(0.01f)] public float slashComboInterval = 0.2f;

        [Header("BF Sword")]
        [Min(0.01f)] public float bfSwordHitInterval = 0.38f;
        [Min(0.2f)] public float bfSwordLength = 1.75f;
        [Min(0.05f)] public float bfSwordThickness = 0.36f;
        [Min(0f)] public float bfSwordForwardOffset = 0.96f;
        [Min(0.1f)] public float bfSwordBaseDamage = 12f;
        [Min(0.02f)] public float bfSwordStunDuration = 0.18f;
        [Min(0.05f)] public float bfSwordVisualScale = 1.9f;
        [Min(0.05f)] public float bfSwordVisualWidthMultiplier = 0.5f;
        public Vector2 bfSwordVisualLocalOffset = new(0f, -0.16f);

        [Header("Chain Lightning")]
        [Min(0.1f)] public float chainLightningBaseDamage = 12f;
        [Min(1)] public int chainLightningBaseJumps = 3;
        [Min(0.1f)] public float chainLightningJumpRange = 3f;
        [Min(0.01f)] public float chainLightningHopDelay = 0.12f;
        [Range(0f, 0.9f)] public float chainLightningDamageDecayPerJump = 0.15f;
        [Min(0.05f)] public float chainLightningAttackInterval = 1.25f;

        [Header("Swing Mace")]
        [Min(0.1f)] public float nearbyLightningBaseDamage = 12f;
        [Range(0.1f, 5f)] public float nearbyLightningDamageMultiplier = 1.25f;
        [Range(0.1f, 5f)] public float nearbyLightningIntervalMultiplier = 1.0f;
        [Min(0.1f)] public float swingMaceBaseDamage = 12f;
        [Min(0.1f)] public float swingMaceAttackInterval = 1.05f;
        [Min(0.05f)] public float swingMaceSwingDuration = 0.28f;
        [Range(10f, 220f)] public float swingMaceArcAngle = 130f;
        [Min(0.05f)] public float swingMaceHitRadius = 0.5f;
        [Range(0.05f, 5f)] public float swingMaceDamageMultiplier = 1.7f;
        [Min(0.05f)] public float swingMaceStunDuration = 0.65f;
        [Min(0.05f)] public float swingMaceVisualLength = 1.25f;
        [Min(0.05f)] public float swingMaceVisualHandleWidth = 0.12f;
        [Min(0.05f)] public float swingMaceVisualHeadSize = 0.38f;

        [Header("Drone")]
        [Min(0.1f)] public float droneBaseDamage = 12f;
        [Min(1)] public int droneBaseCount = 2;
        [Min(0.2f)] public float droneOrbitRadius = 1.2f;
        [Min(30f)] public float droneAngularSpeed = 220f;
        [Min(0.05f)] public float droneHitRadius = 0.32f;
        [Min(0.01f)] public float droneHitCooldownPerEnemy = 0.25f;
        [Range(0.05f, 5f)] public float droneDamageMultiplier = 0.55f;

        [Header("Turret")]
        [Min(0.1f)] public float turretBaseDamage = 12f;
        [Min(0.1f)] public float turretDeployInterval = 3.8f;
        [Min(0.1f)] public float turretLifetime = 8f;
        [Min(1)] public int turretMaxCount = 2;
        [Range(0.1f, 3f)] public float turretRangeMultiplier = 0.85f;
        [Range(0.05f, 5f)] public float turretDamageMultiplier = 0.65f;
        [Min(0.1f)] public float turretProjectileSpeed = 11f;
        [Min(0.1f)] public float turretProjectileLifetime = 1.8f;
        [Min(0.01f)] public float turretShotInterval = 0.5f;
        [Min(0.01f)] public float turretProjectileHitRadius = 0.22f;
        [Min(0.05f)] public float turretVisualScale = 3f;
        [Min(1f)] public float turretVisualAnimationFps = 12f;

        [Header("Aura")]
        [Min(0.1f)] public float auraBaseDamage = 12f;
        [Min(0.01f)] public float auraTickInterval = 1f;
        [Min(0.1f)] public float auraRadius = 1.5f;
        [Range(0.01f, 5f)] public float auraDamageMultiplier = 0.88f;
    }
}
