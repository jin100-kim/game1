using System;
using System.Collections.Generic;
using EJR.Game.Core;
using System.Linq;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class AutoWeaponSystem : MonoBehaviour
    {
        public readonly struct ProjectileSpawnRequest
        {
            public ProjectileSpawnRequest(
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
                Vector3 spawnPosition,
                float visualScale)
            {
                WeaponId = weaponId;
                CoreElement = coreElement;
                CoreLevel = coreLevel;
                Direction = direction;
                Damage = damage;
                Speed = speed;
                Lifetime = lifetime;
                HitRadius = hitRadius;
                MaxHits = maxHits;
                DamageFalloffPerHit = damageFalloffPerHit;
                MinimumDamageMultiplier = minimumDamageMultiplier;
                Color = color;
                SpawnPosition = spawnPosition;
                VisualScale = visualScale;
            }

            public WeaponUpgradeId WeaponId { get; }
            public WeaponCoreElement CoreElement { get; }
            public int CoreLevel { get; }
            public Vector2 Direction { get; }
            public float Damage { get; }
            public float Speed { get; }
            public float Lifetime { get; }
            public float HitRadius { get; }
            public int MaxHits { get; }
            public float DamageFalloffPerHit { get; }
            public float MinimumDamageMultiplier { get; }
            public Color Color { get; }
            public Vector3 SpawnPosition { get; }
            public float VisualScale { get; }
        }

        [SerializeField, Min(0)] private int projectilePoolPrewarmCount = 40;
        [SerializeField, Min(0.01f)] private float targetScanInterval = 0.08f;
        [SerializeField, Min(0.5f)] private float projectileTravelRangeFactor = 1.35f;
        [SerializeField, Min(0.02f)] private float katanaRangeEffectDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float katanaRangeEffectWidth = 0.05f;
        [SerializeField, Range(4, 40)] private int katanaRangeEffectSegments = 14;
        [SerializeField] private Color katanaRangeEffectColor = new(0.2f, 1f, 0.9f, 0.9f);
        [SerializeField, Min(0.01f)] private float katanaSlashFxFps = 18f;
        [SerializeField, Min(0.05f)] private float katanaSlashFxForwardOffset = 0.72f;
        [SerializeField] private Vector2 katanaSlashFxLocalOffset = new(-0.22f, -2.0f);
        [SerializeField, Min(0.05f)] private float katanaSlashFxScale = 6f;
        [SerializeField, Min(0.01f)] private float chainFxDuration = 0.08f;
        [SerializeField, Min(0.005f)] private float chainFxWidth = 0.05f;
        [SerializeField] private Color chainFxColor = new(0.45f, 0.85f, 1f, 0.95f);
        [SerializeField, Min(0.01f)] private float lightningFxDuration = 0.1f;
        [SerializeField, Min(0.01f)] private float auraFxDuration = 0.08f;
        [SerializeField, Min(0.005f)] private float auraFxWidth = 0.045f;
        [SerializeField] private Color auraFxColor = new(0.45f, 1f, 0.75f, 0.75f);
        [SerializeField, Min(0.01f)] private float turretTracerFxDuration = 0.06f;
        [SerializeField, Min(0.005f)] private float turretTracerFxWidth = 0.03f;
        [SerializeField] private Color turretTracerFxColor = new(1f, 0.86f, 0.28f, 0.95f);
        [SerializeField] private Color turretRangeFxColor = new(0.55f, 0.9f, 1f, 0.28f);
        [SerializeField, Range(8, 96)] private int ringFxSegments = 28;
        [SerializeField, Min(0.1f)] private float satelliteVisualAnimationFps = 12f;
        [SerializeField, Min(1)] private int satelliteVisualSortOrder = 33;
        [SerializeField, Min(0.1f)] private float turretVisualAnimationFps = 12f;
        [SerializeField, Min(0.05f)] private float turretVisualScale = 3f;
        [SerializeField, Min(0.1f)] private float satelliteBeamVisualFps = 14f;
        [SerializeField, Min(0.05f)] private float satelliteBeamVisualScale = 3f;
        [SerializeField] private float satelliteBeamVisualYOffset = 0f;
        [Header("Debug Gizmos")]
        [SerializeField] private bool showSatelliteHitGizmos = true;
        [SerializeField] private Color satelliteHitGizmoColor = new(0.35f, 1f, 0.95f, 0.95f);

        private sealed class WeaponRuntime
        {
            public WeaponRuntime(WeaponUpgradeId id, int level)
            {
                WeaponId = id;
                Level = Mathf.Max(1, level);
                Cooldown = 0f;
                BurstShotsRemaining = 0;
                BurstShotCooldown = 0f;
                BurstDirection = Vector2.right;
                BurstTotalShots = 0;
                OrbitAngleDegrees = UnityEngine.Random.Range(0f, 360f);
            }

            public WeaponUpgradeId WeaponId { get; }
            public int Level { get; set; }
            public float Cooldown { get; set; }
            public int BurstShotsRemaining { get; set; }
            public float BurstShotCooldown { get; set; }
            public Vector2 BurstDirection { get; set; }
            public int BurstTotalShots { get; set; }
            public float OrbitAngleDegrees { get; set; }
            public List<Transform> SatelliteVisuals { get; } = new(3);
            public Dictionary<EnemyController, float> SatelliteHitCooldownUntil { get; } = new();
        }

        private sealed class RifleTurretRuntime
        {
            public Transform Root;
            public Vector2 Position;
            public float ExpiresAt;
            public float ShotCooldown;
            public SpriteRenderer Renderer;
            public Sprite IdleFrame;
            public Sprite[] FireFrames;
            public Coroutine FireAnimationCoroutine;
        }

        private WeaponConfig _config;
        private Transform _owner;
        private EnemyRegistry _registry;
        private PlayerStatsRuntime _stats;
        private Func<Vector2, Vector3> _projectileSpawnResolver;
        private Func<ProjectileSpawnRequest, bool> _projectileSpawnOverride;
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
        private static readonly float[] CommonWeaponAttackSpeedBonusByLevel = { 0f, 0f, 0.15f, 0.15f, 0.15f, 0.15f, 0.30f, 0.30f, 0.30f, 0.30f };
        private static readonly float[] CommonWeaponRangeByLevel = { 1f, 1f, 1f, 1.15f, 1.15f, 1.15f, 1.15f, 1.3f, 1.3f, 1.3f };
        private static readonly float[] AuraRangeByLevel = { 1f, 1f, 1f, 1.15f, 1.3f, 1.3f, 1.3f, 1.45f, 1.45f, 1.6f };
        private static readonly float[] RifleLikeDamageByLevel = { 1f, 1.15f, 1.15f, 1.15f, 1.05f, 1.2f, 1.2f, 1.2f, 1.35f, 1.35f };
        private static readonly float[] SniperDamageByLevel = { 1f, 1.15f, 1.15f, 1.15f, 1.5f, 1.65f, 1.65f, 1.65f, 1.8f, 2f };
        private static readonly float[] SmgLikeDamageByLevel = { 1f, 1.15f, 1.15f, 1.15f, 1.3f, 1.45f, 1.45f, 1.45f, 1.6f, 1.6f };
        private static readonly float[] AuraDamageByLevel = { 1f, 1.15f, 1.15f, 1.15f, 1.3f, 1.45f, 1.45f, 1.45f, 1.6f, 1.6f };
        private static readonly int[] RifleExtraByLevel = { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 };
        private static readonly int[] SmgExtraByLevel = { 0, 0, 0, 0, 2, 2, 2, 2, 2, 4 };
        private static readonly int[] SniperExtraByLevel = { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 };
        private static readonly int[] ShotgunExtraByLevel = { 0, 0, 0, 0, 2, 2, 2, 2, 2, 4 };
        private static readonly int[] KatanaExtraByLevel = { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 };
        private static readonly int[] ChainExtraByLevel = { 0, 0, 0, 0, 2, 2, 2, 2, 2, 4 };
        private static readonly int[] SatelliteBeamExtraByLevel = { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 };
        private static readonly int[] DroneExtraByLevel = { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 };
        private static readonly int[] TurretExtraByLevel = { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 };

        public event Action<Vector2> AimUpdated;
        public event Action<Vector2> Fired;
        public event Action<Vector2, Vector2, float> KatanaSlashFxRequested;
        public event Action<Vector3[]> ChainFxRequested;
        public event Action<Vector3, float> AuraPulseFxRequested;
        public event Action<Vector3, float> SatelliteHitFxRequested;
        public event Action<Vector3> SatelliteBeamFxRequested;
        public event Action<Vector3, float, float> TurretDeployed;
        public event Action<Vector3, Vector3> TurretTracerFxRequested;

        public void Initialize(
            WeaponConfig config,
            Transform owner,
            EnemyRegistry registry,
            PlayerStatsRuntime stats,
            Func<Vector2, Vector3> projectileSpawnResolver = null,
            Func<ProjectileSpawnRequest, bool> projectileSpawnOverride = null,
            Rect? projectileCullBounds = null)
        {
            _config = config;
            _owner = owner;
            _registry = registry;
            _stats = stats;
            _projectileSpawnResolver = projectileSpawnResolver;
            _projectileSpawnOverride = projectileSpawnOverride;
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
            var existingById = new Dictionary<WeaponUpgradeId, WeaponRuntime>(_loadout.Count);
            for (var i = 0; i < _loadout.Count; i++)
            {
                var runtime = _loadout[i];
                if (runtime == null)
                {
                    continue;
                }

                existingById[runtime.WeaponId] = runtime;
            }

            var nextLoadout = new List<WeaponRuntime>(Mathf.Max(1, build != null ? build.OwnedWeapons.Count : 0));
            var hasRifleTurretInNextLoadout = false;

            _coreElementByWeapon.Clear();
            _coreLevelByWeapon.Clear();

            if (build == null || build.OwnedWeapons.Count <= 0)
            {
                CleanupLoadoutRuntimeState();
                ClearRifleTurrets();
                _loadout.Clear();
                return;
            }

            for (var i = 0; i < build.OwnedWeapons.Count; i++)
            {
                var id = build.OwnedWeapons[i];
                var level = Mathf.Max(1, build.GetWeaponLevel(id));
                if (!existingById.TryGetValue(id, out var runtime) || runtime == null)
                {
                    runtime = new WeaponRuntime(id, level);
                }
                else
                {
                    existingById.Remove(id);
                    runtime.Level = level;
                }

                nextLoadout.Add(runtime);
                if (id == WeaponUpgradeId.RifleTurret)
                {
                    hasRifleTurretInNextLoadout = true;
                }

                var coreLevel = build.GetWeaponCoreLevel(id);
                if (coreLevel > 0)
                {
                    _coreElementByWeapon[id] = build.GetWeaponCoreElement(id);
                    _coreLevelByWeapon[id] = coreLevel;
                }
            }

            foreach (var pair in existingById)
            {
                CleanupWeaponRuntimeState(pair.Value);
            }

            if (!hasRifleTurretInNextLoadout)
            {
                ClearRifleTurrets();
            }

            _loadout.Clear();
            _loadout.AddRange(nextLoadout);
        }

        private void Update()
        {
            if (_config == null || _owner == null || _registry == null || _stats == null)
            {
                return;
            }

            if (_loadout.Count <= 0)
            {
                return;
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
                case WeaponUpgradeId.Drone:
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

            if (weapon.WeaponId == WeaponUpgradeId.SatelliteBeam)
            {
                if (FireLightning(weapon, out var satelliteBeamDirection))
                {
                    weapon.Cooldown = GetLightningInterval(weapon);
                    SetAimDirection(satelliteBeamDirection);
                }

                return;
            }

            if (weapon.WeaponId == WeaponUpgradeId.Katana && weapon.BurstShotsRemaining > 0)
            {
                weapon.BurstShotCooldown -= Time.deltaTime;
                if (weapon.BurstShotCooldown <= 0f)
                {
                    var slashIndex = Mathf.Max(0, weapon.BurstTotalShots - weapon.BurstShotsRemaining);
                    ExecuteKatanaSlash(weapon, weapon.BurstDirection, slashIndex, Mathf.Max(1, weapon.BurstTotalShots));
                    weapon.BurstShotsRemaining--;
                    weapon.BurstShotCooldown = Mathf.Max(
                        0.01f,
                        ApplyCoreAttackIntervalToValue(GetKatanaComboSlashInterval(), weapon));

                    if (weapon.BurstShotsRemaining <= 0)
                    {
                        weapon.BurstTotalShots = 0;
                        weapon.Cooldown = GetAttackInterval(weapon);
                    }
                }

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
            var count = Mathf.Max(1, _config.smgBurstCount + GetWeaponExtraCount(weapon));
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
            var projectileSpeed = _config.projectileSpeed;
            var projectileLifetime = GetLifetimeCappedByRange(weapon, projectileSpeed, _config.projectileLifetime);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            var bulletCount = Mathf.Max(1, 1 + GetWeaponExtraCount(weapon));
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : _lastAimDirection;
            var spawnCenter = _projectileSpawnResolver != null
                ? _projectileSpawnResolver(normalizedDirection)
                : _owner.position;
            var lateralDirection = new Vector2(-normalizedDirection.y, normalizedDirection.x);
            var shotSpacing = _config != null ? Mathf.Max(0f, _config.rifleParallelShotSpacing) : 0.32f;
            for (var i = 0; i < bulletCount; i++)
            {
                var centeredIndex = i - ((bulletCount - 1) * 0.5f);
                var spawnOffset = lateralDirection * (centeredIndex * shotSpacing);
                SpawnProjectile(
                    weapon.WeaponId,
                    coreElement,
                    coreLevel,
                    normalizedDirection,
                    damage,
                    projectileSpeed,
                    projectileLifetime,
                    _config.projectileHitRadius,
                    1,
                    0f,
                    1f,
                    new Color(1f, 0.95f, 0.35f),
                    spawnCenter + (Vector3)spawnOffset);
            }
        }

        private void FireSmgBullet(WeaponRuntime weapon, Vector2 direction)
        {
            var spread = UnityEngine.Random.Range(-_config.smgSpreadAngle, _config.smgSpreadAngle);
            var spreadDirection = RotateDirection(direction, spread);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.smgShotDamageMultiplier, 0.05f, 2f);
            var projectileSpeed = _config.projectileSpeed * 1.1f;
            var projectileLifetime = GetLifetimeCappedByRange(weapon, projectileSpeed, _config.projectileLifetime * 0.8f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            SpawnProjectile(
                weapon.WeaponId,
                coreElement,
                coreLevel,
                spreadDirection,
                damage,
                projectileSpeed,
                projectileLifetime,
                _config.projectileHitRadius * 0.85f,
                1,
                0f,
                1f,
                new Color(1f, 0.82f, 0.2f));
        }

        private void FireSniper(WeaponRuntime weapon, Vector2 direction)
        {
            var damage = GetWeaponBaseDamage(weapon) * 2f;
            var projectileSpeed = _config.projectileSpeed * 1.6f;
            var projectileLifetime = GetLifetimeCappedByRange(weapon, projectileSpeed, _config.projectileLifetime * 1.25f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            var maxHits = Mathf.Max(1, _config.sniperMaxHits + GetWeaponExtraCount(weapon));
            SpawnProjectile(
                weapon.WeaponId,
                coreElement,
                coreLevel,
                direction,
                damage,
                projectileSpeed,
                projectileLifetime,
                _config.projectileHitRadius * 0.95f,
                maxHits,
                Mathf.Clamp(_config.sniperDamageFalloffPerHit, 0f, 0.9f),
                Mathf.Clamp(_config.sniperMinimumDamageMultiplier, 0.05f, 1f),
                new Color(0.6f, 0.95f, 1f));
        }

        private void FireShotgun(WeaponRuntime weapon, Vector2 direction)
        {
            var pelletCount = Mathf.Max(2, _config.shotgunPelletCount + GetWeaponExtraCount(weapon));
            var spread = Mathf.Max(1f, _config.shotgunSpreadAngle);
            var halfSpread = spread * 0.5f;
            var damagePerPellet = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.shotgunPelletDamageMultiplier, 0.05f, 2f);
            var pelletSpeed = _config.projectileSpeed * 0.95f;
            var pelletLifetime = GetLifetimeCappedByRange(weapon, pelletSpeed, _config.projectileLifetime * 0.75f, rangePaddingMultiplier: 1.25f);
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
                    pelletSpeed,
                    pelletLifetime,
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
                    pelletSpeed,
                    pelletLifetime,
                    _config.projectileHitRadius * 0.9f,
                    1,
                    0f,
                    1f,
                    new Color(1f, 0.65f, 0.2f));
            }
        }

        private void FireKatana(WeaponRuntime weapon, Vector2 direction)
        {
            var totalSlashes = Mathf.Max(1, 1 + GetWeaponExtraCount(weapon));
            weapon.BurstDirection = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : _lastAimDirection;
            weapon.BurstTotalShots = totalSlashes;
            weapon.BurstShotsRemaining = totalSlashes;
            weapon.BurstShotCooldown = 0f;

            ExecuteKatanaSlash(weapon, weapon.BurstDirection, 0, totalSlashes);
            weapon.BurstShotsRemaining--;

            if (weapon.BurstShotsRemaining <= 0)
            {
                weapon.BurstTotalShots = 0;
                weapon.Cooldown = GetAttackInterval(weapon);
                return;
            }

            weapon.BurstShotCooldown = Mathf.Max(
                0.01f,
                ApplyCoreAttackIntervalToValue(GetKatanaComboSlashInterval(), weapon));
        }

        private void ExecuteKatanaSlash(WeaponRuntime weapon, Vector2 direction, int slashIndex, int totalSlashes)
        {
            if (_registry == null || _owner == null || weapon == null)
            {
                return;
            }

            var range = GetWeaponRange(weapon);
            var coneHalfAngle = Mathf.Max(2f, _config.katanaConeAngle) * 0.5f;
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.katanaDamageMultiplier, 0.05f, 3f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            var slashSpreadHalfAngle = totalSlashes <= 1 ? 0f : 10f;
            var origin = (Vector2)_owner.position;
            var t = totalSlashes <= 1 ? 0.5f : slashIndex / (float)(totalSlashes - 1);
            var angleOffset = Mathf.Lerp(-slashSpreadHalfAngle, slashSpreadHalfAngle, t);
            var slashDirection = RotateDirection(direction, angleOffset);
            var searchRadius = range + _registry.GetMaxCollisionRadius();

            SpawnKatanaSlashSpriteFx(origin, slashDirection, range);
            KatanaSlashFxRequested?.Invoke(origin, slashDirection, range);
            Fired?.Invoke(slashDirection);

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

                var angle = Vector2.Angle(slashDirection, toEnemy / centerDistance);
                if (angle <= coneHalfAngle)
                {
                    enemy.ReceiveWeaponDamage(damage, weapon.WeaponId, coreElement, coreLevel);
                }
            }
        }

        private float GetKatanaComboSlashInterval()
        {
            var configured = _config != null ? _config.katanaComboSlashInterval : 0.1f;
            return Mathf.Max(0.01f, configured);
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
            var jumpRange = GetChainJumpRange(weapon, range);
            var maxHits = Mathf.Max(1, _config.chainBaseJumps + GetWeaponExtraCount(weapon));
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
                ChainFxRequested?.Invoke(_fxPoints.ToArray());
            }

            var toFirst = (Vector2)(firstTarget.transform.position - _owner.position);
            var firedDirection = toFirst.sqrMagnitude > 0.000001f ? toFirst.normalized : direction;
            Fired?.Invoke(firedDirection);
        }

        private bool FireLightning(WeaponRuntime weapon, out Vector2 firedDirection)
        {
            firedDirection = _lastAimDirection;
            var range = GetWeaponRange(weapon);
            var origin = (Vector2)_owner.position;
            _candidateEnemies.Clear();
            var limitSq = Mathf.Max(0.01f, range) * Mathf.Max(0.01f, range);
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
                return false;
            }

            var targetCount = Mathf.Max(1, 1 + GetWeaponExtraCount(weapon));
            var hitCount = Mathf.Min(targetCount, _candidateEnemies.Count);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.lightningDamageMultiplier, 0.1f, 5f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            EnemyController firstTarget = null;
            for (var shot = 0; shot < hitCount; shot++)
            {
                var randomIndex = UnityEngine.Random.Range(0, _candidateEnemies.Count);
                var target = _candidateEnemies[randomIndex];
                var lastIndex = _candidateEnemies.Count - 1;
                _candidateEnemies[randomIndex] = _candidateEnemies[lastIndex];
                _candidateEnemies.RemoveAt(lastIndex);

                if (target == null)
                {
                    continue;
                }

                firstTarget ??= target;
                target.ReceiveWeaponDamage(damage, weapon.WeaponId, coreElement, coreLevel);
                var targetCenter = ResolveTargetCenter(target);
                SpawnSatelliteBeamSpriteFx(targetCenter);
                SatelliteBeamFxRequested?.Invoke(targetCenter);
            }

            if (firstTarget == null)
            {
                return false;
            }

            var toTarget = (Vector2)(firstTarget.transform.position - _owner.position);
            firedDirection = toTarget.sqrMagnitude > 0.000001f ? toTarget.normalized : _lastAimDirection;
            Fired?.Invoke(firedDirection);
            return true;
        }

        private void UpdateSatellite(WeaponRuntime weapon)
        {
            var satelliteCount = GetSatelliteCount(weapon);
            EnsureSatelliteVisuals(weapon, satelliteCount);
            if (weapon.SatelliteVisuals.Count <= 0)
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

            var attackRangeMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.AttackRangeMultiplier) : 1f;
            var orbitRadius = Mathf.Max(0.2f, _config.satelliteOrbitRadius) * (1f + (0.02f * tier)) * attackRangeMultiplier;
            var hitRadius = Mathf.Max(0.05f, _config.satelliteHitRadius);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.satelliteDamageMultiplier, 0.05f, 5f);
            var coreElement = GetCoreElement(weapon.WeaponId);
            var coreLevel = GetCoreLevel(weapon.WeaponId);
            var hitCooldown = GetSatelliteHitCooldown(weapon);

            PruneEnemyCooldownMap(weapon.SatelliteHitCooldownUntil);

            for (var satelliteIndex = 0; satelliteIndex < weapon.SatelliteVisuals.Count; satelliteIndex++)
            {
                var satelliteVisual = weapon.SatelliteVisuals[satelliteIndex];
                if (satelliteVisual == null)
                {
                    continue;
                }

                var phase = (360f / Mathf.Max(1, satelliteCount)) * satelliteIndex;
                var angle = (weapon.OrbitAngleDegrees + phase) * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
                var worldPos = (Vector2)_owner.position + offset;
                satelliteVisual.position = new Vector3(worldPos.x, worldPos.y, 0f);

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
                    SatelliteHitFxRequested?.Invoke(enemy.transform.position, hitRadius * 0.9f);
                }
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
            AuraPulseFxRequested?.Invoke(center, range);
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
            var projectileLifetime = GetLifetimeCappedByRange(turretRange, projectileSpeed, Mathf.Max(0.1f, _config.rifleTurretProjectileLifetime), rangePaddingMultiplier: 1.1f);
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
                    SetTurretIdle(turret);
                    turret.ShotCooldown = shotInterval * 0.6f;
                    continue;
                }

                var fireDirection = (Vector2)(target.transform.position - turret.Root.position);
                if (fireDirection.sqrMagnitude <= 0.000001f)
                {
                    SetTurretIdle(turret);
                    turret.ShotCooldown = shotInterval;
                    continue;
                }

                if (turret.Renderer != null && Mathf.Abs(fireDirection.x) > 0.0001f)
                {
                    turret.Renderer.flipX = fireDirection.x < 0f;
                }
                PlayTurretFireAnimation(turret);

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
            var turretWeapon = FindLoadoutWeapon(WeaponUpgradeId.RifleTurret);
            var maxCount = Mathf.Clamp(_config.rifleTurretMaxCount + (turretWeapon != null ? GetWeaponExtraCount(turretWeapon) : 0), 1, 8);
            while (_rifleTurrets.Count >= maxCount)
            {
                DestroyTurretAt(0);
            }

            var turretObject = new GameObject("RifleTurret");
            turretObject.transform.SetParent(null, true);
            turretObject.transform.position = new Vector3(position.x, position.y, 0f);
            turretObject.transform.localScale = Vector3.one;

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(turretObject.transform, false);

            var turretRenderer = visualObject.AddComponent<SpriteRenderer>();
            var turretFrames = RuntimeSpriteFactory.GetSexyTurretAnimationFrames();
            var hasTurretAnimation = turretFrames != null && turretFrames.Length > 0;
            var idleFrame = hasTurretAnimation ? turretFrames[0] : RuntimeSpriteFactory.GetSquareSprite();
            var fireFrames = ExtractFireFrames(turretFrames);
            turretRenderer.sprite = idleFrame;
            turretRenderer.color = Color.white;
            turretRenderer.sortingOrder = 34;
            visualObject.transform.localScale = Vector3.one * Mathf.Max(0.05f, turretVisualScale);

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
                Renderer = turretRenderer,
                IdleFrame = idleFrame,
                FireFrames = fireFrames,
                FireAnimationCoroutine = null,
            });

            TurretDeployed?.Invoke(
                turretObject.transform.position,
                turretRange,
                Mathf.Max(0.1f, _config.rifleTurretLifetime));
        }

        private static Sprite[] ExtractFireFrames(Sprite[] allFrames)
        {
            if (allFrames == null || allFrames.Length <= 1)
            {
                return Array.Empty<Sprite>();
            }

            var fireFrames = new Sprite[allFrames.Length - 1];
            Array.Copy(allFrames, 1, fireFrames, 0, fireFrames.Length);
            return fireFrames;
        }

        private void SetTurretIdle(RifleTurretRuntime turret)
        {
            if (turret == null || turret.Renderer == null || turret.IdleFrame == null)
            {
                return;
            }

            if (turret.FireAnimationCoroutine == null)
            {
                turret.Renderer.sprite = turret.IdleFrame;
            }
        }

        private void PlayTurretFireAnimation(RifleTurretRuntime turret)
        {
            if (turret == null || turret.Renderer == null)
            {
                return;
            }

            if (turret.FireFrames == null || turret.FireFrames.Length <= 0)
            {
                SetTurretIdle(turret);
                return;
            }

            if (turret.FireAnimationCoroutine != null)
            {
                StopCoroutine(turret.FireAnimationCoroutine);
            }

            turret.FireAnimationCoroutine = StartCoroutine(PlayTurretFireAnimationRoutine(turret));
        }

        private System.Collections.IEnumerator PlayTurretFireAnimationRoutine(RifleTurretRuntime turret)
        {
            if (turret == null || turret.Renderer == null)
            {
                yield break;
            }

            var frameDuration = 1f / Mathf.Max(0.1f, turretVisualAnimationFps);
            for (var i = 0; i < turret.FireFrames.Length; i++)
            {
                if (turret.Renderer == null)
                {
                    yield break;
                }

                turret.Renderer.sprite = turret.FireFrames[i];
                yield return new WaitForSeconds(frameDuration);
            }

            if (turret.Renderer != null && turret.IdleFrame != null)
            {
                turret.Renderer.sprite = turret.IdleFrame;
            }

            turret.FireAnimationCoroutine = null;
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
                if (turret.FireAnimationCoroutine != null)
                {
                    StopCoroutine(turret.FireAnimationCoroutine);
                    turret.FireAnimationCoroutine = null;
                }

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

        private void EnsureSatelliteVisuals(WeaponRuntime weapon, int desiredCount)
        {
            if (weapon == null)
            {
                return;
            }

            var clampedCount = Mathf.Clamp(desiredCount, 1, 6);
            while (weapon.SatelliteVisuals.Count < clampedCount)
            {
                weapon.SatelliteVisuals.Add(CreateSatelliteVisual());
            }

            while (weapon.SatelliteVisuals.Count > clampedCount)
            {
                var lastIndex = weapon.SatelliteVisuals.Count - 1;
                var visual = weapon.SatelliteVisuals[lastIndex];
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }

                weapon.SatelliteVisuals.RemoveAt(lastIndex);
            }
        }

        private Transform CreateSatelliteVisual()
        {
            var satelliteRoot = new GameObject("SatelliteVisual");
            satelliteRoot.transform.SetParent(transform, false);

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(satelliteRoot.transform, false);

            var renderer = visualObject.AddComponent<SpriteRenderer>();
            var frames = RuntimeSpriteFactory.GetSexyDroneAnimationFrames();
            var hasAnimation = frames != null && frames.Length > 0;
            renderer.sprite = hasAnimation ? frames[0] : RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = satelliteVisualSortOrder;
            var visualScale = 1.5f;
            visualObject.transform.localScale = Vector3.one * visualScale;
            visualObject.transform.localPosition = GetSpriteCenterAlignOffset(renderer.sprite, visualScale);

            if (hasAnimation && frames.Length > 1)
            {
                var animator = visualObject.AddComponent<SpriteFxAnimator>();
                animator.Initialize(renderer, frames, satelliteVisualAnimationFps, loop: true, destroyOnComplete: false);
            }

            return satelliteRoot.transform;
        }

        private static Vector3 GetSpriteCenterAlignOffset(Sprite sprite, float uniformScale)
        {
            if (sprite == null)
            {
                return Vector3.zero;
            }

            var centerFromPivot = sprite.bounds.center;
            return new Vector3(
                -centerFromPivot.x * uniformScale,
                -centerFromPivot.y * uniformScale,
                0f);
        }

        private int GetSatelliteCount(WeaponRuntime weapon)
        {
            if (_config == null)
            {
                return 2;
            }

            var configuredCount = _config.satelliteBaseCount;
            if (configuredCount <= 0)
            {
                configuredCount = 2;
            }

            return Mathf.Clamp(configuredCount + (weapon != null ? GetWeaponExtraCount(weapon) : 0), 1, 8);
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

        private void SpawnKatanaSlashSpriteFx(Vector2 origin, Vector2 direction, float range)
        {
            var frames = RuntimeSpriteFactory.GetSexySwordAttackAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                return;
            }

            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            var fxObject = new GameObject("KatanaSlashFx");
            fxObject.transform.SetParent(transform, false);

            var forward = Mathf.Max(0.05f, katanaSlashFxForwardOffset);
            var forwardAxis = normalizedDirection;
            var leftAxis = new Vector2(-normalizedDirection.y, normalizedDirection.x);
            var worldOffset = (forwardAxis * katanaSlashFxLocalOffset.x) + (leftAxis * katanaSlashFxLocalOffset.y);
            var fxPosition = origin + (normalizedDirection * forward) + worldOffset;
            fxObject.transform.position = new Vector3(fxPosition.x, fxPosition.y, -0.02f);
            fxObject.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg);
            var scale = Mathf.Max(0.05f, katanaSlashFxScale) * Mathf.Max(0.8f, range * 0.4f);
            fxObject.transform.localScale = Vector3.one * scale;

            var renderer = fxObject.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = Color.white;
            renderer.sortingOrder = 510;

            var animator = fxObject.AddComponent<SpriteFxAnimator>();
            animator.Initialize(renderer, frames, katanaSlashFxFps, loop: false, destroyOnComplete: true);
        }

        private static Vector3 ResolveTargetCenter(EnemyController target)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            var targetCenter = target.transform.position;
            var targetCollider = target.GetComponent<Collider2D>();
            if (targetCollider != null)
            {
                targetCenter = targetCollider.bounds.center;
            }

            return targetCenter;
        }

        private void SpawnSatelliteBeamSpriteFx(Vector3 targetCenter)
        {
            var frames = RuntimeSpriteFactory.GetSexySatelliteBeamAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                return;
            }

            var frame = frames[0];
            var ppu = Mathf.Max(0.0001f, frame.pixelsPerUnit);
            var scale = Mathf.Max(0.05f, satelliteBeamVisualScale);
            var halfHeight = (frame.rect.height / ppu) * 0.5f * scale;
            var yOffset = halfHeight + satelliteBeamVisualYOffset;

            var fxObject = new GameObject("SatelliteBeamFx");
            fxObject.transform.SetParent(transform, false);
            fxObject.transform.position = new Vector3(targetCenter.x, targetCenter.y + yOffset, -0.02f);
            fxObject.transform.localScale = Vector3.one * scale;

            var renderer = fxObject.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = Color.white;
            renderer.sortingOrder = 510;

            if (frames.Length > 1)
            {
                var animator = fxObject.AddComponent<SpriteFxAnimator>();
                animator.Initialize(renderer, frames, satelliteBeamVisualFps, loop: false, destroyOnComplete: true);
                return;
            }

            Destroy(fxObject, Mathf.Max(0.02f, lightningFxDuration));
        }

        private void SpawnTracerFx(Vector3 from, Vector3 to)
        {
            SpawnLineFx(from, to, turretTracerFxColor, turretTracerFxWidth, turretTracerFxDuration, "TurretTracerFx");
            TurretTracerFxRequested?.Invoke(from, to);
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

            var spawnRequest = new ProjectileSpawnRequest(
                weaponId,
                coreElement,
                coreLevel,
                normalizedDirection,
                Mathf.Max(0f, damage),
                Mathf.Max(0.1f, speed),
                Mathf.Max(0.05f, lifetime),
                Mathf.Max(0.05f, hitRadius),
                Mathf.Max(1, maxHits),
                Mathf.Clamp(damageFalloffPerHit, 0f, 0.9f),
                Mathf.Clamp(minimumDamageMultiplier, 0.05f, 1f),
                color,
                spawnPosition,
                Mathf.Max(0.05f, _config.projectileVisualScale));

            if (_projectileSpawnOverride != null && _projectileSpawnOverride.Invoke(spawnRequest))
            {
                Fired?.Invoke(normalizedDirection);
                return;
            }

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
            var intervalMultiplier = GetCombinedAttackIntervalMultiplier(weapon);

            var baseInterval = weapon.WeaponId == WeaponUpgradeId.Rifle
                ? Mathf.Max(0.05f, _config.rifleAttackInterval)
                : Mathf.Max(0.05f, _config.attackInterval);

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

            var nonCoreInterval = baseInterval * intervalMultiplier;
            var withCoreApplied = ApplyCoreAttackIntervalToValue(nonCoreInterval, weapon);
            return Mathf.Max(0.05f, withCoreApplied);
        }

        private float GetLightningInterval(WeaponRuntime weapon)
        {
            return Mathf.Max(0.05f, GetAttackInterval(weapon) * Mathf.Clamp(_config.lightningIntervalMultiplier, 0.1f, 5f));
        }

        private float GetAuraTickInterval(WeaponRuntime weapon)
        {
            var nonCoreInterval = Mathf.Max(0.01f, _config.auraTickInterval) * GetCombinedAttackIntervalMultiplier(weapon);
            return Mathf.Max(0.03f, ApplyCoreAttackIntervalToValue(nonCoreInterval, weapon));
        }

        private float GetSatelliteHitCooldown(WeaponRuntime weapon)
        {
            var nonCoreInterval = Mathf.Max(0.01f, _config.satelliteHitCooldownPerEnemy) * GetCombinedAttackIntervalMultiplier(weapon);
            return Mathf.Max(0.03f, ApplyCoreAttackIntervalToValue(nonCoreInterval, weapon));
        }

        private float GetRifleTurretDeployInterval(WeaponRuntime weapon)
        {
            var nonCoreInterval = Mathf.Max(0.1f, _config.rifleTurretDeployInterval) * GetCombinedAttackIntervalMultiplier(weapon);
            return Mathf.Max(0.1f, ApplyCoreAttackIntervalToValue(nonCoreInterval, weapon));
        }

        private float GetRifleTurretShotInterval(WeaponRuntime weapon)
        {
            return Mathf.Max(0.08f, GetAttackInterval(weapon) * 0.75f);
        }

        private float GetWeaponBaseDamage(WeaponRuntime weapon)
        {
            var weaponLevelMultiplier = GetWeaponDamageMultiplier(weapon);
            var statDamageMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.DamageMultiplier) : 1f;
            var weaponBaseDamage = weapon.WeaponId == WeaponUpgradeId.Rifle
                ? Mathf.Max(0.1f, _config.rifleBaseDamage)
                : Mathf.Max(0.1f, _config.projectileDamage);
            var nonCoreDamage = weaponBaseDamage * statDamageMultiplier * weaponLevelMultiplier;
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
                WeaponCoreElement.Wind => 1f,
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
                1 => 1f / 1.10f,
                2 => 1f / 1.20f,
                _ => 1f / 1.30f,
            };
        }

        private float GetWeaponRange(WeaponRuntime weapon)
        {
            var levelMultiplier = GetWeaponRangeMultiplier(weapon);
            var attackRangeMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.AttackRangeMultiplier) : 1f;

            var baseRange = weapon.WeaponId switch
            {
                WeaponUpgradeId.Rifle => Mathf.Max(0.5f, _config.rifleRange),
                WeaponUpgradeId.Smg => Mathf.Max(0.5f, _config.smgRange),
                WeaponUpgradeId.SniperRifle => Mathf.Max(0.5f, _config.sniperRange),
                WeaponUpgradeId.Shotgun => Mathf.Max(0.5f, _config.shotgunRange),
                WeaponUpgradeId.Katana => Mathf.Max(0.25f, _config.katanaRange),
                WeaponUpgradeId.ChainAttack => Mathf.Max(0.5f, _config.chainAttackRange),
                WeaponUpgradeId.SatelliteBeam => Mathf.Max(0.5f, _config.satelliteBeamRange),
                WeaponUpgradeId.Aura => Mathf.Max(0.2f, _config.auraRadius),
                WeaponUpgradeId.Drone => Mathf.Max(0.2f, _config.droneRange),
                WeaponUpgradeId.RifleTurret => Mathf.Max(0.2f, _config.rifleTurretRange / Mathf.Clamp(_config.rifleTurretRangeMultiplier, 0.1f, 3f)),
                _ => Mathf.Max(0.5f, _config.attackRange),
            };

            return Mathf.Max(0.25f, baseRange * attackRangeMultiplier * levelMultiplier);
        }

        private float GetCombinedAttackIntervalMultiplier(WeaponRuntime weapon)
        {
            var statAttackSpeedMultiplier = _stats != null ? Mathf.Max(0.2f, _stats.AttackIntervalMultiplier) : 1f;
            var weaponAttackSpeedBonus = GetWeaponAttackSpeedBonus(weapon);
            var weaponAttackSpeedMultiplier = 1f / (1f + Mathf.Max(0f, weaponAttackSpeedBonus));
            return statAttackSpeedMultiplier * weaponAttackSpeedMultiplier;
        }

        private static int GetLevelIndex(WeaponRuntime weapon)
        {
            if (weapon == null)
            {
                return 0;
            }

            return Mathf.Clamp(weapon.Level, 1, 10) - 1;
        }

        private static float GetWeaponDamageMultiplier(WeaponRuntime weapon)
        {
            var curve = GetWeaponDamageCurve(weapon != null ? weapon.WeaponId : WeaponUpgradeId.Rifle);
            return curve[GetLevelIndex(weapon)];
        }

        private static float GetWeaponAttackSpeedBonus(WeaponRuntime weapon)
        {
            return CommonWeaponAttackSpeedBonusByLevel[GetLevelIndex(weapon)];
        }

        private static float GetWeaponRangeMultiplier(WeaponRuntime weapon)
        {
            var isAura = weapon != null && weapon.WeaponId == WeaponUpgradeId.Aura;
            return (isAura ? AuraRangeByLevel : CommonWeaponRangeByLevel)[GetLevelIndex(weapon)];
        }

        private static int GetWeaponExtraCount(WeaponRuntime weapon)
        {
            if (weapon == null)
            {
                return 0;
            }

            var index = GetLevelIndex(weapon);
            return weapon.WeaponId switch
            {
                WeaponUpgradeId.Rifle => RifleExtraByLevel[index],
                WeaponUpgradeId.Smg => SmgExtraByLevel[index],
                WeaponUpgradeId.SniperRifle => SniperExtraByLevel[index],
                WeaponUpgradeId.Shotgun => ShotgunExtraByLevel[index],
                WeaponUpgradeId.Katana => KatanaExtraByLevel[index],
                WeaponUpgradeId.ChainAttack => ChainExtraByLevel[index],
                WeaponUpgradeId.SatelliteBeam => SatelliteBeamExtraByLevel[index],
                WeaponUpgradeId.Drone => DroneExtraByLevel[index],
                WeaponUpgradeId.RifleTurret => TurretExtraByLevel[index],
                _ => 0,
            };
        }

        private static float[] GetWeaponDamageCurve(WeaponUpgradeId weaponId)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Rifle => RifleLikeDamageByLevel,
                WeaponUpgradeId.Smg => SmgLikeDamageByLevel,
                WeaponUpgradeId.SniperRifle => SniperDamageByLevel,
                WeaponUpgradeId.Shotgun => SmgLikeDamageByLevel,
                WeaponUpgradeId.Katana => RifleLikeDamageByLevel,
                WeaponUpgradeId.ChainAttack => SmgLikeDamageByLevel,
                WeaponUpgradeId.SatelliteBeam => RifleLikeDamageByLevel,
                WeaponUpgradeId.Drone => SmgLikeDamageByLevel,
                WeaponUpgradeId.RifleTurret => SmgLikeDamageByLevel,
                WeaponUpgradeId.Aura => AuraDamageByLevel,
                _ => RifleLikeDamageByLevel,
            };
        }

        private float GetAuraRange(WeaponRuntime weapon)
        {
            return GetWeaponRange(weapon);
        }

        private float GetRifleTurretRange(WeaponRuntime weapon)
        {
            var baseRange = GetWeaponRange(weapon) * Mathf.Clamp(_config.rifleTurretRangeMultiplier, 0.1f, 3f);
            return Mathf.Max(0.4f, baseRange * (2f / 3f));
        }

        private float GetChainJumpRange(WeaponRuntime weapon, float effectiveChainRange)
        {
            var baseJumpRange = Mathf.Max(0.1f, _config.chainJumpRange);
            var baseChainRange = Mathf.Max(0.1f, _config.chainAttackRange);
            var rangeScale = Mathf.Max(0.1f, effectiveChainRange / baseChainRange);
            return baseJumpRange * rangeScale;
        }

        private float GetLifetimeCappedByRange(WeaponRuntime weapon, float projectileSpeed, float requestedLifetime, float rangePaddingMultiplier = 1f)
        {
            return GetLifetimeCappedByRange(GetWeaponRange(weapon), projectileSpeed, requestedLifetime, rangePaddingMultiplier);
        }

        private float GetLifetimeCappedByRange(float effectiveRange, float projectileSpeed, float requestedLifetime, float rangePaddingMultiplier = 1f)
        {
            var clampedSpeed = Mathf.Max(0.1f, projectileSpeed);
            var clampedRequestedLifetime = Mathf.Max(0.05f, requestedLifetime);
            var clampedRange = Mathf.Max(0.1f, effectiveRange) * Mathf.Max(0.1f, rangePaddingMultiplier);
            var travelDistance = clampedRange * Mathf.Max(0.5f, projectileTravelRangeFactor);
            var lifetimeByRange = travelDistance / clampedSpeed;
            return Mathf.Max(0.05f, Mathf.Min(clampedRequestedLifetime, lifetimeByRange));
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

        private WeaponRuntime FindLoadoutWeapon(WeaponUpgradeId weaponId)
        {
            for (var i = 0; i < _loadout.Count; i++)
            {
                var weapon = _loadout[i];
                if (weapon != null && weapon.WeaponId == weaponId)
                {
                    return weapon;
                }
            }

            return null;
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
                CleanupWeaponRuntimeState(_loadout[i]);
            }
        }

        private static void CleanupWeaponRuntimeState(WeaponRuntime weapon)
        {
            if (weapon == null)
            {
                return;
            }

            for (var visualIndex = 0; visualIndex < weapon.SatelliteVisuals.Count; visualIndex++)
            {
                var visual = weapon.SatelliteVisuals[visualIndex];
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }
            }

            weapon.SatelliteVisuals.Clear();
            weapon.SatelliteHitCooldownUntil.Clear();
        }

        private void OnDrawGizmos()
        {
            if (!showSatelliteHitGizmos || _config == null || _loadout == null || _loadout.Count <= 0)
            {
                return;
            }

            var hitRadius = Mathf.Max(0.05f, _config.satelliteHitRadius);
            Gizmos.color = satelliteHitGizmoColor;

            for (var i = 0; i < _loadout.Count; i++)
            {
                var weapon = _loadout[i];
                if (weapon == null || weapon.WeaponId != WeaponUpgradeId.Drone)
                {
                    continue;
                }

                for (var visualIndex = 0; visualIndex < weapon.SatelliteVisuals.Count; visualIndex++)
                {
                    var visual = weapon.SatelliteVisuals[visualIndex];
                    if (visual == null)
                    {
                        continue;
                    }

                    Gizmos.DrawWireSphere(visual.position, hitRadius);
                }
            }
        }
    }

}
