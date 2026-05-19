using System;
using EJR.Game.Core;

using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemyController : MonoBehaviour
    {
        private enum BossPatternState
        {
            None = 0,
            Telegraph = 1,
            Executing = 2,
            Recovery = 3,
        }

        private enum BossPatternActionKind
        {
            None = 0,
            WizardFanVolley = 1,
            WizardSigilField = 2,
            WizardCrossBurst = 3,
            WarriorChargeCombo = 4,
            WarriorGroundSlam = 5,
            FinalMixedVolley = 6,
            FinalChargeCombo = 7,
            FinalGravityNova = 8,
            FinalSummon = 9,
        }

        private enum VariantActionState
        {
            None = 0,
            Windup = 1,
            Executing = 2,
        }


        private const float VariantProjectileHitRadius = 0.12f;
        private const float VariantProjectileVisualScale = 0.4f;
        private const float WaveTargetMushroomCooldown = 10f;
        private const float WaveTargetMushroomAttackStopDuration = 0.55f;
        private const float WaveTargetMushroomBurstInterval = 0.22f;
        private const int WaveTargetMushroomBurstCount = 3;
        private const int WaveTargetMushroomProjectileCount = 6;
        private const float WaveTargetMushroomProjectileSpeed = 4.8f;
        private const float WaveTargetMushroomProjectileLifetime = 2.35f;
        private const float WaveTargetMushroomProjectileDamageMultiplier = 0.85f;
        private const float WaveTargetSkeletonWindupDuration = 0.5f;
        private const float WaveTargetSkeletonDashDuration = 0.675f;
        private const float WaveTargetSkeletonDashSpeedMultiplier = 4.5f;
        private const float WaveTargetSkeletonCooldown = 2.2f;
        private const float WaveTargetSkeletonIntermission = 0.14f;
        private const float MushroomShooterAttackStopDuration = 0.32f;
        private const float VariantBlinkInterval = 0.12f;
        private const float VariantBomberTelegraphLineWidth = 0.09f;
        private const float VariantBomberTelegraphDurationPadding = 0.06f;
        private const float VariantBomberBurstDuration = 0.22f;
        private const float VariantHealPulseLineWidth = 0.08f;
        private const float VariantHealPulseDuration = 0.34f;
        private const float VariantHealBurstDuration = 0.24f;
        private const float BossProjectileLifetime = 4f;
        private const float BossProjectileHitRadius = 0.14f;
        private const float BossProjectileVisualScale = 0.22f;
        private const float BossProjectileDamageMultiplier = 0.8f;
        private const float BossAimFanSpreadDegrees = 12f;
        private const float WizardFanShotInterval = 0.28f;
        private const float WizardCrossBurstInterval = 0.30f;
        private const float WizardSigilDelay = 0.9f;
        private const float WizardSigilRadius = 0.9f;
        private const float WizardSigilRingRadius = 1.15f;
        private const float WizardSigilDamageMultiplier = 1.05f;
        private const float WarriorChargeIntermission = 0.14f;
        private const float FinalChargeIntermission = 0.10f;
        private const float FinalMixedVolleyInterval = 0.30f;
        private const float FinalGravityDuration = 1.05f;
        private const float FinalGravityRadius = 6.5f;
        private const float FinalGravityPulseInterval = 0.50f;
        private const int GroundSlamProjectileCount = 8;
        private const int GravityPulseProjectileCount = 10;
        private const float GroundSlamTelegraphRadius = 2.25f;
        private const float WizardCrossBurstTelegraphRadius = 2.1f;
        private const float BossDashTelegraphLength = 6.5f;
        private const float BossDashTelegraphWidth = 0.12f;
        private const float BossAreaTelegraphWidth = 0.08f;
        private const int BossAreaTelegraphSegments = 48;
        private const float WindKnockbackCooldown = 0.5f;
        private const float MinorStunInternalCooldown = 0.15f;
        private const float RifleMinorStunDuration = 0.04f;
        private const float ShotgunMinorStunDuration = 0.05f;
        private const float KatanaMinorStunDuration = 0.05f;
        private const float ObstacleProbeBaseDistance = 0.35f;
        private const float ObstacleProbeLeadSeconds = 0.24f;
        private const float ObstacleStuckSeconds = 0.22f;
        private const float ObstacleMinProgress = 0.012f;
        private static readonly float[] ObstacleAvoidanceAngles =
        {
            0f, 35f, -35f, 65f, -65f, 95f, -95f, 130f, -130f, 165f, -165f
        };
        private static readonly Color BossDashTelegraphColor = new(1f, 0.28f, 0.22f, 0.78f);
        private static readonly Color BossDashTelegraphHotColor = new(1f, 0.66f, 0.2f, 0.95f);
        private static readonly Color GroundSlamTelegraphColor = new(1f, 0.56f, 0.2f, 0.9f);
        private static readonly Color WizardCrossTelegraphColor = new(0.95f, 0.44f, 1f, 0.9f);
        private static readonly Color GravityTelegraphColor = new(0.42f, 0.88f, 1f, 0.92f);
        private const float StatusIndicatorScale = 0.08f;
        private const float StatusIndicatorHeightOffset = 0.22f;
        private const float StatusIndicatorSpacing = 0.14f;

        private static readonly Color SlowIndicatorColor = new(0.38f, 0.86f, 1f, 0.95f);
        private static readonly Color LightIndicatorColor = new(1f, 0.92f, 0.36f, 0.95f);
        private static readonly Color VariantBomberTelegraphColor = new(1f, 0.44f, 0.18f, 0.94f);
        private static readonly Color VariantBomberExplosionColor = new(1f, 0.58f, 0.24f, 0.98f);
        private static readonly Color VariantHealPulseColor = new(0.38f, 1f, 0.48f, 0.92f);

        public event Action<float, float> Changed;
        public event Action BossProjectileVolleyStarted;
        public event Action BossMinionSummonRequested;
        public event Action<Vector3, Vector2, float, float, float> BossProjectileSpawned;
        public event Action<Vector3, float, float> BossSigilSpawned;
        public static event Action<EnemyController> Defeated;
        public static event Action<EnemyController, WeaponUpgradeId, float> Damaged;

        private EnemyConfig _config;
        private Transform _target;
        private PlayerHealth _playerHealth;
        private Func<Transform> _targetResolver;
        private Func<PlayerHealth> _playerHealthResolver;
        private EnemyRegistry _registry;
        private ExperienceSystem _experienceSystem;
        private Action<Vector3, int> _experienceOrbSpawner;
        private Action<EnemyController, EnemyVariantDefinition> _variantSplitSpawnHandler;
        private Action<EnemyController> _waveTargetSlimeSplitSpawnHandler;
        private RuntimeSpriteFactory.EnemyVisualKind _visualKind;
        private EnemyVariantDefinition _variantDefinition;
        private bool _isWaveTargetBehavior;
        private bool _isBossBehavior;
        private BossArchetypeId _bossArchetype = BossArchetypeId.Final;
        private RunDifficultyDefinition _bossDifficulty;
        private int _generation;
        private int _waveTargetSlimeThresholdIndex = 1;
        private float _waveTargetMushroomCooldown = WaveTargetMushroomCooldown;
        private int _waveTargetMushroomBurstRemaining;
        private float _waveTargetMushroomBurstTimer;
        private float _waveTargetMushroomAttackStopTimer;
        private bool _waveTargetMushroomBurstAudioPlayed;
        private VariantActionState _waveTargetSkeletonActionState;
        private float _waveTargetSkeletonActionTimer;
        private float _waveTargetSkeletonCooldown;
        private float _waveTargetSkeletonIntermissionTimer;
        private int _waveTargetSkeletonDashRemaining;
        private Vector2 _waveTargetSkeletonDashDirection = Vector2.right;


        private float _health;
        private float _maxHealth;
        private float _moveSpeed;
        private float _contactDamage;
        private float _contactDamageCooldown;
        private float _contactCooldown;
        private float _playerCollisionRadius;
        private float _collisionRadius = 0.3f;
        private Rect _arenaBounds;
        private bool _hasArenaBounds;
        private int _experienceOnDeath = 1;
        private EnemySpriteAnimator _spriteAnimator;
        private SpriteRenderer _visualRenderer;
        private bool _isDead;
        private bool _canPassThroughObstacles;
        private Rigidbody2D _rb;
        private CircleCollider2D _collider;

        private float _activeSlowMultiplier = 1f;
        private float _activeSlowRemaining;
        private float _activeLightBonusMultiplier;
        private float _activeLightRemaining;
        private float _stunRemaining;
        private float _minorStunCooldownUntil = -999f;
        private BossPatternState _bossPatternState;
        private BossPatternActionKind _bossCurrentAction;
        private BossPatternActionKind _bossPreviousAction;
        private float _bossPatternCooldown;
        private float _bossStateTimer;
        private Vector2 _bossDashDirection = Vector2.right;
        private float _bossShotTimer;
        private float _bossExecutionTimer;
        private float _bossSecondaryTimer;
        private int _bossRepeatRemaining;
        private int _bossActionStep;
        private bool _bossPullActive;
        private Vector2 _bossPullCenter;
        private float _bossPullRadius;
        private float _bossPullSpeed;

        private float _variantActionTimer;
        private float _variantCooldownTimer;
        private float _variantAttackStopTimer;
        private float _variantBlinkTimer;
        private Vector2 _variantActionDirection = Vector2.right;
        private VariantActionState _variantActionState;
        private Color _variantBaseColor = Color.white;
        private static Material _bossDashTelegraphMaterial;
        private LineRenderer _bossDashTelegraphLine;
        private LineRenderer _bossAreaTelegraphLine;
        private Transform _statusIndicatorRoot;
        private SpriteRenderer _slowIndicatorRenderer;
        private SpriteRenderer _lightIndicatorRenderer;
        private Vector2 _pendingDesiredVector;
        private Vector2 _pendingFallbackDirection;
        private float _pendingMoveSpeedMultiplier = 1f;
        private bool _pendingAllowObstacleSteering = true;
        private UnityEngine.Tilemaps.Tilemap _propsTilemap;
        private UnityEngine.Tilemaps.Tilemap _groundTilemap;
        private int _obstacleMask;
        private ContactFilter2D _obstacleFilter;
        private Vector2 _knockbackVelocity;
        private Vector2 _lastObstacleProbePosition;
        private float _obstacleStuckTimer;
        private int _obstacleAvoidanceSide = 1;
        private readonly RaycastHit2D[] _castResults = new RaycastHit2D[1];

        private readonly System.Collections.Generic.List<EnemyController> _nearbyBuffer = new(24);

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _health;
        public float CollisionRadius => _collisionRadius;
        public RuntimeSpriteFactory.EnemyVisualKind VisualKind => _visualKind;
        public EnemyVariantId VariantId => _variantDefinition?.Id ?? EnemyVariantId.None;
        public int Generation => _generation;
        public bool IsBoss => _isBossBehavior;
        public bool IsDead => _isDead;

        public bool TryGetVariantExplosionHazard(out Vector2 center, out float radius, out float remainingTime)
        {
            center = transform.position;
            radius = 0f;
            remainingTime = 0f;
            return false;
        }

        public bool TryGetBossPullState(out Vector2 center, out float radius, out float speed)
        {
            center = _bossPullCenter;
            radius = _bossPullRadius;
            speed = _bossPullSpeed;
            return _bossPullActive && radius > 0.0001f && speed > 0.0001f;
        }

        public bool HasBossProjectilePressure()
        {
            if (!IsBoss)
            {
                return false;
            }

            if (_bossPatternState != BossPatternState.Telegraph && _bossPatternState != BossPatternState.Executing)
            {
                return false;
            }

            return _bossCurrentAction == BossPatternActionKind.WizardFanVolley
                || _bossCurrentAction == BossPatternActionKind.WizardCrossBurst
                || _bossCurrentAction == BossPatternActionKind.FinalMixedVolley;
        }

        public bool TryGetBossDashHazard(out Vector2 center, out Vector2 direction, out float length, out float width, out float remainingTime)
        {
            center = transform.position;
            direction = Vector2.right;
            length = 0f;
            width = 0f;
            remainingTime = 0f;

            if (!IsBoss ||
                (_bossCurrentAction != BossPatternActionKind.WarriorChargeCombo && _bossCurrentAction != BossPatternActionKind.FinalChargeCombo))
            {
                return false;
            }

            if (_bossPatternState != BossPatternState.Telegraph && _bossPatternState != BossPatternState.Executing)
            {
                return false;
            }

            direction = _bossDashDirection.sqrMagnitude > 0.000001f ? _bossDashDirection.normalized : GetDirectionToPlayer();
            width = BossDashTelegraphWidth;

            if (_bossPatternState == BossPatternState.Telegraph)
            {
                length = BossDashTelegraphLength;
                remainingTime = Mathf.Max(0f, _bossStateTimer);
                return true;
            }

            var dashSpeed = Mathf.Max(0.1f, _moveSpeed) * GetBossDashSpeedMultiplier(_bossCurrentAction);
            length = Mathf.Max(1.5f, dashSpeed * Mathf.Max(0.05f, _bossExecutionTimer));
            remainingTime = Mathf.Max(0f, _bossExecutionTimer);
            return true;
        }

        public bool TryGetBossRadialHazard(out Vector2 center, out float radius, out float remainingTime)
        {
            center = transform.position;
            radius = 0f;
            remainingTime = 0f;

            if (!IsBoss)
            {
                return false;
            }

            switch (_bossCurrentAction)
            {
                case BossPatternActionKind.WarriorGroundSlam:
                    if (_bossPatternState != BossPatternState.Telegraph)
                    {
                        return false;
                    }

                    radius = GroundSlamTelegraphRadius;
                    remainingTime = Mathf.Max(0f, _bossStateTimer);
                    return true;

                case BossPatternActionKind.WizardCrossBurst:
                    if (_bossPatternState != BossPatternState.Telegraph && _bossPatternState != BossPatternState.Executing)
                    {
                        return false;
                    }

                    radius = WizardCrossBurstTelegraphRadius;
                    remainingTime = _bossPatternState == BossPatternState.Telegraph
                        ? Mathf.Max(0f, _bossStateTimer)
                        : Mathf.Max(0f, _bossShotTimer);
                    return true;

                case BossPatternActionKind.FinalGravityNova:
                    if (_bossPatternState != BossPatternState.Telegraph && _bossPatternState != BossPatternState.Executing)
                    {
                        return false;
                    }

                    radius = FinalGravityRadius;
                    remainingTime = _bossPatternState == BossPatternState.Telegraph
                        ? Mathf.Max(0f, _bossStateTimer)
                        : Mathf.Max(0f, _bossExecutionTimer + (_bossActionStep > 0 ? _bossSecondaryTimer : 0f));
                    return true;
            }

            return false;
        }

        public void Initialize(
            EnemyConfig config,
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            EnemyStatProfile statProfile,
            EnemyAnimationProfile animationProfile,
            Transform target,
            PlayerHealth playerHealth,
            EnemyRegistry registry,
            ExperienceSystem experienceSystem,
            float playerCollisionRadius,
            float collisionRadius,
            float runtimeHealthMultiplier = 1f,
            float runtimeMoveSpeedMultiplier = 1f,
            float runtimeContactDamageMultiplier = 1f,
            bool isBossBehavior = false,
            bool hasArenaBounds = false,
            Rect arenaBounds = default,
            BossArchetypeId bossArchetype = BossArchetypeId.Final,
            RunDifficultyDefinition bossDifficulty = null,
            UnityEngine.Tilemaps.Tilemap groundTilemap = null,
            UnityEngine.Tilemaps.Tilemap propsTilemap = null)
        {
            _config = config;
            _visualKind = visualKind;
            _isBossBehavior = isBossBehavior;
            _target = target;
            _playerHealth = playerHealth;
            _registry = registry;
            _experienceSystem = experienceSystem;
            _groundTilemap = groundTilemap;
            _propsTilemap = propsTilemap;

            _canPassThroughObstacles = animationProfile != null && animationProfile.canPassThroughObstacles;

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody2D>();
            }
            
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            if (_propsTilemap != null) 
            {
                _obstacleMask = 1 << _propsTilemap.gameObject.layer;
                
                _obstacleFilter = new ContactFilter2D();
                _obstacleFilter.SetLayerMask(_obstacleMask);
                _obstacleFilter.useLayerMask = true;
                _obstacleFilter.useTriggers = false;
            }

            _collider = GetComponent<CircleCollider2D>();
            if (_collider == null)
            {
                _collider = gameObject.AddComponent<CircleCollider2D>();
            }
            
            _collider.radius = Mathf.Max(0.05f, collisionRadius);
            // 모든 몬스터는 이제 맵 안에서 생성되므로, 설정에 따라 고정된 충돌 속성을 가짐
            _collider.isTrigger = _canPassThroughObstacles;
            _collider.enabled = true;
            _lastObstacleProbePosition = transform.position;
            _obstacleStuckTimer = 0f;
            _obstacleAvoidanceSide = (GetInstanceID() & 1) == 0 ? 1 : -1;

            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
            _collisionRadius = Mathf.Max(0.05f, collisionRadius);
            _hasArenaBounds = hasArenaBounds;
            _arenaBounds = arenaBounds;
            _bossArchetype = bossArchetype;
            _bossDifficulty = bossDifficulty ?? SharedRunCatalog.GetDifficulty(SharedRunCatalog.DefaultDifficultyId);
            _spriteAnimator = GetComponentInChildren<EnemySpriteAnimator>();
            _visualRenderer = GetComponentInChildren<SpriteRenderer>();

            var healthMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.healthMultiplier) : 1f;
            var moveMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.moveSpeedMultiplier) : 1f;
            var contactDamageMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.contactDamageMultiplier) : 1f;
            var experienceMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.experienceMultiplier) : 1f;
            var elapsedHealthMultiplier = Mathf.Max(0.1f, runtimeHealthMultiplier);
            var elapsedMoveMultiplier = Mathf.Max(0.1f, runtimeMoveSpeedMultiplier);
            var elapsedContactDamageMultiplier = Mathf.Max(0.1f, runtimeContactDamageMultiplier);

            _maxHealth = Mathf.Max(1f, config.maxHealth * healthMultiplier * elapsedHealthMultiplier);
            _moveSpeed = Mathf.Max(0.1f, config.moveSpeed * moveMultiplier * elapsedMoveMultiplier);
            _contactDamage = Mathf.Max(0f, config.contactDamage * contactDamageMultiplier * elapsedContactDamageMultiplier);
            _contactDamageCooldown = Mathf.Max(0.05f, config.contactDamageCooldown);
            _experienceOnDeath = Mathf.Max(1, Mathf.RoundToInt(config.experienceOnDeath * experienceMultiplier));

            _health = _maxHealth;
            _bossPatternState = BossPatternState.None;
            _bossCurrentAction = BossPatternActionKind.None;
            _bossPreviousAction = BossPatternActionKind.None;
            _bossPatternCooldown = IsBoss ? GetBossPatternCooldown() : float.MaxValue;
            _bossStateTimer = 0f;
            _bossDashDirection = Vector2.right;
            _bossShotTimer = 0f;
            _bossExecutionTimer = 0f;
            _bossSecondaryTimer = 0f;
            _bossRepeatRemaining = 0;
            _bossActionStep = 0;
            ClearBossPullState();

            _variantActionTimer = 0f;
            _variantCooldownTimer = 0f;
            _variantAttackStopTimer = 0f;
            _variantBlinkTimer = 0f;
            _variantActionDirection = Vector2.right;
            _variantActionState = VariantActionState.None;
            _variantBaseColor = Color.white;
            _isWaveTargetBehavior = false;
            _waveTargetSlimeSplitSpawnHandler = null;
            _waveTargetSlimeThresholdIndex = 1;
            _waveTargetMushroomCooldown = WaveTargetMushroomCooldown;
            _waveTargetMushroomBurstRemaining = 0;
            _waveTargetMushroomBurstTimer = 0f;
            _waveTargetMushroomAttackStopTimer = 0f;
            _waveTargetMushroomBurstAudioPlayed = false;
            _waveTargetSkeletonActionState = VariantActionState.None;
            _waveTargetSkeletonActionTimer = 0f;
            _waveTargetSkeletonCooldown = WaveTargetSkeletonCooldown;
            _waveTargetSkeletonIntermissionTimer = 0f;
            _waveTargetSkeletonDashRemaining = 0;
            _waveTargetSkeletonDashDirection = Vector2.right;
            _registry.Register(this);
            Changed?.Invoke(_health, _maxHealth);
        }

        public void ConfigureVariant(EnemyVariantDefinition variantDefinition, Action<EnemyController, EnemyVariantDefinition> splitSpawnHandler = null, int generation = 0)
        {
            _variantDefinition = variantDefinition;
            _variantSplitSpawnHandler = splitSpawnHandler;
            _generation = generation;

            _variantActionTimer = 0f;
            _variantCooldownTimer = variantDefinition != null
                ? UnityEngine.Random.Range(0f, Mathf.Max(0.05f, variantDefinition.AttackCooldown * 0.35f))
                : 0f;
            _variantAttackStopTimer = 0f;
            _variantBlinkTimer = 0f;
            _variantActionDirection = Vector2.right;
            _variantActionState = VariantActionState.None;
            _visualRenderer ??= GetComponentInChildren<SpriteRenderer>();
            _variantBaseColor = variantDefinition != null ? variantDefinition.TintColor : Color.white;
            ApplyVariantBaseColor();
        }

        public void ConfigureWaveTargetBehavior(Action<EnemyController> slimeSplitSpawnHandler)
        {
            _isWaveTargetBehavior = true;
            _waveTargetSlimeSplitSpawnHandler = slimeSplitSpawnHandler;
            _waveTargetSlimeThresholdIndex = 1;
            _waveTargetMushroomCooldown = WaveTargetMushroomCooldown;
            _waveTargetMushroomBurstRemaining = 0;
            _waveTargetMushroomBurstTimer = 0f;
            _waveTargetMushroomAttackStopTimer = 0f;
            _waveTargetMushroomBurstAudioPlayed = false;
            _waveTargetSkeletonActionState = VariantActionState.None;
            _waveTargetSkeletonActionTimer = 0f;
            _waveTargetSkeletonCooldown = WaveTargetSkeletonCooldown;
            _waveTargetSkeletonIntermissionTimer = 0f;
            _waveTargetSkeletonDashRemaining = 0;
            _waveTargetSkeletonDashDirection = Vector2.right;
        }

        public void SetTargetResolver(Func<Transform> targetResolver, Func<PlayerHealth> playerHealthResolver)
        {
            _targetResolver = targetResolver;
            _playerHealthResolver = playerHealthResolver;
            RefreshResolvedTarget();
        }

        public void SetExperienceOrbSpawner(Action<Vector3, int> experienceOrbSpawner)
        {
            _experienceOrbSpawner = experienceOrbSpawner;
        }

        private void OnDisable()
        {
            if (_registry != null)
            {
                _registry.Unregister(this);
            }

            ClearBossPullState();
            HideBossDashTelegraphFx();
        }

        private void Update()
        {


            RefreshResolvedTarget();

            if (_isDead || _target == null || _config == null || _playerHealth == null)
            {
                _pendingDesiredVector = Vector2.zero;
                _pendingFallbackDirection = Vector2.right;
                _pendingMoveSpeedMultiplier = 1f;
                _pendingAllowObstacleSteering = true;
                return;
            }

            if (DebugSessionService.IsMonsterLabTimePaused)
            {
                _pendingDesiredVector = Vector2.zero;
                _pendingFallbackDirection = Vector2.right;
                _pendingMoveSpeedMultiplier = 1f;
                _pendingAllowObstacleSteering = true;
                _spriteAnimator?.SetMotion(Vector2.zero);
                return;
            }

            TickCoreEffectDurations();
            var isStunned = _stunRemaining > 0f;
            _pendingDesiredVector = Vector2.zero;
            _pendingFallbackDirection = Vector2.right;
            _pendingMoveSpeedMultiplier = 1f;
            _pendingAllowObstacleSteering = true;

            var handledByWaveTarget = UpdateWaveTargetBehavior(Time.deltaTime, isStunned);
            var handledByBossPattern = !handledByWaveTarget && UpdateBossPattern(Time.deltaTime);
            var handledByVariant = !handledByWaveTarget && !handledByBossPattern && UpdateVariantBehavior(Time.deltaTime, isStunned);
            
            if (!handledByWaveTarget && !handledByBossPattern && !handledByVariant && !isStunned)
            {
                var toPlayer = _target.position - transform.position;
                var distance = toPlayer.magnitude;
                var direction = distance > 0.001f ? (Vector2)(toPlayer / distance) : Vector2.zero;
                var minimumSeparation = CollisionRadius + _playerCollisionRadius;
                var separation = ComputeSeparationVector((Vector2)transform.position) * Mathf.Max(0f, _config.separationWeight);

                var desired = separation;
                if (distance > minimumSeparation)
                {
                    desired += direction;
                }
                
                _pendingDesiredVector = desired;
                _pendingFallbackDirection = direction;
            }

            _contactCooldown -= Time.deltaTime;
            var minimumSeparationForContact = CollisionRadius + _playerCollisionRadius;
            var currentDistance = (_target.position - transform.position).magnitude;
            if (!isStunned && currentDistance <= minimumSeparationForContact + 0.02f && _contactCooldown <= 0f)
            {
                _contactCooldown = _contactDamageCooldown;
                _playerHealth.TakeDamage(_contactDamage);
            }
        }

        private void FixedUpdate()
        {
            if (_isDead || _config == null) return;
            MoveUsingDesiredVector(_pendingDesiredVector, _pendingFallbackDirection, Time.fixedDeltaTime);
            if (_spriteAnimator != null)
            {
                _spriteAnimator.SetMotion(_rb != null ? _rb.linearVelocity : Vector2.zero);
            }
        }

        private Vector3 ResolvePlayerOverlap(Vector3 candidatePosition, float minimumSeparation, Vector2 fallbackDirection)
        {
            var toPlayer = (Vector2)_target.position - (Vector2)candidatePosition;
            var distance = toPlayer.magnitude;
            if (distance >= minimumSeparation)
            {
                return candidatePosition;
            }

            var away = distance > 0.0001f ? -toPlayer / distance : -fallbackDirection;
            if (away.sqrMagnitude <= 0.0001f)
            {
                away = Vector2.right;
            }

            var corrected = (Vector2)_target.position + away * minimumSeparation;
            return new Vector3(corrected.x, corrected.y, 0f);
        }

        private Vector2 ComputeSeparationVector(Vector2 selfPosition)
        {
            if (_registry == null || _config == null)
            {
                return Vector2.zero;
            }

            var separation = Vector2.zero;
            var rangeMultiplier = Mathf.Max(1f, _config.separationRangeMultiplier);
            var overlapPadding = Mathf.Max(0f, _config.overlapResolvePadding);
            var searchRadius = (CollisionRadius * rangeMultiplier) + _registry.GetMaxCollisionRadius() + overlapPadding;
            _registry.GetNearby(selfPosition, searchRadius, _nearbyBuffer);
            var neighbors = _nearbyBuffer;

            for (var i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                if (neighbor == null || ReferenceEquals(neighbor, this))
                {
                    continue;
                }

                var toNeighbor = (Vector2)neighbor.transform.position - selfPosition;
                var centerDistance = toNeighbor.magnitude;
                if (centerDistance <= 0.0001f)
                {
                    separation += (Vector2)UnityEngine.Random.insideUnitCircle.normalized;
                    continue;
                }

                var combinedRadius = CollisionRadius + neighbor.CollisionRadius;
                var influenceRadius = combinedRadius * rangeMultiplier;
                if (centerDistance > influenceRadius)
                {
                    continue;
                }

                var away = -toNeighbor / centerDistance;
                var minimumSpacing = combinedRadius + overlapPadding;
                var weight = 0f;

                if (centerDistance < minimumSpacing)
                {
                    var overlap = minimumSpacing - centerDistance;
                    weight += Mathf.Clamp01(overlap / Mathf.Max(0.0001f, combinedRadius)) * 2.5f;
                }
                else if (rangeMultiplier > 1f)
                {
                    var t = Mathf.InverseLerp(influenceRadius, combinedRadius, centerDistance);
                    weight += t * t * 0.25f;
                }

                if (weight > 0f)
                {
                    separation += away * weight;
                }
            }

            return separation;
        }

        private Vector3 ResolveCrowdOverlaps(Vector3 candidatePosition)
        {
            if (_registry == null || _config == null)
            {
                return candidatePosition;
            }

            var resolved = candidatePosition;
            var padding = Mathf.Max(0f, _config.overlapResolvePadding);
            var searchRadius = CollisionRadius + _registry.GetMaxCollisionRadius() + padding;

            for (var pass = 0; pass < 2; pass++)
            {
                var adjusted = false;
                _registry.GetNearby((Vector2)resolved, searchRadius, _nearbyBuffer);
                var neighbors = _nearbyBuffer;
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighbor = neighbors[i];
                    if (neighbor == null || ReferenceEquals(neighbor, this))
                    {
                        continue;
                    }

                    var toSelf = (Vector2)resolved - (Vector2)neighbor.transform.position;
                    var distance = toSelf.magnitude;
                    var minimum = CollisionRadius + neighbor.CollisionRadius + padding;
                    if (distance >= minimum)
                    {
                        continue;
                    }

                    var away = distance > 0.0001f ? toSelf / distance : (Vector2)(transform.position - _target.position);
                    if (away.sqrMagnitude <= 0.0001f)
                    {
                        away = Vector2.right;
                    }

                    var corrected = (Vector2)neighbor.transform.position + away.normalized * minimum;
                    resolved = new Vector3(corrected.x, corrected.y, 0f);
                    adjusted = true;
                }

                if (!adjusted)
                {
                    break;
                }
            }

            return resolved;
        }

        private void MoveUsingDesiredVector(Vector2 desired, Vector2 fallbackDirection, float deltaTime)
        {
            if (_rb == null) return;

            if (desired.sqrMagnitude > 1f)
            {
                desired.Normalize();
            }

            var effectiveMoveSpeed = _moveSpeed
                * Mathf.Clamp(_activeSlowMultiplier, 0.1f, 1f)
                * Mathf.Max(0.1f, _pendingMoveSpeedMultiplier);
            desired = ResolveObstacleAvoidance(desired, fallbackDirection, effectiveMoveSpeed, deltaTime);
            
            // 물리 엔진의 속도를 직접 제어 (모든 몬스터가 맵 안에서 생성되므로 복잡한 체크 불필요)
            _rb.linearVelocity = (desired * effectiveMoveSpeed) + _knockbackVelocity;
            
            _registry?.NotifyMoved(this, _rb.position);
        }

        private Vector2 ResolveObstacleAvoidance(Vector2 desired, Vector2 fallbackDirection, float effectiveMoveSpeed, float deltaTime)
        {
            if (!_pendingAllowObstacleSteering
                || _canPassThroughObstacles
                || _obstacleMask == 0
                || _collider == null
                || desired.sqrMagnitude <= 0.000001f)
            {
                TrackObstacleProgress(desired, effectiveMoveSpeed, deltaTime);
                return desired;
            }

            TrackObstacleProgress(desired, effectiveMoveSpeed, deltaTime);

            var desiredMagnitude = Mathf.Clamp01(desired.magnitude);
            var desiredDirection = desired / Mathf.Max(0.0001f, desired.magnitude);
            var fallback = fallbackDirection.sqrMagnitude > 0.000001f ? fallbackDirection.normalized : desiredDirection;
            var probeDistance = Mathf.Clamp(
                CollisionRadius + ObstacleProbeBaseDistance + (effectiveMoveSpeed * ObstacleProbeLeadSeconds),
                CollisionRadius + 0.15f,
                1.65f);
            var directBlocked = IsObstacleAhead(desiredDirection, probeDistance);
            var isStuck = _obstacleStuckTimer >= ObstacleStuckSeconds;

            if (!directBlocked && !isStuck)
            {
                return desired;
            }

            var bestScore = float.NegativeInfinity;
            var bestDirection = Vector2.zero;
            for (var i = 0; i < ObstacleAvoidanceAngles.Length; i++)
            {
                var angle = ObstacleAvoidanceAngles[i];
                var candidate = RotateDirection(desiredDirection, angle);
                if (IsObstacleAhead(candidate, probeDistance))
                {
                    continue;
                }

                var side = (desiredDirection.x * candidate.y) - (desiredDirection.y * candidate.x);
                var sideBias = Mathf.Sign(side) == _obstacleAvoidanceSide ? 0.08f : 0f;
                var score = (Vector2.Dot(candidate, desiredDirection) * 1.25f)
                    + (Vector2.Dot(candidate, fallback) * 0.35f)
                    + sideBias;
                if (isStuck && Mathf.Abs(angle) >= 55f)
                {
                    score += 0.35f;
                }

                if (isStuck && Mathf.Abs(angle) <= 0.01f)
                {
                    score -= 0.8f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = candidate;
                }
            }

            if (bestDirection.sqrMagnitude > 0.000001f)
            {
                return bestDirection * desiredMagnitude;
            }

            var tangent = new Vector2(-desiredDirection.y, desiredDirection.x) * _obstacleAvoidanceSide;
            if (!IsObstacleAhead(tangent, probeDistance * 0.7f))
            {
                return tangent * desiredMagnitude;
            }

            return Vector2.zero;
        }

        private void TrackObstacleProgress(Vector2 desired, float effectiveMoveSpeed, float deltaTime)
        {
            if (_rb == null)
            {
                return;
            }

            var currentPosition = _rb.position;
            if (desired.sqrMagnitude <= 0.000001f)
            {
                _obstacleStuckTimer = 0f;
                _lastObstacleProbePosition = currentPosition;
                return;
            }

            var moved = Vector2.Distance(currentPosition, _lastObstacleProbePosition);
            var expectedProgress = Mathf.Max(ObstacleMinProgress, effectiveMoveSpeed * deltaTime * 0.22f);
            _obstacleStuckTimer = moved < expectedProgress
                ? _obstacleStuckTimer + deltaTime
                : 0f;
            _lastObstacleProbePosition = currentPosition;
        }

        private bool IsObstacleAhead(Vector2 direction, float distance)
        {
            if (_collider == null || _obstacleMask == 0 || direction.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            return _collider.Cast(direction.normalized, _obstacleFilter, _castResults, Mathf.Max(0.01f, distance)) > 0;
        }

        private bool IsWalkable(Vector2 pos)
        {
            // 이제 Cast 방식을 사용하므로 IsWalkable은 보조용으로만 둡니다.
            if (_obstacleMask != 0)
            {
                return !Physics2D.OverlapPoint(pos, _obstacleMask);
            }
            return true;
        }

        private bool UpdateVariantBehavior(float deltaTime, bool isStunned)
        {
            if (_variantDefinition == null || _target == null || _playerHealth == null)
            {
                return false;
            }

            _variantCooldownTimer = Mathf.Max(0f, _variantCooldownTimer - deltaTime);
            var desiredMinRange = Mathf.Max(0f, _variantDefinition.DesiredMinRange);
            var desiredMaxRange = _variantDefinition.DesiredMaxRange > 0.1f
                ? Mathf.Max(desiredMinRange + 0.1f, _variantDefinition.DesiredMaxRange)
                : Mathf.Max(desiredMinRange + 0.1f, 4.4f);

            return _variantDefinition.BehaviorKind switch
            {
                EnemyVariantBehaviorKind.SplitOnDeath => false,
                EnemyVariantBehaviorKind.Shooter => UpdateShooterVariant(deltaTime, isStunned, desiredMinRange, desiredMaxRange),
                EnemyVariantBehaviorKind.Charger => UpdateChargerVariant(deltaTime, isStunned),
                _ => false,
            };
        }



        private bool UpdateShooterVariant(float deltaTime, bool isStunned, float minRange, float maxRange, bool keepRange = false)
        {
            if (_target == null)
            {
                return false;
            }

            var toPlayer = (Vector2)(_target.position - transform.position);
            var distance = toPlayer.magnitude;
            var direction = distance > 0.001f ? toPlayer / distance : Vector2.right;
            if (_variantAttackStopTimer > 0f)
            {
                _variantAttackStopTimer = Mathf.Max(0f, _variantAttackStopTimer - deltaTime);
                _pendingDesiredVector = Vector2.zero;
                _pendingFallbackDirection = direction;
                return true;
            }

            if (!isStunned)
            {
                var separation = ComputeSeparationVector((Vector2)transform.position) * Mathf.Max(0f, _config.separationWeight);
                var desired = separation;
                if (distance > maxRange)
                {
                    desired += direction;
                }
                else if (distance < minRange)
                {
                    desired -= direction;
                }
                else if (keepRange)
                {
                    desired += separation * 0.35f;
                }

                _pendingDesiredVector = desired;
                _pendingFallbackDirection = direction;
            }

            if (isStunned || _variantCooldownTimer > 0f)
            {
                return true;
            }

            if (distance > Mathf.Max(maxRange + 1f, 5.5f))
            {
                return true;
            }

            var attackStopDuration = Mathf.Max(
                MushroomShooterAttackStopDuration,
                _spriteAnimator != null ? _spriteAnimator.PlayAttackOneShot(MushroomShooterAttackStopDuration) : 0f);
            _variantAttackStopTimer = attackStopDuration;
            _pendingDesiredVector = Vector2.zero;
            _pendingFallbackDirection = direction;
            SpawnVariantProjectile(
                direction,
                Mathf.Max(0.1f, _variantDefinition.ProjectileSpeed),
                Mathf.Max(0.1f, _variantDefinition.ProjectileLifetime),
                Mathf.Max(0f, _contactDamage * Mathf.Max(0.1f, _variantDefinition.ProjectileDamageMultiplier)),
                _variantBaseColor);
            _variantCooldownTimer = Mathf.Max(0.1f, _variantDefinition.AttackCooldown);
            return true;
        }

        private bool UpdateChargerVariant(float deltaTime, bool isStunned)
        {
            if (_variantActionState == VariantActionState.Windup)
            {
                _variantActionTimer -= deltaTime;
                if (_variantActionTimer <= 0f)
                {
                    ApplyVariantBaseColor();
                    _variantActionState = VariantActionState.Executing;
                    _variantActionTimer = Mathf.Max(0.1f, _variantDefinition.DashDuration);
                    _spriteAnimator?.PlayAttackOneShot(Mathf.Clamp(_variantDefinition.DashDuration * 0.8f, 0.12f, 0.28f));
                }

                return true;
            }

            if (_variantActionState == VariantActionState.Executing)
            {
                var dashDirection = _variantActionDirection.sqrMagnitude > 0.0001f ? _variantActionDirection.normalized : Vector2.right;
                _pendingDesiredVector = dashDirection;
                _pendingFallbackDirection = dashDirection;
                _pendingMoveSpeedMultiplier = Mathf.Max(0.1f, _variantDefinition.DashSpeedMultiplier);
                _pendingAllowObstacleSteering = false;

                _variantActionTimer -= deltaTime;
                if (_variantActionTimer <= 0f)
                {
                    _variantActionState = VariantActionState.None;
                    _variantCooldownTimer = Mathf.Max(0.1f, _variantDefinition.AttackCooldown);
                    ApplyVariantBaseColor();
                }

                return true;
            }

            if (isStunned)
            {
                return false;
            }

            if (_variantCooldownTimer > 0f)
            {
                return false;
            }

            var toPlayer = (Vector2)(_target.position - transform.position);
            var distance = toPlayer.magnitude;
            if (distance <= CollisionRadius + _playerCollisionRadius + 0.15f || distance > 6.5f)
            {
                return false;
            }

            _variantActionDirection = distance > 0.001f ? toPlayer / distance : Vector2.right;
            _variantActionState = VariantActionState.Windup;
            _variantActionTimer = Mathf.Max(0.1f, _variantDefinition.DashTelegraphSeconds);
            _variantBlinkTimer = 0f;
            ApplyVariantBaseColor();
            if (_spriteAnimator != null)
            {
                var windupDuration = Mathf.Clamp(_variantActionTimer, 0.12f, 0.45f);
                if (_spriteAnimator.PlayClipOneShot("Defense", windupDuration) <= 0f)
                {
                    _spriteAnimator.PlayAttackOneShot(windupDuration);
                }
            }
            return true;
        }

        private void UpdateVariantBlink(float deltaTime, Color flashColor)
        {
            _variantBlinkTimer -= deltaTime;
            if (_variantBlinkTimer > 0f)
            {
                return;
            }

            _variantBlinkTimer = VariantBlinkInterval;
            if (_visualRenderer == null)
            {
                return;
            }

            var nextColor = _visualRenderer.color == _variantBaseColor ? flashColor : _variantBaseColor;
            _visualRenderer.color = nextColor;
        }





        private void SpawnVariantProjectile(Vector2 direction, float speed, float lifetime, float damage, Color color, bool allowVfxAudio = true)
        {
            if (_playerHealth == null || damage <= 0f)
            {
                return;
            }

            var projectileObject = new GameObject("EnemyVariantProjectile");
            projectileObject.transform.position = transform.position + new Vector3(0f, Mathf.Max(0.12f, _collisionRadius * 0.35f), 0f);
            var renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = 43;
            renderer.enabled = false; // 밋밋한 스프라이트 숨김
            projectileObject.transform.localScale = Vector3.one * VariantProjectileVisualScale;

            var vfxPrefab = Resources.Load<GameObject>("VFX/Bubble/VFX_2D_Bubble_02_Mask_Loop_Static");
            if (vfxPrefab != null)
            {
                var vfx = UnityEngine.Object.Instantiate(vfxPrefab, projectileObject.transform);
                vfx.transform.localPosition = Vector3.zero;
                vfx.transform.localRotation = Quaternion.identity;
                vfx.transform.localScale = Vector3.one * 1.5f;

                if (!allowVfxAudio)
                {
                    DisableVfxAudio(vfx);
                }

                var particleRenderers = vfx.GetComponentsInChildren<ParticleSystemRenderer>();
                foreach (var psr in particleRenderers)
                {
                    psr.alignment = ParticleSystemRenderSpace.Local;
                }
            }

            var projectile = projectileObject.AddComponent<EnemyVariantProjectile>();
            projectile.Initialize(
                direction,
                speed,
                lifetime,
                damage,
                VariantProjectileHitRadius,
                _playerHealth,
                _playerCollisionRadius);
        }

        private static void DisableVfxAudio(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var audioSources = root.GetComponentsInChildren<AudioSource>(true);
            foreach (var audioSource in audioSources)
            {
                audioSource.Stop();
                audioSource.mute = true;
                audioSource.enabled = false;
            }
        }

        private bool UpdateWaveTargetBehavior(float deltaTime, bool isStunned)
        {
            if (!_isWaveTargetBehavior || _playerHealth == null)
            {
                return false;
            }

            return _visualKind switch
            {
                RuntimeSpriteFactory.EnemyVisualKind.Mushroom => UpdateWaveTargetMushroomBehavior(deltaTime, isStunned),
                RuntimeSpriteFactory.EnemyVisualKind.Skeleton => UpdateWaveTargetSkeletonBehavior(deltaTime, isStunned),
                _ => false,
            };
        }

        private bool UpdateWaveTargetMushroomBehavior(float deltaTime, bool isStunned)
        {
            var toPlayer = _target != null ? (Vector2)(_target.position - transform.position) : Vector2.right;
            var direction = toPlayer.sqrMagnitude > 0.000001f ? toPlayer.normalized : Vector2.right;
            var isAttacking = false;

            if (_waveTargetMushroomAttackStopTimer > 0f)
            {
                _waveTargetMushroomAttackStopTimer = Mathf.Max(0f, _waveTargetMushroomAttackStopTimer - deltaTime);
                isAttacking = true;
            }

            if (_waveTargetMushroomBurstRemaining > 0)
            {
                _waveTargetMushroomBurstTimer -= deltaTime;
                if (_waveTargetMushroomBurstTimer <= 0f)
                {
                    FireWaveTargetMushroomRadialVolley();
                    _waveTargetMushroomBurstRemaining--;
                    _waveTargetMushroomBurstTimer = WaveTargetMushroomBurstInterval;
                }

                isAttacking = true;
            }

            _waveTargetMushroomCooldown = Mathf.Max(0f, _waveTargetMushroomCooldown - deltaTime);
            if (!isStunned && _waveTargetMushroomCooldown <= 0f && _waveTargetMushroomBurstRemaining <= 0)
            {
                _waveTargetMushroomBurstRemaining = WaveTargetMushroomBurstCount;
                _waveTargetMushroomBurstTimer = 0f;
                _waveTargetMushroomCooldown = WaveTargetMushroomCooldown;
                _waveTargetMushroomBurstAudioPlayed = false;
                isAttacking = true;
            }

            if (!isAttacking)
            {
                return false;
            }

            _pendingDesiredVector = Vector2.zero;
            _pendingFallbackDirection = direction;
            return true;
        }

        private bool UpdateWaveTargetSkeletonBehavior(float deltaTime, bool isStunned)
        {
            if (_target == null)
            {
                return false;
            }

            var toPlayer = (Vector2)(_target.position - transform.position);
            var direction = toPlayer.sqrMagnitude > 0.000001f ? toPlayer.normalized : Vector2.right;

            if (_waveTargetSkeletonActionState == VariantActionState.Windup)
            {
                _waveTargetSkeletonActionTimer -= deltaTime;
                _pendingDesiredVector = Vector2.zero;
                _pendingFallbackDirection = direction;
                if (_waveTargetSkeletonActionTimer <= 0f)
                {
                    BeginWaveTargetSkeletonDash(direction);
                }

                return true;
            }

            if (_waveTargetSkeletonActionState == VariantActionState.Executing)
            {
                var dashDirection = _waveTargetSkeletonDashDirection.sqrMagnitude > 0.000001f
                    ? _waveTargetSkeletonDashDirection.normalized
                    : direction;
                _pendingDesiredVector = dashDirection;
                _pendingFallbackDirection = dashDirection;
                _pendingMoveSpeedMultiplier = WaveTargetSkeletonDashSpeedMultiplier;
                _pendingAllowObstacleSteering = false;

                _waveTargetSkeletonActionTimer -= deltaTime;
                if (_waveTargetSkeletonActionTimer <= 0f)
                {
                    _waveTargetSkeletonDashRemaining--;
                    _waveTargetSkeletonActionState = VariantActionState.None;
                    if (_waveTargetSkeletonDashRemaining <= 0)
                    {
                        _waveTargetSkeletonCooldown = WaveTargetSkeletonCooldown;
                        _waveTargetSkeletonIntermissionTimer = 0f;
                    }
                    else
                    {
                        _waveTargetSkeletonIntermissionTimer = WaveTargetSkeletonIntermission;
                    }
                }

                return true;
            }

            if (_waveTargetSkeletonIntermissionTimer > 0f)
            {
                _waveTargetSkeletonIntermissionTimer = Mathf.Max(0f, _waveTargetSkeletonIntermissionTimer - deltaTime);
                _pendingDesiredVector = Vector2.zero;
                _pendingFallbackDirection = direction;
                if (_waveTargetSkeletonIntermissionTimer <= 0f)
                {
                    BeginWaveTargetSkeletonDash(direction);
                }

                return true;
            }

            _waveTargetSkeletonCooldown = Mathf.Max(0f, _waveTargetSkeletonCooldown - deltaTime);
            if (isStunned || _waveTargetSkeletonCooldown > 0f)
            {
                return false;
            }

            _waveTargetSkeletonDashRemaining = UnityEngine.Random.Range(2, 4);
            BeginWaveTargetSkeletonWindup(direction);
            return true;
        }

        private void BeginWaveTargetSkeletonWindup(Vector2 direction)
        {
            _waveTargetSkeletonDashDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            _waveTargetSkeletonActionState = VariantActionState.Windup;
            _waveTargetSkeletonActionTimer = WaveTargetSkeletonWindupDuration;
            _pendingDesiredVector = Vector2.zero;
            _pendingFallbackDirection = _waveTargetSkeletonDashDirection;
            if (_spriteAnimator != null)
            {
                if (_spriteAnimator.PlayClipOneShot("Defense", WaveTargetSkeletonWindupDuration) <= 0f)
                {
                    _spriteAnimator.PlayAttackOneShot(WaveTargetSkeletonWindupDuration);
                }
            }
        }

        private void BeginWaveTargetSkeletonDash(Vector2 direction)
        {
            _waveTargetSkeletonDashDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            _waveTargetSkeletonActionState = VariantActionState.Executing;
            _waveTargetSkeletonActionTimer = WaveTargetSkeletonDashDuration;
            _spriteAnimator?.PlayAttackOneShot(Mathf.Clamp(WaveTargetSkeletonDashDuration * 0.8f, 0.12f, 0.34f));
        }

        private void FireWaveTargetMushroomRadialVolley()
        {
            var attackStopDuration = Mathf.Max(
                WaveTargetMushroomAttackStopDuration,
                _spriteAnimator != null ? _spriteAnimator.PlayAttackOneShot(WaveTargetMushroomAttackStopDuration) : 0f);
            _waveTargetMushroomAttackStopTimer = attackStopDuration;

            var angleOffset = UnityEngine.Random.value * 60f;
            var damage = Mathf.Max(0f, _contactDamage * WaveTargetMushroomProjectileDamageMultiplier);
            var color = new Color(0.42f, 1f, 0.35f, 1f);
            for (var i = 0; i < WaveTargetMushroomProjectileCount; i++)
            {
                var radians = ((360f / WaveTargetMushroomProjectileCount) * i + angleOffset) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                var allowVfxAudio = !_waveTargetMushroomBurstAudioPlayed && i == 0;
                if (allowVfxAudio)
                {
                    _waveTargetMushroomBurstAudioPlayed = true;
                }

                SpawnVariantProjectile(
                    direction,
                    WaveTargetMushroomProjectileSpeed,
                    WaveTargetMushroomProjectileLifetime,
                    damage,
                    color,
                    allowVfxAudio);
            }
        }

        private void ApplyVariantBaseColor()
        {
            if (_visualRenderer != null)
            {
                _visualRenderer.color = _variantBaseColor;
            }

            _spriteAnimator?.SetBaseColor(_variantBaseColor);
        }

        public void ReceiveDamage(float damage)
        {
            ReceiveWeaponDamage(damage, WeaponUpgradeId.Fireball);
        }

        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f)
            {
                return;
            }

            var previousHealth = _health;
            _health = Mathf.Clamp(_health + amount, 0f, _maxHealth);
            var healedAmount = _health - previousHealth;
            if (healedAmount <= 0f)
            {
                return;
            }

            CombatTextSpawner.SpawnHealing(transform.position + new Vector3(0f, 0.8f, 0f), healedAmount);
            Changed?.Invoke(_health, MaxHealth);
        }

        public void ReceiveWeaponDamage(float damage, WeaponUpgradeId sourceWeaponId)
        {
            if (_isDead)
            {
                return;
            }

            var baseDamage = Mathf.Max(0f, damage);
            if (baseDamage <= 0f)
            {
                return;
            }

            var previousHealth = _health;
            var appliedDamage = Mathf.Min(baseDamage, Mathf.Max(0f, previousHealth));
            _health = Mathf.Max(0f, _health - baseDamage);

            if (_health > 0f &&
                !IsBoss &&
                _visualKind != RuntimeSpriteFactory.EnemyVisualKind.Skeleton)
            {
                _spriteAnimator?.PlayHurt();
            }

            var basePopupPosition = transform.position + new Vector3(0f, 0.8f, 0f);
            CombatTextSpawner.SpawnDamage(basePopupPosition, baseDamage, CombatTextSpawner.EnemyDamagedColor);
            ApplyMinorStunForWeapon(sourceWeaponId);
            Damaged?.Invoke(this, sourceWeaponId, appliedDamage);
            Changed?.Invoke(_health, MaxHealth);

            TryTriggerWaveTargetSlimeSplits(previousHealth, _health);

            if (_health <= 0f)
            {
                Die();
            }
        }

        private void TryTriggerWaveTargetSlimeSplits(float previousHealth, float currentHealth)
        {
            if (!_isWaveTargetBehavior ||
                _visualKind != RuntimeSpriteFactory.EnemyVisualKind.Slime ||
                currentHealth <= 0f ||
                _maxHealth <= 0.0001f ||
                _waveTargetSlimeSplitSpawnHandler == null)
            {
                return;
            }

            while (_waveTargetSlimeThresholdIndex <= 2)
            {
                var threshold = _maxHealth * (1f - (_waveTargetSlimeThresholdIndex / 3f));
                if (previousHealth <= threshold)
                {
                    _waveTargetSlimeThresholdIndex++;
                    continue;
                }

                if (currentHealth > threshold)
                {
                    break;
                }

                _waveTargetSlimeThresholdIndex++;
                _waveTargetSlimeSplitSpawnHandler.Invoke(this);
            }
        }



        private void SpawnVariantAreaFx(
            Vector2 origin,
            float radius,
            Color color,
            float lineWidth,
            float duration,
            float burstDuration,
            int spokeCount,
            string name)
        {
            var center = new Vector3(origin.x, origin.y, 0f);
            var fxParent = transform.parent != null ? transform.parent : transform;
            WeaponFxRenderer.SpawnRingFx(
                fxParent,
                center,
                Mathf.Max(0.1f, radius),
                32,
                color,
                Mathf.Max(0.02f, lineWidth),
                Mathf.Max(0.05f, duration),
                name,
                518);
            WeaponFxRenderer.SpawnBurstFx(
                fxParent,
                center,
                color,
                Mathf.Max(4, spokeCount),
                0.14f,
                Mathf.Max(0.24f, radius),
                Mathf.Max(0.03f, lineWidth * 0.72f),
                Mathf.Max(0.05f, burstDuration),
                $"{name}_Burst",
                519);
        }



        public void ApplyStun(float durationSeconds)
        {
            if (_isDead || IsBoss)
            {
                return;
            }

            _stunRemaining = Mathf.Max(_stunRemaining, Mathf.Max(0f, durationSeconds));
        }

        public void ApplyMinorStun(float durationSeconds)
        {
            TryApplyMinorStun(durationSeconds);
        }

        private void ApplyMinorStunForWeapon(WeaponUpgradeId sourceWeaponId)
        {
            var duration = sourceWeaponId switch
            {
                WeaponUpgradeId.Slash => KatanaMinorStunDuration,
                _ => 0f,
            };

            TryApplyMinorStun(duration);
        }

        private void TryApplyMinorStun(float durationSeconds)
        {
            if (_isDead || IsBoss || durationSeconds <= 0f || Time.time < _minorStunCooldownUntil)
            {
                return;
            }

            _minorStunCooldownUntil = Time.time + MinorStunInternalCooldown;
            _stunRemaining = Mathf.Max(_stunRemaining, durationSeconds);
        }

        public void ApplySlow(float multiplier, float duration)
        {
            if (_isDead || IsBoss) return;
            _activeSlowMultiplier = Mathf.Min(_activeSlowMultiplier, multiplier);
            _activeSlowRemaining = Mathf.Max(_activeSlowRemaining, duration);
        }

        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (_isDead || IsBoss) return;
            _knockbackVelocity = direction.normalized * force;
        }

        private void RefreshResolvedTarget()
        {
            if (_targetResolver != null)
            {
                _target = _targetResolver.Invoke();
            }

            if (_playerHealthResolver != null)
            {
                _playerHealth = _playerHealthResolver.Invoke();
            }

            if (_target == null && _playerHealth != null)
            {
                _target = _playerHealth.transform;
            }
        }

        private void Die()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;

            // [추가] 즉시 정지 및 물리 충돌 차단
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.simulated = false; 
            }
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            EndBossPattern();


            if (_experienceOrbSpawner != null)
            {
                _experienceOrbSpawner.Invoke(transform.position, _experienceOnDeath);
            }
            else if (_experienceSystem != null)
            {
                _experienceSystem.SpawnOrb(transform.position, _experienceOnDeath);
            }

            if (_registry != null)
            {
                _registry.Unregister(this);
            }

            if (_variantDefinition != null &&
                _variantDefinition.BehaviorKind == EnemyVariantBehaviorKind.SplitOnDeath)
            {
                _variantSplitSpawnHandler?.Invoke(this, _variantDefinition);
            }

            Defeated?.Invoke(this);

            var destroyDelay = _spriteAnimator != null ? _spriteAnimator.PlayDie() : 0f;
            if (destroyDelay > 0f)
            {
                Destroy(gameObject, destroyDelay);
                return;
            }

            Destroy(gameObject);
        }

        private void TickCoreEffectDurations()
        {
            if (_stunRemaining > 0f)
            {
                _stunRemaining -= Time.deltaTime;
                if (_stunRemaining <= 0f)
                {
                    _stunRemaining = 0f;
                }
            }

            if (_activeSlowRemaining > 0f)
            {
                _activeSlowRemaining -= Time.deltaTime;
                if (_activeSlowRemaining <= 0f)
                {
                    _activeSlowRemaining = 0f;
                    _activeSlowMultiplier = 1f;
                }
            }

            if (_activeLightRemaining > 0f)
            {
                _activeLightRemaining -= Time.deltaTime;
                if (_activeLightRemaining <= 0f)
                {
                    _activeLightRemaining = 0f;
                    _activeLightBonusMultiplier = 0f;
                }
            }

            if (_knockbackVelocity.sqrMagnitude > 0.001f)
            {
                _knockbackVelocity = Vector2.Lerp(_knockbackVelocity, Vector2.zero, Time.deltaTime * 8f);
            }



            UpdateStatusIndicators();
        }

        private bool UpdateBossPattern(float deltaTime)
        {
            if (!IsBoss || _target == null || _playerHealth == null)
            {
                return false;
            }

            if (_bossPatternState == BossPatternState.Telegraph)
            {
                _pendingDesiredVector = Vector2.zero;
                _pendingFallbackDirection = GetDirectionToPlayer();
                if (ShouldShowDashTelegraph(_bossCurrentAction))
                {
                    UpdateBossDashTelegraphFx();
                }

                if (ShouldShowAreaTelegraph(_bossCurrentAction))
                {
                    UpdateBossAreaTelegraphFx();
                }

                _bossStateTimer -= deltaTime;
                if (_bossStateTimer <= 0f)
                {
                    BeginBossExecution();
                }

                return true;
            }

            if (_bossPatternState == BossPatternState.Executing)
            {
                return UpdateBossExecuting(deltaTime);
            }

            if (_bossPatternState == BossPatternState.Recovery)
            {
                _pendingDesiredVector = Vector2.zero;
                _pendingFallbackDirection = GetDirectionToPlayer();
                _bossStateTimer -= deltaTime;
                if (_bossStateTimer <= 0f)
                {
                    EndBossPattern();
                }

                return true;
            }

            _bossPatternCooldown = Mathf.Max(0f, _bossPatternCooldown - deltaTime);
            if (_bossPatternCooldown > 0f)
            {
                return false;
            }

            StartRandomBossPattern();
            return true;
        }





        private void StartRandomBossPattern()
        {
            _bossCurrentAction = PickNextBossAction();
            _bossPatternState = BossPatternState.Telegraph;
            _bossStateTimer = GetBossTelegraphDuration(_bossCurrentAction);
            _bossShotTimer = 0f;
            _bossExecutionTimer = 0f;
            _bossSecondaryTimer = 0f;
            _bossRepeatRemaining = 0;
            _bossActionStep = 0;
            ClearBossPullState();

            if (ShouldShowDashTelegraph(_bossCurrentAction))
            {
                _bossDashDirection = GetDirectionToPlayer();
                UpdateBossDashTelegraphFx();
            }
            else
            {
                HideBossDashTelegraphFx();
            }

            if (ShouldShowAreaTelegraph(_bossCurrentAction))
            {
                UpdateBossAreaTelegraphFx();
            }
            else
            {
                HideBossAreaTelegraphFx();
            }

            PlayBossTelegraphAnimation();
        }

        private void BeginBossExecution()
        {
            HideBossDashTelegraphFx();
            _bossPatternState = BossPatternState.Executing;
            _bossShotTimer = 0f;
            _bossExecutionTimer = 0f;
            _bossSecondaryTimer = 0f;
            _bossActionStep = 0;

            switch (_bossCurrentAction)
            {
                case BossPatternActionKind.WizardFanVolley:
                    _bossRepeatRemaining = ScaleBossActionCount(3);
                    BossProjectileVolleyStarted?.Invoke();
                    break;

                case BossPatternActionKind.WizardSigilField:
                    PlayBossClipOneShot("Attack02", 0.28f);
                    SpawnBossSigils(3, WizardSigilRingRadius, WizardSigilDelay, WizardSigilRadius, WizardSigilDamageMultiplier);
                    EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
                    break;

                case BossPatternActionKind.WizardCrossBurst:
                    _bossRepeatRemaining = ScaleBossActionCount(2);
                    PlayBossClipOneShot("Attack02", 0.24f);
                    SpawnVariantAreaFx(transform.position, WizardCrossBurstTelegraphRadius, WizardCrossTelegraphColor, 0.07f, 0.24f, 0.18f, 8, "BossCrossBurstCastFx");
                    BossProjectileVolleyStarted?.Invoke();
                    break;

                case BossPatternActionKind.WarriorChargeCombo:
                    _bossRepeatRemaining = ScaleBossActionCount(2);
                    BeginBossDashRun(_bossCurrentAction);
                    break;

                case BossPatternActionKind.WarriorGroundSlam:
                    PlayBossClipOneShot("Attack02", 0.30f);
                    SpawnVariantAreaFx(transform.position, GroundSlamTelegraphRadius, GroundSlamTelegraphColor, 0.08f, 0.28f, 0.22f, 10, "BossGroundSlamCastFx");
                    FireBossRadialBurst(GroundSlamProjectileCount, GetBossProjectileSpeed(5.5f));
                    EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
                    break;

                case BossPatternActionKind.FinalMixedVolley:
                    _bossRepeatRemaining = 2;
                    break;

                case BossPatternActionKind.FinalChargeCombo:
                    _bossRepeatRemaining = UnityEngine.Random.Range(2, 4);
                    BeginBossDashRun(_bossCurrentAction);
                    break;

                case BossPatternActionKind.FinalSummon:
                    PlayBossClipOneShot("Attack", 0.30f);
                    BossMinionSummonRequested?.Invoke();
                    EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
                    break;

                case BossPatternActionKind.FinalGravityNova:
                    _bossExecutionTimer = FinalGravityDuration;
                    SpawnVariantAreaFx(transform.position, FinalGravityRadius, GravityTelegraphColor, 0.08f, FinalGravityDuration + 0.12f, 0.22f, 12, "BossGravityNovaCastFx");
                    PlayBossClipOneShot("Attack", Mathf.Clamp(FinalGravityDuration * 0.45f, 0.18f, 0.36f));
                    ApplyBossPullState(transform.position, FinalGravityRadius, GetBossPullSpeed(2.8f));
                    break;
            }
        }

        private bool UpdateBossExecuting(float deltaTime)
        {
            switch (_bossCurrentAction)
            {
                case BossPatternActionKind.WizardFanVolley:
                    return UpdateWizardFanVolley(deltaTime);
                case BossPatternActionKind.WizardCrossBurst:
                    return UpdateWizardCrossBurst(deltaTime);
                case BossPatternActionKind.WarriorChargeCombo:
                case BossPatternActionKind.FinalChargeCombo:
                    return UpdateBossChargeCombo(deltaTime);
                case BossPatternActionKind.FinalMixedVolley:
                    return UpdateFinalMixedVolley(deltaTime);
                case BossPatternActionKind.FinalGravityNova:
                    return UpdateFinalGravityNova(deltaTime);
                default:
                    EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
                    return true;
            }
        }

        private bool UpdateWizardFanVolley(float deltaTime)
        {
            _bossShotTimer -= deltaTime;
            if (_bossShotTimer > 0f)
            {
                return true;
            }

            FireBossFanVolley(GetBossProjectileSpeed(7f));
            _bossRepeatRemaining--;
            if (_bossRepeatRemaining <= 0)
            {
                EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
                return true;
            }

            _bossShotTimer = WizardFanShotInterval;
            return true;
        }

        private bool UpdateWizardCrossBurst(float deltaTime)
        {
            _bossShotTimer -= deltaTime;
            if (_bossShotTimer > 0f)
            {
                return true;
            }

            FireBossRadialBurst(8, GetBossProjectileSpeed(7f));
            _bossRepeatRemaining--;
            if (_bossRepeatRemaining <= 0)
            {
                EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
                return true;
            }

            _bossShotTimer = WizardCrossBurstInterval;
            return true;
        }

        private bool UpdateBossChargeCombo(float deltaTime)
        {
            if (_bossActionStep == 1)
            {
                _pendingDesiredVector = _bossDashDirection;
                _pendingFallbackDirection = _bossDashDirection;
                _pendingMoveSpeedMultiplier = GetBossDashSpeedMultiplier(_bossCurrentAction);
                _pendingAllowObstacleSteering = false;

                _bossExecutionTimer -= deltaTime;
                if (_bossExecutionTimer > 0f)
                {
                    return true;
                }

                _bossRepeatRemaining--;
                if (_bossRepeatRemaining <= 0)
                {
                    EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
                    return true;
                }

                _bossActionStep = 2;
                _bossSecondaryTimer = _bossCurrentAction == BossPatternActionKind.WarriorChargeCombo
                    ? WarriorChargeIntermission
                    : FinalChargeIntermission;
                return true;
            }

            _bossSecondaryTimer -= deltaTime;
            if (_bossSecondaryTimer <= 0f)
            {
                BeginBossDashRun(_bossCurrentAction);
            }

            return true;
        }

        private bool UpdateFinalMixedVolley(float deltaTime)
        {
            _bossShotTimer -= deltaTime;
            if (_bossActionStep == 0)
            {
                FireBossRadialBurst(8, GetBossProjectileSpeed(7.6f));
                _bossActionStep = 1;
                _bossShotTimer = FinalMixedVolleyInterval;
                return true;
            }

            if (_bossShotTimer > 0f)
            {
                return true;
            }

            FireBossFanVolley(GetBossProjectileSpeed(7.6f));
            _bossRepeatRemaining--;
            if (_bossRepeatRemaining <= 0)
            {
                EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
                return true;
            }

            _bossShotTimer = FinalMixedVolleyInterval;
            return true;
        }

        private bool UpdateFinalGravityNova(float deltaTime)
        {
            if (_bossActionStep == 0)
            {
                ApplyBossPullState(transform.position, FinalGravityRadius, GetBossPullSpeed(2.8f));
                _bossExecutionTimer -= deltaTime;
                if (_bossExecutionTimer > 0f)
                {
                    return true;
                }

                ClearBossPullState();
                FireBossRadialBurst(GravityPulseProjectileCount, GetBossProjectileSpeed(6.2f), 1f);
                _bossActionStep = 1;
                _bossSecondaryTimer = FinalGravityPulseInterval;
                return true;
            }

            _bossSecondaryTimer -= deltaTime;
            if (_bossSecondaryTimer > 0f)
            {
                return true;
            }

            FireBossRadialBurst(GravityPulseProjectileCount, GetBossProjectileSpeed(6.2f), 1f);
            EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
            return true;
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

        private void SpawnBossProjectile(Vector2 direction)
        {
            SpawnBossProjectile(direction, 7.2f);
        }

        private void SpawnBossProjectile(Vector2 direction, float speed, float damageMultiplier = BossProjectileDamageMultiplier)
        {
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            BossProjectileSpawned?.Invoke(
                transform.position,
                normalizedDirection,
                speed,
                BossProjectileLifetime,
                BossProjectileVisualScale);
            var projectileObject = new GameObject("BossProjectile");
            projectileObject.transform.position = transform.position;

            var renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(1f, 0.32f, 0.24f, 1f);
            renderer.sortingOrder = 38;
            projectileObject.transform.localScale = Vector3.one * BossProjectileVisualScale;

            var projectile = projectileObject.AddComponent<BossProjectile>();
            projectile.Initialize(
                normalizedDirection,
                speed,
                BossProjectileLifetime,
                Mathf.Max(1f, _contactDamage * Mathf.Max(0.1f, damageMultiplier)),
                BossProjectileHitRadius,
                _playerHealth,
                _playerCollisionRadius);
        }

        private void EndBossPattern()
        {
            HideBossDashTelegraphFx();
            HideBossAreaTelegraphFx();
            ClearBossPullState();
            _pendingDesiredVector = Vector2.zero;
            _pendingFallbackDirection = Vector2.right;
            _bossPreviousAction = _bossCurrentAction;
            _bossPatternState = BossPatternState.None;
            _bossCurrentAction = BossPatternActionKind.None;
            _bossStateTimer = 0f;
            _bossShotTimer = 0f;
            _bossExecutionTimer = 0f;
            _bossSecondaryTimer = 0f;
            _bossRepeatRemaining = 0;
            _bossActionStep = 0;
            _bossPatternCooldown = GetBossPatternCooldown();
        }

        private BossPatternActionKind PickNextBossAction()
        {
            return _bossArchetype switch
            {
                BossArchetypeId.Wizard => PickWeightedBossAction(
                    (BossPatternActionKind.WizardFanVolley, 0.40f),
                    (BossPatternActionKind.WizardSigilField, 0.35f),
                    (BossPatternActionKind.WizardCrossBurst, 0.25f)),
                BossArchetypeId.Warrior => PickWeightedBossAction(
                    (BossPatternActionKind.WarriorChargeCombo, 0.65f),
                    (BossPatternActionKind.WarriorGroundSlam, 0.35f)),
                _ => PickWeightedBossAction(
                    (BossPatternActionKind.FinalSummon, 1f),
                    (BossPatternActionKind.FinalMixedVolley, 1f),
                    (BossPatternActionKind.FinalChargeCombo, 1f)),
            };
        }

        private BossPatternActionKind PickWeightedBossAction(params (BossPatternActionKind action, float weight)[] candidates)
        {
            var canAvoidRepeat = false;
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].action != _bossPreviousAction && candidates[i].weight > 0f)
                {
                    canAvoidRepeat = true;
                    break;
                }
            }

            var totalWeight = 0f;
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].weight <= 0f)
                {
                    continue;
                }

                if (canAvoidRepeat && candidates[i].action == _bossPreviousAction)
                {
                    continue;
                }

                totalWeight += candidates[i].weight;
            }

            if (totalWeight <= 0f)
            {
                return candidates.Length > 0 ? candidates[0].action : BossPatternActionKind.None;
            }

            var roll = UnityEngine.Random.value * totalWeight;
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate.weight <= 0f)
                {
                    continue;
                }

                if (canAvoidRepeat && candidate.action == _bossPreviousAction)
                {
                    continue;
                }

                roll -= candidate.weight;
                if (roll <= 0f)
                {
                    return candidate.action;
                }
            }

            return candidates[candidates.Length - 1].action;
        }

        private bool ShouldShowDashTelegraph(BossPatternActionKind action)
        {
            return action == BossPatternActionKind.WarriorChargeCombo || action == BossPatternActionKind.FinalChargeCombo;
        }

        private bool ShouldShowAreaTelegraph(BossPatternActionKind action)
        {
            return action == BossPatternActionKind.WarriorGroundSlam
                || action == BossPatternActionKind.WizardCrossBurst
                || action == BossPatternActionKind.FinalGravityNova;
        }

        private float GetBossTelegraphDuration(BossPatternActionKind action)
        {
            var baseDuration = action switch
            {
                BossPatternActionKind.WizardFanVolley => 0.85f,
                BossPatternActionKind.WizardSigilField => 0.75f,
                BossPatternActionKind.WizardCrossBurst => 0.70f,
                BossPatternActionKind.WarriorChargeCombo => 0.65f,
                BossPatternActionKind.WarriorGroundSlam => 0.80f,
                BossPatternActionKind.FinalSummon => 0.70f,
                BossPatternActionKind.FinalMixedVolley => 0.75f,
                BossPatternActionKind.FinalChargeCombo => 0.55f,
                BossPatternActionKind.FinalGravityNova => 0.85f,
                _ => 0.75f,
            };

            return baseDuration * GetBossDifficultyScale(_bossDifficulty?.BossTelegraphScale, 1f);
        }

        private float GetBossRecoveryDuration(BossPatternActionKind action)
        {
            var baseDuration = action switch
            {
                BossPatternActionKind.WizardFanVolley => 0.90f,
                BossPatternActionKind.WizardSigilField => 0.90f,
                BossPatternActionKind.WizardCrossBurst => 0.90f,
                BossPatternActionKind.WarriorChargeCombo => 0.85f,
                BossPatternActionKind.WarriorGroundSlam => 0.85f,
                BossPatternActionKind.FinalSummon => 0.75f,
                BossPatternActionKind.FinalMixedVolley => 0.70f,
                BossPatternActionKind.FinalChargeCombo => 0.70f,
                BossPatternActionKind.FinalGravityNova => 0.70f,
                _ => 0.80f,
            };

            return baseDuration * GetBossDifficultyScale(_bossDifficulty?.BossCooldownScale, 1f);
        }

        private float GetBossPatternCooldown()
        {
            var baseCooldown = _bossArchetype switch
            {
                BossArchetypeId.Wizard => UnityEngine.Random.Range(1.20f, 1.65f),
                BossArchetypeId.Warrior => UnityEngine.Random.Range(0.95f, 1.40f),
                _ => UnityEngine.Random.Range(0.90f, 1.30f),
            };

            return baseCooldown * GetBossDifficultyScale(_bossDifficulty?.BossCooldownScale, 1f);
        }

        private float GetBossProjectileSpeed(float baseSpeed)
        {
            return baseSpeed * GetBossDifficultyScale(_bossDifficulty?.BossProjectileSpeedScale, 1f);
        }

        private float GetBossDashSpeedMultiplier(BossPatternActionKind action)
        {
            var baseMultiplier = action == BossPatternActionKind.FinalChargeCombo ? 5.8f : 5.2f;
            return baseMultiplier * GetBossDifficultyScale(_bossDifficulty?.BossDashSpeedScale, 1f);
        }

        private float GetBossDashDuration(BossPatternActionKind action)
        {
            return action == BossPatternActionKind.FinalChargeCombo ? 0.40f : 0.42f;
        }

        private float GetBossPullSpeed(float baseSpeed)
        {
            return baseSpeed * GetBossDifficultyScale(_bossDifficulty?.BossPullSpeedScale, 1f);
        }

        private int ScaleBossActionCount(int baseCount)
        {
            var scale = GetBossDifficultyScale(_bossDifficulty?.BossActionCountScale, 1f);
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * scale));
        }

        private float GetBossDifficultyScale(float? configuredValue, float fallbackValue)
        {
            return Mathf.Max(0.1f, configuredValue ?? fallbackValue);
        }

        private Vector2 GetDirectionToPlayer()
        {
            var toPlayer = (Vector2)(_target.position - transform.position);
            return toPlayer.sqrMagnitude > 0.000001f ? toPlayer.normalized : Vector2.right;
        }

        private void PlayBossTelegraphAnimation()
        {
            var duration = Mathf.Max(0.1f, _bossStateTimer);
            switch (_bossCurrentAction)
            {
                case BossPatternActionKind.WizardFanVolley:
                    PlayBossClipOneShot("Attack01", duration);
                    break;
                case BossPatternActionKind.WizardSigilField:
                case BossPatternActionKind.WizardCrossBurst:
                    PlayBossClipOneShot("Attack02", duration);
                    break;
                case BossPatternActionKind.WarriorChargeCombo:
                    PlayBossClipOneShot("Attack01", duration);
                    break;
                case BossPatternActionKind.WarriorGroundSlam:
                    PlayBossClipOneShot("Attack02", duration);
                    break;
                case BossPatternActionKind.FinalSummon:
                case BossPatternActionKind.FinalMixedVolley:
                case BossPatternActionKind.FinalChargeCombo:
                case BossPatternActionKind.FinalGravityNova:
                    PlayBossClipOneShot("Attack", duration);
                    break;
                default:
                    _spriteAnimator?.PlayHurtOneShot(duration);
                    break;
            }
        }

        private float PlayBossClipOneShot(string clipName, float durationSeconds)
        {
            if (_spriteAnimator == null)
            {
                return 0f;
            }

            var clampedDuration = Mathf.Max(0.05f, durationSeconds);
            if (!string.IsNullOrWhiteSpace(clipName))
            {
                var played = _spriteAnimator.PlayClipOneShot(clipName, clampedDuration);
                if (played > 0f)
                {
                    return played;
                }
            }

            return _spriteAnimator.PlayAttackOneShot(clampedDuration);
        }

        private void BeginBossDashRun(BossPatternActionKind action)
        {
            _bossDashDirection = GetDirectionToPlayer();
            _bossExecutionTimer = Mathf.Max(0.05f, GetBossDashDuration(action));
            _bossActionStep = 1;
            switch (action)
            {
                case BossPatternActionKind.WarriorChargeCombo:
                    PlayBossClipOneShot("Attack01", Mathf.Clamp(_bossExecutionTimer, 0.16f, 0.34f));
                    break;
                case BossPatternActionKind.FinalChargeCombo:
                    PlayBossClipOneShot("Attack", Mathf.Clamp(_bossExecutionTimer, 0.12f, 0.30f));
                    break;
                default:
                    _spriteAnimator?.PlayAttackOneShot(Mathf.Clamp(_bossExecutionTimer, 0.12f, 0.30f));
                    break;
            }
        }

        private void EnterBossRecovery(float duration)
        {
            HideBossDashTelegraphFx();
            HideBossAreaTelegraphFx();
            ClearBossPullState();
            _bossPatternState = BossPatternState.Recovery;
            _bossStateTimer = Mathf.Max(0.05f, duration);
        }

        private void FireBossFanVolley(float projectileSpeed)
        {
            var centerDirection = GetDirectionToPlayer();
            var shotDuration = Mathf.Max(0.08f, WizardFanShotInterval * 0.8f);
            if (_bossCurrentAction == BossPatternActionKind.WizardFanVolley)
            {
                PlayBossClipOneShot("Attack01", shotDuration);
            }
            else
            {
                PlayBossClipOneShot("Attack", shotDuration);
            }

            SpawnBossProjectile(RotateDirection(centerDirection, -BossAimFanSpreadDegrees * 2f), projectileSpeed);
            SpawnBossProjectile(RotateDirection(centerDirection, -BossAimFanSpreadDegrees), projectileSpeed);
            SpawnBossProjectile(centerDirection, projectileSpeed);
            SpawnBossProjectile(RotateDirection(centerDirection, BossAimFanSpreadDegrees), projectileSpeed);
            SpawnBossProjectile(RotateDirection(centerDirection, BossAimFanSpreadDegrees * 2f), projectileSpeed);
        }

        private void FireBossRadialBurst(int projectileCount, float projectileSpeed, float damageMultiplier = BossProjectileDamageMultiplier)
        {
            if (projectileCount <= 0)
            {
                return;
            }

            switch (_bossCurrentAction)
            {
                case BossPatternActionKind.WizardCrossBurst:
                case BossPatternActionKind.WarriorGroundSlam:
                    PlayBossClipOneShot("Attack02", 0.16f);
                    break;
                default:
                    PlayBossClipOneShot("Attack", 0.12f);
                    break;
            }

            for (var i = 0; i < projectileCount; i++)
            {
                var radians = (Mathf.PI * 2f * i) / projectileCount;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                SpawnBossProjectile(direction, projectileSpeed, damageMultiplier);
            }
        }

        private void SpawnBossSigils(int sigilCount, float ringRadius, float delay, float explosionRadius, float damageMultiplier)
        {
            if (sigilCount <= 0)
            {
                EnterBossRecovery(GetBossRecoveryDuration(_bossCurrentAction));
                return;
            }

            PlayBossClipOneShot("Attack02", 0.18f);
            var origin = (Vector2)_target.position;
            var startAngle = UnityEngine.Random.value * 360f;
            for (var i = 0; i < sigilCount; i++)
            {
                var angle = startAngle + ((360f / sigilCount) * i);
                var radians = angle * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * ringRadius;
                CreateBossSigil(origin + offset, delay, explosionRadius, damageMultiplier);
            }
        }

        private void CreateBossSigil(Vector2 position, float delay, float explosionRadius, float damageMultiplier)
        {
            BossSigilSpawned?.Invoke(new Vector3(position.x, position.y, 0f), delay, explosionRadius);

            var hazardObject = new GameObject("BossSigilHazard");
            hazardObject.transform.position = new Vector3(position.x, position.y, 0f);
            var hazard = hazardObject.AddComponent<BossSigilHazard>();
            hazard.Initialize(
                _playerHealth,
                _playerCollisionRadius,
                delay,
                explosionRadius,
                Mathf.Max(1f, _contactDamage * Mathf.Max(0.1f, damageMultiplier)));
        }

        private void ApplyBossPullState(Vector2 center, float radius, float speed)
        {
            _bossPullActive = true;
            _bossPullCenter = center;
            _bossPullRadius = Mathf.Max(0.1f, radius);
            _bossPullSpeed = Mathf.Max(0.1f, speed);
        }

        private void ClearBossPullState()
        {
            _bossPullActive = false;
            _bossPullCenter = Vector2.zero;
            _bossPullRadius = 0f;
            _bossPullSpeed = 0f;
        }

        private void UpdateBossDashTelegraphFx()
        {
            if (!IsBoss)
            {
                HideBossDashTelegraphFx();
                return;
            }

            EnsureBossDashTelegraphFx();
            if (_bossDashTelegraphLine == null)
            {
                return;
            }

            var direction = _bossDashDirection.sqrMagnitude > 0.000001f ? _bossDashDirection.normalized : GetDirectionToPlayer();
            var start = (Vector2)transform.position;
            var end = start + (direction * BossDashTelegraphLength);
            var telegraphDuration = Mathf.Max(0.05f, GetBossTelegraphDuration(_bossCurrentAction));
            var progress = 1f - Mathf.Clamp01(_bossStateTimer / telegraphDuration);
            var width = Mathf.Lerp(BossDashTelegraphWidth * 0.7f, BossDashTelegraphWidth * 1.9f, progress);
            var color = Color.Lerp(BossDashTelegraphColor, BossDashTelegraphHotColor, progress);

            _bossDashTelegraphLine.enabled = true;
            _bossDashTelegraphLine.startWidth = width;
            _bossDashTelegraphLine.endWidth = width;
            _bossDashTelegraphLine.startColor = color;
            _bossDashTelegraphLine.endColor = color;
            _bossDashTelegraphLine.SetPosition(0, new Vector3(start.x, start.y, -0.03f));
            _bossDashTelegraphLine.SetPosition(1, new Vector3(end.x, end.y, -0.03f));
        }

        private void UpdateBossAreaTelegraphFx()
        {
            if (!TryGetBossAreaTelegraphSpec(out var radius, out var color, out var width))
            {
                HideBossAreaTelegraphFx();
                return;
            }

            EnsureBossAreaTelegraphFx();
            if (_bossAreaTelegraphLine == null)
            {
                return;
            }

            _bossAreaTelegraphLine.enabled = true;
            _bossAreaTelegraphLine.startWidth = width;
            _bossAreaTelegraphLine.endWidth = width;
            _bossAreaTelegraphLine.startColor = color;
            _bossAreaTelegraphLine.endColor = color;
            WeaponFxRenderer.SetCircleLinePositions(_bossAreaTelegraphLine, transform.position, radius, BossAreaTelegraphSegments, -0.03f);
        }

        private bool TryGetBossAreaTelegraphSpec(out float radius, out Color color, out float width)
        {
            radius = 0f;
            color = Color.white;
            width = BossAreaTelegraphWidth;

            var time = _bossPatternState == BossPatternState.Telegraph
                ? Mathf.Max(0f, _bossStateTimer)
                : Mathf.Max(0f, _bossExecutionTimer + _bossSecondaryTimer);
            var pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * 12f));

            switch (_bossCurrentAction)
            {
                case BossPatternActionKind.WarriorGroundSlam:
                    if (_bossPatternState != BossPatternState.Telegraph)
                    {
                        return false;
                    }

                    radius = GroundSlamTelegraphRadius;
                    color = Color.Lerp(GroundSlamTelegraphColor, BossDashTelegraphHotColor, pulse * 0.55f);
                    width = Mathf.Lerp(BossAreaTelegraphWidth, BossAreaTelegraphWidth * 1.55f, pulse);
                    return true;

                case BossPatternActionKind.WizardCrossBurst:
                    if (_bossPatternState != BossPatternState.Telegraph)
                    {
                        return false;
                    }

                    radius = WizardCrossBurstTelegraphRadius;
                    color = Color.Lerp(WizardCrossTelegraphColor, Color.white, pulse * 0.35f);
                    width = Mathf.Lerp(BossAreaTelegraphWidth, BossAreaTelegraphWidth * 1.4f, pulse);
                    return true;

                case BossPatternActionKind.FinalGravityNova:
                    if (_bossPatternState != BossPatternState.Telegraph && _bossPatternState != BossPatternState.Executing)
                    {
                        return false;
                    }

                    radius = FinalGravityRadius;
                    if (_bossPatternState == BossPatternState.Telegraph)
                    {
                        color = Color.Lerp(GravityTelegraphColor, Color.white, pulse * 0.25f);
                        width = Mathf.Lerp(BossAreaTelegraphWidth * 1.1f, BossAreaTelegraphWidth * 1.9f, pulse);
                    }
                    else
                    {
                        var timePulse = 0.55f + (0.45f * Mathf.Sin(Time.time * 10f));
                        color = Color.Lerp(GravityTelegraphColor, new Color(0.82f, 0.96f, 1f, 0.98f), timePulse * 0.5f);
                        width = Mathf.Lerp(BossAreaTelegraphWidth * 1.3f, BossAreaTelegraphWidth * 2.2f, timePulse);
                    }

                    return true;
            }

            return false;
        }

        private void EnsureBossDashTelegraphFx()
        {
            if (_bossDashTelegraphLine != null)
            {
                return;
            }

            var fxObject = new GameObject("BossDashTelegraphFx");
            fxObject.transform.SetParent(transform, false);

            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = false;
            lineRenderer.positionCount = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.startWidth = BossDashTelegraphWidth;
            lineRenderer.endWidth = BossDashTelegraphWidth;
            lineRenderer.startColor = BossDashTelegraphColor;
            lineRenderer.endColor = BossDashTelegraphColor;
            lineRenderer.sortingOrder = 520;
            lineRenderer.sharedMaterial = GetOrCreateBossDashTelegraphMaterial();
            lineRenderer.enabled = false;

            _bossDashTelegraphLine = lineRenderer;
        }

        private void EnsureBossAreaTelegraphFx()
        {
            if (_bossAreaTelegraphLine != null)
            {
                return;
            }

            var fxObject = new GameObject("BossAreaTelegraphFx");
            fxObject.transform.SetParent(transform, false);

            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            lineRenderer.positionCount = BossAreaTelegraphSegments;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.startWidth = BossAreaTelegraphWidth;
            lineRenderer.endWidth = BossAreaTelegraphWidth;
            lineRenderer.startColor = GravityTelegraphColor;
            lineRenderer.endColor = GravityTelegraphColor;
            lineRenderer.sortingOrder = 519;
            lineRenderer.sharedMaterial = GetOrCreateBossDashTelegraphMaterial();
            lineRenderer.enabled = false;

            _bossAreaTelegraphLine = lineRenderer;
        }

        private void HideBossDashTelegraphFx()
        {
            if (_bossDashTelegraphLine != null)
            {
                _bossDashTelegraphLine.enabled = false;
            }
        }

        private void HideBossAreaTelegraphFx()
        {
            if (_bossAreaTelegraphLine != null)
            {
                _bossAreaTelegraphLine.enabled = false;
            }
        }

        private static Material GetOrCreateBossDashTelegraphMaterial()
        {
            if (_bossDashTelegraphMaterial != null)
            {
                return _bossDashTelegraphMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _bossDashTelegraphMaterial = new Material(shader)
            {
                name = "BossDashTelegraphMat",
                hideFlags = HideFlags.HideAndDontSave,
            };

            return _bossDashTelegraphMaterial;
        }

        private bool IsInsideArena(Vector3 pos)
        {
            if (!_hasArenaBounds) return true;
            return _arenaBounds.Contains((Vector2)pos);
        }

        private Vector3 ClampPositionToArena(Vector3 candidate)
        {
            if (!_hasArenaBounds)
            {
                return candidate;
            }

            var margin = Mathf.Max(0f, CollisionRadius);
            var minX = _arenaBounds.xMin + margin;
            var maxX = _arenaBounds.xMax - margin;
            var minY = _arenaBounds.yMin + margin;
            var maxY = _arenaBounds.yMax - margin;

            if (maxX < minX)
            {
                maxX = minX;
            }

            if (maxY < minY)
            {
                maxY = minY;
            }

            candidate.x = Mathf.Clamp(candidate.x, minX, maxX);
            candidate.y = Mathf.Clamp(candidate.y, minY, maxY);
            return candidate;
        }




        private void ApplyLightCore(int coreLevel)
        {
            var (bonusMultiplier, duration) = coreLevel switch
            {
                1 => (0.10f, 1.0f),
                2 => (0.20f, 2.0f),
                _ => (0.30f, 5.0f),
            };

            _activeLightBonusMultiplier = Mathf.Max(_activeLightBonusMultiplier, bonusMultiplier);
            _activeLightRemaining = Mathf.Max(_activeLightRemaining, duration);
            UpdateStatusIndicators();
        }













        private void UpdateStatusIndicators()
        {
            var showSlow = _activeSlowRemaining > 0f && _activeSlowMultiplier < 0.999f;
            var showLight = _activeLightRemaining > 0f && _activeLightBonusMultiplier > 0f;
            if (!showSlow && !showLight)
            {
                if (_statusIndicatorRoot != null)
                {
                    _statusIndicatorRoot.gameObject.SetActive(false);
                }

                return;
            }

            EnsureStatusIndicatorObjects();
            if (_statusIndicatorRoot == null)
            {
                return;
            }

            _statusIndicatorRoot.gameObject.SetActive(true);
            var y = CollisionRadius + StatusIndicatorHeightOffset;
            var activeCount = (showSlow ? 1 : 0) + (showLight ? 1 : 0);
            var firstX = -StatusIndicatorSpacing * 0.5f * Mathf.Max(0, activeCount - 1);
            var slotIndex = 0;

            if (_slowIndicatorRenderer != null)
            {
                _slowIndicatorRenderer.enabled = showSlow;
                if (showSlow)
                {
                    _slowIndicatorRenderer.transform.localPosition = new Vector3(firstX + (slotIndex * StatusIndicatorSpacing), y, -0.03f);
                    slotIndex++;
                }
            }

            if (_lightIndicatorRenderer != null)
            {
                _lightIndicatorRenderer.enabled = showLight;
                if (showLight)
                {
                    _lightIndicatorRenderer.transform.localPosition = new Vector3(firstX + (slotIndex * StatusIndicatorSpacing), y, -0.03f);
                }
            }
        }

        private void EnsureStatusIndicatorObjects()
        {
            if (_statusIndicatorRoot == null)
            {
                var rootObject = new GameObject("StatusIndicators");
                rootObject.transform.SetParent(transform, false);
                _statusIndicatorRoot = rootObject.transform;
            }

            if (_slowIndicatorRenderer == null)
            {
                _slowIndicatorRenderer = CreateStatusIndicator("SlowIndicator", SlowIndicatorColor);
            }

            if (_lightIndicatorRenderer == null)
            {
                _lightIndicatorRenderer = CreateStatusIndicator("LightIndicator", LightIndicatorColor);
            }
        }

        private SpriteRenderer CreateStatusIndicator(string objectName, Color color)
        {
            if (_statusIndicatorRoot == null)
            {
                return null;
            }

            var indicatorObject = new GameObject(objectName);
            indicatorObject.transform.SetParent(_statusIndicatorRoot, false);
            indicatorObject.transform.localScale = Vector3.one * StatusIndicatorScale;
            indicatorObject.transform.localPosition = new Vector3(0f, CollisionRadius + StatusIndicatorHeightOffset, -0.03f);

            var renderer = indicatorObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = 49;
            renderer.enabled = false;
            return renderer;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.95f);
            Gizmos.DrawWireSphere(transform.position, CollisionRadius);
        }
    }

    [DisallowMultipleComponent]
    public sealed class SpriteFxAnimator : MonoBehaviour
    {
        private SpriteRenderer _targetRenderer;
        private Sprite[] _frames = Array.Empty<Sprite>();
        private float _framesPerSecond = 12f;
        private bool _loop;
        private bool _destroyOnComplete;
        private int _currentFrameIndex;
        private float _frameCursor;
        private bool _isPlaying;

        public void Initialize(
            SpriteRenderer targetRenderer,
            Sprite[] frames,
            float framesPerSecond,
            bool loop,
            bool destroyOnComplete)
        {
            _targetRenderer = targetRenderer;
            _frames = frames ?? Array.Empty<Sprite>();
            _framesPerSecond = Mathf.Max(0.1f, framesPerSecond);
            _loop = loop;
            _destroyOnComplete = destroyOnComplete;

            _currentFrameIndex = 0;
            _frameCursor = 0f;
            _isPlaying = _frames.Length > 0 && _targetRenderer != null;

            if (_isPlaying)
            {
                _targetRenderer.sprite = _frames[0];
            }
        }

        public void PlayFromStart()
        {
            if (_targetRenderer == null || _frames == null || _frames.Length == 0)
            {
                _isPlaying = false;
                return;
            }

            _currentFrameIndex = 0;
            _frameCursor = 0f;
            _isPlaying = true;
            _targetRenderer.sprite = _frames[0];
        }

        private void Update()
        {
            if (!_isPlaying || _targetRenderer == null || _frames == null || _frames.Length <= 0)
            {
                return;
            }

            _frameCursor += Time.deltaTime * _framesPerSecond;
            var steps = Mathf.FloorToInt(_frameCursor);
            if (steps <= 0)
            {
                return;
            }

            _frameCursor -= steps;
            _currentFrameIndex += steps;

            if (_loop)
            {
                _currentFrameIndex %= _frames.Length;
                _targetRenderer.sprite = _frames[_currentFrameIndex];
                return;
            }

            if (_currentFrameIndex < _frames.Length)
            {
                _targetRenderer.sprite = _frames[_currentFrameIndex];
                return;
            }

            _isPlaying = false;
            _targetRenderer.sprite = _frames[_frames.Length - 1];
            if (_destroyOnComplete)
            {
                Destroy(gameObject);
            }
        }
    }

    public sealed class BossProjectile : MonoBehaviour
    {
        private Vector2 _direction;
        private float _speed;
        private float _lifetime;
        private float _damage;
        private float _hitRadius;
        private float _playerCollisionRadius;
        private PlayerHealth _targetPlayer;
        private static readonly System.Collections.Generic.List<BossProjectile> s_activeProjectiles = new();

        public static System.Collections.Generic.IReadOnlyList<BossProjectile> ActiveProjectiles => s_activeProjectiles;
        public Vector2 WorldPosition => transform.position;
        public Vector2 Direction => _direction;
        public float Speed => _speed;
        public float RemainingLifetime => Mathf.Max(0f, _lifetime);
        public float HitRadius => _hitRadius;

        public void Initialize(
            Vector2 direction,
            float speed,
            float lifetime,
            float damage,
            float hitRadius,
            PlayerHealth targetPlayer,
            float playerCollisionRadius)
        {
            _direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            _speed = Mathf.Max(0.1f, speed);
            _lifetime = Mathf.Max(0.05f, lifetime);
            _damage = Mathf.Max(0f, damage);
            _hitRadius = Mathf.Max(0.02f, hitRadius);
            _targetPlayer = targetPlayer;
            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
        }

        private void OnEnable()
        {
            if (!s_activeProjectiles.Contains(this))
            {
                s_activeProjectiles.Add(this);
            }
        }

        private void OnDisable()
        {
            s_activeProjectiles.Remove(this);
        }

        private void Update()
        {
            if (DebugSessionService.IsMonsterLabTimePaused)
            {
                return;
            }

            transform.position += new Vector3(_direction.x, _direction.y, 0f) * (_speed * Time.deltaTime);
            _lifetime -= Time.deltaTime;
            if (_lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_targetPlayer == null || _damage <= 0f)
            {
                return;
            }

            var hitLimit = _hitRadius + _playerCollisionRadius;
            var hitLimitSq = hitLimit * hitLimit;
            var playerPos = (Vector2)_targetPlayer.transform.position;
            var projectilePos = (Vector2)transform.position;
            if ((playerPos - projectilePos).sqrMagnitude > hitLimitSq)
            {
                return;
            }

            _targetPlayer.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
}
