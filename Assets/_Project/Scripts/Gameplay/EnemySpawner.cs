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
        private EnemyController _bossEnemy;

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
            _bossEnemy = null;
        }

        private void Update()
        {
            if (_config == null || _target == null || _playerHealth == null || _registry == null)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;
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

            SpawnEnemy(PickEnemyVisualKind());
            _spawnTimer = SpawnMath.CalculateSpawnInterval(
                _elapsedSeconds,
                _config.initialSpawnInterval,
                _config.minimumSpawnInterval,
                _config.spawnRampSeconds);
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

        private EnemyController SpawnEnemy(RuntimeSpriteFactory.EnemyVisualKind visualKind, Vector3? requestedPosition = null)
        {
            var statProfile = _config.GetStatProfile(visualKind);
            var collisionRadius = CalculateCollisionRadius(statProfile);

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
                collisionRadius);

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

            var t = Mathf.InverseLerp(
                Mathf.Max(0f, _config.mushroomPhaseStartSeconds),
                GetBossWaveStartSeconds(),
                _elapsedSeconds);
            var mushroomChance = Mathf.Lerp(
                Mathf.Clamp01(_config.mushroomRatioAtPhaseStart),
                Mathf.Clamp01(_config.mushroomRatioBeforeBoss),
                t);

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
    }
}
