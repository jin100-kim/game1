using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        private EnemyConfig _config;
        private Transform _target;
        private PlayerHealth _playerHealth;
        private EnemyRegistry _registry;
        private ExperienceSystem _experienceSystem;
        private float _playerCollisionRadius;

        private float _elapsedSeconds;
        private float _spawnTimer;
        private bool _bossWaveTriggered;
        private bool _wave1Triggered;
        private bool _wave2Triggered;
        private EnemyController _bossEnemy;

        public float ElapsedSeconds => _elapsedSeconds;
        public bool IsBossWaveTriggered => _bossWaveTriggered;
        public bool IsBossWaveCleared => _bossWaveTriggered && _bossEnemy == null;
        public float BossWaveStartSeconds => GetBossWaveStartSeconds();

        public void Initialize(
            EnemyConfig config,
            Transform target,
            PlayerHealth playerHealth,
            EnemyRegistry registry,
            ExperienceSystem experienceSystem,
            float playerCollisionRadius)
        {
            _config = config;
            _target = target;
            _playerHealth = playerHealth;
            _registry = registry;
            _experienceSystem = experienceSystem;
            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
            _spawnTimer = 0f;
            _elapsedSeconds = 0f;
            _bossWaveTriggered = false;
            _wave1Triggered = false;
            _wave2Triggered = false;
            _bossEnemy = null;
        }

        private void Update()
        {
            if (_config == null || _target == null || _playerHealth == null || _registry == null)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;
            TryTriggerTimedWaves();
            if (!_bossWaveTriggered && _elapsedSeconds >= GetBossWaveStartSeconds())
            {
                TriggerBossWave();
            }

            if (_bossWaveTriggered)
            {
                return;
            }

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f)
            {
                return;
            }

            SpawnDynamicTickEnemies();
            _spawnTimer = CalculateNextSpawnInterval();
        }

        public void TriggerBossWave()
        {
            if (_bossWaveTriggered || _config == null || _target == null)
            {
                return;
            }

            _bossWaveTriggered = true;
            _spawnTimer = float.MaxValue;

            var bossProfile = _config.GetStatProfile(RuntimeSpriteFactory.EnemyVisualKind.Boss);
            var bossRadius = CalculateCollisionRadius(bossProfile);
            var bossSpawnRadius = Mathf.Max(0.1f, _config.bossSpawnRadius);
            var bossPosition = FindSpawnPosition(
                bossRadius,
                bossSpawnRadius * 0.9f,
                bossSpawnRadius * 1.15f);
            _bossEnemy = SpawnEnemy(RuntimeSpriteFactory.EnemyVisualKind.Boss, bossPosition);

            var skeletonCount = Mathf.Max(1, _config.bossWaveSkeletonCount);
            var minRadius = Mathf.Max(0.1f, _config.skeletonWaveMinRadius);
            var maxRadius = Mathf.Max(minRadius + 0.1f, _config.skeletonWaveMaxRadius);
            var angleOffset = Random.value * Mathf.PI * 2f;

            for (var i = 0; i < skeletonCount; i++)
            {
                var t = i / (float)skeletonCount;
                var angle = angleOffset + (Mathf.PI * 2f * t) + Random.Range(-0.12f, 0.12f);
                var radius = Random.Range(minRadius, maxRadius);
                var ringPosition = _target.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                SpawnEnemy(RuntimeSpriteFactory.EnemyVisualKind.Skeleton, ringPosition);
            }
        }

        public void DebugAdvanceSeconds(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            DebugSetElapsedSeconds(_elapsedSeconds + seconds);
        }

        public void DebugSetElapsedSeconds(float seconds)
        {
            _elapsedSeconds = Mathf.Max(0f, seconds);
            TryTriggerTimedWaves();
            if (!_bossWaveTriggered && _elapsedSeconds >= GetBossWaveStartSeconds())
            {
                TriggerBossWave();
            }
        }

        public void DebugSkipToBossWave()
        {
            DebugSetElapsedSeconds(GetBossWaveStartSeconds());
            if (!_bossWaveTriggered)
            {
                TriggerBossWave();
            }
        }

        private EnemyController SpawnEnemy(RuntimeSpriteFactory.EnemyVisualKind visualKind, Vector3? requestedPosition = null)
        {
            var statProfile = _config.GetStatProfile(visualKind);
            var collisionRadius = CalculateCollisionRadius(statProfile);
            var runtimeMinuteTier = Mathf.Max(0, Mathf.FloorToInt(_elapsedSeconds / 60f));
            var runtimeMoveSpeedMultiplier = 1f + (runtimeMinuteTier * 0.05f);
            var runtimeHealthMultiplier = 1f + (runtimeMinuteTier * 0.10f);

            var spawnPosition = requestedPosition.HasValue
                ? requestedPosition.Value
                : FindSpawnPosition(collisionRadius, _config.minSpawnRadius, _config.maxSpawnRadius);

            if (!IsSpawnClear(spawnPosition, collisionRadius))
            {
                spawnPosition = FindSpawnPosition(collisionRadius, _config.minSpawnRadius, _config.maxSpawnRadius);
            }

            var enemyObject = new GameObject(visualKind == RuntimeSpriteFactory.EnemyVisualKind.Boss ? "BossEnemy" : "Enemy");
            enemyObject.transform.position = spawnPosition;

            var animationProfile = _config.GetAnimationProfile(visualKind);
            var enemyFrames = RuntimeSpriteFactory.GetEnemyAnimationFrames(visualKind);
            var baseSprite = enemyFrames.Length > 0 ? enemyFrames[0] : RuntimeSpriteFactory.GetSquareSprite();

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(enemyObject.transform, false);
            visualObject.transform.localPosition = new Vector3(0f, _config.visualYOffset, 0f);

            var renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = baseSprite;
            renderer.color = Color.white;
            var scaleMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.visualScaleMultiplier) : 1f;
            var visualWorldSize = Mathf.Max(0.1f, _config.visualScale * scaleMultiplier);
            ApplyVisualScale(visualObject.transform, renderer.sprite, visualWorldSize);
            if (enemyFrames.Length > 1)
            {
                var spriteAnimator = visualObject.AddComponent<EnemySpriteAnimator>();
                spriteAnimator.Initialize(renderer, enemyFrames, animationProfile);
            }

            var enemy = enemyObject.AddComponent<EnemyController>();
            enemy.Initialize(
                _config,
                visualKind,
                statProfile,
                _target,
                _playerHealth,
                _registry,
                _experienceSystem,
                _playerCollisionRadius,
                collisionRadius,
                runtimeHealthMultiplier,
                runtimeMoveSpeedMultiplier);

            var healthBar = enemyObject.AddComponent<WorldHealthBar>();
            var healthBarYOffset = _config.visualYOffset + Mathf.Max(0.28f, visualWorldSize * 0.36f);
            healthBar.Initialize(
                new Vector3(0f, healthBarYOffset, 0f),
                0.82f,
                0.1f,
                new Color(1f, 0.3f, 0.35f, 0.95f),
                new Color(0f, 0f, 0f, 0.55f),
                24);
            healthBar.SetHealth(enemy.CurrentHealth, enemy.MaxHealth);
            enemy.Changed += healthBar.SetHealth;

            return enemy;
        }

        private void TryTriggerTimedWaves()
        {
            if (_bossWaveTriggered || _config == null || _target == null || !_config.enableTimedWaves)
            {
                return;
            }

            var bossStart = GetBossWaveStartSeconds();

            if (!_wave1Triggered &&
                _elapsedSeconds >= Mathf.Max(1f, _config.wave1TimeSeconds) &&
                _config.wave1TimeSeconds < bossStart)
            {
                TriggerConfiguredWave(_config.wave1SlimeCount, _config.wave1MushroomCount, _config.wave1SkeletonCount);
                _wave1Triggered = true;
            }

            if (!_wave2Triggered &&
                _elapsedSeconds >= Mathf.Max(1f, _config.wave2TimeSeconds) &&
                _config.wave2TimeSeconds < bossStart)
            {
                TriggerConfiguredWave(_config.wave2SlimeCount, _config.wave2MushroomCount, _config.wave2SkeletonCount);
                _wave2Triggered = true;
            }
        }

        private void TriggerConfiguredWave(int slimeCount, int mushroomCount, int skeletonCount)
        {
            var validSlimeCount = _config.spawnSlime ? Mathf.Max(0, slimeCount) : 0;
            var validMushroomCount = _config.spawnMushroom ? Mathf.Max(0, mushroomCount) : 0;
            var validSkeletonCount = _config.spawnSkeleton ? Mathf.Max(0, skeletonCount) : 0;
            var total = validSlimeCount + validMushroomCount + validSkeletonCount;
            if (total <= 0)
            {
                return;
            }

            var minRadius = Mathf.Max(0.1f, _config.timedWaveMinRadius);
            var maxRadius = Mathf.Max(minRadius + 0.1f, _config.timedWaveMaxRadius);
            var angleOffset = Random.value * Mathf.PI * 2f;
            var spawnIndex = 0;

            SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind.Slime, validSlimeCount, total, ref spawnIndex, angleOffset, minRadius, maxRadius);
            SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind.Mushroom, validMushroomCount, total, ref spawnIndex, angleOffset, minRadius, maxRadius);
            SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind.Skeleton, validSkeletonCount, total, ref spawnIndex, angleOffset, minRadius, maxRadius);
        }

        private void SpawnWaveEnemies(
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            int count,
            int total,
            ref int spawnIndex,
            float angleOffset,
            float minRadius,
            float maxRadius)
        {
            for (var i = 0; i < count; i++)
            {
                var t = total > 0 ? spawnIndex / (float)total : 0f;
                var angle = angleOffset + (Mathf.PI * 2f * t) + Random.Range(-0.15f, 0.15f);
                var radius = Random.Range(minRadius, maxRadius);
                var position = _target.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                SpawnEnemy(visualKind, position);
                spawnIndex++;
            }
        }

        private static void ApplyVisualScale(Transform targetTransform, Sprite sprite, float desiredWorldSize)
        {
            var clampedSize = Mathf.Max(0.1f, desiredWorldSize);
            if (sprite == null)
            {
                targetTransform.localScale = Vector3.one * clampedSize;
                return;
            }

            var spriteBounds = sprite.bounds.size;
            var spriteSize = Mathf.Max(spriteBounds.x, spriteBounds.y);
            if (spriteSize <= 0.0001f)
            {
                targetTransform.localScale = Vector3.one * clampedSize;
                return;
            }

            var uniformScale = clampedSize / spriteSize;
            targetTransform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        }

        private Vector3 FindSpawnPosition(float candidateRadius, float minSpawnRadius, float maxSpawnRadius)
        {
            const int maxTries = 12;
            var fallback = _target.position;
            var minRadius = Mathf.Max(0.1f, minSpawnRadius);
            var maxRadius = Mathf.Max(minRadius + 0.1f, maxSpawnRadius);

            for (var attempt = 0; attempt < maxTries; attempt++)
            {
                var angle = Random.value * Mathf.PI * 2f;
                var radius = Random.Range(minRadius, maxRadius);
                var candidate = _target.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

                fallback = candidate;
                if (IsSpawnClear(candidate, candidateRadius))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private bool IsSpawnClear(Vector3 candidate, float candidateRadius)
        {
            var toPlayer = ((Vector2)candidate - (Vector2)_target.position).magnitude;
            var minimumToPlayer = _playerCollisionRadius + candidateRadius + 0.01f;
            if (toPlayer < minimumToPlayer)
            {
                return false;
            }

            if (_registry == null)
            {
                return true;
            }

            var enemies = _registry.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var other = enemies[i];
                if (other == null)
                {
                    continue;
                }

                var minimum = candidateRadius + other.CollisionRadius;
                var distance = ((Vector2)candidate - (Vector2)other.transform.position).magnitude;
                if (distance < minimum)
                {
                    return false;
                }
            }

            return true;
        }

        private float CalculateCollisionRadius(EnemyStatProfile statProfile)
        {
            var multiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.collisionRadiusMultiplier) : 1f;
            return Mathf.Max(0.05f, _config.collisionRadius * multiplier);
        }

        private RuntimeSpriteFactory.EnemyVisualKind PickEnemyVisualKind()
        {
            var canSpawnSlime = _config.spawnSlime;
            var canSpawnMushroom = _config.spawnMushroom;
            if (!canSpawnSlime && !canSpawnMushroom)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Slime;
            }

            if (_elapsedSeconds < Mathf.Max(0f, _config.mushroomPhaseStartSeconds))
            {
                return canSpawnSlime
                    ? RuntimeSpriteFactory.EnemyVisualKind.Slime
                    : RuntimeSpriteFactory.EnemyVisualKind.Mushroom;
            }

            if (!canSpawnSlime)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Mushroom;
            }

            if (!canSpawnMushroom)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Slime;
            }

            var phaseStart = Mathf.Max(0f, _config.mushroomPhaseStartSeconds);
            var phaseEnd = Mathf.Max(phaseStart + 1f, _config.wave2TimeSeconds);
            float mushroomChance;

            if (_elapsedSeconds < phaseStart)
            {
                // Before phase start: slime only.
                mushroomChance = 0f;
            }
            else if (_elapsedSeconds < phaseEnd)
            {
                // Middle phase (e.g. 3~6 min): configured mixed ratio.
                mushroomChance = Mathf.Clamp01(_config.mushroomRatioAtPhaseStart);
            }
            else
            {
                // After middle phase: configured post-phase ratio.
                mushroomChance = Mathf.Clamp01(_config.mushroomRatioBeforeBoss);
            }

            return Random.value < mushroomChance
                ? RuntimeSpriteFactory.EnemyVisualKind.Mushroom
                : RuntimeSpriteFactory.EnemyVisualKind.Slime;
        }

        private float GetBossWaveStartSeconds()
        {
            var phaseStart = _config != null ? Mathf.Max(0f, _config.mushroomPhaseStartSeconds) : 300f;
            var bossStart = _config != null ? Mathf.Max(1f, _config.bossWaveStartSeconds) : 600f;
            return Mathf.Max(phaseStart + 1f, bossStart);
        }

        private void SpawnDynamicTickEnemies()
        {
            var aliveCount = GetAliveEnemyCount();
            var targetAliveCount = GetTargetAliveCount();
            var spawnCount = CalculateSpawnCountForTick(aliveCount, targetAliveCount);

            for (var i = 0; i < spawnCount; i++)
            {
                if (IsAtHardAliveCap())
                {
                    break;
                }

                SpawnEnemy(PickEnemyVisualKind());
            }
        }

        private float CalculateNextSpawnInterval()
        {
            var baseInterval = SpawnMath.CalculateSpawnInterval(
                _elapsedSeconds,
                _config.initialSpawnInterval,
                _config.minimumSpawnInterval,
                _config.spawnRampSeconds);

            if (_config == null || !_config.enableDynamicDensity)
            {
                return baseInterval;
            }

            var targetAliveCount = Mathf.Max(1, GetTargetAliveCount());
            var aliveCount = GetAliveEnemyCount();
            var densityRatio = aliveCount / (float)targetAliveCount;

            float densityScale;
            if (densityRatio < 1f)
            {
                densityScale = Mathf.Lerp(
                    Mathf.Clamp(_config.lowDensityIntervalScaleMin, 0.2f, 1f),
                    1f,
                    densityRatio);
            }
            else
            {
                var t = Mathf.Clamp01((densityRatio - 1f) / 0.6f);
                densityScale = Mathf.Lerp(
                    1f,
                    Mathf.Max(1f, _config.highDensityIntervalScaleMax),
                    t);
            }

            return Mathf.Max(0.03f, baseInterval * densityScale);
        }

        private int CalculateSpawnCountForTick(int aliveCount, int targetAliveCount)
        {
            if (_config == null)
            {
                return 1;
            }

            if (!_config.enableDynamicDensity)
            {
                return IsAtHardAliveCap() ? 0 : 1;
            }

            if (IsAtHardAliveCap())
            {
                return 0;
            }

            var deficit = Mathf.Max(0, targetAliveCount - aliveCount);
            if (deficit <= 0)
            {
                return 1;
            }

            var chunk = Mathf.Max(1, Mathf.RoundToInt(targetAliveCount * 0.25f));
            var extraSpawns = Mathf.Min(
                Mathf.Max(0, _config.lowDensityExtraSpawnMax),
                Mathf.CeilToInt(deficit / (float)chunk));

            return 1 + extraSpawns;
        }

        private int GetAliveEnemyCount()
        {
            return _registry != null && _registry.Enemies != null ? _registry.Enemies.Count : 0;
        }

        private int GetTargetAliveCount()
        {
            if (_config == null)
            {
                return 12;
            }

            var start = Mathf.Max(1, _config.targetAliveStart);
            var end = Mathf.Max(start, _config.targetAliveEnd);
            var rampSeconds = Mathf.Max(1f, _config.targetAliveRampSeconds);
            var t = Mathf.Clamp01(_elapsedSeconds / rampSeconds);
            var exponent = Mathf.Max(0.1f, _config.targetAliveCurveExponent);
            var curvedT = Mathf.Pow(t, exponent);
            return Mathf.RoundToInt(Mathf.Lerp(start, end, curvedT));
        }

        private bool IsAtHardAliveCap()
        {
            if (_config == null)
            {
                return false;
            }

            var hardCap = Mathf.Max(1, _config.hardAliveCap);
            return GetAliveEnemyCount() >= hardCap;
        }
    }
}
