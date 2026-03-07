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
            Lightning = 6,
            Satellite = 7,
            RifleTurret = 8,
            Aura = 9,
        }

        [Header("General")]
        public WeaponType weaponType = WeaponType.Rifle;
        [Min(0.05f)] public float attackInterval = 0.8f;
        [Min(0.5f)] public float attackRange = 7.5f;
        [Min(1f)] public float projectileDamage = 12f;

        [Header("Rifle")]
        [Min(0.05f)] public float rifleAttackInterval = 0.7f;
        [Min(0.1f)] public float rifleBaseDamage = 12f;

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
        [Min(2)] public int shotgunPelletCount = 7;
        [Range(1f, 120f)] public float shotgunSpreadAngle = 36f;
        [Range(0.05f, 2f)] public float shotgunPelletDamageMultiplier = 0.3f;

        [Header("Katana (Melee Cone)")]
        [Min(0.25f)] public float katanaRange = 2.1f;
        [Range(5f, 180f)] public float katanaConeAngle = 80f;
        [Min(0.1f)] public float katanaBaseDamage = 5f;
        [Range(0.05f, 3f)] public float katanaDamageMultiplier = 1f;

        [Header("Chain Attack")]
        [Min(1)] public int chainBaseJumps = 3;
        [Min(0.1f)] public float chainJumpRange = 2.2f;
        [Range(0f, 0.9f)] public float chainDamageDecayPerJump = 0.15f;

        [Header("Lightning")]
        [Range(0.1f, 5f)] public float lightningDamageMultiplier = 1.25f;
        [Range(0.1f, 5f)] public float lightningIntervalMultiplier = 1.0f;

        [Header("Satellite")]
        [Min(0.2f)] public float satelliteOrbitRadius = 1.25f;
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
        [Min(0.1f)] public float auraRadius = 2.2f;
        [Range(0.01f, 5f)] public float auraDamageMultiplier = 0.22f;

    }
}
