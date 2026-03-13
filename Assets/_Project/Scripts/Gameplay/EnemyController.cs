using System;
using EJR.Game.Core;
using Unity.Netcode;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemyController : MonoBehaviour
    {
        private enum BossPatternState
        {
            None = 0,
            TelegraphDash = 1,
            Dashing = 2,
            DashPause = 3,
            TelegraphProjectile = 4,
            ProjectileVolley = 5,
        }

        private const float FireExplosionRadius = 0.725f;
        private const float FireExplosionMaxRadiusMultiplier = 1.25f;
        private const float FireExplosionMinRadiusRatio = 0.5f;
        private const float FireExplosionFxDuration = 0.18f;
        private const float FireExplosionFxLineWidth = 0.06f;
        private const int FireExplosionFxSegments = 28;
        private static readonly Color FireExplosionFxColor = new(1f, 0.45f, 0.1f, 0.9f);
        private const float FireStackFxFps = 10f;
        private const float FireBoomFxFps = 14f;
        private const float FireStackFxScale = 2.7f;
        private const float FireBoomFxScale = 6f;
        private const float BossTelegraphDuration = 1f;
        private const float BossEnragedTelegraphDuration = 0.8f;
        private const float BossDashDuration = 0.8f;
        private const float BossShortDashDurationMultiplier = 0.5f;
        private const float BossDashPauseDuration = 0.5f;
        private const float BossDashSpeedMultiplier = 6f;
        private const float BossProjectileSpeed = 7.2f;
        private const float BossProjectileLifetime = 4f;
        private const float BossProjectileHitRadius = 0.14f;
        private const float BossProjectileVisualScale = 0.22f;
        private const float BossProjectileDamageMultiplier = 0.8f;
        private const float BossPhase1ProjectileShotInterval = 0.4f;
        private const float BossPhase2ProjectileShotInterval = 0.1f;
        private const int BossPhase1VolleyShots = 3;
        private const int BossPhase2VolleyShots = 7;
        private const float BossDashPatternChance = 0.5f;
        private const float BossAimSpreadDegrees = 15f;
        private const float BossPhase2HealthThreshold = 0.5f;
        private const float BossDashTelegraphLength = 6.5f;
        private const float BossDashTelegraphWidth = 0.12f;
        private const float WindKnockbackCooldown = 0.5f;
        private static readonly Color BossDashTelegraphColor = new(1f, 0.28f, 0.22f, 0.78f);
        private const float StatusIndicatorScale = 0.08f;
        private const float StatusIndicatorHeightOffset = 0.22f;
        private const float StatusIndicatorSpacing = 0.14f;
        private static readonly Color FireIndicatorColor = new(1f, 0.45f, 0.1f, 0.95f);
        private static readonly Color SlowIndicatorColor = new(0.38f, 0.86f, 1f, 0.95f);
        private static readonly Color LightIndicatorColor = new(1f, 0.92f, 0.36f, 0.95f);

        public event Action<float, float> Changed;
        public event Action BossProjectileVolleyStarted;

        private EnemyConfig _config;
        private Transform _target;
        private PlayerHealth _playerHealth;
        private Func<Transform> _targetResolver;
        private Func<PlayerHealth> _playerHealthResolver;
        private EnemyRegistry _registry;
        private ExperienceSystem _experienceSystem;
        private Action<Vector3, int> _experienceOrbSpawner;
        private RuntimeSpriteFactory.EnemyVisualKind _visualKind;
        private NetworkObject _networkObject;

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
        private bool _isDead;

        private float _activeSlowMultiplier = 1f;
        private float _activeSlowRemaining;
        private float _activeLightBonusMultiplier;
        private float _activeLightRemaining;
        private float _lastWindKnockbackAt = -999f;
        private float _fireAccumulatedDamage;
        private int _fireAccumulatedHits;
        private int _fireTriggerHitCount = int.MaxValue;
        private BossPatternState _bossPatternState;
        private float _bossPatternCooldown;
        private float _bossStateTimer;
        private Vector2 _bossDashDirection = Vector2.right;
        private int _bossEightWayShotsRemaining;
        private int _bossAimShotsRemaining;
        private int _bossDashTotalInPattern;
        private int _bossDashRemainingInPattern;
        private int _bossDashStartedInPattern;
        private bool _bossUseDashPatternNext;
        private bool _bossUseEightWayNext = true;
        private float _bossShotTimer;
        private static Material _fireExplosionFxMaterial;
        private static Material _bossDashTelegraphMaterial;
        private Transform _fireStackFxRoot;
        private SpriteFxAnimator _fireStackFxAnimator;
        private LineRenderer _bossDashTelegraphLine;
        private Transform _statusIndicatorRoot;
        private SpriteRenderer _fireIndicatorRenderer;
        private SpriteRenderer _slowIndicatorRenderer;
        private SpriteRenderer _lightIndicatorRenderer;

        private readonly System.Collections.Generic.List<EnemyController> _nearbyBuffer = new(24);

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _health;
        public float CollisionRadius => _collisionRadius;
        public RuntimeSpriteFactory.EnemyVisualKind VisualKind => _visualKind;
        public bool IsBoss => _visualKind == RuntimeSpriteFactory.EnemyVisualKind.Boss;
        public bool IsDead => _isDead;

        public void Initialize(
            EnemyConfig config,
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            EnemyStatProfile statProfile,
            Transform target,
            PlayerHealth playerHealth,
            EnemyRegistry registry,
            ExperienceSystem experienceSystem,
            float playerCollisionRadius,
            float collisionRadius,
            float runtimeHealthMultiplier = 1f,
            float runtimeMoveSpeedMultiplier = 1f,
            bool hasArenaBounds = false,
            Rect arenaBounds = default)
        {
            _config = config;
            _visualKind = visualKind;
            _target = target;
            _playerHealth = playerHealth;
            _registry = registry;
            _experienceSystem = experienceSystem;
            _networkObject = GetComponent<NetworkObject>();
            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
            _collisionRadius = Mathf.Max(0.05f, collisionRadius);
            _hasArenaBounds = hasArenaBounds;
            _arenaBounds = arenaBounds;
            _spriteAnimator = GetComponentInChildren<EnemySpriteAnimator>();

            var healthMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.healthMultiplier) : 1f;
            var moveMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.moveSpeedMultiplier) : 1f;
            var contactDamageMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.contactDamageMultiplier) : 1f;
            var experienceMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.experienceMultiplier) : 1f;
            var elapsedHealthMultiplier = Mathf.Max(0.1f, runtimeHealthMultiplier);
            var elapsedMoveMultiplier = Mathf.Max(0.1f, runtimeMoveSpeedMultiplier);

            _maxHealth = Mathf.Max(1f, config.maxHealth * healthMultiplier * elapsedHealthMultiplier);
            _moveSpeed = Mathf.Max(0.1f, config.moveSpeed * moveMultiplier * elapsedMoveMultiplier);
            _contactDamage = Mathf.Max(0f, config.contactDamage * contactDamageMultiplier);
            _contactDamageCooldown = Mathf.Max(0.05f, config.contactDamageCooldown);
            _experienceOnDeath = Mathf.Max(1, Mathf.RoundToInt(config.experienceOnDeath * experienceMultiplier));

            _health = _maxHealth;
            _bossPatternState = BossPatternState.None;
            _bossPatternCooldown = IsBoss ? GetBossPatternCooldownForCurrentHealth() : float.MaxValue;
            _bossStateTimer = 0f;
            _bossDashDirection = Vector2.right;
            _bossEightWayShotsRemaining = 0;
            _bossAimShotsRemaining = 0;
            _bossDashTotalInPattern = 0;
            _bossDashRemainingInPattern = 0;
            _bossDashStartedInPattern = 0;
            _bossUseDashPatternNext = UnityEngine.Random.value < GetBossDashPatternChance();
            _bossUseEightWayNext = true;
            _bossShotTimer = 0f;
            _registry.Register(this);
            Changed?.Invoke(_health, _maxHealth);
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

            HideBossDashTelegraphFx();
        }

        private void Update()
        {
            if (_networkObject == null)
            {
                _networkObject = GetComponent<NetworkObject>();
            }

            if (_networkObject != null &&
                _networkObject.IsSpawned &&
                NetworkManager.Singleton != null &&
                !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            RefreshResolvedTarget();

            if (_isDead || _target == null || _config == null || _playerHealth == null)
            {
                return;
            }

            TickCoreEffectDurations();

            var previousPosition = transform.position;
            var handledByBossPattern = UpdateBossPattern(Time.deltaTime);
            if (!handledByBossPattern)
            {
                var toPlayer = _target.position - transform.position;
                var distance = toPlayer.magnitude;
                var direction = distance > 0.001f ? toPlayer / distance : Vector3.zero;
                var minimumSeparation = CollisionRadius + _playerCollisionRadius;
                var separation = ComputeSeparationVector((Vector2)transform.position) * Mathf.Max(0f, _config.separationWeight);

                var desired = separation;
                if (distance > minimumSeparation)
                {
                    desired += (Vector2)direction;
                }

                if (desired.sqrMagnitude > 1f)
                {
                    desired.Normalize();
                }

                var effectiveMoveSpeed = _moveSpeed * Mathf.Clamp(_activeSlowMultiplier, 0.1f, 1f);
                var moveBudget = effectiveMoveSpeed * Time.deltaTime;
                var next = transform.position + (Vector3)(desired * moveBudget);
                next = ResolvePlayerOverlap(next, minimumSeparation, (Vector2)direction);
                next = ResolveCrowdOverlaps(next);
                transform.position = next;
                _registry?.NotifyMoved(this, transform.position);
            }

            if (_spriteAnimator != null)
            {
                var velocity = ((Vector2)(transform.position - previousPosition)) / Mathf.Max(0.0001f, Time.deltaTime);
                _spriteAnimator.SetMotion(velocity);
            }

            _contactCooldown -= Time.deltaTime;
            var minimumSeparationForContact = CollisionRadius + _playerCollisionRadius;
            var currentDistance = (_target.position - transform.position).magnitude;
            if (currentDistance <= minimumSeparationForContact + 0.02f && _contactCooldown <= 0f)
            {
                _contactCooldown = _contactDamageCooldown;
                _playerHealth.TakeDamage(_contactDamage);
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

        public void ReceiveDamage(float damage)
        {
            ReceiveWeaponDamage(damage, WeaponUpgradeId.Rifle, WeaponCoreElement.None, 0);
        }

        public void ReceiveWeaponDamage(float damage, WeaponUpgradeId sourceWeaponId, WeaponCoreElement coreElement, int coreLevel)
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

            var lightBonusMultiplier = 0f;
            if (_activeLightRemaining > 0f && _activeLightBonusMultiplier > 0f)
            {
                lightBonusMultiplier = _activeLightBonusMultiplier;
            }

            if (coreElement == WeaponCoreElement.Light && coreLevel > 0)
            {
                var immediateLightMultiplier = GetLightBonusMultiplierForLevel(Mathf.Clamp(coreLevel, 1, PlayerBuildRuntime.MaxCoreLevel));
                lightBonusMultiplier = Mathf.Max(lightBonusMultiplier, immediateLightMultiplier);
            }

            var lightBonusDamage = baseDamage * lightBonusMultiplier;

            var appliedDamage = baseDamage + lightBonusDamage;
            _health = Mathf.Max(0f, _health - appliedDamage);
            if (_health > 0f &&
                _visualKind != RuntimeSpriteFactory.EnemyVisualKind.Boss &&
                _visualKind != RuntimeSpriteFactory.EnemyVisualKind.Skeleton)
            {
                _spriteAnimator?.PlayHurt();
            }

            var basePopupPosition = transform.position + new Vector3(0f, 0.8f, 0f);
            CombatTextSpawner.SpawnDamage(basePopupPosition, baseDamage, CombatTextSpawner.EnemyDamagedColor);
            if (lightBonusDamage > 0f)
            {
                CombatTextSpawner.SpawnDamage(basePopupPosition + new Vector3(0f, 0.18f, 0f), lightBonusDamage, CombatTextSpawner.LightBonusColor);
            }
            Changed?.Invoke(_health, MaxHealth);

            if (coreLevel > 0)
            {
                ApplyWeaponCoreOnHit(coreElement, coreLevel, appliedDamage);
            }

            if (_health <= 0f)
            {
                Die();
            }
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
            EndBossPattern();
            TriggerFireExplosionIfReady();

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

            if (_networkObject == null)
            {
                _networkObject = GetComponent<NetworkObject>();
            }

            if (_networkObject != null && _networkObject.IsSpawned)
            {
                _networkObject.Despawn(true);
                return;
            }

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

            UpdateStatusIndicators();
        }

        private bool UpdateBossPattern(float deltaTime)
        {
            if (!IsBoss || _playerHealth == null || _target == null)
            {
                return false;
            }

            switch (_bossPatternState)
            {
                case BossPatternState.TelegraphDash:
                    UpdateBossDashTelegraphFx();
                    _bossStateTimer -= deltaTime;
                    if (_bossStateTimer <= 0f)
                    {
                        HideBossDashTelegraphFx();
                        BeginBossDash();
                    }

                    return true;

                case BossPatternState.TelegraphProjectile:
                    _bossStateTimer -= deltaTime;
                    if (_bossStateTimer <= 0f)
                    {
                        BeginBossProjectileVolley();
                    }

                    return true;

                case BossPatternState.Dashing:
                {
                    var step = _bossDashDirection * (Mathf.Max(0.1f, _moveSpeed) * BossDashSpeedMultiplier) * deltaTime;
                    var next = (Vector2)transform.position + step;
                    var clamped = ClampPositionToArena(new Vector3(next.x, next.y, transform.position.z));
                    transform.position = clamped;
                    _registry?.NotifyMoved(this, transform.position);

                    _bossStateTimer -= deltaTime;
                    if (_bossStateTimer <= 0f)
                    {
                        if (_bossDashRemainingInPattern > 0)
                        {
                            _bossPatternState = BossPatternState.DashPause;
                            _bossStateTimer = BossDashPauseDuration;
                        }
                        else
                        {
                            EndBossPattern();
                        }
                    }

                    return true;
                }

                case BossPatternState.DashPause:
                    _bossStateTimer -= deltaTime;
                    if (_bossStateTimer <= 0f)
                    {
                        BeginBossDash();
                    }

                    return true;

                case BossPatternState.ProjectileVolley:
                    _bossShotTimer -= deltaTime;
                    if (_bossShotTimer <= 0f)
                    {
                        FireBossVolleyStep();
                    }

                    return true;
            }

            _bossPatternCooldown -= deltaTime;
            if (_bossPatternCooldown > 0f)
            {
                return false;
            }

            StartRandomBossPattern();
            return true;
        }

        private void StartRandomBossPattern()
        {
            var useDashPattern = _bossUseDashPatternNext;
            _bossUseDashPatternNext = !_bossUseDashPatternNext;
            if (useDashPattern)
            {
                _bossDashTotalInPattern = GetBossDashCountForCurrentHealth();
                _bossDashRemainingInPattern = _bossDashTotalInPattern;
                _bossDashStartedInPattern = 0;
                _bossPatternState = BossPatternState.TelegraphDash;
                var telegraphDuration = GetBossTelegraphDurationForCurrentHealth();
                _bossStateTimer = telegraphDuration;
                _spriteAnimator?.PlayHurtOneShot(telegraphDuration);
                UpdateBossDashTelegraphFx();
                return;
            }

            HideBossDashTelegraphFx();
            var projectileTelegraphDuration = GetBossTelegraphDurationForCurrentHealth();
            _bossPatternState = BossPatternState.TelegraphProjectile;
            _bossStateTimer = projectileTelegraphDuration;
            _spriteAnimator?.PlayHurtOneShot(projectileTelegraphDuration);
        }

        private void BeginBossProjectileVolley()
        {
            HideBossDashTelegraphFx();
            _bossPatternState = BossPatternState.ProjectileVolley;
            _bossEightWayShotsRemaining = GetBossEightWayShotsForCurrentHealth();
            _bossAimShotsRemaining = GetBossAimShotsForCurrentHealth();
            _bossUseEightWayNext = true;
            _bossShotTimer = 0f;
            BossProjectileVolleyStarted?.Invoke();
        }

        private void BeginBossDash()
        {
            HideBossDashTelegraphFx();
            if (_bossDashRemainingInPattern <= 0)
            {
                _bossDashRemainingInPattern = 1;
            }

            _bossDashStartedInPattern++;
            _bossDashRemainingInPattern = Mathf.Max(0, _bossDashRemainingInPattern - 1);
            var toPlayer = (Vector2)(_target.position - transform.position);
            _bossDashDirection = toPlayer.sqrMagnitude > 0.000001f ? toPlayer.normalized : Vector2.right;
            _bossPatternState = BossPatternState.Dashing;
            var useShortDash = _bossDashTotalInPattern >= 3;
            var dashDuration = useShortDash
                ? BossDashDuration * BossShortDashDurationMultiplier
                : BossDashDuration;
            _bossStateTimer = Mathf.Max(0.05f, dashDuration);
        }

        private void FireBossVolleyStep()
        {
            var shotDuration = Mathf.Max(0.08f, GetBossProjectileShotIntervalForCurrentHealth() * 0.8f);
            _spriteAnimator?.PlayAttackOneShot(shotDuration);

            var canFireEightWay = _bossEightWayShotsRemaining > 0;
            var canFireAim = _bossAimShotsRemaining > 0;
            if (!canFireEightWay && !canFireAim)
            {
                EndBossPattern();
                return;
            }

            var fireEightWay = canFireEightWay && (!canFireAim || _bossUseEightWayNext);
            if (fireEightWay)
            {
                FireBossEightWayBurst();
                _bossEightWayShotsRemaining--;
            }
            else
            {
                FireBossAimShot();
                _bossAimShotsRemaining--;
            }

            if (_bossEightWayShotsRemaining > 0 && _bossAimShotsRemaining > 0)
            {
                _bossUseEightWayNext = !_bossUseEightWayNext;
            }
            else
            {
                _bossUseEightWayNext = _bossEightWayShotsRemaining > 0;
            }

            if (_bossEightWayShotsRemaining <= 0 && _bossAimShotsRemaining <= 0)
            {
                EndBossPattern();
                return;
            }

            _bossShotTimer = GetBossProjectileShotIntervalForCurrentHealth();
        }

        private void FireBossEightWayBurst()
        {
            for (var i = 0; i < 8; i++)
            {
                var radians = (Mathf.PI * 2f * i) / 8f;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                SpawnBossProjectile(direction);
            }
        }

        private void FireBossAimShot()
        {
            var toPlayer = (Vector2)(_target.position - transform.position);
            var centerDirection = toPlayer.sqrMagnitude > 0.000001f ? toPlayer.normalized : Vector2.right;
            SpawnBossProjectile(centerDirection);
            SpawnBossProjectile(RotateDirection(centerDirection, -BossAimSpreadDegrees));
            SpawnBossProjectile(RotateDirection(centerDirection, BossAimSpreadDegrees));
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
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
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
                BossProjectileSpeed,
                BossProjectileLifetime,
                Mathf.Max(1f, _contactDamage * BossProjectileDamageMultiplier),
                BossProjectileHitRadius,
                _playerHealth,
                _playerCollisionRadius);
        }

        private void EndBossPattern()
        {
            HideBossDashTelegraphFx();
            _bossPatternState = BossPatternState.None;
            _bossStateTimer = 0f;
            _bossShotTimer = 0f;
            _bossEightWayShotsRemaining = 0;
            _bossAimShotsRemaining = 0;
            _bossDashTotalInPattern = 0;
            _bossDashRemainingInPattern = 0;
            _bossDashStartedInPattern = 0;
            _bossUseEightWayNext = true;
            _bossPatternCooldown = GetBossPatternCooldownForCurrentHealth();
        }

        private float GetBossHealthRatio()
        {
            if (_maxHealth <= 0.0001f)
            {
                return 1f;
            }

            return Mathf.Clamp01(_health / _maxHealth);
        }

        private float GetBossDashPatternChance()
        {
            return BossDashPatternChance;
        }

        private int GetBossDashCountForCurrentHealth()
        {
            var healthRatio = GetBossHealthRatio();
            return healthRatio > BossPhase2HealthThreshold ? 1 : 3;
        }

        private int GetBossEightWayShotsForCurrentHealth()
        {
            var healthRatio = GetBossHealthRatio();
            if (healthRatio > BossPhase2HealthThreshold)
            {
                return BossPhase1VolleyShots;
            }

            return BossPhase2VolleyShots;
        }

        private int GetBossAimShotsForCurrentHealth()
        {
            var healthRatio = GetBossHealthRatio();
            if (healthRatio > BossPhase2HealthThreshold)
            {
                return BossPhase1VolleyShots;
            }

            return BossPhase2VolleyShots;
        }

        private float GetBossProjectileShotIntervalForCurrentHealth()
        {
            var healthRatio = GetBossHealthRatio();
            if (healthRatio > BossPhase2HealthThreshold)
            {
                return BossPhase1ProjectileShotInterval;
            }

            return BossPhase2ProjectileShotInterval;
        }

        private float GetBossPatternCooldownForCurrentHealth()
        {
            var healthRatio = GetBossHealthRatio();
            if (healthRatio > BossPhase2HealthThreshold)
            {
                return UnityEngine.Random.Range(2.8f, 4.6f);
            }

            return UnityEngine.Random.Range(1.3f, 2.4f);
        }

        private float GetBossTelegraphDurationForCurrentHealth()
        {
            var healthRatio = GetBossHealthRatio();
            return healthRatio > BossPhase2HealthThreshold
                ? BossTelegraphDuration
                : BossEnragedTelegraphDuration;
        }

        private void UpdateBossDashTelegraphFx()
        {
            if (!IsBoss || _target == null)
            {
                HideBossDashTelegraphFx();
                return;
            }

            EnsureBossDashTelegraphFx();
            if (_bossDashTelegraphLine == null)
            {
                return;
            }

            var toPlayer = (Vector2)(_target.position - transform.position);
            var direction = toPlayer.sqrMagnitude > 0.000001f ? toPlayer.normalized : Vector2.right;
            var start = (Vector2)transform.position;
            var end = start + (direction * BossDashTelegraphLength);

            _bossDashTelegraphLine.enabled = true;
            _bossDashTelegraphLine.SetPosition(0, new Vector3(start.x, start.y, -0.03f));
            _bossDashTelegraphLine.SetPosition(1, new Vector3(end.x, end.y, -0.03f));
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

        private void HideBossDashTelegraphFx()
        {
            if (_bossDashTelegraphLine != null)
            {
                _bossDashTelegraphLine.enabled = false;
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

        private void ApplyWeaponCoreOnHit(WeaponCoreElement coreElement, int coreLevel, float dealtDamage)
        {
            var clampedLevel = Mathf.Clamp(coreLevel, 1, PlayerBuildRuntime.MaxCoreLevel);
            switch (coreElement)
            {
                case WeaponCoreElement.Fire:
                    ApplyFireCore(clampedLevel, dealtDamage);
                    break;
                case WeaponCoreElement.Wind:
                    ApplyWaterCore(clampedLevel);
                    break;
                case WeaponCoreElement.Light:
                    ApplyLightCore(clampedLevel);
                    break;
                case WeaponCoreElement.Water:
                    ApplyWindCore(clampedLevel);
                    break;
            }
        }

        private void ApplyFireCore(int coreLevel, float dealtDamage)
        {
            var (accumulateRatio, hitThreshold) = coreLevel switch
            {
                1 => (0.10f, 5),
                2 => (0.20f, 4),
                _ => (0.30f, 2),
            };

            _fireAccumulatedDamage += Mathf.Max(0f, dealtDamage) * accumulateRatio;
            _fireAccumulatedHits++;
            _fireTriggerHitCount = hitThreshold;
            ShowFireStackFx();
            UpdateStatusIndicators();

            if (_fireAccumulatedHits >= _fireTriggerHitCount)
            {
                TriggerFireExplosionIfReady();
            }
        }

        private void ApplyWindCore(int coreLevel)
        {
            if (IsBoss)
            {
                return;
            }

            var (slowPercent, duration) = coreLevel switch
            {
                1 => (0.30f, 1.0f),
                2 => (0.50f, 1.0f),
                _ => (0.80f, 1.0f),
            };

            var slowMultiplier = Mathf.Clamp01(1f - slowPercent);
            _activeSlowMultiplier = Mathf.Min(_activeSlowMultiplier, slowMultiplier);
            _activeSlowRemaining = Mathf.Max(_activeSlowRemaining, duration);
            UpdateStatusIndicators();
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

        private static float GetLightBonusMultiplierForLevel(int coreLevel)
        {
            return coreLevel switch
            {
                1 => 0.10f,
                2 => 0.20f,
                _ => 0.30f,
            };
        }

        private void ApplyWaterCore(int coreLevel)
        {
            if (IsBoss)
            {
                return;
            }

            if (Time.time < _lastWindKnockbackAt + WindKnockbackCooldown)
            {
                return;
            }

            var knockbackDistance = coreLevel switch
            {
                1 => 0.1f,
                2 => 0.2f,
                _ => 0.3f,
            };

            if (knockbackDistance <= 0f)
            {
                return;
            }

            var away = _target != null
                ? (Vector2)(transform.position - _target.position)
                : Vector2.zero;
            if (away.sqrMagnitude <= 0.000001f)
            {
                away = UnityEngine.Random.insideUnitCircle;
            }

            if (away.sqrMagnitude <= 0.000001f)
            {
                away = Vector2.right;
            }

            var next = (Vector2)transform.position + (away.normalized * knockbackDistance);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            _registry?.NotifyMoved(this, transform.position);
            _lastWindKnockbackAt = Time.time;
        }

        private void ShowFireStackFx()
        {
            var frames = RuntimeSpriteFactory.GetSexyFireStackAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                return;
            }

            if (_fireStackFxRoot == null)
            {
                var fxObject = new GameObject("FireStackFx");
                fxObject.transform.SetParent(transform, false);
                var stackScale = Mathf.Max(0.05f, FireStackFxScale);
                fxObject.transform.localScale = Vector3.one * stackScale;

                var renderer = fxObject.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.color = Color.white;
                renderer.sortingOrder = 45;
                fxObject.transform.localPosition = GetColliderCenteredOffset(renderer.sprite, stackScale, -0.02f);

                _fireStackFxAnimator = fxObject.AddComponent<SpriteFxAnimator>();
                _fireStackFxAnimator.Initialize(renderer, frames, FireStackFxFps, loop: true, destroyOnComplete: false);
                _fireStackFxRoot = fxObject.transform;
            }

            if (_fireStackFxRoot != null && !_fireStackFxRoot.gameObject.activeSelf)
            {
                _fireStackFxRoot.gameObject.SetActive(true);
                _fireStackFxAnimator?.PlayFromStart();
            }
        }

        private void HideFireStackFx()
        {
            if (_fireStackFxRoot != null)
            {
                _fireStackFxRoot.gameObject.SetActive(false);
            }
        }

        private void SpawnFireBoomFx(Vector2 origin, float explosionRadius)
        {
            var frames = RuntimeSpriteFactory.GetSexyFireBoomAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                return;
            }

            var fxObject = new GameObject("FireBoomFx");
            var boomScale = Mathf.Max(0.1f, FireBoomFxScale * explosionRadius);
            fxObject.transform.localScale = Vector3.one * boomScale;

            var renderer = fxObject.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = Color.white;
            renderer.sortingOrder = 530;
            var centeredOffset = GetColliderCenteredOffset(renderer.sprite, boomScale, -0.02f);
            fxObject.transform.position = new Vector3(origin.x, origin.y, 0f) + centeredOffset;

            var animator = fxObject.AddComponent<SpriteFxAnimator>();
            animator.Initialize(renderer, frames, FireBoomFxFps, loop: false, destroyOnComplete: true);
        }

        private static Vector3 GetColliderCenteredOffset(Sprite sprite, float uniformScale, float z)
        {
            if (sprite == null)
            {
                return new Vector3(0f, 0f, z);
            }

            var centerFromPivot = sprite.bounds.center;
            return new Vector3(
                -centerFromPivot.x * uniformScale,
                -centerFromPivot.y * uniformScale,
                z);
        }

        private void TriggerFireExplosionIfReady()
        {
            if (_fireAccumulatedDamage <= 0f)
            {
                ResetFireAccumulation();
                return;
            }

            var explosionDamage = _fireAccumulatedDamage;
            var hitProgress = Mathf.Clamp01(_fireAccumulatedHits / (float)Mathf.Max(1, _fireTriggerHitCount));
            var maxExplosionRadius = FireExplosionRadius * FireExplosionMaxRadiusMultiplier;
            var scaledRadiusRatio = Mathf.Lerp(FireExplosionMinRadiusRatio, 1f, hitProgress);
            var explosionRadius = maxExplosionRadius * scaledRadiusRatio;
            ResetFireAccumulation();

            if (_registry == null)
            {
                return;
            }

            if (explosionRadius <= 0.0001f)
            {
                return;
            }

            var origin = (Vector2)transform.position;
            SpawnFireBoomFx(origin, explosionRadius);
            var searchRadius = explosionRadius + _registry.GetMaxCollisionRadius();
            _registry.GetNearby(origin, searchRadius, _nearbyBuffer);

            for (var i = 0; i < _nearbyBuffer.Count; i++)
            {
                var enemy = _nearbyBuffer[i];
                if (enemy == null || ReferenceEquals(enemy, this))
                {
                    continue;
                }

                var toEnemy = (Vector2)enemy.transform.position - origin;
                var distance = toEnemy.magnitude;
                var limit = explosionRadius + enemy.CollisionRadius;
                if (distance > limit)
                {
                    continue;
                }

                enemy.ReceiveDamage(explosionDamage);
            }
        }

        private static void SpawnFireExplosionRangeFx(Vector2 origin, float radius)
        {
            var fxObject = new GameObject("FireExplosionFx");
            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.positionCount = FireExplosionFxSegments;
            lineRenderer.startWidth = FireExplosionFxLineWidth;
            lineRenderer.endWidth = FireExplosionFxLineWidth;
            lineRenderer.startColor = FireExplosionFxColor;
            lineRenderer.endColor = FireExplosionFxColor;
            lineRenderer.sortingOrder = 520;
            lineRenderer.sharedMaterial = GetOrCreateFireExplosionFxMaterial();

            for (var i = 0; i < FireExplosionFxSegments; i++)
            {
                var t = i / (float)FireExplosionFxSegments;
                var angle = t * Mathf.PI * 2f;
                var point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                lineRenderer.SetPosition(i, new Vector3(point.x, point.y, -0.02f));
            }

            Destroy(fxObject, FireExplosionFxDuration);
        }

        private static Material GetOrCreateFireExplosionFxMaterial()
        {
            if (_fireExplosionFxMaterial != null)
            {
                return _fireExplosionFxMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _fireExplosionFxMaterial = new Material(shader)
            {
                name = "FireExplosionFxMat",
                hideFlags = HideFlags.HideAndDontSave,
            };

            return _fireExplosionFxMaterial;
        }

        private void ResetFireAccumulation()
        {
            _fireAccumulatedDamage = 0f;
            _fireAccumulatedHits = 0;
            _fireTriggerHitCount = int.MaxValue;
            HideFireStackFx();
            UpdateStatusIndicators();
        }

        private void UpdateStatusIndicators()
        {
            var showSlow = _activeSlowRemaining > 0f && _activeSlowMultiplier < 0.999f;
            var showLight = _activeLightRemaining > 0f && _activeLightBonusMultiplier > 0f;
            var showFire = _fireAccumulatedHits > 0 && _fireAccumulatedDamage > 0f && _fireTriggerHitCount < int.MaxValue;
            if (!showSlow && !showLight && !showFire)
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
            var activeCount = (showFire ? 1 : 0) + (showSlow ? 1 : 0) + (showLight ? 1 : 0);
            var firstX = -StatusIndicatorSpacing * 0.5f * Mathf.Max(0, activeCount - 1);
            var slotIndex = 0;

            if (_fireIndicatorRenderer != null)
            {
                _fireIndicatorRenderer.enabled = showFire;
                if (showFire)
                {
                    _fireIndicatorRenderer.transform.localPosition = new Vector3(firstX + (slotIndex * StatusIndicatorSpacing), y, -0.03f);
                    slotIndex++;
                }
            }

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

            if (_fireIndicatorRenderer == null)
            {
                _fireIndicatorRenderer = CreateStatusIndicator("FireIndicator", FireIndicatorColor);
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

        private void Update()
        {
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
