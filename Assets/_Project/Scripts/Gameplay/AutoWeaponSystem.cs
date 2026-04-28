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

        #region System: Lifecycle & Engine

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

        private void OnDisable()
        {
            CleanupLoadoutRuntimeState();
            _nextLifestealAt = -999f;
        }

        private void UpdateWeapon(WeaponRuntime weapon)
        {
            if (weapon == null) return;

            weapon.Cooldown -= Time.deltaTime;
            weapon.Strategy?.Update(weapon, this);
            
            if (weapon.Cooldown <= 0f)
            {
                if (FireWeapon(weapon))
                {
                    // 발사 성공 시에만 쿨타임 설정
                    weapon.Cooldown = GetAttackInterval(weapon);
                }
                else
                {
                    // 적이 없어 발사 실패 시, 다음 프레임에 즉시 다시 시도하도록 쿨타임 0 유지
                    weapon.Cooldown = 0f;
                }
            }
        }

        private bool FireWeapon(WeaponRuntime weapon)
        {
            if (weapon == null || !TryResolveFireDirection(weapon, out var fireDirection)) return false;
            weapon.Strategy?.OnFire(weapon, this, fireDirection);
            return true;
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
            if (weapon == null) return;

            if (weapon.ActiveChainCoroutine != null)
            {
                StopCoroutine(weapon.ActiveChainCoroutine);
                weapon.ActiveChainCoroutine = null;
            }

            weapon.BurstTotalShots = 0;
            weapon.BurstOrigin = Vector2.zero;
        }

        #endregion

        [SerializeField, Min(0)] private int projectilePoolPrewarmCount = 40;
        [SerializeField, Min(0.01f)] private float targetScanInterval = 0.08f;
        [SerializeField, Min(0.5f)] private float projectileTravelRangeFactor = 1.35f;
        [Header("Debug Gizmos")]
        [SerializeField] private bool showWeaponCollisionGizmos = true;
        [SerializeField] private Color satelliteHitGizmoColor = new(0.35f, 1f, 0.95f, 0.95f);
        
        [SerializeField] private Color chainFxColor = new(0.45f, 0.85f, 1f, 0.95f);
        [SerializeField] private Color turretTracerFxColor = new(1f, 0.86f, 0.28f, 0.95f);

        public WeaponConfig Config => _config;
        public Transform Owner => _owner;
        public EnemyRegistry Registry => _registry;
        public PlayerStatsRuntime Stats => _stats;
        public PlayerBuildRuntime Build => _build;
        public Transform ProjectilePoolRoot => _projectilePoolRoot;
        public PlayerHealth PlayerHealth => _playerHealth;


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
        private EnemyController _currentTarget;
        private Vector2 _lastAimDirection = Vector2.right;
        public Vector2 LastAimDirection => _lastAimDirection;
        public Vector2 FacingDirection => _facingDirectionResolver != null ? _facingDirectionResolver() : Vector2.right;
        private float _targetScanCooldown;
        private float _nextLifestealAt = -999f;
        private Transform _projectilePoolRoot;
        private readonly Queue<Projectile> _projectilePool = new();
        private readonly List<EnemyController> _cleanupEnemies = new(16);

        private readonly List<WeaponRuntime> _loadout = new(4);
        private readonly List<EnemyController> _nearbyEnemies = new(32);
        private readonly List<EnemyController> _candidateEnemies = new(64);

        public event Action<Vector2> AimUpdated;
        public event Action<Vector2> Fired;
        public event Action<ProjectileSpawnRequest> ProjectileVisualRequested;
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
            _nextLifestealAt = -999f;
            _currentTarget = null;
            _lastAimDirection = Vector2.right;
            _targetScanCooldown = 0f;
            EnsureProjectilePool();
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

            if (build == null || build.OwnedWeapons.Count <= 0)
            {
                CleanupLoadoutRuntimeState();
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

                runtime.Strategy = WeaponStrategyFactory.GetStrategy(id);
                runtime.Strategy?.OnInitialize(runtime, this);
                nextLoadout.Add(runtime);
            }

            foreach (var pair in existingById)
            {
                CleanupWeaponRuntimeState(pair.Value);
            }

            _loadout.Clear();
            _loadout.AddRange(nextLoadout);
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

            // 풀로 돌아올 때 동적으로 생성된 VFX 자식 오브젝트들을 정리합니다.
            for (var i = projectileObject.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(projectileObject.transform.GetChild(i).gameObject);
            }

            projectileObject.SetActive(false);
            projectileObject.transform.SetParent(_projectilePoolRoot, false);
            _projectilePool.Enqueue(projectile);
        }

        private float ApplyContextualDamageModifiers(float damage, EnemyController enemy)
        {
            if (_build == null || enemy == null)
            {
                return Mathf.Max(0f, damage);
            }

            var attackerPosition = _owner != null ? _owner.position : Vector3.zero;
            return Mathf.Max(0f, damage) * _build.GetContextualDamageMultiplier(enemy, attackerPosition);
        }

        private void TryApplyDirectHitLifesteal(float damageDealt, EnemyController enemy)
        {
            if (_build == null || _playerHealth == null)
            {
                return;
            }

            if (damageDealt <= 0f)
            {
                return;
            }

            var lifestealRatio = _build.LifestealDamageRatio;
            if (lifestealRatio <= 0f)
            {
                return;
            }

            var now = Time.time;
            if (now + 0.0001f < _nextLifestealAt)
            {
                return;
            }

            _nextLifestealAt = now + Mathf.Max(0f, _build.LifestealInternalCooldown);
            var effectiveDamage = damageDealt;
            if (enemy != null && enemy.IsBoss)
            {
                effectiveDamage *= Mathf.Clamp01(_build.LifestealBossMultiplier);
            }

            var healAmount = effectiveDamage * lifestealRatio;
            if (healAmount <= 0f)
            {
                return;
            }

            var clampedHeal = Mathf.Max(1f, healAmount);
            if (_build.LifestealMaxHealPerHit > 0f)
            {
                clampedHeal = Mathf.Min(clampedHeal, _build.LifestealMaxHealPerHit);
            }

            _playerHealth.Heal(clampedHeal);
        }


        private float GetProjectileVisualScale(WeaponUpgradeId weaponId, float baseScale)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Fireball => 1.0f,
                _ => baseScale,
            };
        }

        private Quaternion GetProjectileVisualRotation(WeaponUpgradeId weaponId, Vector2 direction)
        {
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle);
        }

        private Sprite GetProjectileVisualSprite(WeaponUpgradeId weaponId)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Fireball => RuntimeSpriteFactory.GetFireballProjectileSprite(),
                _ => null,
            };
        }

        private bool ShouldUseProjectileSourceColor(WeaponUpgradeId weaponId)
        {
            return weaponId == WeaponUpgradeId.Fireball;
        }

        private bool GetProjectileVisualFlipX(WeaponUpgradeId weaponId, Vector2 direction)
        {
            return false;
        }

        public Projectile SpawnProjectile(
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
            Vector3? overrideSpawnPosition = null,
            Action<float, EnemyController> directHitCallback = null,
            bool isFragment = false,
            EnemyController initialIgnoreTarget = null)
        {
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : _lastAimDirection;
            SetAimDirection(normalizedDirection);

            var spawnPosition = overrideSpawnPosition
                ?? (_projectileSpawnResolver != null ? _projectileSpawnResolver(normalizedDirection) : _owner.position);
            var projectileVisualScale = GetProjectileVisualScale(weaponId, _config.projectileVisualScale);

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
                projectileVisualScale);

            if (_projectileSpawnOverride != null && _projectileSpawnOverride.Invoke(spawnRequest))
            {
                ProjectileVisualRequested?.Invoke(spawnRequest);
                Fired?.Invoke(normalizedDirection);
                return null;
            }

            var projectile = GetPooledProjectile();
            var projectileTransform = projectile.transform;
            var rotation = GetProjectileVisualRotation(weaponId, normalizedDirection);
            projectileTransform.SetPositionAndRotation(spawnPosition, rotation);
            projectileTransform.localScale = Vector3.one * spawnRequest.VisualScale;

            // [비주얼 처리] 설정된 프리팹이 있으면 소환하고, 없으면 기존 스프라이트 방식을 사용합니다.
            var prefab = GetProjectilePrefab(weaponId, normalizedDirection);
            var renderer = projectile.GetComponent<SpriteRenderer>();

            if (prefab != null)
            {
                // 프리팹이 있으면 기존 스프라이트 렌더러는 끄고 프리팹을 자식으로 생성합니다.
                if (renderer != null) renderer.enabled = false;
                var vfx = Instantiate(prefab, projectileTransform);
                vfx.transform.localPosition = Vector3.zero;
                vfx.transform.localRotation = Quaternion.identity;
                
                // [수정] 위/아래 전용 프리팹인지 확인
                var isDirectionalPrefab = (weaponId == WeaponUpgradeId.Fireball) && 
                                          (prefab == _config.fireballUpProjectilePrefab || prefab == _config.fireballDownProjectilePrefab);
                
                if (isDirectionalPrefab)
                {
                    projectileTransform.rotation = Quaternion.identity;
                }
                else if (normalizedDirection.x < 0f)
                {
                    // [추가] 옆으로 쏘는 프리팹인데 왼쪽 방향이라면 좌우 반전(Flip) 처리
                    // 회전은 0도로 초기화하고 Scale만 뒤집어서 빛 방향을 유지합니다.
                    projectileTransform.rotation = Quaternion.identity;
                    vfx.transform.localScale = new Vector3(-1f, 1f, 1f);
                }
                else
                {
                    vfx.transform.localScale = Vector3.one;
                }
            }
            else if (renderer != null)
            {
                // 프리팹이 등록되지 않은 경우 기존 스프라이트 로직 유지
                renderer.enabled = true;
                renderer.sprite = GetProjectileVisualSprite(weaponId);
                renderer.color = ShouldUseProjectileSourceColor(weaponId) ? Color.white : color;
                renderer.flipX = GetProjectileVisualFlipX(weaponId, normalizedDirection);
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
                (dmg, enemy) => {
                    TryApplyDirectHitLifesteal(dmg, enemy);
                    // [추가] 명중 시 임팩트 VFX 소환
                    SpawnImpactVfx(weaponId, enemy.transform.position);
                    // [추가] 외부에서 정의한 추가 명중 로직 수행
                    directHitCallback?.Invoke(dmg, enemy);
                },
                _useProjectileBoundsCulling,
                _projectileCullBounds,
                _owner,
                _build,
                isFragment,
                initialIgnoreTarget);

            ProjectileVisualRequested?.Invoke(spawnRequest);
            Fired?.Invoke(normalizedDirection);
            return projectile;
        }

        private void SpawnImpactVfx(WeaponUpgradeId weaponId, Vector3 position)
        {
            var prefab = GetImpactPrefab(weaponId);
            if (prefab == null) return;
            
            var vfx = Instantiate(prefab, position, Quaternion.identity);

            if (weaponId == WeaponUpgradeId.Bubble)
            {
                vfx.name = "BubbleImpactVfx";
                vfx.transform.localScale = Vector3.one * 1.5f; // 임팩트도 1.5배로 키워서 폭발감을 줌

                var particleRenderers = vfx.GetComponentsInChildren<ParticleSystemRenderer>();
                foreach (var psr in particleRenderers)
                {
                    psr.alignment = ParticleSystemRenderSpace.Local;
                }
            }

            // 이펙트 프리팹들은 보통 스스로 파괴되는 로직이 있거나 파티클 시스템입니다.
            // 안전을 위해 2초 뒤 파괴 설정을 해둡니다.
            Destroy(vfx, 2f);
        }

        private GameObject GetProjectilePrefab(WeaponUpgradeId weaponId, Vector2 direction)
        {
            if (_config == null) return null;
            return weaponId switch
            {
                WeaponUpgradeId.Fireball => ResolveFireballPrefab(direction),
                _ => _config.projectilePrefab,
            };
        }

        private GameObject ResolveFireballPrefab(Vector2 direction)
        {
            // Y축 방향이 강할 때 전용 프리팹을 우선 반환합니다.
            if (direction.y > 0.7f && _config.fireballUpProjectilePrefab != null) return _config.fireballUpProjectilePrefab;
            if (direction.y < -0.7f && _config.fireballDownProjectilePrefab != null) return _config.fireballDownProjectilePrefab;
            
            return _config.fireballProjectilePrefab ?? _config.projectilePrefab;
        }

        private GameObject GetImpactPrefab(WeaponUpgradeId weaponId)
        {
            if (_config == null) return null;
            return weaponId switch
            {
                WeaponUpgradeId.Fireball => _config.fireballImpactVfxPrefab ?? _config.impactVfxPrefab,
                WeaponUpgradeId.Bubble => Resources.Load<GameObject>("VFX/Bubble/VFX_2D_Projectile_Burst_Impact_01_Color_Static"),
                _ => _config.impactVfxPrefab,
            };
        }

        public void DealDirectWeaponDamage(EnemyController enemy, float damage, WeaponUpgradeId weaponId)
        {
            if (enemy == null)
            {
                return;
            }

            var appliedDamage = ApplyContextualDamageModifiers(damage, enemy);
            enemy.ReceiveWeaponDamage(appliedDamage, weaponId);
            TryApplyDirectHitLifesteal(appliedDamage, enemy);
        }

        #region System: Targeting & Utilities

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

        public void RequestWeaponSound(WeaponUpgradeId weaponId, WeaponSoundKind kind, Vector3 worldPosition)
        {
            WeaponSoundRequested?.Invoke(new WeaponSoundRequest(weaponId, kind, worldPosition));
        }

        public Vector3 GetOwnerSoundPosition()
        {
            return _owner != null ? _owner.position : Vector3.zero;
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

        public EnemyController FindNearestUsableFrom(Vector2 origin, float maxDistance)
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

        public bool IsEnemyUsable(EnemyController enemy)
        {
            return enemy != null && IsInsideAimBounds(enemy.transform.position);
        }

        public void PruneEnemyCooldownMap(Dictionary<EnemyController, float> cooldownMap)
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

        public static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            var x = (direction.x * cosine) - (direction.y * sine);
            var y = (direction.x * sine) + (direction.y * cosine);
            var rotated = new Vector2(x, y);
            return rotated.sqrMagnitude > 0.000001f ? rotated.normalized : Vector2.right;
        }

        #endregion

        #region System: Stats & Balancing

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

        public float GetLifetimeCappedByRange(float effectiveRange, float projectileSpeed, float requestedLifetime, float rangePaddingMultiplier = 1f)
        {
            var clampedSpeed = Mathf.Max(0.1f, projectileSpeed);
            var clampedRequestedLifetime = Mathf.Max(0.05f, requestedLifetime);
            var clampedRange = Mathf.Max(0.1f, effectiveRange) * Mathf.Max(0.1f, rangePaddingMultiplier);
            var travelDistance = clampedRange * Mathf.Max(0.5f, projectileTravelRangeFactor);
            var lifetimeByRange = travelDistance / clampedSpeed;
            return Mathf.Max(0.05f, Mathf.Min(clampedRequestedLifetime, lifetimeByRange));
        }

        public float GetLifetimeCappedByRange(WeaponRuntime weapon, float projectileSpeed, float requestedLifetime, float rangePaddingMultiplier = 1f)
        {
            return GetLifetimeCappedByRange(GetWeaponRange(weapon), projectileSpeed, requestedLifetime, rangePaddingMultiplier);
        }

        public float GetAttackInterval(WeaponRuntime weapon)
        {
            if (weapon == null) return 1f;
            if (weapon.Strategy != null) return weapon.Strategy.GetAttackInterval(weapon, this);
            return 1f;
        }

        public float GetWeaponBaseDamage(WeaponRuntime weapon)
        {
            if (weapon == null) return 0f;
            var baseDamage = weapon.Strategy != null ? weapon.Strategy.GetBaseDamage(weapon, this) : 0f;
            
            // 글로벌 공격력 + 무기 전용 피해량 보너스 적용
            var statDamageMultiplier = _stats != null ? _stats.DamageMultiplier : 1f;
            var weaponDamageMultiplier = 1f + GetWeaponDamageBonusPercent(weapon);
            
            return baseDamage * statDamageMultiplier * weaponDamageMultiplier;
        }

        public float GetWeaponRange(WeaponRuntime weapon)
        {
            if (weapon == null) return 0f;
            var baseRange = weapon.Strategy != null ? weapon.Strategy.GetRange(weapon, this) : 0f;
            
            // 글로벌 범위 스탯 + 무기 전용 범위 보너스 적용
            var statRangeMultiplier = _stats != null ? _stats.AttackRangeMultiplier : 1f;
            var weaponRangeMultiplier = 1f + GetWeaponRangeBonusPercent(weapon);
            
            return baseRange * statRangeMultiplier * weaponRangeMultiplier;
        }

        public float GetCombinedAttackIntervalMultiplier(WeaponRuntime weapon)
        {
            var statAttackSpeedMultiplier = _stats != null ? Mathf.Max(0.2f, _stats.AttackIntervalMultiplier) : 1f;
            var weaponAttackSpeedMultiplier = 1f / (1f + GetWeaponAttackSpeedBonusPercent(weapon));
            return statAttackSpeedMultiplier * weaponAttackSpeedMultiplier;
        }

        public int GetWeaponExtraCount(WeaponRuntime weapon)
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

        public float GetWeaponRangeBonusPercent(WeaponRuntime weapon)
        {
            if (weapon == null || _build == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, _build.GetWeaponRangeBonusPercentTotal(weapon.WeaponId)) / 100f;
        }

        #endregion

        #region Debug: Gizmos
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
            if (weapon?.Strategy == null) return;
            var color = GetWeaponCollisionGizmoColor(weapon.WeaponId);
            weapon.Strategy.OnDrawGizmos(weapon, this, color);
        }

        private Color GetWeaponCollisionGizmoColor(WeaponUpgradeId weaponId)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Fireball => new Color(1f, 0.45f, 0.15f, 0.45f),
                WeaponUpgradeId.Slash => new Color(1f, 0.15f, 0.45f, 0.45f),
                _ => new Color(1f, 1f, 1f, 0.45f),
            };
        }
        #endregion
    }
}
