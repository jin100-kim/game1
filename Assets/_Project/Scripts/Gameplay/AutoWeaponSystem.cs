using System;
using System.Collections.Generic;
using EJR.Game.Audio;
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
        [SerializeField, Min(0.01f)] private float chainFxDuration = 0.25f;
        [SerializeField, Min(0.005f)] private float chainFxWidth = 0.05f;
        [SerializeField] private Color chainFxColor = new(0.45f, 0.85f, 1f, 0.95f);
        [SerializeField, Min(0.01f)] private float lightningFxDuration = 0.1f;
        [SerializeField, Min(0.01f)] private float auraFxDuration = 0.08f;
        [SerializeField, Min(0.005f)] private float auraFxWidth = 0.032f;
        [SerializeField, Min(0.005f)] private float auraIdleWidth = 0.018f;
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
        [SerializeField] private bool showWeaponCollisionGizmos = true;
        [SerializeField] private bool showSatelliteHitGizmos = true;
        [SerializeField] private Color satelliteHitGizmoColor = new(0.35f, 1f, 0.95f, 0.95f);

        private readonly struct BfSwordBladeSnapshot
        {
            public BfSwordBladeSnapshot(Vector2 start, Vector2 end, float bladeRadius, float recordedAt)
            {
                Start = start;
                End = end;
                BladeRadius = bladeRadius;
                RecordedAt = recordedAt;
            }

            public Vector2 Start { get; }
            public Vector2 End { get; }
            public float BladeRadius { get; }
            public float RecordedAt { get; }
        }

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
                BurstOrigin = Vector2.zero;
                OrbitAngleDegrees = UnityEngine.Random.Range(0f, 360f);
            }

            public WeaponUpgradeId WeaponId { get; }
            public int Level { get; set; }
            public float Cooldown { get; set; }
            public int BurstShotsRemaining { get; set; }
            public float BurstShotCooldown { get; set; }
            public Vector2 BurstDirection { get; set; }
            public int BurstTotalShots { get; set; }
            public Vector2 BurstOrigin { get; set; }
            public float OrbitAngleDegrees { get; set; }
            public List<Transform> SatelliteVisuals { get; } = new(3);
            public Dictionary<EnemyController, float> SatelliteHitCooldownUntil { get; } = new();
            public HashSet<EnemyController> BfSwordInsideEnemies { get; } = new();
            public List<BfSwordBladeSnapshot> BfSwordBladeHistory { get; } = new(24);
            public Dictionary<EnemyController, float> BfSwordAfterimageHitCooldownUntil { get; } = new();
            public List<SpriteRenderer> BfSwordAfterimageRenderers { get; } = new(2);
            public List<BatRuntime> BatInstances { get; } = new(4);
            public HashSet<EnemyController> MaceHitEnemies { get; } = new();
            public HashSet<EnemyController> MaceStunnedEnemies { get; } = new();
            public Coroutine ActiveChainCoroutine { get; set; }
            public Transform MaceVisualRoot { get; set; }
            public bool IsMaceSwingActive { get; set; }
            public float MaceSwingElapsed { get; set; }
            public Vector2 MaceSwingDirection { get; set; }
            public float NextBfSwordSoundAt { get; set; }
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

        private sealed class BatRuntime
        {
            public Transform Root;
            public SpriteRenderer Renderer;
            public EnemyController LatchedTarget;
            public float SpawnedAt;
            public float SeekAt;
            public float HitCooldown;
            public float OrbitSeedDegrees;
            public Vector2 LaunchDirection;
            public float PendingHealAmount;
            public int HitsLanded;
            public bool ReturningToOwner;
        }

        private WeaponConfig _config;
        private Transform _owner;
        private PlayerHealth _playerHealth;
        private EnemyRegistry _registry;
        private PlayerStatsRuntime _stats;
        private PlayerBuildRuntime _build;
        private Func<Vector2, Vector3> _projectileSpawnResolver;
        private Func<ProjectileSpawnRequest, bool> _projectileSpawnOverride;
        private Func<Vector2> _facingDirectionResolver;
        private bool _useProjectileBoundsCulling;
        private Rect _projectileCullBounds;
        private float _batOverflowMaxHealthProgress;

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
        private Transform _projectilePoolRoot;
        private LineRenderer _persistentAuraLine;
        private bool _ownerUsesExternalAuraPresentation;
        private float _nextLifestealAt = -999f;
        private const int CollisionGizmoSegments = 24;
        private const float BfSwordHitSoundCooldown = 0.12f;
        private const float BfSwordAfterimageDamageMultiplier = 0.5f;
        private const float BfSwordAfterimageMinorStunDuration = 0.05f;
        private const float BfSwordAfterimageHitCooldown = 0.15f;
        private const float BfSwordAfterimageDelayStep = 0.08f;
        private const float BfSwordAfterimageSnapshotLifetime = 0.35f;
        private const float MaceHandleMinorStunDuration = 0.05f;
        private const float MaceLengthRangeBonusShare = 0.7f;
        private const float MaceHeadRangeBonusShare = 0.3f;

        public event Action<Vector2> AimUpdated;
        public event Action<Vector2> Fired;
        public event Action<ProjectileSpawnRequest> ProjectileVisualRequested;
        public event Action<Vector2, Vector2, float, int> KatanaSlashFxRequested;
        public event Action<Vector3[]> ChainFxRequested;
        public event Action<Vector3, float> AuraPulseFxRequested;
        public event Action<Vector3, float> SatelliteHitFxRequested;
        public event Action<Vector3> SatelliteBeamFxRequested;
        public event Action<Vector3, float, float> TurretDeployed;
        public event Action<Vector3, Vector3> TurretTracerFxRequested;
        public event Action<WeaponSoundRequest> WeaponSoundRequested;

        public void Initialize(
            WeaponConfig config,
            Transform owner,
            EnemyRegistry registry,
            PlayerStatsRuntime stats,
            PlayerHealth playerHealth = null,
            Func<Vector2, Vector3> projectileSpawnResolver = null,
            Func<ProjectileSpawnRequest, bool> projectileSpawnOverride = null,
            Rect? projectileCullBounds = null,
            Func<Vector2> facingDirectionResolver = null)
        {
            _config = config;
            _owner = owner;
            _playerHealth = playerHealth;
            _registry = registry;
            _stats = stats;
            _projectileSpawnResolver = projectileSpawnResolver;
            _projectileSpawnOverride = projectileSpawnOverride;
            _facingDirectionResolver = facingDirectionResolver;
            _useProjectileBoundsCulling = projectileCullBounds.HasValue;
            _projectileCullBounds = projectileCullBounds.GetValueOrDefault();
            _ownerUsesExternalAuraPresentation = false;
            _batOverflowMaxHealthProgress = 0f;
            _nextLifestealAt = -999f;
            _currentTarget = null;
            _lastAimDirection = Vector2.right;
            _targetScanCooldown = 0f;
            EnsureProjectilePool();
        }

        private void RequestWeaponSound(WeaponUpgradeId weaponId, WeaponSoundKind kind, Vector3 worldPosition)
        {
            WeaponSoundRequested?.Invoke(new WeaponSoundRequest(weaponId, kind, worldPosition));
        }

        private Vector3 GetOwnerSoundPosition()
        {
            return _owner != null ? _owner.position : Vector3.zero;
        }

        private void OnDisable()
        {
            CleanupLoadoutRuntimeState();
            ClearRifleTurrets();
            SetPersistentAuraVisible(false);
            _batOverflowMaxHealthProgress = 0f;
            _nextLifestealAt = -999f;
        }

        public void ConfigureLoadout(PlayerBuildRuntime build, PlayerStatsRuntime stats)
        {
            _build = build;
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

            if (build == null || build.OwnedWeapons.Count <= 0)
            {
                CleanupLoadoutRuntimeState();
                ClearRifleTurrets();
                SetPersistentAuraVisible(false);
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
            if (FindLoadoutWeapon(WeaponUpgradeId.Aura) == null)
            {
                SetPersistentAuraVisible(false);
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
                SetPersistentAuraVisible(false);
                return;
            }

            RefreshAimDirection();

            for (var i = 0; i < _loadout.Count; i++)
            {
                var weapon = _loadout[i];
                UpdateWeapon(weapon);
            }

            UpdatePersistentAuraVisual();
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
                case WeaponUpgradeId.SniperRifle:
                    UpdateBatWeapon(weapon);
                    return;
                case WeaponUpgradeId.BfSword:
                    UpdateBfSword(weapon);
                    return;
                case WeaponUpgradeId.SatelliteBeam:
                    UpdateMace(weapon);
                    return;
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
                    weapon.BurstShotCooldown = Mathf.Max(0.01f, Mathf.Max(0.01f, _config.smgBurstShotInterval) * GetCombinedAttackIntervalMultiplier(weapon));
                    if (weapon.BurstShotsRemaining <= 0)
                    {
                        weapon.Cooldown = GetAttackInterval(weapon);
                    }
                }

                return;
            }

            if (weapon.WeaponId == WeaponUpgradeId.Rifle && weapon.BurstShotsRemaining > 0)
            {
                weapon.BurstShotCooldown -= Time.deltaTime;
                if (weapon.BurstShotCooldown <= 0f)
                {
                    FireRifleBurstShot(weapon);
                    weapon.BurstShotsRemaining--;
                    if (weapon.BurstShotsRemaining <= 0)
                    {
                        weapon.BurstTotalShots = 0;
                        weapon.Cooldown = GetAttackInterval(weapon);
                    }
                    else
                    {
                        weapon.BurstShotCooldown = Mathf.Max(
                            0.01f,
                            GetRifleBurstShotInterval() * GetCombinedAttackIntervalMultiplier(weapon));
                    }
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
                        GetKatanaComboSlashInterval() * GetCombinedAttackIntervalMultiplier(weapon));

                    if (weapon.BurstShotsRemaining <= 0)
                    {
                        weapon.BurstTotalShots = 0;
                        weapon.BurstOrigin = Vector2.zero;
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
                case WeaponUpgradeId.Rifle:
                    FireRifle(weapon, fireDirection);
                    break;
                case WeaponUpgradeId.Smg:
                    FireFireball(weapon, fireDirection);
                    weapon.Cooldown = GetAttackInterval(weapon);
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
                    break;
                case WeaponUpgradeId.ChainAttack:
                    FireChainAttack(weapon, fireDirection);
                    weapon.Cooldown = GetAttackInterval(weapon);
                    break;
                default:
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
            weapon.BurstShotCooldown = Mathf.Max(0.01f, Mathf.Max(0.01f, _config.smgBurstShotInterval) * GetCombinedAttackIntervalMultiplier(weapon));
            if (weapon.BurstShotsRemaining <= 0)
            {
                weapon.Cooldown = GetAttackInterval(weapon);
            }
        }

        private void FireRifle(WeaponRuntime weapon, Vector2 direction)
        {
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : _lastAimDirection;
            var baseShotCount = _config != null ? Mathf.Max(1, _config.rifleBaseShotCount) : 2;
            var totalShots = Mathf.Max(1, baseShotCount + GetWeaponExtraCount(weapon));
            weapon.BurstDirection = normalizedDirection;
            weapon.BurstTotalShots = totalShots;
            weapon.BurstShotsRemaining = totalShots;
            weapon.BurstShotCooldown = 0f;

            FireRifleBurstShot(weapon);
            weapon.BurstShotsRemaining--;
            if (weapon.BurstShotsRemaining <= 0)
            {
                weapon.BurstTotalShots = 0;
                weapon.Cooldown = GetAttackInterval(weapon);
                return;
            }

            weapon.BurstShotCooldown = Mathf.Max(
                0.01f,
                GetRifleBurstShotInterval() * GetCombinedAttackIntervalMultiplier(weapon));
        }

        private void FireRifleBurstShot(WeaponRuntime weapon)
        {
            if (weapon == null)
            {
                return;
            }

            var direction = weapon.BurstDirection.sqrMagnitude > 0.000001f ? weapon.BurstDirection.normalized : _lastAimDirection;
            var damage = GetWeaponBaseDamage(weapon);
            var projectileSpeed = Mathf.Max(0.1f, _config.projectileSpeed);
            var projectileLifetime = GetLifetimeCappedByRange(weapon, projectileSpeed, Mathf.Max(0.1f, _config.projectileLifetime));
            var spawnCenter = _projectileSpawnResolver != null
                ? _projectileSpawnResolver(direction)
                : (_owner != null ? _owner.position : Vector3.zero);

            SpawnProjectile(
                weapon.WeaponId,
                direction,
                damage,
                projectileSpeed,
                projectileLifetime,
                _config.projectileHitRadius,
                1,
                0f,
                1f,
                new Color(1f, 0.95f, 0.35f),
                spawnCenter);
            RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, spawnCenter);
            Fired?.Invoke(direction);
        }

        private void FireFireball(WeaponRuntime weapon, Vector2 direction)
        {
            var projectileCount = Mathf.Max(1, 1 + GetWeaponExtraCount(weapon));
            var damage = GetWeaponBaseDamage(weapon);
            var projectileSpeed = Mathf.Max(0.1f, _config.fireballProjectileSpeed);
            var projectileLifetime = GetLifetimeCappedByRange(
                weapon,
                projectileSpeed,
                Mathf.Max(0.1f, _config.fireballProjectileLifetime),
                rangePaddingMultiplier: 1.2f);
            var hitRadius = Mathf.Max(0.05f, _config.fireballProjectileHitRadius);
            var baseDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : _lastAimDirection;
            var soundPosition = _projectileSpawnResolver != null
                ? _projectileSpawnResolver(baseDirection)
                : GetOwnerSoundPosition();
            var ownerPosition = _owner != null ? (Vector2)_owner.position : (Vector2)soundPosition;
            var range = GetWeaponRange(weapon);
            List<EnemyController> reservedTargets = null;
            if (projectileCount > 1)
            {
                reservedTargets = new List<EnemyController>(projectileCount);
                var primaryTarget = FindPreferredAdditionalFireballTarget(ownerPosition, range, baseDirection, reservedTargets);
                if (primaryTarget != null)
                {
                    reservedTargets.Add(primaryTarget);
                }
            }

            for (var i = 0; i < projectileCount; i++)
            {
                var shotDirection = baseDirection;
                if (i > 0)
                {
                    var alternateTarget = FindPreferredAdditionalFireballTarget(ownerPosition, range, baseDirection, reservedTargets);
                    if (alternateTarget != null)
                    {
                        reservedTargets?.Add(alternateTarget);
                        var toTarget = (Vector2)alternateTarget.transform.position - ownerPosition;
                        if (toTarget.sqrMagnitude > 0.000001f)
                        {
                            shotDirection = toTarget.normalized;
                        }
                    }
                }

                SpawnProjectile(
                    weapon.WeaponId,
                    shotDirection,
                    damage,
                    projectileSpeed,
                    projectileLifetime,
                    hitRadius,
                    1,
                    0f,
                    1f,
                    new Color(1f, 0.42f, 0.08f));
            }

            RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, soundPosition);
        }

        private void FireSmgBullet(WeaponRuntime weapon, Vector2 direction)
        {
            var spread = UnityEngine.Random.Range(-_config.smgSpreadAngle, _config.smgSpreadAngle);
            var spreadDirection = RotateDirection(direction, spread);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.smgShotDamageMultiplier, 0.05f, 2f);
            var projectileSpeed = _config.projectileSpeed * 1.1f;
            var projectileLifetime = GetLifetimeCappedByRange(weapon, projectileSpeed, _config.projectileLifetime * 0.8f);
            SpawnProjectile(
                weapon.WeaponId,
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
            var maxHits = Mathf.Max(1, _config.sniperMaxHits + GetWeaponExtraCount(weapon));
            SpawnProjectile(
                weapon.WeaponId,
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
            var soundPosition = _projectileSpawnResolver != null
                ? _projectileSpawnResolver(direction)
                : GetOwnerSoundPosition();

            if (pelletCount == 1)
            {
                SpawnProjectile(
                    weapon.WeaponId,
                    direction,
                    damagePerPellet,
                    pelletSpeed,
                    pelletLifetime,
                    _config.projectileHitRadius * 0.9f,
                    1,
                    0f,
                    1f,
                    new Color(1f, 0.65f, 0.2f));
                RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, soundPosition);
                return;
            }

            for (var i = 0; i < pelletCount; i++)
            {
                var t = pelletCount <= 1 ? 0.5f : i / (float)(pelletCount - 1);
                var angle = Mathf.Lerp(-halfSpread, halfSpread, t);
                var pelletDirection = RotateDirection(direction, angle);
                SpawnProjectile(
                    weapon.WeaponId,
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

            RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, soundPosition);
        }

        private void FireKatana(WeaponRuntime weapon, Vector2 direction)
        {
            var baseSlashCount = _config != null ? Mathf.Max(1, _config.katanaBaseSlashCount) : 2;
            var totalSlashes = Mathf.Max(1, baseSlashCount + GetWeaponExtraCount(weapon));
            weapon.BurstDirection = direction.sqrMagnitude > 0.000001f
                ? direction.normalized
                : _lastAimDirection;
            weapon.BurstTotalShots = totalSlashes;
            weapon.BurstOrigin = _owner != null ? (Vector2)_owner.position : Vector2.zero;
            weapon.BurstShotsRemaining = totalSlashes;
            weapon.BurstShotCooldown = 0f;

            ExecuteKatanaSlash(weapon, weapon.BurstDirection, 0, totalSlashes);
            weapon.BurstShotsRemaining--;

            if (weapon.BurstShotsRemaining <= 0)
            {
                weapon.BurstTotalShots = 0;
                weapon.BurstOrigin = Vector2.zero;
                weapon.Cooldown = GetAttackInterval(weapon);
                return;
            }

            weapon.BurstShotCooldown = Mathf.Max(
                0.01f,
                GetKatanaComboSlashInterval() * GetCombinedAttackIntervalMultiplier(weapon));
        }

        private void UpdateBfSword(WeaponRuntime weapon)
        {
            if (_registry == null || _owner == null || weapon == null || _config == null)
            {
                return;
            }

            GetBfSwordBladeSegment(weapon, out var start, out var end, out var bladeRadius);
            var bladeLength = Vector2.Distance(start, end);
            var searchCenter = (start + end) * 0.5f;
            var searchRadius = (bladeLength * 0.5f) + bladeRadius + _registry.GetMaxCollisionRadius();
            var damage = GetWeaponBaseDamage(weapon);
            var currentTime = Time.time;

            _chainHitEnemies.Clear();
            _registry.GetNearby(searchCenter, searchRadius, _nearbyEnemies);
            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (!IsEnemyUsable(enemy))
                {
                    continue;
                }

                if (!IsInsideBfSwordHitbox(enemy, start, end, bladeRadius))
                {
                    continue;
                }

                _chainHitEnemies.Add(enemy);
                if (weapon.BfSwordInsideEnemies.Contains(enemy))
                {
                    continue;
                }

                weapon.BfSwordInsideEnemies.Add(enemy);
                DealDirectWeaponDamage(enemy, damage, WeaponUpgradeId.BfSword);
                enemy.ApplyStun(Mathf.Max(0.02f, _config.bfSwordStunDuration));
                if (currentTime >= weapon.NextBfSwordSoundAt)
                {
                    RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, enemy.transform.position);
                    weapon.NextBfSwordSoundAt = currentTime + BfSwordHitSoundCooldown;
                }
            }

            _cleanupEnemies.Clear();
            foreach (var enemy in weapon.BfSwordInsideEnemies)
            {
                if (enemy == null || !ContainsEnemy(_chainHitEnemies, enemy))
                {
                    _cleanupEnemies.Add(enemy);
                }
            }

            for (var i = 0; i < _cleanupEnemies.Count; i++)
            {
                weapon.BfSwordInsideEnemies.Remove(_cleanupEnemies[i]);
            }

            _cleanupEnemies.Clear();
            UpdateBfSwordAfterimages(weapon, start, end, bladeRadius, damage, currentTime);
            RecordBfSwordBladeSnapshot(weapon, start, end, bladeRadius, currentTime);
        }

        private void UpdateBfSwordAfterimages(
            WeaponRuntime weapon,
            Vector2 currentStart,
            Vector2 currentEnd,
            float currentBladeRadius,
            float mainDamage,
            float currentTime)
        {
            var afterimageCount = _build != null ? Mathf.Clamp(_build.GetBfSwordAfterimageCount(), 0, 2) : 0;
            if (afterimageCount <= 0)
            {
                for (var i = 0; i < weapon.BfSwordAfterimageRenderers.Count; i++)
                {
                    if (weapon.BfSwordAfterimageRenderers[i] != null)
                    {
                        weapon.BfSwordAfterimageRenderers[i].enabled = false;
                    }
                }

                weapon.BfSwordBladeHistory.Clear();
                weapon.BfSwordAfterimageHitCooldownUntil.Clear();
                return;
            }

            EnsureBfSwordAfterimageRenderers(weapon, afterimageCount);
            CleanupExpiredBfSwordAfterimageHitCooldowns(weapon, currentTime);

            for (var afterimageIndex = 0; afterimageIndex < weapon.BfSwordAfterimageRenderers.Count; afterimageIndex++)
            {
                var renderer = weapon.BfSwordAfterimageRenderers[afterimageIndex];
                if (renderer == null)
                {
                    continue;
                }

                if (afterimageIndex >= afterimageCount ||
                    !TryGetBfSwordAfterimageSnapshot(weapon, currentTime - (BfSwordAfterimageDelayStep * (afterimageIndex + 1)), out var snapshot))
                {
                    renderer.enabled = false;
                    continue;
                }

                UpdateBfSwordAfterimageRenderer(renderer, snapshot, afterimageIndex);

                var bladeLength = Vector2.Distance(snapshot.Start, snapshot.End);
                var searchCenter = (snapshot.Start + snapshot.End) * 0.5f;
                var searchRadius = (bladeLength * 0.5f) + snapshot.BladeRadius + _registry.GetMaxCollisionRadius();
                _registry.GetNearby(searchCenter, searchRadius, _nearbyEnemies);

                for (var enemyIndex = 0; enemyIndex < _nearbyEnemies.Count; enemyIndex++)
                {
                    var enemy = _nearbyEnemies[enemyIndex];
                    if (!IsEnemyUsable(enemy))
                    {
                        continue;
                    }

                    if (IsInsideBfSwordHitbox(enemy, currentStart, currentEnd, currentBladeRadius))
                    {
                        continue;
                    }

                    if (!IsInsideBfSwordHitbox(enemy, snapshot.Start, snapshot.End, snapshot.BladeRadius))
                    {
                        continue;
                    }

                    if (weapon.BfSwordAfterimageHitCooldownUntil.TryGetValue(enemy, out var cooldownUntil) &&
                        cooldownUntil > currentTime)
                    {
                        continue;
                    }

                    weapon.BfSwordAfterimageHitCooldownUntil[enemy] = currentTime + BfSwordAfterimageHitCooldown;
                    DealDirectWeaponDamage(enemy, mainDamage * BfSwordAfterimageDamageMultiplier, WeaponUpgradeId.BfSword);
                    enemy.ApplyMinorStun(BfSwordAfterimageMinorStunDuration);
                }
            }
        }

        private void RecordBfSwordBladeSnapshot(WeaponRuntime weapon, Vector2 start, Vector2 end, float bladeRadius, float currentTime)
        {
            weapon.BfSwordBladeHistory.Add(new BfSwordBladeSnapshot(start, end, bladeRadius, currentTime));
            var cutoffTime = currentTime - BfSwordAfterimageSnapshotLifetime;
            var history = weapon.BfSwordBladeHistory;
            var removeCount = 0;
            while (removeCount < history.Count && history[removeCount].RecordedAt < cutoffTime)
            {
                removeCount++;
            }

            if (removeCount > 0)
            {
                history.RemoveRange(0, removeCount);
            }
        }

        private static bool TryGetBfSwordAfterimageSnapshot(WeaponRuntime weapon, float targetTime, out BfSwordBladeSnapshot snapshot)
        {
            var history = weapon.BfSwordBladeHistory;
            for (var i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].RecordedAt <= targetTime)
                {
                    snapshot = history[i];
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        private void CleanupExpiredBfSwordAfterimageHitCooldowns(WeaponRuntime weapon, float currentTime)
        {
            _cleanupEnemies.Clear();
            foreach (var pair in weapon.BfSwordAfterimageHitCooldownUntil)
            {
                if (pair.Key == null || pair.Value <= currentTime)
                {
                    _cleanupEnemies.Add(pair.Key);
                }
            }

            for (var i = 0; i < _cleanupEnemies.Count; i++)
            {
                weapon.BfSwordAfterimageHitCooldownUntil.Remove(_cleanupEnemies[i]);
            }

            _cleanupEnemies.Clear();
        }

        private void EnsureBfSwordAfterimageRenderers(WeaponRuntime weapon, int activeCount)
        {
            while (weapon.BfSwordAfterimageRenderers.Count < 2)
            {
                var afterimageObject = new GameObject($"BfSwordAfterimageFx{weapon.BfSwordAfterimageRenderers.Count + 1}");
                afterimageObject.transform.SetParent(transform, false);
                var renderer = afterimageObject.AddComponent<SpriteRenderer>();
                renderer.sprite = GetBfSwordAfterimageSprite();
                renderer.enabled = false;
                ApplyBfSwordAfterimageSorting(renderer, weapon.BfSwordAfterimageRenderers.Count);
                weapon.BfSwordAfterimageRenderers.Add(renderer);
            }

            for (var i = 0; i < weapon.BfSwordAfterimageRenderers.Count; i++)
            {
                var renderer = weapon.BfSwordAfterimageRenderers[i];
                if (renderer != null && i >= activeCount)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void UpdateBfSwordAfterimageRenderer(SpriteRenderer renderer, BfSwordBladeSnapshot snapshot, int afterimageIndex)
        {
            if (renderer == null)
            {
                return;
            }

            var alpha = afterimageIndex == 0 ? 0.34f : 0.2f;
            var widthMultiplier = afterimageIndex == 0 ? 0.9f : 0.76f;
            var sprite = GetBfSwordAfterimageSprite();
            renderer.sprite = sprite;
            renderer.color = new Color(0.9f, 0.94f, 1f, alpha);
            ApplyBfSwordAfterimageSorting(renderer, afterimageIndex);

            var direction = snapshot.End - snapshot.Start;
            var bladeLength = Mathf.Max(0.05f, direction.magnitude);
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            var flipX = normalizedDirection.x < 0f;
            var signedAngleFromHorizontal = Mathf.Atan2(normalizedDirection.y, Mathf.Abs(normalizedDirection.x)) * Mathf.Rad2Deg;
            if (flipX)
            {
                signedAngleFromHorizontal = -signedAngleFromHorizontal;
            }

            var visualWorldPosition = ResolveBfSwordAfterimageWorldPosition(normalizedDirection, flipX, signedAngleFromHorizontal, sprite);
            var baseBladeLength = _config != null ? Mathf.Max(0.2f, _config.bfSwordLength) : 1f;
            var desiredWorldSize = (_config != null ? Mathf.Max(0.05f, _config.bfSwordVisualScale) : 0.95f) * (bladeLength / baseBladeLength);
            var spriteBounds = sprite != null ? sprite.bounds.size : Vector3.one;
            var spriteSize = Mathf.Max(0.0001f, Mathf.Max(spriteBounds.x, spriteBounds.y));
            var uniformScale = desiredWorldSize / spriteSize;
            var widthScale = (_config != null ? Mathf.Max(0.05f, _config.bfSwordVisualWidthMultiplier) : 0.5f) * widthMultiplier;

            renderer.flipX = flipX;
            renderer.transform.position = visualWorldPosition;
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, signedAngleFromHorizontal);
            renderer.transform.localScale = new Vector3(uniformScale, uniformScale * widthScale, 1f);
            renderer.enabled = true;
        }

        private static Sprite GetBfSwordAfterimageSprite()
        {
            var frames = RuntimeSpriteFactory.GetSexyBfSwordAnimationFrames();
            return frames != null && frames.Length > 0 ? frames[0] : RuntimeSpriteFactory.GetSquareSprite();
        }

        private Vector3 ResolveBfSwordAfterimageWorldPosition(Vector2 normalizedDirection, bool flipX, float rotationDegrees, Sprite sprite)
        {
            if (_owner == null)
            {
                return Vector3.zero;
            }

            var orbitCenterLocal = ResolveWeaponOrbitCenterLocal();
            var localPosition = WeaponVisualLayoutUtility.CalculateWeaponLocalPosition(
                orbitCenterLocal,
                normalizedDirection,
                _config != null ? Mathf.Max(0f, _config.bfSwordForwardOffset) : 0.48f,
                _config != null ? _config.bfSwordVisualLocalOffset : new Vector2(0f, -0.08f),
                flipX,
                rotationDegrees,
                sprite);
            return _owner.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0f));
        }

        private Vector2 ResolveWeaponOrbitCenterLocal()
        {
            if (_owner == null)
            {
                return Vector2.zero;
            }

            var visual = _owner.Find("Visual");
            if (visual != null)
            {
                var visualRenderer = visual.GetComponent<SpriteRenderer>();
                if (visualRenderer != null)
                {
                    var worldCenter = visualRenderer.bounds.center;
                    var localCenter = _owner.InverseTransformPoint(worldCenter);
                    return new Vector2(localCenter.x, localCenter.y);
                }
            }

            return Vector2.zero;
        }

        private void ApplyBfSwordAfterimageSorting(SpriteRenderer renderer, int afterimageIndex)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sortingOrder = -2 - afterimageIndex;
            if (_owner == null)
            {
                return;
            }

            var ownerRenderers = _owner.GetComponentsInChildren<SpriteRenderer>();
            if (ownerRenderers == null || ownerRenderers.Length <= 0)
            {
                return;
            }

            var sortingLayerId = ownerRenderers[0].sortingLayerID;
            var lowestSortingOrder = ownerRenderers[0].sortingOrder;
            for (var i = 1; i < ownerRenderers.Length; i++)
            {
                var ownerRenderer = ownerRenderers[i];
                if (ownerRenderer == null || ownerRenderer.sortingOrder >= lowestSortingOrder)
                {
                    continue;
                }

                sortingLayerId = ownerRenderer.sortingLayerID;
                lowestSortingOrder = ownerRenderer.sortingOrder;
            }

            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = lowestSortingOrder - 1 - afterimageIndex;
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
            var slashSpreadHalfAngle = totalSlashes <= 1 ? 0f : 10f;
            var origin = weapon.BurstTotalShots > 0 ? weapon.BurstOrigin : (Vector2)_owner.position;
            var t = totalSlashes <= 1 ? 0.5f : slashIndex / (float)(totalSlashes - 1);
            var angleOffset = Mathf.Lerp(-slashSpreadHalfAngle, slashSpreadHalfAngle, t);
            var slashDirection = RotateDirection(direction, angleOffset);
            var searchRadius = range + _registry.GetMaxCollisionRadius();

            SpawnKatanaSlashSpriteFx(origin, slashDirection, range, slashIndex);
            KatanaSlashFxRequested?.Invoke(origin, slashDirection, range, slashIndex);
            RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, origin);
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
                    DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
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
                    DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                }
            }
        }

        private float GetKatanaComboSlashInterval()
        {
            var configured = _config != null ? _config.katanaComboSlashInterval : 0.1f;
            return Mathf.Max(0.01f, configured);
        }

        private Vector2 ResolveFacingDirection()
        {
            if (_facingDirectionResolver != null)
            {
                var resolved = _facingDirectionResolver.Invoke();
                if (resolved.sqrMagnitude > 0.000001f)
                {
                    return resolved.normalized;
                }
            }

            return _lastAimDirection.sqrMagnitude > 0.000001f ? _lastAimDirection.normalized : Vector2.right;
        }

        private float GetBfSwordLength(WeaponRuntime weapon)
        {
            return GetWeaponRange(weapon);
        }

        private float GetBfSwordThickness(WeaponRuntime weapon)
        {
            var thickness = _config != null ? Mathf.Max(0.05f, _config.bfSwordThickness) : 0.55f;
            return thickness * GetBfSwordWidthMultiplier();
        }

        private void GetBfSwordBladeSegment(WeaponRuntime weapon, out Vector2 start, out Vector2 end, out float bladeRadius)
        {
            var facingDirection = ResolveFacingDirection();
            var bladeCenter = GetBfSwordBladeCenter(facingDirection);
            var bladeLength = GetBfSwordLength(weapon);
            bladeRadius = GetBfSwordThickness(weapon) * 0.5f;

            var halfSegment = facingDirection * (bladeLength * 0.5f);
            start = bladeCenter - halfSegment;
            end = bladeCenter + halfSegment;
        }

        private Vector2 GetBfSwordBladeCenter(Vector2 facingDirection)
        {
            var normalizedDirection = facingDirection.sqrMagnitude > 0.000001f ? facingDirection.normalized : Vector2.right;
            var origin = _owner != null ? (Vector2)_owner.position : Vector2.zero;
            var forwardOffset = _config != null ? Mathf.Max(0f, _config.bfSwordForwardOffset) : 0.48f;
            var visualOffset = _config != null ? _config.bfSwordVisualLocalOffset : new Vector2(0f, -0.08f);
            return origin + (normalizedDirection * forwardOffset) + visualOffset;
        }

        private static bool IsInsideBfSwordHitbox(EnemyController enemy, Vector2 start, Vector2 end, float bladeRadius)
        {
            if (enemy == null)
            {
                return false;
            }

            var totalRadius = Mathf.Max(0.01f, bladeRadius + enemy.CollisionRadius);
            return DistancePointToSegment((Vector2)enemy.transform.position, start, end) <= totalRadius;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSq = segment.sqrMagnitude;
            if (lengthSq <= 0.000001f)
            {
                return Vector2.Distance(point, start);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSq);
            var closest = start + (segment * t);
            return Vector2.Distance(point, closest);
        }

        private void FireChainAttack(WeaponRuntime weapon, Vector2 direction)
        {
            var range = GetWeaponRange(weapon);
            var firstTarget = FindNearestUsable(range);
            if (firstTarget == null)
            {
                return;
            }

            if (weapon.ActiveChainCoroutine != null)
            {
                StopCoroutine(weapon.ActiveChainCoroutine);
            }

            RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, GetOwnerSoundPosition());
            weapon.ActiveChainCoroutine = StartCoroutine(ExecuteChainAttackRoutine(weapon, firstTarget, direction));
        }

        private System.Collections.IEnumerator ExecuteChainAttackRoutine(WeaponRuntime weapon, EnemyController firstTarget, Vector2 fallbackDirection)
        {
            var hitHistory = new List<EnemyController>(8);
            var currentDamage = GetWeaponBaseDamage(weapon);
            var decay = _build != null && _build.DoesChainAttackIgnoreDecay()
                ? 0f
                : Mathf.Clamp(_config.chainDamageDecayPerJump, 0f, 0.9f);
            var range = GetWeaponRange(weapon);
            var jumpRange = GetChainJumpRange(weapon, range);
            var maxHits = Mathf.Max(1, _config.chainBaseJumps + GetWeaponExtraCount(weapon));
            var hopDelay = Mathf.Max(0.02f, _config.chainHopDelay);
            var previousPoint = _owner != null ? (Vector2)_owner.position : Vector2.zero;
            var currentTarget = firstTarget;

            for (var hop = 0; hop < maxHits && currentTarget != null; hop++)
            {
                if (!IsEnemyUsable(currentTarget))
                {
                    currentTarget = FindNearestChainTarget(previousPoint, jumpRange, hitHistory, null);
                    if (currentTarget == null)
                    {
                        break;
                    }
                }

                var currentPoint = (Vector2)currentTarget.transform.position;
                SpawnChainBeamFx(previousPoint, currentPoint);
                ChainFxRequested?.Invoke(new[] { (Vector3)previousPoint, (Vector3)currentPoint });
                DealDirectWeaponDamage(currentTarget, currentDamage, weapon.WeaponId);
                if (!ContainsEnemy(hitHistory, currentTarget))
                {
                    hitHistory.Add(currentTarget);
                }

                if (hop == 0)
                {
                    var firedDirection = (currentPoint - previousPoint).sqrMagnitude > 0.000001f
                        ? (currentPoint - previousPoint).normalized
                        : fallbackDirection;
                    Fired?.Invoke(firedDirection);
                }

                previousPoint = currentPoint;
                currentDamage = Mathf.Max(0.1f, currentDamage * (1f - decay));
                if (hop >= maxHits - 1)
                {
                    break;
                }

                yield return new WaitForSeconds(hopDelay);
                currentTarget = FindNearestChainTarget(previousPoint, jumpRange, hitHistory, currentTarget);
            }

            weapon.ActiveChainCoroutine = null;
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
                DealDirectWeaponDamage(target, damage, weapon.WeaponId);
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

            var attackSpeedScale = _stats != null ? Mathf.Max(0.2f, 1f / _stats.AttackIntervalMultiplier) : 1f;
            var weaponAttackSpeedScale = 1f + GetWeaponAttackSpeedBonusPercent(weapon);
            var orbitSpeed = Mathf.Max(30f, _config.satelliteAngularSpeed) * attackSpeedScale * weaponAttackSpeedScale;
            weapon.OrbitAngleDegrees += orbitSpeed * Time.deltaTime;
            if (weapon.OrbitAngleDegrees > 360f)
            {
                weapon.OrbitAngleDegrees -= 360f;
            }

            var attackRangeMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.AttackRangeMultiplier) : 1f;
            var weaponRangeScale = 1f + GetWeaponRangeBonusPercent(weapon);
            var orbitRadius = Mathf.Max(0.2f, _config.satelliteOrbitRadius) * weaponRangeScale * attackRangeMultiplier;
            var hitRadius = Mathf.Max(0.05f, _config.satelliteHitRadius);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.satelliteDamageMultiplier, 0.05f, 5f);
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
                    DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
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

                DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
            }
        }

        private void UpdateBatWeapon(WeaponRuntime weapon)
        {
            var hadBatInstances = weapon.BatInstances.Count > 0;
            UpdateBatInstances(weapon);

            if (hadBatInstances && weapon.BatInstances.Count <= 0)
            {
                weapon.Cooldown = GetAttackInterval(weapon);
            }

            if (weapon.BatInstances.Count > 0)
            {
                return;
            }

            if (weapon.Cooldown > 0f)
            {
                return;
            }

            var batCount = Mathf.Max(1, 1 + GetWeaponExtraCount(weapon));
            var spawnedAny = false;
            for (var i = 0; i < batCount; i++)
            {
                SpawnBatInstance(weapon);
                spawnedAny = true;
            }

            weapon.Cooldown = spawnedAny
                ? GetAttackInterval(weapon)
                : Mathf.Max(0.15f, GetAttackInterval(weapon) * 0.25f);

            if (spawnedAny)
            {
                Fired?.Invoke(_lastAimDirection);
            }
        }

        private void UpdateBatInstances(WeaponRuntime weapon)
        {
            if (_owner == null || weapon == null || weapon.BatInstances.Count <= 0)
            {
                return;
            }

            var attackIntervalMultiplier = Mathf.Max(0.05f, GetCombinedAttackIntervalMultiplier(weapon));
            var attackSpeedFactor = 1f / attackIntervalMultiplier;
            var moveSpeed = Mathf.Max(0.1f, _config.batMoveSpeed * attackSpeedFactor);
            var orbitRadius = Mathf.Max(0.1f, _config.batOrbitRadius);
            var launchDuration = Mathf.Max(0f, _config.batOrbitDuration);
            var latchRange = GetWeaponRange(weapon);
            var hitInterval = Mathf.Max(0.05f, _config.batHitInterval * attackIntervalMultiplier);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.batDamageMultiplier, 0.05f, 5f);
            var hitsBeforeReturn = Mathf.Max(1, _config.batHitsBeforeReturn);

            for (var i = weapon.BatInstances.Count - 1; i >= 0; i--)
            {
                var bat = weapon.BatInstances[i];
                if (bat?.Root == null)
                {
                    weapon.BatInstances.RemoveAt(i);
                    continue;
                }

                if (bat.ReturningToOwner)
                {
                    var toOwner = (Vector2)_owner.position - (Vector2)bat.Root.position;
                    var distance = toOwner.magnitude;
                    if (distance <= 0.18f)
                    {
                        if (bat.PendingHealAmount > 0.001f)
                        {
                            ApplyBatHealing(bat.PendingHealAmount);
                        }

                        Destroy(bat.Root.gameObject);
                        weapon.BatInstances.RemoveAt(i);
                        continue;
                    }

                    bat.Root.position += (Vector3)(toOwner / Mathf.Max(0.0001f, distance)) * (moveSpeed * 1.35f * Time.deltaTime);
                    continue;
                }

                if (bat.HitsLanded >= hitsBeforeReturn)
                {
                    BeginBatReturn(weapon, bat);
                    continue;
                }

                if (bat.LatchedTarget == null || !IsEnemyUsable(bat.LatchedTarget))
                {
                    var previousTarget = bat.LatchedTarget;
                    bat.LatchedTarget = Time.time >= bat.SeekAt
                        ? FindNearestUsableFrom((Vector2)bat.Root.position, latchRange)
                        : null;
                    if (previousTarget == null && bat.LatchedTarget != null)
                    {
                        bat.HitCooldown = Mathf.Max(bat.HitCooldown, Time.time + hitInterval);
                        RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Latch, bat.Root.position);
                    }
                }

                if (bat.LatchedTarget == null)
                {
                    if (Time.time < bat.SpawnedAt + launchDuration)
                    {
                        bat.Root.position += (Vector3)(bat.LaunchDirection * moveSpeed * Time.deltaTime);
                    }
                    else
                    {
                        var orbitAngle = (bat.OrbitSeedDegrees + ((Time.time - bat.SeekAt) * 180f)) * Mathf.Deg2Rad;
                        var orbitTarget = (Vector2)_owner.position + (new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle)) * orbitRadius);
                        var toOrbit = orbitTarget - (Vector2)bat.Root.position;
                        var orbitDistance = toOrbit.magnitude;
                        if (orbitDistance > 0.02f)
                        {
                            bat.Root.position += (Vector3)(toOrbit / Mathf.Max(0.0001f, orbitDistance)) * (moveSpeed * Time.deltaTime);
                        }
                    }

                    continue;
                }

                var latchTargetPosition = (Vector2)bat.LatchedTarget.transform.position;
                var toTarget = latchTargetPosition - (Vector2)bat.Root.position;
                var targetDistance = toTarget.magnitude;
                if (targetDistance > 0.18f)
                {
                    bat.Root.position += (Vector3)(toTarget / Mathf.Max(0.0001f, targetDistance)) * (moveSpeed * Time.deltaTime);
                    continue;
                }

                bat.Root.position = new Vector3(latchTargetPosition.x, latchTargetPosition.y, 0f);
                if (Time.time >= bat.HitCooldown)
                {
                    DealDirectWeaponDamage(bat.LatchedTarget, damage, weapon.WeaponId);
                    bat.PendingHealAmount += Mathf.Max(_config.batMinimumHealPerHit, damage * Mathf.Clamp01(_config.batHealPerDamageMultiplier));
                    bat.HitsLanded++;
                    bat.HitCooldown = Time.time + hitInterval;
                    bat.LatchedTarget = null;
                    if (bat.HitsLanded >= hitsBeforeReturn)
                    {
                        BeginBatReturn(weapon, bat);
                    }
                }
            }
        }

        private void SpawnBatInstance(WeaponRuntime weapon)
        {
            var batObject = new GameObject("Bat");
            batObject.transform.SetParent(transform, false);
            batObject.transform.position = _owner != null ? _owner.position : Vector3.zero;
            batObject.transform.localScale = Vector3.one * Mathf.Max(0.05f, _config.batVisualScale);

            var renderer = batObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(0.88f, 0.24f, 0.72f, 0.95f);
            renderer.sortingOrder = 35;

            weapon.BatInstances.Add(new BatRuntime
            {
                Root = batObject.transform,
                Renderer = renderer,
                LatchedTarget = null,
                SpawnedAt = Time.time,
                SeekAt = Time.time + Mathf.Max(0f, _config.batOrbitDuration),
                HitCooldown = Time.time,
                OrbitSeedDegrees = UnityEngine.Random.Range(0f, 360f),
                LaunchDirection = RotateDirection(Vector2.right, UnityEngine.Random.Range(0f, 360f)).normalized,
                PendingHealAmount = 0f,
                HitsLanded = 0,
                ReturningToOwner = false,
            });
            RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Spawn, batObject.transform.position);
        }

        private void ApplyBatHealing(float healAmount)
        {
            if (_playerHealth == null || healAmount <= 0f)
            {
                return;
            }

            var missingHealth = Mathf.Max(0f, _playerHealth.MaxHealth - _playerHealth.CurrentHealth);
            _playerHealth.Heal(healAmount);
            var overflow = Mathf.Max(0f, healAmount - missingHealth);

            if (overflow <= 0.0001f)
            {
                return;
            }

            _batOverflowMaxHealthProgress += overflow;
            while (_batOverflowMaxHealthProgress >= 20f)
            {
                _build?.AddRuntimeMaxHealthFlat(1f);
                _playerHealth.SetMaxHealth(_playerHealth.MaxHealth + 1f, healDelta: true);
                _batOverflowMaxHealthProgress -= 20f;
            }
        }

        private void BeginBatReturn(WeaponRuntime weapon, BatRuntime bat)
        {
            if (bat == null || bat.ReturningToOwner)
            {
                return;
            }

            bat.ReturningToOwner = true;
            bat.LatchedTarget = null;
            RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Return, bat.Root != null ? bat.Root.position : GetOwnerSoundPosition());
            if (bat.Renderer != null)
            {
                bat.Renderer.color = new Color(0.52f, 1f, 0.72f, 0.95f);
            }
        }

        private void UpdateMace(WeaponRuntime weapon)
        {
            if (weapon.IsMaceSwingActive)
            {
                weapon.MaceSwingElapsed += Time.deltaTime;
                var duration = Mathf.Max(0.05f, _config.maceSwingDuration * GetCombinedAttackIntervalMultiplier(weapon));
                var progress = Mathf.Clamp01(weapon.MaceSwingElapsed / duration);
                UpdateMaceSwingState(weapon, progress);
                if (progress >= 1f)
                {
                    weapon.IsMaceSwingActive = false;
                    weapon.MaceSwingElapsed = 0f;
                    weapon.MaceHitEnemies.Clear();
                    weapon.MaceStunnedEnemies.Clear();
                    if (weapon.MaceVisualRoot != null)
                    {
                        weapon.MaceVisualRoot.gameObject.SetActive(false);
                    }
                }

                return;
            }

            if (weapon.Cooldown > 0f)
            {
                return;
            }

            if (!TryResolveFireDirection(weapon, out var targetDirection))
            {
                return;
            }

            weapon.MaceSwingDirection = targetDirection.sqrMagnitude > 0.000001f ? targetDirection.normalized : Vector2.right;
            weapon.IsMaceSwingActive = true;
            weapon.MaceSwingElapsed = 0f;
            weapon.MaceHitEnemies.Clear();
            weapon.MaceStunnedEnemies.Clear();
            UpdateMaceSwingState(weapon, 0f);
            weapon.Cooldown = GetAttackInterval(weapon);
            Fired?.Invoke(weapon.MaceSwingDirection);
        }

        private void UpdatePersistentAuraVisual()
        {
            if (_ownerUsesExternalAuraPresentation || _owner == null)
            {
                SetPersistentAuraVisible(false);
                return;
            }

            var auraWeapon = FindLoadoutWeapon(WeaponUpgradeId.Aura);
            if (auraWeapon == null)
            {
                SetPersistentAuraVisible(false);
                return;
            }

            EnsurePersistentAuraLine();
            if (_persistentAuraLine == null)
            {
                return;
            }

            var color = auraFxColor;
            color.a = Mathf.Clamp01(color.a * 0.55f);
            WeaponFxRenderer.ConfigureLineRenderer(
                _persistentAuraLine,
                color,
                auraIdleWidth,
                loop: true,
                useWorldSpace: true);
            WeaponFxRenderer.SetCircleLinePositions(
                _persistentAuraLine,
                _owner.position,
                GetAuraRange(auraWeapon),
                ringFxSegments,
                -0.02f);
            _persistentAuraLine.enabled = true;
        }

        private void EnsurePersistentAuraLine()
        {
            if (_persistentAuraLine != null)
            {
                return;
            }

            var auraObject = new GameObject("AuraIdleFx");
            auraObject.transform.SetParent(transform, false);
            _persistentAuraLine = auraObject.AddComponent<LineRenderer>();
            _persistentAuraLine.enabled = false;
        }

        private void SetPersistentAuraVisible(bool isVisible)
        {
            if (_persistentAuraLine == null)
            {
                return;
            }

            _persistentAuraLine.enabled = isVisible;
        }

        private void UpdateMaceSwingState(WeaponRuntime weapon, float progress)
        {
            if (_owner == null || weapon == null)
            {
                return;
            }

            EnsureMaceVisual(weapon);
            if (weapon.MaceVisualRoot == null)
            {
                return;
            }

            weapon.MaceVisualRoot.gameObject.SetActive(true);
            weapon.MaceVisualRoot.position = _owner.position;
            var halfArc = Mathf.Max(5f, _config.maceArcAngle) * 0.5f;
            var swingDirection = RotateDirection(weapon.MaceSwingDirection, Mathf.Lerp(-halfArc, halfArc, progress));
            weapon.MaceVisualRoot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(swingDirection.y, swingDirection.x) * Mathf.Rad2Deg - 90f);

            var range = GetMaceLength(weapon);
            var handle = weapon.MaceVisualRoot.Find("Handle");
            var head = weapon.MaceVisualRoot.Find("Head");
            var headVisualSize = GetMaceVisualHeadSize(weapon);
            var hitRadius = GetMaceHeadHitRadius(weapon);
            if (handle != null)
            {
                handle.localPosition = new Vector3(0f, range * 0.5f, 0f);
                handle.localScale = new Vector3(Mathf.Max(0.05f, _config.maceVisualHandleWidth), Mathf.Max(0.1f, range), 1f);
            }

            if (head != null)
            {
                head.localPosition = new Vector3(0f, range, 0f);
                head.localScale = Vector3.one * headVisualSize;
            }

            var headWorldPosition = weapon.MaceVisualRoot.TransformPoint(new Vector3(0f, range, 0f));
            var handleEndDistance = Mathf.Max(0.1f, range - Mathf.Max(headVisualSize, hitRadius));
            var handleStart = (Vector2)weapon.MaceVisualRoot.position;
            var handleEnd = (Vector2)weapon.MaceVisualRoot.TransformPoint(new Vector3(0f, handleEndDistance, 0f));
            var handleHitRadius = Mathf.Max(_config.maceVisualHandleWidth * 0.5f, hitRadius * 0.3f);
            var damage = GetWeaponBaseDamage(weapon) * Mathf.Clamp(_config.maceDamageMultiplier, 0.05f, 5f);
            var stunDuration = Mathf.Max(0.05f, _config.maceStunDuration) * (1f + (0.25f * (_build != null ? _build.GetWeaponMilestoneCount(weapon.WeaponId) : 0)));

            var handleMidpoint = (handleStart + handleEnd) * 0.5f;
            var handleSearchRadius = Vector2.Distance(handleStart, handleEnd) * 0.5f + handleHitRadius;
            _registry.GetNearby(handleMidpoint, handleSearchRadius + _registry.GetMaxCollisionRadius(), _nearbyEnemies);
            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (!IsEnemyUsable(enemy) || weapon.MaceHitEnemies.Contains(enemy))
                {
                    continue;
                }

                var totalRadius = handleHitRadius + enemy.CollisionRadius;
                if (DistancePointToSegment((Vector2)enemy.transform.position, handleStart, handleEnd) > totalRadius)
                {
                    continue;
                }

                weapon.MaceHitEnemies.Add(enemy);
                DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                enemy.ApplyMinorStun(MaceHandleMinorStunDuration);
            }

            _registry.GetNearby(headWorldPosition, hitRadius + _registry.GetMaxCollisionRadius(), _nearbyEnemies);
            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (!IsEnemyUsable(enemy))
                {
                    continue;
                }

                var limit = hitRadius + enemy.CollisionRadius;
                if (((Vector2)enemy.transform.position - (Vector2)headWorldPosition).sqrMagnitude > limit * limit)
                {
                    continue;
                }

                if (!weapon.MaceHitEnemies.Contains(enemy))
                {
                    weapon.MaceHitEnemies.Add(enemy);
                    DealDirectWeaponDamage(enemy, damage, weapon.WeaponId);
                }

                if (weapon.MaceStunnedEnemies.Contains(enemy))
                {
                    continue;
                }

                weapon.MaceStunnedEnemies.Add(enemy);
                enemy.ApplyStun(stunDuration);
            }
        }

        private void EnsureMaceVisual(WeaponRuntime weapon)
        {
            if (weapon == null || weapon.MaceVisualRoot != null)
            {
                return;
            }

            var root = new GameObject("MaceVisual");
            root.transform.SetParent(transform, false);

            var handleObject = new GameObject("Handle");
            handleObject.transform.SetParent(root.transform, false);
            var handleRenderer = handleObject.AddComponent<SpriteRenderer>();
            handleRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            handleRenderer.color = new Color(0.56f, 0.40f, 0.25f, 1f);
            handleRenderer.sortingOrder = 34;

            var headObject = new GameObject("Head");
            headObject.transform.SetParent(root.transform, false);
            var headRenderer = headObject.AddComponent<SpriteRenderer>();
            headRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            headRenderer.color = new Color(0.82f, 0.82f, 0.88f, 1f);
            headRenderer.sortingOrder = 35;

            weapon.MaceVisualRoot = root.transform;
            weapon.MaceVisualRoot.gameObject.SetActive(false);
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
                RequestWeaponSound(weapon.WeaponId, WeaponSoundKind.Primary, turret.Root.position);
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
            WeaponFxRenderer.ConfigureLineRenderer(rangeRenderer, turretRangeFxColor, 0.03f, true, false);
            WeaponFxRenderer.SetCircleLinePositions(rangeRenderer, Vector3.zero, turretRange, ringFxSegments, 0f);

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

            RequestWeaponSound(WeaponUpgradeId.RifleTurret, WeaponSoundKind.Deploy, turretObject.transform.position);
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

        private EnemyController FindPreferredAdditionalFireballTarget(
            Vector2 origin,
            float maxDistance,
            Vector2 preferredDirection,
            List<EnemyController> excludedTargets)
        {
            if (_registry == null)
            {
                return null;
            }

            var normalizedPreferred = preferredDirection.sqrMagnitude > 0.000001f ? preferredDirection.normalized : Vector2.right;
            var enemies = _registry.Enemies;
            EnemyController bestForward = null;
            EnemyController bestAny = null;
            var bestForwardSq = Mathf.Max(0.01f, maxDistance) * Mathf.Max(0.01f, maxDistance);
            var bestAnySq = bestForwardSq;

            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!IsEnemyUsable(enemy))
                {
                    continue;
                }

                if (excludedTargets != null && ContainsEnemy(excludedTargets, enemy))
                {
                    continue;
                }

                var toEnemy = (Vector2)enemy.transform.position - origin;
                var distanceSq = toEnemy.sqrMagnitude;
                if (distanceSq <= 0.000001f || distanceSq > bestAnySq)
                {
                    continue;
                }

                if (distanceSq < bestAnySq)
                {
                    bestAnySq = distanceSq;
                    bestAny = enemy;
                }

                var alignment = Vector2.Dot(normalizedPreferred, toEnemy.normalized);
                if (alignment < 0.25f || distanceSq >= bestForwardSq)
                {
                    continue;
                }

                bestForwardSq = distanceSq;
                bestForward = enemy;
            }

            return bestForward ?? bestAny;
        }

        private EnemyController FindNearestChainTarget(Vector2 from, float jumpRange, List<EnemyController> hitHistory, EnemyController lastTarget)
        {
            _registry.GetNearby(from, jumpRange + _registry.GetMaxCollisionRadius(), _nearbyEnemies);
            EnemyController bestFresh = null;
            EnemyController bestRepeat = null;
            var bestFreshSq = Mathf.Max(0.01f, jumpRange) * Mathf.Max(0.01f, jumpRange);
            var bestRepeatSq = bestFreshSq;

            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (!IsEnemyUsable(enemy) || ReferenceEquals(enemy, lastTarget))
                {
                    continue;
                }

                var distanceSq = ((Vector2)enemy.transform.position - from).sqrMagnitude;
                if (distanceSq > bestFreshSq)
                {
                    continue;
                }

                if (ContainsEnemy(hitHistory, enemy))
                {
                    if (distanceSq < bestRepeatSq)
                    {
                        bestRepeatSq = distanceSq;
                        bestRepeat = enemy;
                    }

                    continue;
                }

                bestFreshSq = distanceSq;
                bestFresh = enemy;
            }

            return bestFresh ?? bestRepeat;
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
            WeaponFxRenderer.ConfigureLineRenderer(lineRenderer, katanaRangeEffectColor, katanaRangeEffectWidth, false, true);

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

        private void SpawnKatanaSlashSpriteFx(Vector2 origin, Vector2 direction, float range, int slashIndex)
        {
            WeaponFxRenderer.SpawnKatanaSlashFx(
                transform,
                origin,
                direction,
                range,
                slashIndex,
                katanaSlashFxForwardOffset,
                katanaSlashFxLocalOffset,
                katanaSlashFxScale,
                katanaSlashFxFps,
                510);
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
            WeaponFxRenderer.SpawnSatelliteBeamFx(
                transform,
                targetCenter,
                satelliteBeamVisualScale,
                satelliteBeamVisualYOffset,
                satelliteBeamVisualFps,
                lightningFxDuration,
                510);
        }

        private void SpawnChainBeamFx(Vector3 from, Vector3 to)
        {
            WeaponFxRenderer.SpawnStretchBeamFx(
                null,
                from,
                to,
                satelliteBeamVisualScale,
                chainFxDuration,
                chainFxColor,
                chainFxWidth,
                "ChainFx");
        }

        private void SpawnTracerFx(Vector3 from, Vector3 to)
        {
            SpawnLineFx(from, to, turretTracerFxColor, turretTracerFxWidth, turretTracerFxDuration, "TurretTracerFx");
            TurretTracerFxRequested?.Invoke(from, to);
        }

        private void SpawnLineFx(Vector3 from, Vector3 to, Color color, float width, float duration, string name)
        {
            WeaponFxRenderer.SpawnLineFx(transform, from, to, color, width, duration, name);
        }

        private void SpawnRingFx(Vector2 center, float radius, Color color, float width, float duration, string name)
        {
            WeaponFxRenderer.SpawnRingFx(transform, center, radius, ringFxSegments, color, width, duration, name);
        }

        private void SpawnPolylineFx(List<Vector3> points, Color color, float width, float duration, bool loop, string name)
        {
            WeaponFxRenderer.SpawnPolylineFx(transform, points, color, width, duration, loop, name);
        }

        private void ConfigureLineRenderer(LineRenderer lineRenderer, Color color, float width, bool loop, bool useWorldSpace)
        {
            WeaponFxRenderer.ConfigureLineRenderer(lineRenderer, color, width, loop, useWorldSpace);
        }

        private void SetCircleLinePositions(LineRenderer lineRenderer, Vector2 center, float radius, int segments, float z)
        {
            WeaponFxRenderer.SetCircleLinePositions(lineRenderer, center, radius, segments, z);
        }

        private void SpawnProjectile(
            WeaponUpgradeId weaponId,
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
                ProjectileVisualRequested?.Invoke(spawnRequest);
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
                ReturnProjectileToPool,
                TryApplyDirectHitLifesteal,
                _useProjectileBoundsCulling,
                _projectileCullBounds);

            ProjectileVisualRequested?.Invoke(spawnRequest);
            Fired?.Invoke(normalizedDirection);
        }

        public void ClearActiveProjectiles()
        {
            if (_projectilePoolRoot == null)
            {
                return;
            }

            var activeProjectiles = _projectilePoolRoot.GetComponentsInChildren<Projectile>(includeInactive: false);
            for (var i = 0; i < activeProjectiles.Length; i++)
            {
                var projectile = activeProjectiles[i];
                if (projectile == null || !projectile.gameObject.activeSelf)
                {
                    continue;
                }

                ReturnProjectileToPool(projectile);
            }
        }

        private float GetAttackInterval(WeaponRuntime weapon)
        {
            var intervalMultiplier = GetCombinedAttackIntervalMultiplier(weapon);

            var baseInterval = weapon.WeaponId switch
            {
                WeaponUpgradeId.Rifle => Mathf.Max(0.05f, _config.rifleAttackInterval),
                WeaponUpgradeId.Smg => Mathf.Max(0.05f, _config.fireballAttackInterval),
                WeaponUpgradeId.SniperRifle => Mathf.Max(0.05f, _config.batAttackInterval),
                WeaponUpgradeId.BfSword => Mathf.Max(0.01f, _config.bfSwordHitInterval),
                WeaponUpgradeId.SatelliteBeam => Mathf.Max(0.05f, _config.maceAttackInterval),
                _ => Mathf.Max(0.05f, _config.attackInterval),
            };

            if (weapon.WeaponId == WeaponUpgradeId.Shotgun)
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

            return Mathf.Max(0.05f, baseInterval * intervalMultiplier);
        }

        private float GetLightningInterval(WeaponRuntime weapon)
        {
            return Mathf.Max(0.05f, GetAttackInterval(weapon) * Mathf.Clamp(_config.lightningIntervalMultiplier, 0.1f, 5f));
        }

        private float GetAuraTickInterval(WeaponRuntime weapon)
        {
            return Mathf.Max(0.03f, Mathf.Max(0.01f, _config.auraTickInterval) * GetCombinedAttackIntervalMultiplier(weapon));
        }

        private float GetSatelliteHitCooldown(WeaponRuntime weapon)
        {
            return Mathf.Max(0.03f, Mathf.Max(0.01f, _config.satelliteHitCooldownPerEnemy) * GetCombinedAttackIntervalMultiplier(weapon));
        }

        private float GetRifleTurretDeployInterval(WeaponRuntime weapon)
        {
            return Mathf.Max(0.1f, Mathf.Max(0.1f, _config.rifleTurretDeployInterval) * GetCombinedAttackIntervalMultiplier(weapon));
        }

        private float GetRifleTurretShotInterval(WeaponRuntime weapon)
        {
            return Mathf.Max(0.08f, GetAttackInterval(weapon) * 0.75f);
        }

        private float GetWeaponBaseDamage(WeaponRuntime weapon)
        {
            var statDamageMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.DamageMultiplier) : 1f;
            var weaponDamageMultiplier = 1f + GetWeaponDamageBonusPercent(weapon);
            var weaponBaseDamage = weapon.WeaponId switch
            {
                WeaponUpgradeId.Rifle => Mathf.Max(0.1f, _config.rifleBaseDamage),
                WeaponUpgradeId.Smg => Mathf.Max(0.1f, _config.fireballBaseDamage),
                WeaponUpgradeId.SniperRifle => Mathf.Max(0.1f, _config.batBaseDamage),
                WeaponUpgradeId.Shotgun => Mathf.Max(0.1f, _config.shotgunBaseDamage),
                WeaponUpgradeId.Katana => Mathf.Max(0.1f, _config.katanaBaseDamage),
                WeaponUpgradeId.BfSword => Mathf.Max(0.1f, _config.bfSwordBaseDamage),
                WeaponUpgradeId.ChainAttack => Mathf.Max(0.1f, _config.chainAttackBaseDamage),
                WeaponUpgradeId.SatelliteBeam => Mathf.Max(0.1f, _config.maceBaseDamage),
                WeaponUpgradeId.Drone => Mathf.Max(0.1f, _config.droneBaseDamage),
                WeaponUpgradeId.RifleTurret => Mathf.Max(0.1f, _config.rifleTurretBaseDamage),
                WeaponUpgradeId.Aura => Mathf.Max(0.1f, _config.auraBaseDamage),
                _ => Mathf.Max(0.1f, _config.projectileDamage),
            };
            return Mathf.Max(0.1f, weaponBaseDamage * statDamageMultiplier * weaponDamageMultiplier);
        }

        private float GetWeaponRange(WeaponRuntime weapon)
        {
            var attackRangeMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.AttackRangeMultiplier) : 1f;
            var weaponRangeMultiplier = 1f + GetWeaponRangeBonusPercent(weapon);

            var baseRange = weapon.WeaponId switch
            {
                WeaponUpgradeId.Rifle => Mathf.Max(0.5f, _config.rifleRange),
                WeaponUpgradeId.Smg => Mathf.Max(0.5f, _config.fireballRange),
                WeaponUpgradeId.SniperRifle => Mathf.Max(0.5f, _config.batLatchRange),
                WeaponUpgradeId.Shotgun => Mathf.Max(0.5f, _config.shotgunRange),
                WeaponUpgradeId.Katana => Mathf.Max(0.25f, _config.katanaRange),
                WeaponUpgradeId.BfSword => Mathf.Max(0.2f, _config.bfSwordLength),
                WeaponUpgradeId.ChainAttack => Mathf.Max(0.5f, _config.chainAttackRange),
                WeaponUpgradeId.SatelliteBeam => Mathf.Max(0.25f, _config.maceRange),
                WeaponUpgradeId.Aura => Mathf.Max(0.2f, _config.auraRadius),
                WeaponUpgradeId.Drone => Mathf.Max(0.2f, _config.droneRange),
                WeaponUpgradeId.RifleTurret => Mathf.Max(0.2f, _config.rifleTurretRange / Mathf.Clamp(_config.rifleTurretRangeMultiplier, 0.1f, 3f)),
                _ => Mathf.Max(0.5f, _config.attackRange),
            };

            if (weapon != null && weapon.WeaponId == WeaponUpgradeId.BfSword)
            {
                weaponRangeMultiplier *= GetBfSwordLengthMultiplier();
            }
            else if (weapon != null && weapon.WeaponId == WeaponUpgradeId.SatelliteBeam)
            {
                return Mathf.Max(0.25f, GetMaceLength(weapon) + GetMaceHeadHitRadius(weapon));
            }
            else if (weapon != null && weapon.WeaponId == WeaponUpgradeId.Aura)
            {
                weaponRangeMultiplier *= GetAuraRangeMultiplier();
            }

            return Mathf.Max(0.25f, baseRange * attackRangeMultiplier * weaponRangeMultiplier);
        }

        private float GetMaceLength(WeaponRuntime weapon)
        {
            var baseLength = _config != null ? Mathf.Max(0.25f, _config.maceRange) : 1f;
            var scale = GetMaceRangeComponentScale(weapon, MaceLengthRangeBonusShare);
            return Mathf.Max(0.25f, baseLength * scale);
        }

        private float GetMaceHeadHitRadius(WeaponRuntime weapon)
        {
            var baseRadius = _config != null ? Mathf.Max(0.05f, _config.maceHitRadius) : 0.5f;
            var scale = GetMaceRangeComponentScale(weapon, MaceHeadRangeBonusShare);
            return Mathf.Max(0.05f, baseRadius * scale);
        }

        private float GetMaceVisualHeadSize(WeaponRuntime weapon)
        {
            var baseSize = _config != null ? Mathf.Max(0.05f, _config.maceVisualHeadSize) : 0.38f;
            var scale = GetMaceRangeComponentScale(weapon, MaceHeadRangeBonusShare);
            return Mathf.Max(0.05f, baseSize * scale);
        }

        private float GetMaceRangeComponentScale(WeaponRuntime weapon, float bonusShare)
        {
            var attackRangeMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.AttackRangeMultiplier) : 1f;
            var weaponRangeMultiplier = 1f + GetWeaponRangeBonusPercent(weapon);
            var totalScale = Mathf.Max(0.1f, attackRangeMultiplier * weaponRangeMultiplier);
            var delta = totalScale - 1f;
            return Mathf.Max(0.1f, 1f + (delta * Mathf.Clamp01(bonusShare)));
        }

        private float GetCombinedAttackIntervalMultiplier(WeaponRuntime weapon)
        {
            var statAttackSpeedMultiplier = _stats != null ? Mathf.Max(0.2f, _stats.AttackIntervalMultiplier) : 1f;
            var weaponAttackSpeedMultiplier = 1f / (1f + GetWeaponAttackSpeedBonusPercent(weapon));
            return statAttackSpeedMultiplier * weaponAttackSpeedMultiplier;
        }

        private int GetWeaponExtraCount(WeaponRuntime weapon)
        {
            if (weapon == null || _build == null)
            {
                return 0;
            }

            return Mathf.Max(0, _build.GetWeaponExtraCountBonus(weapon.WeaponId));
        }

        private float GetWeaponDamageBonusPercent(WeaponRuntime weapon)
        {
            if (weapon == null || _build == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, _build.GetWeaponDamageBonusPercentTotal(weapon.WeaponId)) / 100f;
        }

        private float GetWeaponAttackSpeedBonusPercent(WeaponRuntime weapon)
        {
            if (weapon == null || _build == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, _build.GetWeaponAttackSpeedBonusPercentTotal(weapon.WeaponId)) / 100f;
        }

        private float GetWeaponRangeBonusPercent(WeaponRuntime weapon)
        {
            if (weapon == null || _build == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, _build.GetWeaponRangeBonusPercentTotal(weapon.WeaponId)) / 100f;
        }

        private float GetBfSwordWidthMultiplier()
        {
            if (_build == null)
            {
                return 1f;
            }

            return Mathf.Max(1f, _build.GetBfSwordWidthMultiplier());
        }

        private float GetBfSwordLengthMultiplier()
        {
            if (_build == null)
            {
                return 1f;
            }

            return Mathf.Max(1f, _build.GetBfSwordLengthMultiplier());
        }

        private float GetAuraRangeMultiplier()
        {
            if (_build == null)
            {
                return 1f;
            }

            return Mathf.Max(1f, _build.GetAuraMilestoneRangeMultiplier());
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

        private void DealDirectWeaponDamage(EnemyController enemy, float damage, WeaponUpgradeId weaponId)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.ReceiveWeaponDamage(damage, weaponId);
            TryApplyDirectHitLifesteal();
        }

        private void TryApplyDirectHitLifesteal()
        {
            if (_build == null || _playerHealth == null)
            {
                return;
            }

            var healPerHit = _build.LifestealHealPerHit;
            if (healPerHit <= 0)
            {
                return;
            }

            var now = Time.time;
            if (now + 0.0001f < _nextLifestealAt)
            {
                return;
            }

            _nextLifestealAt = now + Mathf.Max(0f, _build.LifestealInternalCooldown);
            _playerHealth.Heal(healPerHit);
        }

        private void CleanupLoadoutRuntimeState()
        {
            for (var i = 0; i < _loadout.Count; i++)
            {
                CleanupWeaponRuntimeState(_loadout[i]);
            }
        }

        private void CleanupWeaponRuntimeState(WeaponRuntime weapon)
        {
            if (weapon == null)
            {
                return;
            }

            if (weapon.ActiveChainCoroutine != null)
            {
                StopCoroutine(weapon.ActiveChainCoroutine);
                weapon.ActiveChainCoroutine = null;
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
            weapon.BfSwordInsideEnemies.Clear();
            weapon.BfSwordBladeHistory.Clear();
            weapon.BfSwordAfterimageHitCooldownUntil.Clear();
            weapon.MaceHitEnemies.Clear();
            weapon.MaceStunnedEnemies.Clear();

            for (var afterimageIndex = 0; afterimageIndex < weapon.BfSwordAfterimageRenderers.Count; afterimageIndex++)
            {
                var afterimageRenderer = weapon.BfSwordAfterimageRenderers[afterimageIndex];
                if (afterimageRenderer != null)
                {
                    Destroy(afterimageRenderer.gameObject);
                }
            }

            weapon.BfSwordAfterimageRenderers.Clear();

            for (var batIndex = weapon.BatInstances.Count - 1; batIndex >= 0; batIndex--)
            {
                var bat = weapon.BatInstances[batIndex];
                if (bat?.Root != null)
                {
                    Destroy(bat.Root.gameObject);
                }
            }

            weapon.BatInstances.Clear();

            if (weapon.MaceVisualRoot != null)
            {
                Destroy(weapon.MaceVisualRoot.gameObject);
                weapon.MaceVisualRoot = null;
            }

            weapon.IsMaceSwingActive = false;
            weapon.MaceSwingElapsed = 0f;
            weapon.BurstTotalShots = 0;
            weapon.BurstOrigin = Vector2.zero;
        }

        private void OnDrawGizmos()
        {
            if (!showWeaponCollisionGizmos || _config == null || _loadout == null || _loadout.Count <= 0 || _owner == null)
            {
                return;
            }

            var aimDirection = _lastAimDirection.sqrMagnitude > 0.000001f ? _lastAimDirection.normalized : Vector2.right;

            for (var i = 0; i < _loadout.Count; i++)
            {
                var weapon = _loadout[i];
                if (weapon == null)
                {
                    continue;
                }

                DrawWeaponCollisionGizmo(weapon, aimDirection);
            }
        }

        private void DrawWeaponCollisionGizmo(WeaponRuntime weapon, Vector2 aimDirection)
        {
            var color = GetWeaponCollisionGizmoColor(weapon.WeaponId);
            switch (weapon.WeaponId)
            {
                case WeaponUpgradeId.Rifle:
                    DrawRifleCollisionGizmo(weapon, aimDirection, color);
                    break;
                case WeaponUpgradeId.Smg:
                    DrawSmgCollisionGizmo(weapon, aimDirection, color);
                    break;
                case WeaponUpgradeId.SniperRifle:
                    DrawSingleProjectileCollisionGizmo(weapon, aimDirection, color, _config.projectileHitRadius * 0.95f);
                    break;
                case WeaponUpgradeId.Shotgun:
                    DrawShotgunCollisionGizmo(weapon, aimDirection, color);
                    break;
                case WeaponUpgradeId.Katana:
                    DrawKatanaCollisionGizmo(weapon, aimDirection, color);
                    break;
                case WeaponUpgradeId.BfSword:
                    DrawBfSwordCollisionGizmo(weapon, color);
                    break;
                case WeaponUpgradeId.ChainAttack:
                    DrawChainCollisionGizmo(weapon, color);
                    break;
                case WeaponUpgradeId.SatelliteBeam:
                    DrawCircleCollisionGizmo(_owner.position, GetWeaponRange(weapon), color);
                    break;
                case WeaponUpgradeId.Drone:
                    DrawDroneCollisionGizmo(weapon, color);
                    break;
                case WeaponUpgradeId.RifleTurret:
                    DrawTurretCollisionGizmo(weapon, color);
                    break;
                case WeaponUpgradeId.Aura:
                    DrawCircleCollisionGizmo(_owner.position, GetAuraRange(weapon), color);
                    break;
            }
        }

        private void DrawRifleCollisionGizmo(WeaponRuntime weapon, Vector2 aimDirection, Color color)
        {
            var range = GetWeaponRange(weapon);
            var hitRadius = Mathf.Max(0.02f, _config.projectileHitRadius);
            var spawnCenter = ResolveProjectileGizmoSpawnCenter(aimDirection);
            DrawProjectilePathGizmo(spawnCenter, aimDirection, range, hitRadius, color);
        }

        private void DrawSmgCollisionGizmo(WeaponRuntime weapon, Vector2 aimDirection, Color color)
        {
            var range = GetWeaponRange(weapon);
            var hitRadius = Mathf.Max(0.02f, _config.fireballProjectileHitRadius);
            var projectileCount = Mathf.Max(1, 1 + GetWeaponExtraCount(weapon));
            var spawnCenter = ResolveProjectileGizmoSpawnCenter(aimDirection);
            var ownerPosition = _owner != null ? (Vector2)_owner.position : (Vector2)spawnCenter;
            List<EnemyController> reservedTargets = null;
            if (projectileCount > 1)
            {
                reservedTargets = new List<EnemyController>(projectileCount);
                var primaryTarget = FindPreferredAdditionalFireballTarget(ownerPosition, range, aimDirection, reservedTargets);
                if (primaryTarget != null)
                {
                    reservedTargets.Add(primaryTarget);
                }
            }

            DrawProjectilePathGizmo(spawnCenter, aimDirection, range, hitRadius, color);
            for (var i = 1; i < projectileCount; i++)
            {
                var shotDirection = aimDirection;
                var alternateTarget = FindPreferredAdditionalFireballTarget(ownerPosition, range, aimDirection, reservedTargets);
                if (alternateTarget != null)
                {
                    reservedTargets?.Add(alternateTarget);
                    var toTarget = (Vector2)alternateTarget.transform.position - ownerPosition;
                    if (toTarget.sqrMagnitude > 0.000001f)
                    {
                        shotDirection = toTarget.normalized;
                    }
                }

                DrawProjectilePathGizmo(spawnCenter, shotDirection, range, hitRadius, color);
            }
        }

        private void DrawSingleProjectileCollisionGizmo(WeaponRuntime weapon, Vector2 aimDirection, Color color, float hitRadius)
        {
            DrawProjectilePathGizmo(
                ResolveProjectileGizmoSpawnCenter(aimDirection),
                aimDirection,
                GetWeaponRange(weapon),
                Mathf.Max(0.02f, hitRadius),
                color);
        }

        private void DrawShotgunCollisionGizmo(WeaponRuntime weapon, Vector2 aimDirection, Color color)
        {
            var pelletCount = Mathf.Max(2, _config.shotgunPelletCount + GetWeaponExtraCount(weapon));
            var spread = Mathf.Max(1f, _config.shotgunSpreadAngle);
            var halfSpread = spread * 0.5f;
            var hitRadius = Mathf.Max(0.02f, _config.projectileHitRadius * 0.9f);
            var spawnCenter = ResolveProjectileGizmoSpawnCenter(aimDirection);
            var range = GetWeaponRange(weapon);

            if (pelletCount == 1)
            {
                DrawProjectilePathGizmo(spawnCenter, aimDirection, range, hitRadius, color);
                return;
            }

            for (var i = 0; i < pelletCount; i++)
            {
                var t = pelletCount <= 1 ? 0.5f : i / (float)(pelletCount - 1);
                var angle = Mathf.Lerp(-halfSpread, halfSpread, t);
                DrawProjectilePathGizmo(spawnCenter, RotateDirection(aimDirection, angle), range, hitRadius, color);
            }
        }

        private void DrawKatanaCollisionGizmo(WeaponRuntime weapon, Vector2 aimDirection, Color color)
        {
            var range = GetWeaponRange(weapon);
            var halfAngle = Mathf.Max(2f, _config.katanaConeAngle) * 0.5f;
            var origin = weapon != null && weapon.BurstTotalShots > 0 ? weapon.BurstOrigin : (Vector2)_owner.position;
            DrawConeCollisionGizmo(origin, aimDirection, range, halfAngle, color);
        }

        private float GetRifleBurstShotInterval()
        {
            var configured = _config != null ? _config.rifleBurstShotInterval : 0.08f;
            return Mathf.Max(0.01f, configured);
        }

        private static float GetSpreadAngle(int projectileCount, int projectileIndex, float totalSpreadAngle)
        {
            if (projectileCount <= 1 || totalSpreadAngle <= 0.01f)
            {
                return 0f;
            }

            var halfSpread = totalSpreadAngle * 0.5f;
            var t = projectileCount <= 1 ? 0.5f : projectileIndex / (float)(projectileCount - 1);
            return Mathf.Lerp(-halfSpread, halfSpread, t);
        }

        private void DrawBfSwordCollisionGizmo(WeaponRuntime weapon, Color color)
        {
            GetBfSwordBladeSegment(weapon, out var start, out var end, out var radius);
            DrawCapsuleCollisionGizmo(start, end, radius, color);
        }

        private void DrawChainCollisionGizmo(WeaponRuntime weapon, Color color)
        {
            var range = GetWeaponRange(weapon);
            DrawCircleCollisionGizmo(_owner.position, range, color);
            DrawCircleCollisionGizmo(_owner.position, GetChainJumpRange(weapon, range), new Color(color.r, color.g, color.b, 0.55f));
        }

        private void DrawDroneCollisionGizmo(WeaponRuntime weapon, Color color)
        {
            var orbitRadius = _config != null ? Mathf.Max(0.2f, _config.satelliteOrbitRadius) : 1.2f;
            var hitRadius = Mathf.Max(0.05f, _config.satelliteHitRadius);
            DrawCircleCollisionGizmo(_owner.position, orbitRadius, new Color(color.r, color.g, color.b, 0.4f));

            if (showSatelliteHitGizmos)
            {
                Gizmos.color = satelliteHitGizmoColor;
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

        private void DrawTurretCollisionGizmo(WeaponRuntime weapon, Color color)
        {
            var turretRange = GetRifleTurretRange(weapon);
            for (var i = 0; i < _rifleTurrets.Count; i++)
            {
                var turret = _rifleTurrets[i];
                if (turret == null || turret.Root == null)
                {
                    continue;
                }

                DrawCircleCollisionGizmo(turret.Root.position, turretRange, color);
            }
        }

        private void DrawProjectilePathGizmo(Vector2 start, Vector2 direction, float range, float hitRadius, Color color)
        {
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            var end = start + (normalizedDirection * Mathf.Max(0.1f, range));
            Gizmos.color = color;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(start, hitRadius);
            Gizmos.DrawWireSphere(end, hitRadius);
        }

        private void DrawConeCollisionGizmo(Vector2 origin, Vector2 direction, float range, float halfAngle, Color color)
        {
            var clampedRange = Mathf.Max(0.05f, range);
            var clampedHalfAngle = Mathf.Clamp(halfAngle, 1f, 179f);
            var left = RotateDirection(direction, -clampedHalfAngle);
            var right = RotateDirection(direction, clampedHalfAngle);

            Gizmos.color = color;
            Gizmos.DrawLine(origin, origin + (left * clampedRange));
            Gizmos.DrawLine(origin, origin + (right * clampedRange));

            var previousPoint = origin + (left * clampedRange);
            for (var segmentIndex = 1; segmentIndex <= CollisionGizmoSegments; segmentIndex++)
            {
                var t = segmentIndex / (float)CollisionGizmoSegments;
                var angle = Mathf.Lerp(-clampedHalfAngle, clampedHalfAngle, t);
                var nextPoint = origin + (RotateDirection(direction, angle) * clampedRange);
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }

        private void DrawCapsuleCollisionGizmo(Vector2 start, Vector2 end, float radius, Color color)
        {
            var clampedRadius = Mathf.Max(0.01f, radius);
            var axis = end - start;
            var normalizedAxis = axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector2.right;
            var normal = new Vector2(-normalizedAxis.y, normalizedAxis.x) * clampedRadius;

            var p0 = start + normal;
            var p1 = end + normal;
            var p2 = end - normal;
            var p3 = start - normal;

            Gizmos.color = color;
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p3, p2);
            Gizmos.DrawWireSphere(start, clampedRadius);
            Gizmos.DrawWireSphere(end, clampedRadius);
        }

        private static void DrawCircleCollisionGizmo(Vector3 center, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, Mathf.Max(0.01f, radius));
        }

        private Vector3 ResolveProjectileGizmoSpawnCenter(Vector2 direction)
        {
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            if (_projectileSpawnResolver != null)
            {
                return _projectileSpawnResolver(normalizedDirection);
            }

            return _owner != null ? _owner.position : Vector3.zero;
        }

        private static Color GetWeaponCollisionGizmoColor(WeaponUpgradeId weaponId)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Rifle => new Color(0.45f, 1f, 0.95f, 0.95f),
                WeaponUpgradeId.Smg => new Color(0.9f, 0.95f, 0.35f, 0.95f),
                WeaponUpgradeId.SniperRifle => new Color(1f, 0.65f, 0.35f, 0.95f),
                WeaponUpgradeId.Shotgun => new Color(1f, 0.5f, 0.2f, 0.95f),
                WeaponUpgradeId.Katana => new Color(1f, 0.9f, 0.95f, 0.95f),
                WeaponUpgradeId.BfSword => new Color(0.3f, 1f, 0.3f, 0.95f),
                WeaponUpgradeId.ChainAttack => new Color(0.55f, 0.85f, 1f, 0.95f),
                WeaponUpgradeId.SatelliteBeam => new Color(0.95f, 0.95f, 0.45f, 0.95f),
                WeaponUpgradeId.Drone => new Color(0.45f, 1f, 0.9f, 0.95f),
                WeaponUpgradeId.RifleTurret => new Color(1f, 0.86f, 0.28f, 0.95f),
                WeaponUpgradeId.Aura => new Color(0.45f, 1f, 0.75f, 0.95f),
                _ => new Color(1f, 1f, 1f, 0.95f),
            };
        }
    }

}
