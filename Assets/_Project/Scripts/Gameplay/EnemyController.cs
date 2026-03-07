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
            TelegraphDash = 1,
            Dashing = 2,
            ProjectileVolley = 3,
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
        private const float FireStackFxScale = 1.35f;
        private const float FireBoomFxScale = 3.3f;
        private const float BossTelegraphDuration = 1f;
        private const float BossDashDuration = 0.8f;
        private const float BossDashSpeedMultiplier = 6f;
        private const float BossProjectileSpeed = 7.2f;
        private const float BossProjectileLifetime = 4f;
        private const float BossProjectileHitRadius = 0.14f;
        private const float BossProjectileVisualScale = 0.22f;
        private const float BossProjectileDamageMultiplier = 0.8f;
        private const float BossProjectileShotInterval = 0.2f;
        private const int BossEightWayShots = 10;
        private const int BossAimShots = 10;
        private const float BossAimSpreadDegrees = 15f;

        public event Action<float, float> Changed;

        private EnemyConfig _config;
        private Transform _target;
        private PlayerHealth _playerHealth;
        private EnemyRegistry _registry;
        private ExperienceSystem _experienceSystem;
        private RuntimeSpriteFactory.EnemyVisualKind _visualKind;

        private float _health;
        private float _maxHealth;
        private float _moveSpeed;
        private float _contactDamage;
        private float _contactDamageCooldown;
        private float _contactCooldown;
        private float _playerCollisionRadius;
        private float _collisionRadius = 0.3f;
        private int _experienceOnDeath = 1;
        private EnemySpriteAnimator _spriteAnimator;
        private bool _isDead;

        private float _activeSlowMultiplier = 1f;
        private float _activeSlowRemaining;
        private float _activeLightBonusMultiplier;
        private float _activeLightRemaining;
        private float _fireAccumulatedDamage;
        private int _fireAccumulatedHits;
        private int _fireTriggerHitCount = int.MaxValue;
        private BossPatternState _bossPatternState;
        private float _bossPatternCooldown;
        private float _bossStateTimer;
        private Vector2 _bossDashDirection = Vector2.right;
        private int _bossEightWayShotsRemaining;
        private int _bossAimShotsRemaining;
        private bool _bossUseEightWayNext = true;
        private float _bossShotTimer;
        private static Material _fireExplosionFxMaterial;
        private Transform _fireStackFxRoot;
        private SpriteFxAnimator _fireStackFxAnimator;

        private readonly System.Collections.Generic.List<EnemyController> _nearbyBuffer = new(24);

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _health;
        public float CollisionRadius => _collisionRadius;
        public RuntimeSpriteFactory.EnemyVisualKind VisualKind => _visualKind;
        public bool IsBoss => _visualKind == RuntimeSpriteFactory.EnemyVisualKind.Boss;

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
            float runtimeMoveSpeedMultiplier = 1f)
        {
            _config = config;
            _visualKind = visualKind;
            _target = target;
            _playerHealth = playerHealth;
            _registry = registry;
            _experienceSystem = experienceSystem;
            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
            _collisionRadius = Mathf.Max(0.05f, collisionRadius);
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
            _bossPatternCooldown = IsBoss ? UnityEngine.Random.Range(1.2f, 2.1f) : float.MaxValue;
            _bossStateTimer = 0f;
            _bossDashDirection = Vector2.right;
            _bossEightWayShotsRemaining = 0;
            _bossAimShotsRemaining = 0;
            _bossUseEightWayNext = true;
            _bossShotTimer = 0f;
            _registry.Register(this);
            Changed?.Invoke(_health, _maxHealth);
        }

        private void OnDisable()
        {
            if (_registry != null)
            {
                _registry.Unregister(this);
            }
        }

        private void Update()
        {
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

        private void Die()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;
            EndBossPattern();
            TriggerFireExplosionIfReady();

            if (_experienceSystem != null)
            {
                _experienceSystem.SpawnOrb(transform.position, _experienceOnDeath);
            }

            if (_registry != null)
            {
                _registry.Unregister(this);
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
                    _bossStateTimer -= deltaTime;
                    if (_bossStateTimer <= 0f)
                    {
                        BeginBossDash();
                    }

                    return true;

                case BossPatternState.Dashing:
                {
                    var step = _bossDashDirection * (Mathf.Max(0.1f, _moveSpeed) * BossDashSpeedMultiplier) * deltaTime;
                    var next = (Vector2)transform.position + step;
                    transform.position = new Vector3(next.x, next.y, transform.position.z);
                    _registry?.NotifyMoved(this, transform.position);

                    _bossStateTimer -= deltaTime;
                    if (_bossStateTimer <= 0f)
                    {
                        EndBossPattern();
                    }

                    return true;
                }

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
            var patternIndex = UnityEngine.Random.Range(0, 2);
            switch (patternIndex)
            {
                case 0:
                    _bossPatternState = BossPatternState.TelegraphDash;
                    _bossStateTimer = BossTelegraphDuration;
                    _spriteAnimator?.PlayHurtOneShot(BossTelegraphDuration);
                    break;

                default:
                    _bossPatternState = BossPatternState.ProjectileVolley;
                    _bossEightWayShotsRemaining = BossEightWayShots;
                    _bossAimShotsRemaining = BossAimShots;
                    _bossUseEightWayNext = true;
                    _bossShotTimer = 0f;
                    break;
            }
        }

        private void BeginBossDash()
        {
            var toPlayer = (Vector2)(_target.position - transform.position);
            _bossDashDirection = toPlayer.sqrMagnitude > 0.000001f ? toPlayer.normalized : Vector2.right;
            _bossPatternState = BossPatternState.Dashing;
            _bossStateTimer = BossDashDuration;
        }

        private void FireBossVolleyStep()
        {
            var shotDuration = Mathf.Max(0.08f, BossProjectileShotInterval * 0.8f);
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

            _bossShotTimer = BossProjectileShotInterval;
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
            _bossPatternState = BossPatternState.None;
            _bossStateTimer = 0f;
            _bossShotTimer = 0f;
            _bossEightWayShotsRemaining = 0;
            _bossAimShotsRemaining = 0;
            _bossUseEightWayNext = true;
            _bossPatternCooldown = UnityEngine.Random.Range(2.4f, 4.2f);
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
            SpawnFireExplosionRangeFx(origin, explosionRadius);
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
