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

        private sealed class WeaponRuntime
        {
            public WeaponRuntime(WeaponUpgradeId id, int level)
            {
                WeaponId = id;
                Level = Mathf.Max(1, level);
                Cooldown = 0f;
                BurstShotsRemaining = 0;
                BurstShotCooldown = 0f;
            }

            public WeaponUpgradeId WeaponId { get; }
            public int Level { get; set; }
            public float Cooldown { get; set; }
            public int BurstShotsRemaining { get; set; }
            public float BurstShotCooldown { get; set; }
        }

        private WeaponConfig _config;
        private Transform _owner;
        private EnemyRegistry _registry;
        private PlayerStatsRuntime _stats;
        private Func<Vector2, Vector3> _projectileSpawnResolver;
        private Func<Vector2?> _aimDirectionOverrideProvider;
        private bool _useProjectileBoundsCulling;
        private Rect _projectileCullBounds;

        private EnemyController _currentTarget;
        private Vector2 _lastAimDirection = Vector2.right;
        private float _targetScanCooldown;

        private readonly List<WeaponRuntime> _loadout = new(4);
        private readonly List<EnemyController> _nearbyEnemies = new(32);
        private readonly Queue<Projectile> _projectilePool = new();
        private readonly Dictionary<WeaponUpgradeId, WeaponCoreElement> _coreElementByWeapon = new();
        private readonly Dictionary<WeaponUpgradeId, int> _coreLevelByWeapon = new();
        private Transform _projectilePoolRoot;
        private static Material _katanaRangeEffectMaterial;

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

        public void SetAimDirectionOverrideProvider(Func<Vector2?> aimDirectionOverrideProvider)
        {
            _aimDirectionOverrideProvider = aimDirectionOverrideProvider;
        }

        public void ConfigureLoadout(PlayerBuildRuntime build, PlayerStatsRuntime stats)
        {
            _stats = stats ?? _stats;
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
                _currentTarget = _registry.FindNearest(_owner.position, maxRange);
            }

            if (TryGetAimOverride(out var overrideDirection))
            {
                SetAimDirection(overrideDirection);
                return;
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
                    weapon.BurstShotCooldown = Mathf.Max(0.01f, _config.smgBurstShotInterval);
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
                default:
                    FireRifle(weapon, fireDirection);
                    weapon.Cooldown = GetAttackInterval(weapon);
                    break;
            }
        }

        private bool TryResolveFireDirection(WeaponRuntime weapon, out Vector2 direction)
        {
            direction = _lastAimDirection;

            if (TryGetAimOverride(out var overrideDirection))
            {
                direction = overrideDirection;
                SetAimDirection(direction);
                return true;
            }

            var range = GetWeaponRange(weapon);
            if (!IsTargetUsable(_currentTarget, range))
            {
                _currentTarget = _registry.FindNearest(_owner.position, range);
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

        private bool TryGetAimOverride(out Vector2 direction)
        {
            direction = Vector2.zero;
            if (_aimDirectionOverrideProvider == null)
            {
                return false;
            }

            var maybe = _aimDirectionOverrideProvider.Invoke();
            if (!maybe.HasValue)
            {
                return false;
            }

            var value = maybe.Value;
            if (value.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            direction = value.normalized;
            return true;
        }

        private void StartSmgBurst(WeaponRuntime weapon, Vector2 direction)
        {
            var count = Mathf.Max(1, _config.smgBurstCount);
            weapon.BurstShotsRemaining = count;
            weapon.BurstShotCooldown = 0f;
            FireSmgBullet(weapon, direction);
            weapon.BurstShotsRemaining--;
            weapon.BurstShotCooldown = Mathf.Max(0.01f, _config.smgBurstShotInterval);
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
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = false;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.startWidth = katanaRangeEffectWidth;
            lineRenderer.endWidth = katanaRangeEffectWidth;
            lineRenderer.startColor = katanaRangeEffectColor;
            lineRenderer.endColor = katanaRangeEffectColor;
            lineRenderer.sortingOrder = 500;
            lineRenderer.sharedMaterial = GetOrCreateKatanaRangeEffectMaterial();

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

        private static Material GetOrCreateKatanaRangeEffectMaterial()
        {
            if (_katanaRangeEffectMaterial != null)
            {
                return _katanaRangeEffectMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _katanaRangeEffectMaterial = new Material(shader)
            {
                name = "KatanaRangeFxMat",
                hideFlags = HideFlags.HideAndDontSave,
            };

            return _katanaRangeEffectMaterial;
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
            Color color)
        {
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : _lastAimDirection;
            SetAimDirection(normalizedDirection);

            var spawnPosition = _projectileSpawnResolver != null
                ? _projectileSpawnResolver(normalizedDirection)
                : _owner.position;

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
            var levelMultiplier = Mathf.Max(0.35f, 1f - (0.03f * tier));
            var attackSpeed = _stats != null ? Mathf.Max(0.2f, _stats.AttackIntervalMultiplier) : 1f;

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

            return Mathf.Max(0.05f, baseInterval * attackSpeed * levelMultiplier);
        }

        private float GetWeaponBaseDamage(WeaponRuntime weapon)
        {
            var tier = Mathf.Max(0, weapon.Level - 1);
            var levelMultiplier = 1f + (0.18f * tier);
            var damageMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.DamageMultiplier) : 1f;
            return Mathf.Max(0.1f, _config.projectileDamage * damageMultiplier * levelMultiplier);
        }

        private float GetWeaponRange(WeaponRuntime weapon)
        {
            var tier = Mathf.Max(0, weapon.Level - 1);
            var levelMultiplier = 1f + (0.04f * tier);
            var attackRangeMultiplier = _stats != null ? Mathf.Max(0.1f, _stats.AttackRangeMultiplier) : 1f;

            var baseRange = weapon.WeaponId == WeaponUpgradeId.Katana
                ? Mathf.Max(0.25f, _config.katanaRange)
                : Mathf.Max(0.5f, _config.attackRange);

            return Mathf.Max(0.25f, baseRange * attackRangeMultiplier * levelMultiplier);
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

            var limit = Mathf.Max(0.01f, maxDistance);
            return ((Vector2)(target.transform.position - _owner.position)).sqrMagnitude <= limit * limit;
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
    }
}
