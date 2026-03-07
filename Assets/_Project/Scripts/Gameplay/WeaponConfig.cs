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
        }

        [Header("General")]
        public WeaponType weaponType = WeaponType.Rifle;
        [Min(0.05f)] public float attackInterval = 0.8f;
        [Min(0.5f)] public float attackRange = 7.5f;
        [Min(1f)] public float projectileDamage = 12f;

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
        [Range(0.05f, 3f)] public float katanaDamageMultiplier = 1.1f;

    }
}
