using UnityEngine;

namespace EJR.Game.Gameplay
{
    [CreateAssetMenu(menuName = "EJR/Config/Weapon", fileName = "WeaponConfig")]
    public sealed class WeaponConfig : ScriptableObject
    {
        public enum WeaponType
        {
            Rifle = 0,
            Smg = 1,
            SniperRifle = 2,
            Shotgun = 3,
            Katana = 4,
            ChainAttack = 5,
            SatelliteBeam = 6,
            Drone = 7,
            RifleTurret = 8,
            Aura = 9,
        }

        [Header("General")]
        public WeaponType weaponType = WeaponType.Rifle;
        [Min(0.05f)] public float attackInterval = 0.8f;
        [Min(0.5f)] public float attackRange = 5f;
        [Min(1f)] public float projectileDamage = 12f;

        [Header("Base Range by Weapon")]
        [Min(0.5f)] public float rifleRange = 5f;
        [Min(0.5f)] public float smgRange = 3.5f;
        [Min(0.5f)] public float sniperRange = 7f;
        [Min(0.5f)] public float shotgunRange = 3f;
        [Min(0.5f)] public float chainAttackRange = 3f;
        [Min(0.5f)] public float satelliteBeamRange = 4f;
        [Min(0.2f)] public float droneRange = 1.5f;
        [Min(0.2f)] public float rifleTurretRange = 4f;

        [Header("Rifle")]
        [Min(0.05f)] public float rifleAttackInterval = 0.7f;
        [Min(0.1f)] public float rifleBaseDamage = 12f;
        [Min(0f)] public float rifleParallelShotSpacing = 0.32f;

        [Header("Projectile Base")]
        [Min(0.5f)] public float projectileSpeed = 10f;
        [Min(0.1f)] public float projectileLifetime = 2f;
        [Min(0.05f)] public float projectileHitRadius = 0.25f;
        [Min(0.05f)] public float projectileVisualScale = 0.25f;

        [Header("SMG")]
        [Min(1)] public int smgBurstCount = 4;
        [Min(0.01f)] public float smgBurstShotInterval = 0.06f;
        [Range(0.05f, 2f)] public float smgShotDamageMultiplier = 0.5f;
        [Range(0f, 25f)] public float smgSpreadAngle = 6f;

        [Header("Sniper")]
        [Min(1)] public int sniperMaxHits = 4;
        [Range(0f, 0.9f)] public float sniperDamageFalloffPerHit = 0.2f;
        [Range(0.05f, 1f)] public float sniperMinimumDamageMultiplier = 0.35f;

        [Header("Shotgun")]
        [Min(2)] public int shotgunPelletCount = 4;
        [Range(1f, 120f)] public float shotgunSpreadAngle = 36f;
        [Range(0.05f, 2f)] public float shotgunPelletDamageMultiplier = 0.5f;

        [Header("Katana (Melee Cone)")]
        [Min(0.25f)] public float katanaRange = 1.6f;
        [Range(5f, 180f)] public float katanaConeAngle = 80f;
        [Min(0.1f)] public float katanaBaseDamage = 5f;
        [Range(0.05f, 3f)] public float katanaDamageMultiplier = 1f;
        [Min(0.01f)] public float katanaComboSlashInterval = 0.1f;

        [Header("Chain Attack")]
        [Min(1)] public int chainBaseJumps = 3;
        [Min(0.1f)] public float chainJumpRange = 3f;
        [Range(0f, 0.9f)] public float chainDamageDecayPerJump = 0.15f;

        [Header("Satellite Beam")]
        [Range(0.1f, 5f)] public float lightningDamageMultiplier = 1.25f;
        [Range(0.1f, 5f)] public float lightningIntervalMultiplier = 1.0f;

        [Header("Drone")]
        [Min(1)] public int satelliteBaseCount = 2;
        [Min(0.2f)] public float satelliteOrbitRadius = 1.2f;
        [Min(30f)] public float satelliteAngularSpeed = 220f;
        [Min(0.05f)] public float satelliteHitRadius = 0.32f;
        [Min(0.01f)] public float satelliteHitCooldownPerEnemy = 0.25f;
        [Range(0.05f, 5f)] public float satelliteDamageMultiplier = 0.55f;

        [Header("Rifle Turret")]
        [Min(0.1f)] public float rifleTurretDeployInterval = 3.8f;
        [Min(0.1f)] public float rifleTurretLifetime = 8f;
        [Min(1)] public int rifleTurretMaxCount = 2;
        [Range(0.1f, 3f)] public float rifleTurretRangeMultiplier = 0.85f;
        [Range(0.05f, 5f)] public float rifleTurretDamageMultiplier = 0.65f;
        [Min(0.5f)] public float rifleTurretProjectileSpeed = 11f;
        [Min(0.1f)] public float rifleTurretProjectileLifetime = 1.8f;

        [Header("Aura")]
        [Min(0.01f)] public float auraTickInterval = 0.25f;
        [Min(0.1f)] public float auraRadius = 1.5f;
        [Range(0.01f, 5f)] public float auraDamageMultiplier = 0.22f;

    }
}
