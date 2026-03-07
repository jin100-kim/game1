using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class AutoWeaponSystem : MonoBehaviour
    {
        [SerializeField, Min(0)] private int projectilePoolPrewarmCount = 40;
        [SerializeField, Min(0.01f)] private float targetScanInterval = 0.08f;
        [SerializeField, Min(0.02f)] private float katanaRangeEffectDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float katanaRangeEffectWidth = 0.05f;
        [SerializeField, Range(4, 40)] private int katanaRangeEffectSegments = 14;
        [SerializeField] private Color katanaRangeEffectColor = new(0.2f, 1f, 0.9f, 0.9f);
        [SerializeField, Min(0.01f)] private float chainFxDuration = 0.08f;
        [SerializeField, Min(0.005f)] private float chainFxWidth = 0.05f;
        [SerializeField] private Color chainFxColor = new(0.45f, 0.85f, 1f, 0.95f);
        [SerializeField, Min(0.01f)] private float lightningFxDuration = 0.1f;
        [SerializeField, Min(0.005f)] private float lightningFxWidth = 0.07f;
        [SerializeField] private Color lightningFxColor = new(1f, 0.96f, 0.55f, 0.95f);
        [SerializeField, Min(0.01f)] private float auraFxDuration = 0.08f;
        [SerializeField, Min(0.005f)] private float auraFxWidth = 0.045f;
        [SerializeField] private Color auraFxColor = new(0.45f, 1f, 0.75f, 0.75f);
        [SerializeField, Min(0.01f)] private float turretTracerFxDuration = 0.06f;
        [SerializeField, Min(0.005f)] private float turretTracerFxWidth = 0.03f;
        [SerializeField] private Color turretTracerFxColor = new(1f, 0.86f, 0.28f, 0.95f);
        [SerializeField] private Color turretRangeFxColor = new(0.55f, 0.9f, 1f, 0.28f);
        [SerializeField, Range(8, 96)] private int ringFxSegments = 28;

        private sealed class WeaponRuntime
        {
            public WeaponRuntime(WeaponUpgradeId id, int level)
            {
                WeaponId = id;
                Level = Mathf.Max(1, level);
                Cooldown = 0f;
                BurstShotsRemaining = 0;
                BurstShotCooldown = 0f;
                OrbitAngleDegrees = UnityEngine.Random.Range(0f, 360f);
            }

            public WeaponUpgradeId WeaponId { get; }
            public int Level { get; set; }
            public float Cooldown { get; set; }
            public int BurstShotsRemaining { get; set; }
            public float BurstShotCooldown { get; set; }
            public float OrbitAngleDegrees { get; set; }
            public Transform SatelliteVisual { get; set; }
            public Dictionary<EnemyController, float> SatelliteHitCooldownUntil { get; } = new();
        }

        private sealed class RifleTurretRuntime
        {
            public Transform Root;
            public Vector2 Position;
            public float ExpiresAt;
            public float ShotCooldown;
        }

        private WeaponConfig _config;
        private Transform _owner;
        private EnemyRegistry _registry;
        private PlayerStatsRuntime _stats;
        private Func<Vector2, Vector3> _projectileSpawnResolver;
        private bool _useProjectileBoundsCulling;
        private Rect _projectileCullBounds;

        private EnemyController _currentTarget;
        private Vector2 _lastAimDirection = Vector2.right;
        private float _targetScanCooldown;

        private readonly List<WeaponRuntime> _loadout = new(4);
        private readonly List<EnemyController> _nearbyEnemies = new(32);
        private readonly List<EnemyController> _candidateEnemies = new(64);
        private readonly List<EnemyController> _chainHitEnemies = new(16);
        private readonly List<EnemyController> _cleanupEnemies = new(16);
        private readonly List<Vector3> _fxPoints = new(32);
        private readonly List<RifleTurretRuntime> _rifleTurrets = new(4);
        private readonly Queue<Projectile> _projectilePool = new();
        private readonly Dictionary<WeaponUpgradeId, WeaponCoreElement> _coreElementByWeapon = new();
        private readonly Dictionary<WeaponUpgradeId, int> _coreLevelByWeapon = new();
        private Transform _projectilePoolRoot;
        private static Material _sharedFxMaterial;

        public event Action<Vector2> AimUpdated;
        public event Action<Vector2> Fired;

        public void Initialize(
            WeaponConfig config,
            Transform owner,
            EnemyRegistry registry,
            PlayerStatsRuntime stats,
            Func<Vector2, Vector3> projectileSpawnResolver = null,
            Rect? projectileCullBounds = null)
        {
            _config = config;
            _owner = owner;
            _registry = registry;
            _stats = stats;
            _projectileSpawnResolver = projectileSpawnResolver;
            _useProjectileBoundsCulling = projectileCullBounds.HasValue;
            _projectileCullBounds = projectileCullBounds.GetValueOrDefault();
            _currentTarget = null;
            _lastAimDirection = Vector2.right;
            _targetScanCooldown = 0f;
            EnsureProjectilePool();
        }

        private void OnDisable()
        {
            CleanupLoadoutRuntimeState();
            ClearRifleTurrets();
        }

        public void ConfigureLoadout(PlayerBuildRuntime build, PlayerStatsRuntime stats)
        {
            _stats = stats ?? _stats;
            CleanupLoadoutRuntimeState();
            ClearRifleTurrets();
            _loadout.Clear();
            _coreElementByWeapon.Clear();
            _coreLevelByWeapon.Clear();

            if (build == null || build.OwnedWeapons.Count <= 0)
            {
                _loadout.Add(new WeaponRuntime(WeaponUpgradeId.Rifle, 1));
                return;
            }

            for (var i = 0; i < build.OwnedWeapons.Count; i++)
            {
                var id = build.OwnedWeapons[i];
                var level = Mathf.Max(1, build.GetWeaponLevel(id));
                _loadout.Add(new WeaponRuntime(id, level));

                var coreLevel = build.GetWeaponCoreLevel(id);
                if (coreLevel > 0)
                {
                    _coreElementByWeapon[id] = build.GetWeaponCoreElement(id);
                    _coreLevelByWeapon[id] = coreLevel;
                }
            }
        }

        private void Update()
        {
            if (_config == null || _owner == null || _registry == null || _stats == null)
            {
                return;
            }

            if (_loadout.Count <= 0)
            {
                _loadout.Add(new WeaponRuntime(WeaponUpgradeId.Rifle, 1));
            }

            RefreshAimDirection();

            for (var i = 0; i < _loadout.Count; i++)
            {
                var weapon = _loadout[i];
                UpdateWeapon(weapon);
            }
        }

        private void RefreshAimDirection()
        {
            var maxRange = GetMaximumLoadoutRange();
            _targetScanCooldown -= Time.deltaTime;
            if (_targetScanCooldown <= 0f || !IsTargetUsable(_currentTarget, maxRange))
            {
                _targetScanCooldown = Mathf.Max(0.01f, targetScanInterval);
                _currentTarget = FindNearestUsable(maxRange);
            }

            if (!IsTargetUsable(_currentTarget, maxRange))
            {
                return;
            }

            var toTarget = (Vector2)(_currentTarget.transform.position - _owner.position);
            if (toTarget.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            SetAimDirection(toTarget.normalized);
        }

        private void SetAimDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            var normalized = direction.normalized;
            if (Vector2.Dot(_lastAimDirection, normalized) >= 0.9998f)
            {
                return;
            }

            _lastAimDirection = normalized;
            AimUpdated?.Invoke(normalized);
        }

        private void UpdateWeapon(WeaponRuntime weapon)
        {
            if (weapon == null)
            {
                return;
            }

            weapon.Cooldown -= Time.deltaTime;

            switch (weapon.WeaponId)
            {
                case WeaponUpgradeId.Satellite:
                    UpdateSatellite(weapon);
                    return;
                case WeaponUpgradeId.RifleTurret:
                    UpdateRifleTurret(weapon);
                    return;
                case WeaponUpgradeId.Aura:
                    UpdateAura(weapon);
                    return;
            }

            if (weapon.WeaponId == WeaponUpgradeId.Smg && weapon.BurstShotsRemaining > 0)
            {
                weapon.BurstShotCooldown -= Time.deltaTime;
                if (weapon.BurstShotCooldown <= 0f)
                {
                    if (TryResolveFireDirection(weapon, out var burstDirection))
                    {
                        FireSmgBullet(weapon, burstDirection);
                    }

                    weapon.BurstShotsRemaining--;
                    weapon.BurstShotCooldown = Mathf.Max(0.01f, ApplyCoreAttackIntervalToValue(Mathf.Max(0.01f, _config.smgBurstShotInterval), weapon));
                    if (weapon.BurstShotsRemaining <= 0)
                    {
                        weapon.Cooldown = GetAttackInterval(weapon);
                    }
                }

                return;
            }

            if (weapon.Cooldown > 0f)
            {
                return;
            }

            if (!TryResolveFireDirection(weapon, out var fireDirection))
            {
                return;
            }

            switch (weapon.WeaponId)
            {
                case WeaponUpgradeId.Smg:
                    StartSmgBurst(weapon, fireDirection);
                    break;
                case WeaponUpgradeId.SniperRifle:
                    FireSniper(weapon, fireDirection);
                    weapon.Cooldown = GetAttackInterval(weapon);
                    break;
                case WeaponUpgradeId.Shotgun:
                    FireShotgun(weapon, fireDirection);
                    weapon.Cooldown = GetAttackInterval(weapon);
                    break;
                case WeaponUpgradeId.Katana:
                    FireKatana(weapon, fireDirection);
                    weapon.Cooldown = GetAttackInterval(weapon);
                    break;
                case WeaponUpgradeId.ChainAttack:
                    FireChainAttack(weapon, fireDirection);
                    weapon.Cooldown = GetAttackInterval(weapon);
                    break;
                case WeaponUpgradeId.Lightning:
                    FireLightning(weapon, fireDirection);
                    weapon.Cooldown = GetLightningInterval(weapon);
                    break;
                default:
                    FireRifle(weapon, fireDirection);
                    weapon.Cooldown = GetAttackInterval(weapon);
                    break;
            }
        }

        private bool TryResolveFireDirection(WeaponRuntime weapon, out Vector2 direction)
        {
            direction = _lastAimDirection;

            var range = GetWeaponRange(weapon);
            if (!IsTargetUsable(_currentTarget, range))
            {
                _currentTarget = FindNearestUsable(range);
            }

            if (!IsTargetUsable(_currentTarget, range))
            {
                return false;
            }

            var toTarget = (Vector2)(_currentTarget.transform.position - _owner.position);
            if (toTarget.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            direction = toTarget.normalized;
            SetAimDirection(direction);
            return true;
        }

        private void StartSmgBurst(WeaponRuntime weapon, Vector2 direction)
        {
            var count = Mathf.Max(1, _config.smgBurstCount);
            weapon.BurstShotsRemaining = count;
            weapon.BurstShotCooldown = 0f;
            FireSmgBullet(weapon, direction);
            weapon.BurstShotsRemaining--;
            weapon.BurstShotCooldown = Mathf.Max(0.01f, ApplyCoreAttackIntervalToValue(Mathf.Max(0.01f, _config.smgBurstShotInterval), weapon));
            if (weapon.BurstShotsRemaining <= 0)
            {
                weapon.Cooldown = GetAttackInterval(weapon);
            }
        }

        private void FireRifle(WeaponRuntime weapon, Vector2 direction)
        {
            var damage = GetWeaponBaseDamage(weapon);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            SpawnProjectile(
                weapon.WeaponId,
                coreElement,
                coreLevel,
                direction,
                damage,
                _config.projectileSpeed,
                _config.projectileLifetime,
                _config.projectileHitRadius,
                1,
                0f,
                1f,
                new Color(1f, 0.95f, 0.35f));
        }

        private void FireSmgBullet(WeaponRuntime weapon, Vector2 direction)
        {
            var spread = UnityEngine.Random.Range(-_config.smgSpreadAngle, _config.smgSpreadAngle);
            var spreadDirection = RotateDirection(direction, spread);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.smgShotDamageMultiplier, 0.05f, 2f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            SpawnProjectile(
                weapon.WeaponId,
                coreElement,
                coreLevel,
                spreadDirection,
                damage,
                _config.projectileSpeed * 1.1f,
                _config.projectileLifetime * 0.8f,
                _config.projectileHitRadius * 0.85f,
                1,
                0f,
                1f,
                new Color(1f, 0.82f, 0.2f));
        }

        private void FireSniper(WeaponRuntime weapon, Vector2 direction)
        {
            var damage = GetWeaponBaseDamage(weapon) * 2f;
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            SpawnProjectile(
                weapon.WeaponId,
                coreElement,
                coreLevel,
                direction,
                damage,
                _config.projectileSpeed * 1.6f,
                _config.projectileLifetime * 1.25f,
                _config.projectileHitRadius * 0.95f,
                Mathf.Max(1, _config.sniperMaxHits),
                Mathf.Clamp(_config.sniperDamageFalloffPerHit, 0f, 0.9f),
                Mathf.Clamp(_config.sniperMinimumDamageMultiplier, 0.05f, 1f),
                new Color(0.6f, 0.95f, 1f));
        }

        private void FireShotgun(WeaponRuntime weapon, Vector2 direction)
        {
            var pelletCount = Mathf.Max(2, _config.shotgunPelletCount);
            var spread = Mathf.Max(1f, _config.shotgunSpreadAngle);
            var halfSpread = spread * 0.5f;
            var damagePerPellet = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.shotgunPelletDamageMultiplier, 0.05f, 2f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);

            if (pelletCount == 1)
            {
                SpawnProjectile(
                    weapon.WeaponId,
                    coreElement,
                    coreLevel,
                    direction,
                    damagePerPellet,
                    _config.projectileSpeed * 0.95f,
                    _config.projectileLifetime * 0.75f,
                    _config.projectileHitRadius * 0.9f,
                    1,
                    0f,
                    1f,
                    new Color(1f, 0.65f, 0.2f));
                return;
            }

            for (var i = 0; i < pelletCount; i++)
            {
                var t = pelletCount <= 1 ? 0.5f : i / (float)(pelletCount - 1);
                var angle = Mathf.Lerp(-halfSpread, halfSpread, t);
                var pelletDirection = RotateDirection(direction, angle);
                SpawnProjectile(
                    weapon.WeaponId,
                    coreElement,
                    coreLevel,
                    pelletDirection,
                    damagePerPellet,
                    _config.projectileSpeed * 0.95f,
                    _config.projectileLifetime * 0.75f,
                    _config.projectileHitRadius * 0.9f,
                    1,
                    0f,
                    1f,
                    new Color(1f, 0.65f, 0.2f));
            }
        }

        private void FireKatana(WeaponRuntime weapon, Vector2 direction)
        {
            var range = GetWeaponRange(weapon);
            var coneHalfAngle = Mathf.Max(2f, _config.katanaConeAngle) * 0.5f;
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.katanaDamageMultiplier, 0.05f, 3f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);

            var origin = (Vector2)_owner.position;
            SpawnKatanaRangeEffect(origin, direction, range, coneHalfAngle);
            var searchRadius = range + _registry.GetMaxCollisionRadius();
            _registry.GetNearby(origin, searchRadius, _nearbyEnemies);

            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                var toEnemy = (Vector2)enemy.transform.position - origin;
                var centerDistance = toEnemy.magnitude;
                if (centerDistance <= 0.0001f)
                {
                    enemy.ReceiveWeaponDamage(damage, weapon.WeaponId, coreElement, coreLevel);
                    continue;
                }

                var surfaceDistance = Mathf.Max(0f, centerDistance - enemy.CollisionRadius);
                if (surfaceDistance > range)
                {
                    continue;
                }

                var angle = Vector2.Angle(direction, toEnemy / centerDistance);
                if (angle <= coneHalfAngle)
                {
                    enemy.ReceiveWeaponDamage(damage, weapon.WeaponId, coreElement, coreLevel);
                }
            }

            Fired?.Invoke(direction);
        }

        private void FireChainAttack(WeaponRuntime weapon, Vector2 direction)
        {
            var range = GetWeaponRange(weapon);
            var firstTarget = FindNearestUsable(range);
            if (firstTarget == null)
            {
                return;
            }

            _chainHitEnemies.Clear();
            _fxPoints.Clear();
            _fxPoints.Add(_owner.position);

            var currentDamage = GetWeaponBaseDamage(weapon);
            var decay = Mathf.Clamp(_config.chainDamageDecayPerJump, 0f, 0.9f);
            var jumpRange = Mathf.Max(0.1f, _config.chainJumpRange);
            var maxHits = Mathf.Max(1, _config.chainBaseJumps + Mathf.FloorToInt((weapon.Level - 1) / 3f));
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);

            var currentTarget = firstTarget;
            for (var hop = 0; hop < maxHits && currentTarget != null; hop++)
            {
                if (ContainsEnemy(_chainHitEnemies, currentTarget))
                {
                    break;
                }

                _chainHitEnemies.Add(currentTarget);
                currentTarget.ReceiveWeaponDamage(currentDamage, weapon.WeaponId, coreElement, coreLevel);
                _fxPoints.Add(currentTarget.transform.position);
                currentDamage = Mathf.Max(0.1f, currentDamage * (1f - decay));
                currentTarget = FindNearestChainTarget((Vector2)currentTarget.transform.position, jumpRange, _chainHitEnemies);
            }

            if (_fxPoints.Count >= 2)
            {
                SpawnPolylineFx(_fxPoints, chainFxColor, chainFxWidth, chainFxDuration, false, "ChainFx");
            }

            var toFirst = (Vector2)(firstTarget.transform.position - _owner.position);
            var firedDirection = toFirst.sqrMagnitude > 0.000001f ? toFirst.normalized : direction;
            Fired?.Invoke(firedDirection);
        }

        private void FireLightning(WeaponRuntime weapon, Vector2 direction)
        {
            var range = GetWeaponRange(weapon);
            var target = FindRandomUsableInRange((Vector2)_owner.position, range);
            if (target == null)
            {
                return;
            }

            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.lightningDamageMultiplier, 0.1f, 5f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            target.ReceiveWeaponDamage(damage, weapon.WeaponId, coreElement, coreLevel);
            SpawnLightningStrikeFx(target.transform.position);

            var toTarget = (Vector2)(target.transform.position - _owner.position);
            var firedDirection = toTarget.sqrMagnitude > 0.000001f ? toTarget.normalized : direction;
            Fired?.Invoke(firedDirection);
        }

        private void UpdateSatellite(WeaponRuntime weapon)
        {
            EnsureSatelliteVisual(weapon);
            if (weapon.SatelliteVisual == null)
            {
                return;
            }

            var tier = Mathf.Max(0, weapon.Level - 1);
            var attackSpeedScale = _stats != null ? Mathf.Max(0.2f, 1f / _stats.AttackIntervalMultiplier) : 1f;
            var orbitSpeed = Mathf.Max(30f, _config.satelliteAngularSpeed) * (1f + (0.02f * tier)) * attackSpeedScale;
            weapon.OrbitAngleDegrees += orbitSpeed * Time.deltaTime;
            if (weapon.OrbitAngleDegrees > 360f)
            {
                weapon.OrbitAngleDegrees -= 360f;
            }

            var orbitRadius = Mathf.Max(0.2f, _config.satelliteOrbitRadius) * (1f + (0.02f * tier));
            var radians = weapon.OrbitAngleDegrees * Mathf.Deg2Rad;
            var offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * orbitRadius;
            var worldPos = (Vector2)_owner.position + offset;
            weapon.SatelliteVisual.position = new Vector3(worldPos.x, worldPos.y, 0f);

            PruneEnemyCooldownMap(weapon.SatelliteHitCooldownUntil);

            var hitRadius = Mathf.Max(0.05f, _config.satelliteHitRadius);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.satelliteDamageMultiplier, 0.05f, 5f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            var hitCooldown = GetSatelliteHitCooldown(weapon);

            _registry.GetNearby(worldPos, hitRadius + _registry.GetMaxCollisionRadius(), _nearbyEnemies);
            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (!IsEnemyUsable(enemy))
                {
                    continue;
                }

                var limit = hitRadius + enemy.CollisionRadius;
                if (((Vector2)enemy.transform.position - worldPos).sqrMagnitude > limit * limit)
                {
                    continue;
                }

                if (weapon.SatelliteHitCooldownUntil.TryGetValue(enemy, out var nextHitAt) && Time.time < nextHitAt)
                {
                    continue;
                }

                weapon.SatelliteHitCooldownUntil[enemy] = Time.time + hitCooldown;
                enemy.ReceiveWeaponDamage(damage, weapon.WeaponId, coreElement, coreLevel);
                SpawnRingFx((Vector2)enemy.transform.position, hitRadius * 0.9f, auraFxColor, auraFxWidth, 0.06f, "SatelliteHitFx");
            }
        }

        private void UpdateRifleTurret(WeaponRuntime weapon)
        {
            UpdateRifleTurretInstances(weapon);

            if (weapon.Cooldown > 0f)
            {
                return;
            }

            DeployRifleTurret((Vector2)_owner.position, GetRifleTurretRange(weapon));
            weapon.Cooldown = GetRifleTurretDeployInterval(weapon);
        }

        private void UpdateAura(WeaponRuntime weapon)
        {
            if (weapon.Cooldown > 0f)
            {
                return;
            }

            weapon.Cooldown = GetAuraTickInterval(weapon);

            var center = (Vector2)_owner.position;
            var range = GetAuraRange(weapon);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.auraDamageMultiplier, 0.01f, 5f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);

            SpawnRingFx(center, range, auraFxColor, auraFxWidth, auraFxDuration, "AuraFx");
            _registry.GetNearby(center, range + _registry.GetMaxCollisionRadius(), _nearbyEnemies);

            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (!IsEnemyUsable(enemy))
                {
                    continue;
                }

                var toEnemy = (Vector2)enemy.transform.position - center;
                var centerDistance = toEnemy.magnitude;
                var surfaceDistance = Mathf.Max(0f, centerDistance - enemy.CollisionRadius);
                if (surfaceDistance > range)
                {
                    continue;
                }

                enemy.ReceiveWeaponDamage(damage, weapon.WeaponId, coreElement, coreLevel);
            }
        }

        private void UpdateRifleTurretInstances(WeaponRuntime weapon)
        {
            if (_rifleTurrets.Count <= 0)
            {
                return;
            }

            var turretRange = GetRifleTurretRange(weapon);
            var turretDamage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.rifleTurretDamageMultiplier, 0.05f, 5f);
            var shotInterval = GetRifleTurretShotInterval(weapon);
            var projectileSpeed = Mathf.Max(0.1f, _config.rifleTurretProjectileSpeed);
            var projectileLifetime = Mathf.Max(0.1f, _config.rifleTurretProjectileLifetime);
            var projectileHitRadius = Mathf.Max(0.05f, _config.projectileHitRadius * 0.9f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);

            for (var i = _rifleTurrets.Count - 1; i >= 0; i--)
            {
                var turret = _rifleTurrets[i];
                if (turret == null || turret.Root == null || Time.time >= turret.ExpiresAt)
                {
                    DestroyTurretAt(i);
                    continue;
                }

                turret.ShotCooldown -= Time.deltaTime;
                if (turret.ShotCooldown > 0f)
                {
                    continue;
                }

                var target = FindNearestUsableFrom(turret.Position, turretRange);
                if (target == null)
                {
                    turret.ShotCooldown = shotInterval * 0.6f;
                    continue;
                }

                var fireDirection = (Vector2)(target.transform.position - turret.Root.position);
                if (fireDirection.sqrMagnitude <= 0.000001f)
                {
                    turret.ShotCooldown = shotInterval;
                    continue;
                }

                SpawnProjectile(
                    weapon.WeaponId,
                    coreElement,
                    coreLevel,
                    fireDirection.normalized,
                    turretDamage,
                    projectileSpeed,
                    projectileLifetime,
                    projectileHitRadius,
                    1,
                    0f,
                    1f,
                    new Color(0.95f, 0.95f, 0.75f),
                    turret.Root.position);

                SpawnTracerFx(turret.Root.position, target.transform.position);
                turret.ShotCooldown = shotInterval;
            }
        }

        private void DeployRifleTurret(Vector2 position, float turretRange)
        {
            var maxCount = Mathf.Clamp(_config.rifleTurretMaxCount, 1, 6);
            while (_rifleTurrets.Count >= maxCount)
            {
                DestroyTurretAt(0);
            }

            var turretObject = new GameObject("RifleTurret");
            turretObject.transform.SetParent(transform, false);
            turretObject.transform.position = new Vector3(position.x, position.y, 0f);
            turretObject.transform.localScale = Vector3.one * 0.3f;

            var turretRenderer = turretObject.AddComponent<SpriteRenderer>();
            turretRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            turretRenderer.color = new Color(0.7f, 0.9f, 1f, 0.9f);
            turretRenderer.sortingOrder = 34;

            var rangeFxObject = new GameObject("RifleTurretRangeFx");
            rangeFxObject.transform.SetParent(turretObject.transform, false);
            rangeFxObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);

            var rangeRenderer = rangeFxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(rangeRenderer, turretRangeFxColor, 0.03f, true, false);
            SetCircleLinePositions(rangeRenderer, Vector2.zero, turretRange, ringFxSegments, 0f);

            _rifleTurrets.Add(new RifleTurretRuntime
            {
                Root = turretObject.transform,
                Position = position,
                ExpiresAt = Time.time + Mathf.Max(0.1f, _config.rifleTurretLifetime),
                ShotCooldown = 0f,
            });
        }

        private void DestroyTurretAt(int index)
        {
            if (index < 0 || index >= _rifleTurrets.Count)
            {
                return;
            }

            var turret = _rifleTurrets[index];
            if (turret != null && turret.Root != null)
            {
                Destroy(turret.Root.gameObject);
            }

            _rifleTurrets.RemoveAt(index);
        }

        private void ClearRifleTurrets()
        {
            for (var i = _rifleTurrets.Count - 1; i >= 0; i--)
            {
                DestroyTurretAt(i);
            }

            _rifleTurrets.Clear();
        }

        private void EnsureSatelliteVisual(WeaponRuntime weapon)
        {
            if (weapon.SatelliteVisual != null)
            {
                return;
            }

            var satelliteObject = new GameObject("SatelliteVisual");
            satelliteObject.transform.SetParent(transform, false);
            var renderer = satelliteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(0.72f, 1f, 0.9f, 0.95f);
            renderer.sortingOrder = 33;
            satelliteObject.transform.localScale = Vector3.one * 0.22f;
            weapon.SatelliteVisual = satelliteObject.transform;
        }

        private EnemyController FindRandomUsableInRange(Vector2 origin, float maxDistance)
        {
            _candidateEnemies.Clear();
            var limitSq = Mathf.Max(0.01f, maxDistance) * Mathf.Max(0.01f, maxDistance);
            var enemies = _registry.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!IsEnemyUsable(enemy))
                {
                    continue;
                }

                if (((Vector2)enemy.transform.position - origin).sqrMagnitude > limitSq)
                {
                    continue;
                }

                _candidateEnemies.Add(enemy);
            }

            if (_candidateEnemies.Count <= 0)
            {
                return null;
            }

            return _candidateEnemies[UnityEngine.Random.Range(0, _candidateEnemies.Count)];
        }

        private EnemyController FindNearestChainTarget(Vector2 from, float jumpRange, List<EnemyController> excluded)
        {
            _registry.GetNearby(from, jumpRange + _registry.GetMaxCollisionRadius(), _nearbyEnemies);
            EnemyController best = null;
            var bestSq = Mathf.Max(0.01f, jumpRange) * Mathf.Max(0.01f, jumpRange);

            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (!IsEnemyUsable(enemy) || ContainsEnemy(excluded, enemy))
                {
                    continue;
                }

                var distanceSq = ((Vector2)enemy.transform.position - from).sqrMagnitude;
                if (distanceSq > bestSq)
                {
                    continue;
                }

                bestSq = distanceSq;
                best = enemy;
            }

            return best;
        }

        private EnemyController FindNearestUsableFrom(Vector2 origin, float maxDistance)
        {
            var enemies = _registry.Enemies;
            EnemyController best = null;
            var bestDistanceSq = Mathf.Max(0.01f, maxDistance) * Mathf.Max(0.01f, maxDistance);

            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!IsEnemyUsable(enemy))
                {
                    continue;
                }

                var distanceSq = ((Vector2)enemy.transform.position - origin).sqrMagnitude;
                if (distanceSq > bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                best = enemy;
            }

            return best;
        }

        private static bool ContainsEnemy(List<EnemyController> list, EnemyController enemy)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], enemy))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsEnemyUsable(EnemyController enemy)
        {
            return enemy != null && IsInsideAimBounds(enemy.transform.position);
        }

        private void PruneEnemyCooldownMap(Dictionary<EnemyController, float> cooldownMap)
        {
            if (cooldownMap == null || cooldownMap.Count <= 0)
            {
                return;
            }

            _cleanupEnemies.Clear();
            foreach (var pair in cooldownMap)
            {
                if (pair.Key == null || pair.Value < Time.time - 1.5f)
                {
                    _cleanupEnemies.Add(pair.Key);
                }
            }

            for (var i = 0; i < _cleanupEnemies.Count; i++)
            {
                cooldownMap.Remove(_cleanupEnemies[i]);
            }
        }

        private void SpawnKatanaRangeEffect(Vector2 origin, Vector2 direction, float range, float coneHalfAngle)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            var segments = Mathf.Clamp(katanaRangeEffectSegments, 4, 40);
            var fxObject = new GameObject("KatanaRangeFx");
            fxObject.transform.SetParent(transform, false);

            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, katanaRangeEffectColor, katanaRangeEffectWidth, false, true);

            var totalPoints = segments + 3;
            lineRenderer.positionCount = totalPoints;
            lineRenderer.SetPosition(0, new Vector3(origin.x, origin.y, -0.02f));

            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var angle = Mathf.Lerp(-coneHalfAngle, coneHalfAngle, t);
                var rayDirection = RotateDirection(normalizedDirection, angle);
                var point = origin + (rayDirection * range);
                lineRenderer.SetPosition(i + 1, new Vector3(point.x, point.y, -0.02f));
            }

            lineRenderer.SetPosition(totalPoints - 1, new Vector3(origin.x, origin.y, -0.02f));
            Destroy(fxObject, Mathf.Max(0.02f, katanaRangeEffectDuration));
        }

        private void SpawnLightningStrikeFx(Vector3 targetPosition)
        {
            var top = targetPosition + new Vector3(0f, 1.6f, 0f);
            SpawnLineFx(top, targetPosition, lightningFxColor, lightningFxWidth, lightningFxDuration, "LightningStrikeFx");
            SpawnRingFx(targetPosition, 0.35f, lightningFxColor, 0.05f, lightningFxDuration, "LightningImpactFx");
        }

        private void SpawnTracerFx(Vector3 from, Vector3 to)
        {
            SpawnLineFx(from, to, turretTracerFxColor, turretTracerFxWidth, turretTracerFxDuration, "TurretTracerFx");
        }

        private void SpawnLineFx(Vector3 from, Vector3 to, Color color, float width, float duration, string name)
        {
            _fxPoints.Clear();
            _fxPoints.Add(new Vector3(from.x, from.y, -0.02f));
            _fxPoints.Add(new Vector3(to.x, to.y, -0.02f));
            SpawnPolylineFx(_fxPoints, color, width, duration, false, name);
        }

        private void SpawnRingFx(Vector2 center, float radius, Color color, float width, float duration, string name)
        {
            var fxObject = new GameObject(name);
            fxObject.transform.SetParent(transform, false);
            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, color, width, true, true);
            SetCircleLinePositions(lineRenderer, center, radius, ringFxSegments, -0.02f);
            Destroy(fxObject, Mathf.Max(0.02f, duration));
        }

        private void SpawnPolylineFx(List<Vector3> points, Color color, float width, float duration, bool loop, string name)
        {
            if (points == null || points.Count <= 1)
            {
                return;
            }

            var fxObject = new GameObject(name);
            fxObject.transform.SetParent(transform, false);
            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, color, width, loop, true);
            lineRenderer.positionCount = points.Count;
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                lineRenderer.SetPosition(i, new Vector3(p.x, p.y, -0.02f));
            }

            Destroy(fxObject, Mathf.Max(0.02f, duration));
        }

        private void ConfigureLineRenderer(LineRenderer lineRenderer, Color color, float width, bool loop, bool useWorldSpace)
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.useWorldSpace = useWorldSpace;
            lineRenderer.loop = loop;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.startWidth = Mathf.Max(0.001f, width);
            lineRenderer.endWidth = Mathf.Max(0.001f, width);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.sortingOrder = 500;
            lineRenderer.sharedMaterial = GetOrCreateSharedFxMaterial();
        }

        private void SetCircleLinePositions(LineRenderer lineRenderer, Vector2 center, float radius, int segments, float z)
        {
            if (lineRenderer == null)
            {
                return;
            }

            var clampedRadius = Mathf.Max(0.01f, radius);
            var clampedSegments = Mathf.Clamp(segments, 8, 96);
            lineRenderer.positionCount = clampedSegments;

            for (var i = 0; i < clampedSegments; i++)
            {
                var t = i / (float)clampedSegments;
                var angle = t * Mathf.PI * 2f;
                var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * clampedRadius;
                lineRenderer.SetPosition(i, new Vector3(p.x, p.y, z));
            }
        }

        private static Material GetOrCreateSharedFxMaterial()
        {
            if (_sharedFxMaterial != null)
            {
                return _sharedFxMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _sharedFxMaterial = new Material(shader)
            {
                name = "WeaponFxMat",
                hideFlags = HideFlags.HideAndDontSave,
            };

            return _sharedFxMaterial;
        }

        private void SpawnProjectile(
            WeaponUpgradeId weaponId,
            WeaponCoreElement coreElement,
            int coreLevel,
            Vector2 direction,
            float damage,
            float speed,
            float lifetime,
            float hitRadius,
            int maxHits,
            float damageFalloffPerHit,
            float minimumDamageMultiplier,
            Color color,
            Vector3? overrideSpawnPosition = null)
        {
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : _lastAimDirection;
            SetAimDirection(normalizedDirection);

            var spawnPosition = overrideSpawnPosition
                ?? (_projectileSpawnResolver != null ? _projectileSpawnResolver(normalizedDirection) : _owner.position);

            var projectile = GetPooledProjectile();
            var projectileTransform = projectile.transform;
            projectileTransform.SetPositionAndRotation(spawnPosition, Quaternion.identity);

            var visualScale = Mathf.Max(0.05f, _config.projectileVisualScale);
            projectileTransform.localScale = Vector3.one * visualScale;

            var renderer = projectile.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = color;
            }

            projectile.Initialize(
                _registry,
                new Vector3(normalizedDirection.x, normalizedDirection.y, 0f),
                Mathf.Max(0.1f, speed),
                Mathf.Max(0f, damage),
                Mathf.Max(0.05f, lifetime),
                Mathf.Max(0.05f, hitRadius),
                Mathf.Max(1, maxHits),
                Mathf.Clamp(damageFalloffPerHit, 0f, 0.9f),
                Mathf.Clamp(minimumDamageMultiplier, 0.05f, 1f),
                weaponId,
                coreElement,
                coreLevel,
                ReturnProjectileToPool,
                _useProjectileBoundsCulling,
                _projectileCullBounds);

            Fired?.Invoke(normalizedDirection);
        }

        private float GetAttackInterval(WeaponRuntime weapon)
        {
            var tier = Mathf.Max(0, weapon.Level - 1);
            var weaponLevelMultiplier = Mathf.Max(0.35f, 1f - (0.03f * tier));
            var statAttackSpeedMultiplier = _stats != null ? Mathf.Max(0.2f, _stats.AttackIntervalMultiplier) : 1f;

            var baseInterval = Mathf.Max(0.05f, _config.attackInterval);
            if (weapon.WeaponId == WeaponUpgradeId.Smg)
            {
                baseInterval *= 0.92f;
            }
            else if (weapon.WeaponId == WeaponUpgradeId.SniperRifle)
            {
                baseInterval *= 1.18f;
            }
            else if (weapon.WeaponId == WeaponUpgradeId.Shotgun)
            {
                baseInterval *= 1.08f;
            }
            else if (weapon.WeaponId == WeaponUpgradeId.Katana)
            {
                baseInterval *= 0.85f;
            }
            else if (weapon.WeaponId == WeaponUpgradeId.ChainAttack)
            {
                baseInterval *= 1.05f;
            }

            var nonCoreInterval = baseInterval * statAttackSpeedMultiplier * weaponLevelMultiplier;
            var withCoreApplied = ApplyCoreAttackIntervalToValue(nonCoreInterval, weapon);
            return Mathf.Max(0.05f, withCoreApplied);
        }

        private float GetLightningInterval(WeaponRuntime weapon)
        {
            return Mathf.Max(0.05f, GetAttackInterval(weapon) * Mathf.Clamp(_config.lightningIntervalMultiplier, 0.1f, 5f));
        }

        private float GetAuraTickInterval(WeaponRuntime weapon)
        {
            var tier = Mathf.Max(0, weapon.Level - 1);
            var weaponLevelMultiplier = Mathf.Max(0.45f, 1f - (0.025f * tier));
            var statAttackSpeedMultiplier = _stats != null ? Mathf.Max(0.2f, _stats.AttackIntervalMultiplier) : 1f;
            var nonCoreInterval = Mathf.Max(0.01f, _config.auraTickInterval) * statAttackSpeedMultiplier * weaponLevelMultiplier;
            return Mathf.Max(0.03f, ApplyCoreAttackIntervalToValue(nonCoreInterval, weapon));
        }

        private float GetSatelliteHitCooldown(WeaponRuntime weapon)
        {
            var tier = Mathf.Max(0, weapon.Level - 1);
            var weaponLevelMultiplier = Mathf.Max(0.35f, 1f - (0.025f * tier));
            var statAttackSpeedMultiplier = _stats != null ? Mathf.Max(0.2f, _stats.AttackIntervalMultiplier) : 1f;
            var nonCoreInterval = Mathf.Max(0.01f, _config.satelliteHitCooldownPerEnemy) * statAttackSpeedMultiplier * weaponLevelMultiplier;
            return Mathf.Max(0.03f, ApplyCoreAttackIntervalToValue(nonCoreInterval, weapon));
        }

        private float GetRifleTurretDeployInterval(WeaponRuntime weapon)
        {
            var tier = Mathf.Max(0, weapon.Level - 1);
            var weaponLevelMultiplier = Mathf.Max(0.4f, 1f - (0.03f * tier));
            var statAttackSpeedMultiplier = _stats != null ? Mathf.Max(0.2f, _stats.AttackIntervalMultiplier) : 1f;
            var nonCoreInterval = Mathf.Max(0.1f, _config.rifleTurretDeployInterval) * statAttackSpeedMultiplier * weaponLevelMultiplier;
            return Mathf.Max(0.1f, ApplyCoreAttackIntervalToValue(nonCoreInterval, weapon));
        }

        private float GetRifleTurretShotInterval(WeaponRuntime weapon)
        {
            return Mathf.Max(0.08f, GetAttackInterval(weapon) * 0.75f);
        }

        private float GetWeaponBaseDamage(WeaponRuntime weapon)
        {
            var tier = Mathf.Max(0, weapon.Level - 1);
            var weaponLevelMultiplier = 1f + (0.18f * tier);
            var statDamageMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.DamageMultiplier) : 1f;
            var nonCoreDamage = _config.projectileDamage * statDamageMultiplier * weaponLevelMultiplier;
            var withCoreApplied = ApplyCoreDamageToValue(nonCoreDamage, weapon);
            return Mathf.Max(0.1f, withCoreApplied);
        }

        private float ApplyCoreDamageToValue(float value, WeaponRuntime weapon)
        {
            return Mathf.Max(0f, value) * GetCoreDamageMultiplier(weapon);
        }

        private float ApplyCoreAttackIntervalToValue(float interval, WeaponRuntime weapon)
        {
            return Mathf.Max(0f, interval) * GetCoreAttackIntervalMultiplier(weapon);
        }

        private float GetCoreDamageMultiplier(WeaponRuntime weapon)
        {
            var coreElement = GetCoreElement(weapon.WeaponId);
            var rawCoreLevel = GetCoreLevel(weapon.WeaponId);
            if (rawCoreLevel <= 0)
            {
                return 1f;
            }
            var coreLevel = Mathf.Clamp(rawCoreLevel, 1, PlayerBuildRuntime.MaxCoreLevel);

            return coreElement switch
            {
                WeaponCoreElement.Wind => coreLevel switch
                {
                    1 => 0.90f,
                    2 => 0.85f,
                    _ => 0.80f,
                },
                WeaponCoreElement.Water => coreLevel switch
                {
                    1 => 1.10f,
                    2 => 1.20f,
                    _ => 1.30f,
                },
                _ => 1f,
            };
        }

        private float GetCoreAttackIntervalMultiplier(WeaponRuntime weapon)
        {
            var coreElement = GetCoreElement(weapon.WeaponId);
            var rawCoreLevel = GetCoreLevel(weapon.WeaponId);
            if (rawCoreLevel <= 0)
            {
                return 1f;
            }
            var coreLevel = Mathf.Clamp(rawCoreLevel, 1, PlayerBuildRuntime.MaxCoreLevel);

            if (coreElement != WeaponCoreElement.Wind)
            {
                return 1f;
            }

            return coreLevel switch
            {
                1 => 0.80f,
                2 => 0.65f,
                _ => 0.50f,
            };
        }

        private float GetWeaponRange(WeaponRuntime weapon)
        {
            var tier = Mathf.Max(0, weapon.Level - 1);
            var levelMultiplier = 1f + (0.04f * tier);
            var attackRangeMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.AttackRangeMultiplier) : 1f;

            var baseRange = weapon.WeaponId switch
            {
                WeaponUpgradeId.Katana => Mathf.Max(0.25f, _config.katanaRange),
                WeaponUpgradeId.Aura => Mathf.Max(0.2f, _config.auraRadius),
                WeaponUpgradeId.Satellite => Mathf.Max(0.2f, _config.satelliteOrbitRadius + _config.satelliteHitRadius),
                _ => Mathf.Max(0.5f, _config.attackRange),
            };

            return Mathf.Max(0.25f, baseRange * attackRangeMultiplier * levelMultiplier);
        }

        private float GetAuraRange(WeaponRuntime weapon)
        {
            return GetWeaponRange(weapon);
        }

        private float GetRifleTurretRange(WeaponRuntime weapon)
        {
            return Mathf.Max(0.4f, GetWeaponRange(weapon) * Mathf.Clamp(_config.rifleTurretRangeMultiplier, 0.1f, 3f));
        }

        private float GetMaximumLoadoutRange()
        {
            var maxRange = Mathf.Max(0.5f, _config.attackRange);
            for (var i = 0; i < _loadout.Count; i++)
            {
                var range = GetWeaponRange(_loadout[i]);
                if (range > maxRange)
                {
                    maxRange = range;
                }
            }

            return maxRange;
        }

        private WeaponCoreElement GetCoreElement(WeaponUpgradeId weaponId)
        {
            return _coreElementByWeapon.TryGetValue(weaponId, out var coreElement)
                ? coreElement
                : WeaponCoreElement.None;
        }

        private int GetCoreLevel(WeaponUpgradeId weaponId)
        {
            return _coreLevelByWeapon.TryGetValue(weaponId, out var coreLevel)
                ? coreLevel
                : 0;
        }

        private bool IsTargetUsable(EnemyController target, float maxDistance)
        {
            if (target == null || _owner == null)
            {
                return false;
            }

            if (!IsInsideAimBounds(target.transform.position))
            {
                return false;
            }

            var limit = Mathf.Max(0.01f, maxDistance);
            return ((Vector2)(target.transform.position - _owner.position)).sqrMagnitude <= limit * limit;
        }

        private EnemyController FindNearestUsable(float maxDistance)
        {
            if (_registry == null || _owner == null)
            {
                return null;
            }

            var enemies = _registry.Enemies;
            EnemyController best = null;
            var bestDistanceSq = Mathf.Max(0.01f, maxDistance) * Mathf.Max(0.01f, maxDistance);
            var ownerPosition = _owner.position;

            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!IsEnemyUsable(enemy))
                {
                    continue;
                }

                var distanceSq = (enemy.transform.position - ownerPosition).sqrMagnitude;
                if (distanceSq > bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                best = enemy;
            }

            return best;
        }

        private bool IsInsideAimBounds(Vector3 worldPosition)
        {
            if (!_useProjectileBoundsCulling)
            {
                return true;
            }

            return worldPosition.x >= _projectileCullBounds.xMin
                && worldPosition.x <= _projectileCullBounds.xMax
                && worldPosition.y >= _projectileCullBounds.yMin
                && worldPosition.y <= _projectileCullBounds.yMax;
        }

        private static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            var x = (direction.x * cosine) - (direction.y * sine);
            var y = (direction.x * sine) + (direction.y * cosine);
            var rotated = new Vector2(x, y);
            return rotated.sqrMagnitude > 0.000001f ? rotated.normalized : Vector2.right;
        }

        private void EnsureProjectilePool()
        {
            if (_projectilePoolRoot == null)
            {
                var root = new GameObject("ProjectilePool");
                root.transform.SetParent(transform, false);
                _projectilePoolRoot = root.transform;
            }

            var targetCount = Mathf.Max(0, projectilePoolPrewarmCount);
            while (_projectilePool.Count < targetCount)
            {
                var projectile = CreateProjectileInstance();
                ReturnProjectileToPool(projectile);
            }
        }

        private Projectile GetPooledProjectile()
        {
            while (_projectilePool.Count > 0)
            {
                var pooled = _projectilePool.Dequeue();
                if (pooled != null)
                {
                    pooled.gameObject.SetActive(true);
                    return pooled;
                }
            }

            var created = CreateProjectileInstance();
            created.gameObject.SetActive(true);
            return created;
        }

        private Projectile CreateProjectileInstance()
        {
            var projectileObject = new GameObject("Projectile");
            projectileObject.transform.SetParent(_projectilePoolRoot, false);

            var renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(1f, 0.95f, 0.35f);

            var projectile = projectileObject.AddComponent<Projectile>();
            projectileObject.SetActive(false);
            return projectile;
        }

        private void ReturnProjectileToPool(Projectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            var projectileObject = projectile.gameObject;
            projectileObject.SetActive(false);
            projectileObject.transform.SetParent(_projectilePoolRoot, false);
            _projectilePool.Enqueue(projectile);
        }

        private void CleanupLoadoutRuntimeState()
        {
            for (var i = 0; i < _loadout.Count; i++)
            {
                var weapon = _loadout[i];
                if (weapon == null)
                {
                    continue;
                }

                if (weapon.SatelliteVisual != null)
                {
                    Destroy(weapon.SatelliteVisual.gameObject);
                    weapon.SatelliteVisual = null;
                }

                weapon.SatelliteHitCooldownUntil.Clear();
            }
        }
    }
}
