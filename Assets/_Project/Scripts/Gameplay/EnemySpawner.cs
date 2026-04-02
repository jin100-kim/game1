using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EJR.Game.Gameplay
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        private const int BossProjectileVolleySkeletonCount = 5;
        private const float WaveRewardPickupRadius = 0.8f;

        private EnemyConfig _config;
        private Transform _target;
        private PlayerHealth _playerHealth;
        private EnemyRegistry _registry;
        private ExperienceSystem _experienceSystem;
        private float _playerCollisionRadius;
        private Rect _arenaBounds;
        private bool _hasArenaBounds;

        private float _elapsedSeconds;
        private float _spawnTimer;
        private bool _bossWaveTriggered;
        private bool _wave1Triggered;
        private bool _wave2Triggered;
        private bool _pendingWave2;
        private bool _pendingBoss;
        private int _activeWaveIndex;
        private int _activeWaveRemainingCount;
        private EnemyController _bossEnemy;
        private EnemyController _activeWaveTargetEnemy;
        private string _activeWaveTargetLabel = string.Empty;
        private Camera _spawnReferenceCamera;
        private RuntimeSpriteFactory.EnemyVisualKind _bossVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Boss;
        private readonly HashSet<EnemyController> _activeWaveEnemies = new();
        private readonly List<WaveRewardChest> _activeRewardChests = new();
        private int _spawnSequenceCounter;
        private int _bossSpawnSequence;
        private int _waveTargetSpawnSequence;

        public float ElapsedSeconds => _elapsedSeconds;
        public bool IsBossWaveTriggered => _bossWaveTriggered;
        public bool IsBossWaveCleared => _bossWaveTriggered && _bossEnemy == null;
        public float BossWaveStartSeconds => GetBossWaveStartSeconds();
        public EnemyController CurrentBoss => _bossEnemy;
        public EnemyController CurrentWaveTarget => _activeWaveTargetEnemy;
        public bool HasActiveWave => _activeWaveIndex > 0 && _activeWaveRemainingCount > 0;
        public int ActiveWaveIndex => _activeWaveIndex;
        public int ActiveWaveRemainingCount => _activeWaveRemainingCount;
        public Transform CurrentRewardChestTransform => GetLatestRewardChest()?.transform;

        public event Action<int> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action<int> WaveRewardChestCollected;
        public event Action<int, int> WaveStateChanged;

        public void GetRewardChestWorldPositions(List<Vector3> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (var i = _activeRewardChests.Count - 1; i >= 0; i--)
            {
                var chest = _activeRewardChests[i];
                if (chest == null)
                {
                    _activeRewardChests.RemoveAt(i);
                    continue;
                }

                results.Add(chest.transform.position);
            }
        }

        public void Initialize(
            EnemyConfig config,
            Transform target,
            PlayerHealth playerHealth,
            EnemyRegistry registry,
            ExperienceSystem experienceSystem,
            float playerCollisionRadius,
            Rect arenaBounds,
            RuntimeSpriteFactory.EnemyVisualKind bossVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Boss)
        {
            _config = config;
            _target = target;
            _playerHealth = playerHealth;
            _registry = registry;
            _experienceSystem = experienceSystem;
            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
            _arenaBounds = arenaBounds;
            _hasArenaBounds = arenaBounds.width > 0f && arenaBounds.height > 0f;
            _spawnTimer = 0f;
            _elapsedSeconds = 0f;
            _bossWaveTriggered = false;
            _wave1Triggered = false;
            _wave2Triggered = false;
            _pendingWave2 = false;
            _pendingBoss = false;
            _activeWaveIndex = 0;
            _activeWaveRemainingCount = 0;
            _bossEnemy = null;
            _activeWaveTargetEnemy = null;
            _activeWaveTargetLabel = string.Empty;
            _spawnReferenceCamera = Camera.main;
            _bossVisualKind = bossVisualKind;
            _activeWaveEnemies.Clear();
            _activeRewardChests.Clear();
            _spawnSequenceCounter = 0;
            _bossSpawnSequence = 0;
            _waveTargetSpawnSequence = 0;
        }

        private void Update()
        {
            if (_config == null || _target == null || _playerHealth == null || _registry == null)
            {
                return;
            }

            _elapsedSeconds += Time.deltaTime;
            TryTriggerTimedWaves();

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
            if (_bossWaveTriggered || _config == null || _target == null || HasActiveWave)
            {
                if (!_bossWaveTriggered && HasActiveWave)
                {
                    _pendingBoss = true;
                }

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
            _bossEnemy = SpawnEnemy(_bossVisualKind, bossPosition, bossProfile, isBoss: true);
            if (_bossEnemy != null)
            {
                _bossSpawnSequence = ++_spawnSequenceCounter;
                _bossEnemy.BossProjectileVolleyStarted += HandleBossProjectileVolleyStarted;
            }

            var skeletonCount = Mathf.Max(1, _config.bossWaveSkeletonCount);
            var skeletonRadius = CalculateCollisionRadius(_config.GetStatProfile(RuntimeSpriteFactory.EnemyVisualKind.Skeleton));
            var minRadius = Mathf.Max(0.1f, _config.skeletonWaveMinRadius);
            var maxRadius = Mathf.Max(minRadius + 0.1f, _config.skeletonWaveMaxRadius);
            ApplyOffscreenRadiusFloor(skeletonRadius, ref minRadius, ref maxRadius);
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

        private void HandleBossProjectileVolleyStarted()
        {
            if (!_bossWaveTriggered || _config == null || !_config.spawnSkeleton)
            {
                return;
            }

            var spawnCenter = _bossEnemy != null ? _bossEnemy.transform.position : _target.position;
            var bossRadius = _bossEnemy != null ? Mathf.Max(0.1f, _bossEnemy.CollisionRadius) : 0.9f;
            var skeletonRadius = CalculateCollisionRadius(_config.GetStatProfile(RuntimeSpriteFactory.EnemyVisualKind.Skeleton));
            // Boss projectile pattern summons should appear near the boss, not off-screen.
            var minRadius = Mathf.Max(0.8f, bossRadius + skeletonRadius + 0.15f);
            var maxRadius = Mathf.Max(minRadius + 0.45f, minRadius + (bossRadius * 0.75f));
            var angleOffset = Random.value * Mathf.PI * 2f;

            for (var i = 0; i < BossProjectileVolleySkeletonCount; i++)
            {
                var t = i / (float)BossProjectileVolleySkeletonCount;
                var angle = angleOffset + (Mathf.PI * 2f * t) + Random.Range(-0.14f, 0.14f);
                var radius = Random.Range(minRadius, maxRadius);
                var position = spawnCenter + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                SpawnEnemy(RuntimeSpriteFactory.EnemyVisualKind.Skeleton, position);
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
        }

        public void DebugSkipToBossWave()
        {
            DebugSetElapsedSeconds(GetBossWaveStartSeconds());
            if (!_bossWaveTriggered)
            {
                TriggerBossWave();
            }
        }

        private EnemyController SpawnEnemy(
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            Vector3? requestedPosition = null,
            EnemyStatProfile statProfileOverride = null,
            bool isBoss = false,
            bool trackWaveTarget = false)
        {
            var visualStatProfile = _config.GetStatProfile(visualKind);
            var statProfile = statProfileOverride ?? visualStatProfile;
            var collisionRadius = CalculateCollisionRadius(statProfile);
            var runtimeMinuteTier = Mathf.Max(0, Mathf.FloorToInt(_elapsedSeconds / 60f));
            var runtimeMoveSpeedMultiplier = 1f + (runtimeMinuteTier * 0.05f);
            var runtimeHealthMultiplier = 1f + (runtimeMinuteTier * 0.10f);
            var runtimeContactDamageMultiplier = 1f + (Mathf.Min(runtimeMinuteTier, 10) * 0.10f);

            var spawnPosition = requestedPosition.HasValue
                ? requestedPosition.Value
                : FindSpawnPosition(collisionRadius, _config.minSpawnRadius, _config.maxSpawnRadius);

            if (!IsSpawnClear(spawnPosition, collisionRadius))
            {
                spawnPosition = FindSpawnPosition(collisionRadius, _config.minSpawnRadius, _config.maxSpawnRadius);
            }

            var enemyObject = new GameObject(isBoss ? "BossEnemy" : "Enemy");
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
            var scaleMultiplier = visualStatProfile != null ? Mathf.Max(0.1f, visualStatProfile.visualScaleMultiplier) : 1f;
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
                runtimeMoveSpeedMultiplier,
                runtimeContactDamageMultiplier,
                isBoss,
                _hasArenaBounds,
                _arenaBounds);

            if (!isBoss)
            {
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
            }

            if (trackWaveTarget)
            {
                TrackWaveEnemy(enemy);
            }

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
                _wave1Triggered = true;
                StartConfiguredWave(1, _config.wave1SlimeCount, _config.wave1MushroomCount, _config.wave1SkeletonCount);
            }

            if (!_wave2Triggered &&
                _elapsedSeconds >= Mathf.Max(1f, _config.wave2TimeSeconds) &&
                _config.wave2TimeSeconds < bossStart)
            {
                _wave2Triggered = true;
                if (HasActiveWave)
                {
                    _pendingWave2 = true;
                }
                else
                {
                    StartConfiguredWave(2, _config.wave2SlimeCount, _config.wave2MushroomCount, _config.wave2SkeletonCount);
                }
            }

            if (!_bossWaveTriggered && _elapsedSeconds >= bossStart)
            {
                if (HasActiveWave)
                {
                    _pendingBoss = true;
                }
                else
                {
                    TriggerBossWave();
                }
            }
        }

        private void StartConfiguredWave(int waveIndex, int slimeCount, int mushroomCount, int skeletonCount)
        {
            var targetVisualKind = GetWaveTargetVisualKind(waveIndex);
            var validSlimeCount = _config.spawnSlime ? Mathf.Max(0, slimeCount) : 0;
            var validMushroomCount = _config.spawnMushroom ? Mathf.Max(0, mushroomCount) : 0;
            var validSkeletonCount = _config.spawnSkeleton ? Mathf.Max(0, skeletonCount) : 0;

            if (targetVisualKind == RuntimeSpriteFactory.EnemyVisualKind.Slime && validSlimeCount > 0)
            {
                validSlimeCount--;
            }
            else if (targetVisualKind == RuntimeSpriteFactory.EnemyVisualKind.Mushroom && validMushroomCount > 0)
            {
                validMushroomCount--;
            }

            var total = validSlimeCount + validMushroomCount + validSkeletonCount + 1;
            if (total <= 0)
            {
                TryProcessPendingWaveSchedule();
                return;
            }

            _activeWaveIndex = Mathf.Max(0, waveIndex);
            _activeWaveRemainingCount = 0;
            _activeWaveEnemies.Clear();
            _activeWaveTargetEnemy = null;
            _activeWaveTargetLabel = GetWaveTargetLabel(_activeWaveIndex);
            _waveTargetSpawnSequence = 0;

            var minRadius = Mathf.Max(0.1f, _config.timedWaveMinRadius);
            var maxRadius = Mathf.Max(minRadius + 0.1f, _config.timedWaveMaxRadius);
            var angleOffset = Random.value * Mathf.PI * 2f;
            var spawnIndex = 0;

            SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind.Slime, validSlimeCount, total, ref spawnIndex, angleOffset, minRadius, maxRadius);
            SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind.Mushroom, validMushroomCount, total, ref spawnIndex, angleOffset, minRadius, maxRadius);
            SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind.Skeleton, validSkeletonCount, total, ref spawnIndex, angleOffset, minRadius, maxRadius);
            SpawnWaveTargetEnemy(targetVisualKind, total, ref spawnIndex, angleOffset, minRadius, maxRadius);
            WaveStarted?.Invoke(_activeWaveIndex);
            RaiseWaveStateChanged();
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
            var candidateRadius = CalculateCollisionRadius(_config.GetStatProfile(visualKind));
            var adjustedMinRadius = Mathf.Max(0.1f, minRadius);
            var adjustedMaxRadius = Mathf.Max(adjustedMinRadius + 0.1f, maxRadius);
            ApplyOffscreenRadiusFloor(candidateRadius, ref adjustedMinRadius, ref adjustedMaxRadius);

            for (var i = 0; i < count; i++)
            {
                var t = total > 0 ? spawnIndex / (float)total : 0f;
                var angle = angleOffset + (Mathf.PI * 2f * t) + Random.Range(-0.15f, 0.15f);
                var radius = Random.Range(adjustedMinRadius, adjustedMaxRadius);
                var position = _target.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                SpawnEnemy(visualKind, position);
                spawnIndex++;
            }
        }

        private void SpawnWaveTargetEnemy(
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            int total,
            ref int spawnIndex,
            float angleOffset,
            float minRadius,
            float maxRadius)
        {
            var baseProfile = _config.GetStatProfile(visualKind);
            var targetProfile = new EnemyStatProfile
            {
                healthMultiplier = Mathf.Max(1f, baseProfile != null ? baseProfile.healthMultiplier : 1f) * 5.5f,
                moveSpeedMultiplier = Mathf.Max(0.1f, baseProfile != null ? baseProfile.moveSpeedMultiplier : 1f) * 0.92f,
                contactDamageMultiplier = Mathf.Max(0.1f, baseProfile != null ? baseProfile.contactDamageMultiplier : 1f) * 1.25f,
                experienceMultiplier = Mathf.Max(0.1f, baseProfile != null ? baseProfile.experienceMultiplier : 1f) * 3f,
                visualScaleMultiplier = Mathf.Max(0.1f, baseProfile != null ? baseProfile.visualScaleMultiplier : 1f) * 1.55f,
                collisionRadiusMultiplier = Mathf.Max(0.1f, baseProfile != null ? baseProfile.collisionRadiusMultiplier : 1f) * 1.35f,
            };

            var candidateRadius = CalculateCollisionRadius(targetProfile);
            var adjustedMinRadius = Mathf.Max(0.1f, minRadius);
            var adjustedMaxRadius = Mathf.Max(adjustedMinRadius + 0.1f, maxRadius);
            ApplyOffscreenRadiusFloor(candidateRadius, ref adjustedMinRadius, ref adjustedMaxRadius);

            var t = total > 0 ? spawnIndex / (float)total : 0f;
            var angle = angleOffset + (Mathf.PI * 2f * t) + Random.Range(-0.08f, 0.08f);
            var radius = Random.Range(adjustedMinRadius, adjustedMaxRadius);
            var position = _target.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            var enemy = SpawnEnemy(visualKind, position, targetProfile, trackWaveTarget: true);
            spawnIndex++;
            if (enemy == null)
            {
                return;
            }

            _activeWaveTargetEnemy = enemy;
            _waveTargetSpawnSequence = ++_spawnSequenceCounter;
            ApplyWaveTargetPresentation(enemy);
        }

        public bool TryGetPriorityBossBarTarget(out EnemyController enemy, out string label)
        {
            enemy = null;
            label = string.Empty;

            var boss = _bossEnemy;
            if (boss == null || boss.IsDead)
            {
                boss = null;
            }

            var waveTarget = _activeWaveTargetEnemy;
            if (waveTarget == null || waveTarget.IsDead)
            {
                waveTarget = null;
            }

            if (boss == null && waveTarget == null)
            {
                return false;
            }

            if (waveTarget != null && (boss == null || _waveTargetSpawnSequence >= _bossSpawnSequence))
            {
                enemy = waveTarget;
                label = string.IsNullOrWhiteSpace(_activeWaveTargetLabel) ? "웨이브 목표" : _activeWaveTargetLabel;
                return true;
            }

            enemy = boss;
            label = "보스";
            return true;
        }

        private RuntimeSpriteFactory.EnemyVisualKind GetWaveTargetVisualKind(int waveIndex)
        {
            return waveIndex <= 1
                ? RuntimeSpriteFactory.EnemyVisualKind.Slime
                : RuntimeSpriteFactory.EnemyVisualKind.Mushroom;
        }

        private string GetWaveTargetLabel(int waveIndex)
        {
            return waveIndex <= 1 ? "거대 슬라임" : "거대 버섯";
        }

        private void ApplyWaveTargetPresentation(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.gameObject.name = $"{GetWaveTargetLabel(_activeWaveIndex)}Enemy";
            var visualRoot = enemy.transform.Find("Visual");
            if (visualRoot == null)
            {
                return;
            }

            var glowObject = new GameObject("WaveTargetGlow");
            glowObject.transform.SetParent(visualRoot, false);
            glowObject.transform.localPosition = new Vector3(0f, -0.02f, 0.02f);
            var glowRenderer = glowObject.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            glowRenderer.color = new Color(1f, 1f, 1f, 0.18f);
            glowRenderer.sortingOrder = 13;
            var glowScale = Mathf.Max(0.6f, enemy.CollisionRadius * 3f);
            glowObject.transform.localScale = new Vector3(glowScale, glowScale, 1f);
        }

        private WaveRewardChest GetLatestRewardChest()
        {
            WaveRewardChest latestChest = null;
            var latestSequence = int.MinValue;
            for (var i = _activeRewardChests.Count - 1; i >= 0; i--)
            {
                var chest = _activeRewardChests[i];
                if (chest == null)
                {
                    _activeRewardChests.RemoveAt(i);
                    continue;
                }

                if (chest.SpawnSequence <= latestSequence)
                {
                    continue;
                }

                latestSequence = chest.SpawnSequence;
                latestChest = chest;
            }

            return latestChest;
        }

        private void SpawnWaveRewardChest(int waveIndex, Vector3 position)
        {
            var chestObject = new GameObject($"WaveRewardChest_{Mathf.Max(1, waveIndex)}");
            chestObject.transform.position = position;
            var chest = chestObject.AddComponent<WaveRewardChest>();
            _activeRewardChests.Add(chest);
            chest.Initialize(
                _target,
                waveIndex,
                ++_spawnSequenceCounter,
                WaveRewardPickupRadius,
                HandleWaveRewardChestCollected,
                HandleWaveRewardChestReleased);
        }

        private void HandleWaveRewardChestCollected(WaveRewardChest chest)
        {
            if (chest == null)
            {
                return;
            }

            _activeRewardChests.Remove(chest);
            WaveRewardChestCollected?.Invoke(chest.WaveIndex);
        }

        private void HandleWaveRewardChestReleased(WaveRewardChest chest)
        {
            if (chest == null)
            {
                return;
            }

            _activeRewardChests.Remove(chest);
        }

        private void OnEnable()
        {
            EnemyController.Defeated += HandleTrackedEnemyDefeated;
        }

        private void OnDisable()
        {
            EnemyController.Defeated -= HandleTrackedEnemyDefeated;
        }

        private void HandleTrackedEnemyDefeated(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (_bossEnemy == enemy)
            {
                _bossEnemy = null;
                _bossSpawnSequence = 0;
            }

            if (_activeWaveEnemies.Count <= 0 || !_activeWaveEnemies.Remove(enemy))
            {
                return;
            }

            if (_activeWaveTargetEnemy == enemy)
            {
                SpawnWaveRewardChest(_activeWaveIndex, enemy.transform.position);
                _activeWaveTargetEnemy = null;
                _activeWaveTargetLabel = string.Empty;
                _waveTargetSpawnSequence = 0;
            }

            _activeWaveRemainingCount = Mathf.Max(0, _activeWaveEnemies.Count);
            RaiseWaveStateChanged();
            if (_activeWaveRemainingCount > 0)
            {
                return;
            }

            var clearedWaveIndex = _activeWaveIndex;
            _activeWaveIndex = 0;
            _activeWaveRemainingCount = 0;
            _activeWaveEnemies.Clear();
            WaveCleared?.Invoke(clearedWaveIndex);
            RaiseWaveStateChanged();
            TryProcessPendingWaveSchedule();
        }

        private void TrackWaveEnemy(EnemyController enemy)
        {
            if (enemy == null)
            {
                return;
            }

            _activeWaveEnemies.Add(enemy);
            _activeWaveRemainingCount = _activeWaveEnemies.Count;
        }

        private void RaiseWaveStateChanged()
        {
            WaveStateChanged?.Invoke(_activeWaveIndex, _activeWaveRemainingCount);
        }

        private void TryProcessPendingWaveSchedule()
        {
            if (HasActiveWave || _bossWaveTriggered || _config == null)
            {
                return;
            }

            if (_pendingWave2)
            {
                _pendingWave2 = false;
                StartConfiguredWave(2, _config.wave2SlimeCount, _config.wave2MushroomCount, _config.wave2SkeletonCount);
                return;
            }

            if (_pendingBoss)
            {
                _pendingBoss = false;
                TriggerBossWave();
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
            ApplyOffscreenRadiusFloor(candidateRadius, ref minRadius, ref maxRadius);

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

        private void ApplyOffscreenRadiusFloor(float candidateRadius, ref float minRadius, ref float maxRadius)
        {
            var offscreenMinRadius = GetMinimumOffscreenRadius(candidateRadius);
            if (offscreenMinRadius <= 0f)
            {
                return;
            }

            minRadius = Mathf.Max(minRadius, offscreenMinRadius);
            maxRadius = Mathf.Max(maxRadius, minRadius + 0.1f);
        }

        private float GetMinimumOffscreenRadius(float candidateRadius)
        {
            if (_spawnReferenceCamera == null)
            {
                _spawnReferenceCamera = Camera.main;
            }

            if (_spawnReferenceCamera == null)
            {
                return 0f;
            }

            var camera = _spawnReferenceCamera;
            if (camera.orthographic)
            {
                var halfHeight = camera.orthographicSize;
                var halfWidth = halfHeight * Mathf.Max(0.1f, camera.aspect);
                var halfDiagonal = Mathf.Sqrt((halfWidth * halfWidth) + (halfHeight * halfHeight));
                var padding = Mathf.Max(0f, _config != null ? _config.offscreenSpawnPadding : 0f);
                return halfDiagonal + padding + _playerCollisionRadius + Mathf.Max(0.05f, candidateRadius);
            }

            // If perspective is used unexpectedly, skip the offscreen clamp rather than guessing wrong.
            return 0f;
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
