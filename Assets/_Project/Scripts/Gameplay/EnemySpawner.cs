using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EJR.Game.Gameplay
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        private const int BossProjectileVolleySkeletonCount = 2;
        private const float WaveRewardPickupRadius = 0.8f;
        private const float WaveTargetVisualScaleMultiplier = 2f;
        private const float WaveTargetCollisionRadiusMultiplier = 1.7f;
        private const float DebugVariantSpawnRingRadius = 2.4f;

        private EnemyConfig _config;
        private Transform _target;
        private PlayerHealth _playerHealth;
        private EnemyRegistry _registry;
        private ExperienceSystem _experienceSystem;
        private float _playerCollisionRadius;
        private Rect _arenaBounds;
        private bool _hasArenaBounds;
        private UnityEngine.Tilemaps.Tilemap _groundTilemap;
        private UnityEngine.Tilemaps.Tilemap _propsTilemap;

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
        private BossArchetypeId _bossArchetype = BossArchetypeId.Final;
        private RunDifficultyDefinition _bossDifficulty;
        private string _mapId = SharedRunCatalog.DefaultMapId;
        private readonly HashSet<EnemyController> _activeWaveEnemies = new();
        private readonly List<WaveRewardChest> _activeRewardChests = new();
        private readonly HashSet<EnemyController> _debugMonsterLabEnemies = new();
        private int _spawnSequenceCounter;
        private int _bossSpawnSequence;
        private int _waveTargetSpawnSequence;
        private bool _debugMonsterLabEnabled;
        private bool _debugMonsterLabTimePaused;
        private EnemyVariantId _debugSelectedVariantId = EnemyVariantId.SlimeSplit;

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
        public bool DebugMonsterLabEnabled => _debugMonsterLabEnabled;
        public bool DebugMonsterLabTimePaused => _debugMonsterLabTimePaused;
        public EnemyVariantId DebugSelectedVariantId => _debugSelectedVariantId;

        public event Action<int> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action<int> WaveRewardChestCollected;
        public event Action<int, int> WaveStateChanged;

        public void GetRewardChestWorldPositions(List<Vector3> results)
        {
            if (results == null) return;
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
            RuntimeSpriteFactory.EnemyVisualKind bossVisualKind = RuntimeSpriteFactory.EnemyVisualKind.Boss,
            string mapId = SharedRunCatalog.DefaultMapId,
            BossArchetypeId bossArchetype = BossArchetypeId.Final,
            RunDifficultyDefinition bossDifficulty = null,
            UnityEngine.Tilemaps.Tilemap groundTilemap = null,
            UnityEngine.Tilemaps.Tilemap propsTilemap = null)
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
            _mapId = string.IsNullOrWhiteSpace(mapId) ? SharedRunCatalog.DefaultMapId : mapId;
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
            _bossArchetype = bossArchetype;
            _bossDifficulty = bossDifficulty ?? SharedRunCatalog.GetDifficulty(SharedRunCatalog.DefaultDifficultyId);
            _groundTilemap = groundTilemap;
            _propsTilemap = propsTilemap;
            _activeWaveEnemies.Clear();
            _activeRewardChests.Clear();
            _spawnSequenceCounter = 0;
            _bossSpawnSequence = 0;
            _waveTargetSpawnSequence = 0;
            _debugMonsterLabEnabled = false;
            _debugMonsterLabTimePaused = false;
            _debugSelectedVariantId = EnemyVariantId.SlimeSplit;
            _debugMonsterLabEnemies.Clear();
            DebugSessionService.SetMonsterLabTimePaused(false);
        }

        private void Update()
        {
            if (_config == null || _target == null || _playerHealth == null || _registry == null) return;
            if (_debugMonsterLabEnabled) return;

            _elapsedSeconds += Time.deltaTime;
            TryTriggerTimedWaves();

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                SpawnDynamicTickEnemies();
                _spawnTimer = CalculateNextSpawnInterval();
            }
        }

        public void TriggerBossWave()
        {
            if (_bossWaveTriggered || _config == null || _target == null || HasActiveWave)
            {
                if (!_bossWaveTriggered && HasActiveWave) _pendingBoss = true;
                return;
            }

            _bossWaveTriggered = true;
            // No longer pausing spawn timer here.

            var bossProfile = _config.GetStatProfile(RuntimeSpriteFactory.EnemyVisualKind.Boss);
            var bossRadius = CalculateCollisionRadius(bossProfile);
            var bossSpawnRadius = Mathf.Max(0.1f, _config.bossSpawnRadius);
            var bossPosition = FindSpawnPosition(bossRadius, bossSpawnRadius * 0.9f, bossSpawnRadius * 1.15f);
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
            if (!_bossWaveTriggered || _config == null || !_config.spawnSkeleton) return;
            var spawnCenter = _bossEnemy != null ? _bossEnemy.transform.position : _target.position;
            var bossRadius = _bossEnemy != null ? Mathf.Max(0.1f, _bossEnemy.CollisionRadius) : 0.9f;
            var skeletonRadius = CalculateCollisionRadius(_config.GetStatProfile(RuntimeSpriteFactory.EnemyVisualKind.Skeleton));
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
            if (seconds <= 0f) return;
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
            if (!_bossWaveTriggered) TriggerBossWave();
        }

        public void DebugStartWave1() => DebugStartConfiguredWave(1);
        public void DebugStartWave2() => DebugStartConfiguredWave(2);

        public void DebugStartBossWave()
        {
            if (_config == null || _target == null) return;
            if (_debugMonsterLabEnabled) DebugSetMonsterLabEnabled(false);
            DebugClearNonBossEnemies();
            _wave1Triggered = true;
            _wave2Triggered = true;
            _pendingWave2 = false;
            _pendingBoss = false;
            _bossWaveTriggered = false;
            _elapsedSeconds = GetBossWaveStartSeconds();
            _spawnTimer = float.MaxValue;
            TriggerBossWave();
        }

        public void DebugSetMonsterLabEnabled(bool enabled)
        {
            if (_debugMonsterLabEnabled == enabled) return;
            _debugMonsterLabEnabled = enabled;
            _debugMonsterLabTimePaused = false;
            DebugSessionService.SetMonsterLabTimePaused(false);
            DebugClearNonBossEnemies();
            ClearActiveRewardChests();
            ResetWaveTracking();
            _spawnTimer = enabled ? float.MaxValue : 0f;
        }

        public void DebugSetMonsterLabTimePaused(bool paused)
        {
            _debugMonsterLabTimePaused = _debugMonsterLabEnabled && paused;
            DebugSessionService.SetMonsterLabTimePaused(_debugMonsterLabTimePaused);
        }

        public void DebugSetSelectedVariant(EnemyVariantId variantId)
        {
            if (variantId != EnemyVariantId.None) _debugSelectedVariantId = variantId;
        }

        public void DebugSpawnVariant(EnemyVariantId variantId, int count)
        {
            if (!_debugMonsterLabEnabled || _target == null || count <= 0) return;
            var definition = SharedEnemyVariantCatalog.Get(variantId);
            if (definition == null) return;
            _debugSelectedVariantId = definition.Id;
            var center = _target.position;
            var angleOffset = Random.value * Mathf.PI * 2f;
            for (var i = 0; i < count; i++)
            {
                var angle = angleOffset + ((Mathf.PI * 2f * i) / Mathf.Max(1, count));
                var radius = count <= 1 ? 1.8f : DebugVariantSpawnRingRadius + Random.Range(-0.25f, 0.25f);
                var position = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                SpawnVariantEnemy(definition, position, trackAsDebugMonster: true);
            }
        }

        public void DebugClearNonBossEnemies()
        {
            ClearActiveRewardChests();
            ResetWaveTracking();
            if (_registry?.Enemies != null)
            {
                var enemies = new List<EnemyController>(_registry.Enemies);
                foreach (var enemy in enemies) if (enemy != null) Destroy(enemy.gameObject);
            }
            _debugMonsterLabEnemies.Clear();
            _bossEnemy = null;
            _bossSpawnSequence = 0;
            _activeWaveTargetEnemy = null;
            _activeWaveTargetLabel = string.Empty;
            _waveTargetSpawnSequence = 0;
        }

        private void DebugStartConfiguredWave(int waveIndex)
        {
            if (_config == null || _target == null) return;
            if (_debugMonsterLabEnabled) DebugSetMonsterLabEnabled(false);
            DebugClearNonBossEnemies();
            _bossWaveTriggered = false;
            _pendingBoss = false;
            _pendingWave2 = false;
            _spawnTimer = 0f;

            if (waveIndex <= 1)
            {
                _elapsedSeconds = Mathf.Max(0f, _config.wave1TimeSeconds);
                _wave1Triggered = true;
                _wave2Triggered = false;
                StartConfiguredWave(1, _config.wave1SlimeCount, _config.wave1MushroomCount, _config.wave1SkeletonCount);
            }
            else
            {
                _elapsedSeconds = Mathf.Max(_config.wave1TimeSeconds, _config.wave2TimeSeconds);
                _wave1Triggered = true;
                _wave2Triggered = true;
                StartConfiguredWave(2, _config.wave2SlimeCount, _config.wave2MushroomCount, _config.wave2SkeletonCount);
            }
        }

        private EnemyController SpawnEnemy(
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            Vector3? requestedPosition = null,
            EnemyStatProfile statProfileOverride = null,
            bool isBoss = false,
            bool trackWaveTarget = false,
            bool ignoreSpawnClearance = false)
        {
            var visualStatProfile = _config.GetStatProfile(visualKind);
            var statProfile = statProfileOverride ?? visualStatProfile;
            var collisionRadius = CalculateCollisionRadius(statProfile);
            
            var spawnPosResult = requestedPosition.HasValue
                ? requestedPosition.Value
                : FindSpawnPosition(collisionRadius, _config.minSpawnRadius, _config.maxSpawnRadius);

            if (!spawnPosResult.HasValue)
            {
                return null;
            }

            var spawnPosition = spawnPosResult.Value;

            if (!ignoreSpawnClearance && !IsSpawnClear(spawnPosition, collisionRadius))
            {
                var retryPos = FindSpawnPosition(collisionRadius, _config.minSpawnRadius, _config.maxSpawnRadius);
                if (!retryPos.HasValue) return null;
                spawnPosition = retryPos.Value;
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
            var scaleMultiplier = (statProfile ?? visualStatProfile)?.visualScaleMultiplier ?? 1f;
            var visualWorldSize = Mathf.Max(0.1f, _config.visualScale * scaleMultiplier);
            ApplyVisualScale(visualObject.transform, renderer.sprite, visualWorldSize);
            
            if (enemyFrames.Length > 1)
            {
                visualObject.AddComponent<EnemySpriteAnimator>().Initialize(renderer, enemyFrames, animationProfile);
            }

            var runtimeMinuteTier = Mathf.Max(0, Mathf.FloorToInt(_elapsedSeconds / 60f));
            var enemy = enemyObject.AddComponent<EnemyController>();
            enemy.Initialize(
                _config, visualKind, statProfile, animationProfile, _target, _playerHealth, _registry, _experienceSystem,
                _playerCollisionRadius, collisionRadius,
                isBoss ? 1f + (Mathf.Min(runtimeMinuteTier, 10) * 0.06f) : 1f + (runtimeMinuteTier * 0.10f),
                1f + (runtimeMinuteTier * 0.05f),
                1f + (Mathf.Min(runtimeMinuteTier, 10) * 0.10f),
                isBoss, _hasArenaBounds, _arenaBounds,
                isBoss ? _bossArchetype : BossArchetypeId.Final,
                isBoss ? _bossDifficulty : null,
                _groundTilemap, _propsTilemap);

            if (!isBoss)
            {
                var healthBar = enemyObject.AddComponent<WorldHealthBar>();
                healthBar.Initialize(new Vector3(0f, _config.visualYOffset + visualWorldSize * 0.36f, 0f), 0.82f, 0.1f, 
                    new Color(1f, 0.3f, 0.35f, 0.95f), new Color(0f, 0f, 0f, 0.55f), 24);
                healthBar.SetHealth(enemy.CurrentHealth, enemy.MaxHealth);
                enemy.Changed += healthBar.SetHealth;
            }

            if (trackWaveTarget) TrackWaveEnemy(enemy);
            return enemy;
        }

        private EnemyController SpawnVariantEnemy(EnemyVariantDefinition definition, Vector3? requestedPosition = null, bool trackAsDebugMonster = false)
        {
            if (definition == null) return null;
            var profile = SharedEnemyVariantCatalog.CreateVariantStatProfile(_config, definition);
            var pos = requestedPosition;
            if (trackAsDebugMonster && requestedPosition.HasValue)
                pos = ResolveDebugSpawnPosition(requestedPosition.Value, CalculateCollisionRadius(profile));
            
            var enemy = SpawnEnemy(definition.BaseVisualKind, pos, profile);
            if (enemy != null)
            {
                enemy.ConfigureVariant(definition, HandleVariantSplitSpawnRequested);
                enemy.gameObject.name = definition.DisplayName;
                if (trackAsDebugMonster) _debugMonsterLabEnemies.Add(enemy);
            }
            return enemy;
        }

        private void TryTriggerTimedWaves()
        {
            if (_bossWaveTriggered || _config == null || _target == null || !_config.enableTimedWaves) return;
            var bossStart = GetBossWaveStartSeconds();

            if (!_wave1Triggered && _elapsedSeconds >= Mathf.Max(1f, _config.wave1TimeSeconds) && _config.wave1TimeSeconds < bossStart)
            {
                _wave1Triggered = true;
                StartConfiguredWave(1, _config.wave1SlimeCount, _config.wave1MushroomCount, _config.wave1SkeletonCount);
            }

            if (!_wave2Triggered && _elapsedSeconds >= Mathf.Max(1f, _config.wave2TimeSeconds) && _config.wave2TimeSeconds < bossStart)
            {
                _wave2Triggered = true;
                if (HasActiveWave) _pendingWave2 = true;
                else StartConfiguredWave(2, _config.wave2SlimeCount, _config.wave2MushroomCount, _config.wave2SkeletonCount);
            }

            if (!_bossWaveTriggered && _elapsedSeconds >= bossStart)
            {
                if (HasActiveWave) _pendingBoss = true;
                else TriggerBossWave();
            }
        }

        private void StartConfiguredWave(int waveIndex, int slimeCount, int mushroomCount, int skeletonCount)
        {
            var targetKind = waveIndex <= 1 ? RuntimeSpriteFactory.EnemyVisualKind.Slime : RuntimeSpriteFactory.EnemyVisualKind.Mushroom;
            var sCount = _config.spawnSlime ? Mathf.Max(0, slimeCount) : 0;
            var mCount = _config.spawnMushroom ? Mathf.Max(0, mushroomCount) : 0;
            var skCount = _config.spawnSkeleton ? Mathf.Max(0, skeletonCount) : 0;

            if (targetKind == RuntimeSpriteFactory.EnemyVisualKind.Slime && sCount > 0) sCount--;
            else if (targetKind == RuntimeSpriteFactory.EnemyVisualKind.Mushroom && mCount > 0) mCount--;

            var total = sCount + mCount + skCount + 1;
            if (total <= 0) { TryProcessPendingWaveSchedule(); return; }

            _activeWaveIndex = waveIndex;
            _activeWaveRemainingCount = 0;
            _activeWaveEnemies.Clear();
            _activeWaveTargetEnemy = null;
            _activeWaveTargetLabel = waveIndex <= 1 ? "거대 슬라임" : "거대 버섯";
            _waveTargetSpawnSequence = 0;

            var minR = Mathf.Max(0.1f, _config.timedWaveMinRadius);
            var maxR = Mathf.Max(minR + 0.1f, _config.timedWaveMaxRadius);
            var angleOffset = Random.value * Mathf.PI * 2f;
            var spawnIdx = 0;

            SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind.Slime, sCount, total, ref spawnIdx, angleOffset, minR, maxR);
            SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind.Mushroom, mCount, total, ref spawnIdx, angleOffset, minR, maxR);
            SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind.Skeleton, skCount, total, ref spawnIdx, angleOffset, minR, maxR);
            SpawnWaveTargetEnemy(targetKind, total, ref spawnIdx, angleOffset, minR, maxR);
            
            WaveStarted?.Invoke(_activeWaveIndex);
            RaiseWaveStateChanged();
        }

        private void SpawnWaveEnemies(RuntimeSpriteFactory.EnemyVisualKind visualKind, int count, int total, ref int spawnIndex, float angleOffset, float minRadius, float maxRadius)
        {
            var cRadius = CalculateCollisionRadius(_config.GetStatProfile(visualKind));
            var adjMin = minRadius;
            var adjMax = maxRadius;
            ApplyOffscreenRadiusFloor(cRadius, ref adjMin, ref adjMax);

            for (var i = 0; i < count; i++)
            {
                var t = total > 0 ? spawnIndex / (float)total : 0f;
                var angle = angleOffset + (Mathf.PI * 2f * t) + Random.Range(-0.15f, 0.15f);
                var radius = Random.Range(adjMin, adjMax);
                var pos = _target.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                var def = SharedEnemyVariantCatalog.PickWaveVariant(_mapId, _activeWaveIndex, visualKind, i, count);
                if (def != null) SpawnVariantEnemy(def, pos);
                else SpawnEnemy(visualKind, pos);
                spawnIndex++;
            }
        }

        private void SpawnWaveTargetEnemy(RuntimeSpriteFactory.EnemyVisualKind visualKind, int total, ref int spawnIndex, float angleOffset, float minRadius, float maxRadius)
        {
            var baseProfile = _config.GetStatProfile(visualKind);
            var targetProfile = new EnemyStatProfile
            {
                healthMultiplier = (baseProfile?.healthMultiplier ?? 1f) * 5.5f,
                moveSpeedMultiplier = (baseProfile?.moveSpeedMultiplier ?? 1f) * 0.92f,
                contactDamageMultiplier = (baseProfile?.contactDamageMultiplier ?? 1f) * 1.25f,
                experienceMultiplier = (baseProfile?.experienceMultiplier ?? 1f) * 3f,
                visualScaleMultiplier = (baseProfile?.visualScaleMultiplier ?? 1f) * WaveTargetVisualScaleMultiplier,
                collisionRadiusMultiplier = (baseProfile?.collisionRadiusMultiplier ?? 1f) * WaveTargetCollisionRadiusMultiplier,
            };

            var cRadius = CalculateCollisionRadius(targetProfile);
            var adjMin = minRadius;
            var adjMax = maxRadius;
            ApplyOffscreenRadiusFloor(cRadius, ref adjMin, ref adjMax);

            var t = total > 0 ? spawnIndex / (float)total : 0f;
            var angle = angleOffset + (Mathf.PI * 2f * t) + Random.Range(-0.08f, 0.08f);
            var radius = Random.Range(adjMin, adjMax);
            var pos = _target.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            var enemy = SpawnEnemy(visualKind, pos, targetProfile, trackWaveTarget: true);
            spawnIndex++;
            if (enemy != null)
            {
                _activeWaveTargetEnemy = enemy;
                _waveTargetSpawnSequence = ++_spawnSequenceCounter;
                enemy.gameObject.name = $"{_activeWaveTargetLabel}Enemy";
            }
        }

        public bool TryGetPriorityBossBarTarget(out EnemyController enemy, out string label)
        {
            enemy = null; label = string.Empty;
            var boss = _bossEnemy; if (boss != null && boss.IsDead) boss = null;
            var target = _activeWaveTargetEnemy; if (target != null && target.IsDead) target = null;
            if (boss == null && target == null) return false;
            if (target != null && (boss == null || _waveTargetSpawnSequence >= _bossSpawnSequence))
            {
                enemy = target; label = _activeWaveTargetLabel; return true;
            }
            enemy = boss; label = "보스"; return true;
        }

        private void HandleTrackedEnemyDefeated(EnemyController enemy)
        {
            if (enemy == null) return;
            _debugMonsterLabEnemies.Remove(enemy);
            if (_bossEnemy == enemy) { _bossEnemy = null; _bossSpawnSequence = 0; }
            if (_activeWaveEnemies.Remove(enemy))
            {
                if (_activeWaveTargetEnemy == enemy)
                {
                    SpawnWaveRewardChest(_activeWaveIndex, enemy.transform.position);
                    _activeWaveTargetEnemy = null;
                    _waveTargetSpawnSequence = 0;
                }
                _activeWaveRemainingCount = _activeWaveEnemies.Count;
                RaiseWaveStateChanged();
                if (_activeWaveRemainingCount <= 0)
                {
                    var idx = _activeWaveIndex; _activeWaveIndex = 0; WaveCleared?.Invoke(idx);
                    RaiseWaveStateChanged();
                    TryProcessPendingWaveSchedule();
                }
            }
        }

        private void SpawnWaveRewardChest(int waveIndex, Vector3 position)
        {
            var chest = new GameObject($"WaveRewardChest_{waveIndex}").AddComponent<WaveRewardChest>();
            chest.transform.position = position;
            _activeRewardChests.Add(chest);
            chest.Initialize(_target, waveIndex, ++_spawnSequenceCounter, WaveRewardPickupRadius, HandleWaveRewardChestCollected, c => _activeRewardChests.Remove(c));
        }

        private void HandleWaveRewardChestCollected(WaveRewardChest chest)
        {
            if (chest == null) return;
            _activeRewardChests.Remove(chest);
            WaveRewardChestCollected?.Invoke(chest.WaveIndex);
        }

        private void TrackWaveEnemy(EnemyController enemy)
        {
            if (enemy == null) return;
            _activeWaveEnemies.Add(enemy);
            _activeWaveRemainingCount = _activeWaveEnemies.Count;
        }

        private void RaiseWaveStateChanged() => WaveStateChanged?.Invoke(_activeWaveIndex, _activeWaveRemainingCount);
        private void ResetWaveTracking() { _activeWaveIndex = 0; _activeWaveRemainingCount = 0; _activeWaveEnemies.Clear(); _activeWaveTargetEnemy = null; RaiseWaveStateChanged(); }
        private void ClearActiveRewardChests() { foreach (var c in _activeRewardChests) if (c != null) Destroy(c.gameObject); _activeRewardChests.Clear(); }

        private void HandleVariantSplitSpawnRequested(EnemyController source, EnemyVariantDefinition definition)
        {
            if (source == null || definition?.BehaviorKind != EnemyVariantBehaviorKind.SplitOnDeath) return;
            
            // 세대 제한 체크
            if (source.Generation >= definition.SplitGenerationLimit) return;

            var count = Mathf.Max(0, definition.SplitSpawnCount);
            if (count <= 0) return;

            var nextGeneration = source.Generation + 1;
            var statProfile = SharedEnemyVariantCatalog.CreateVariantStatProfile(_config, definition);
            
            // 자식 세대는 더 작고 약하게 (예: 60% 크기, 50% 체력)
            float scaleMultiplier = Mathf.Pow(0.6f, nextGeneration);
            statProfile.healthMultiplier *= 0.5f;
            statProfile.visualScaleMultiplier *= scaleMultiplier;
            statProfile.collisionRadiusMultiplier *= scaleMultiplier;

            var childRadius = CalculateCollisionRadius(statProfile);
            var ringR = Mathf.Max(0.3f, childRadius * 2.0f);
            var angleOffset = UnityEngine.Random.value * Mathf.PI * 2f;

            for (var i = 0; i < count; i++)
            {
                var angle = angleOffset + ((Mathf.PI * 2f * i) / Mathf.Max(1, count));
                var pos = source.transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * ringR;
                pos = ClampToArena(pos);
                
                // 자식도 동일한 변종(SlimeSplit)으로 생성하되, 세대를 높여서 전달
                var child = SpawnEnemy(definition.BaseVisualKind, pos, statProfile, ignoreSpawnClearance: true);
                if (child != null)
                {
                    child.ConfigureVariant(definition, HandleVariantSplitSpawnRequested, nextGeneration);
                    child.gameObject.name = $"{definition.DisplayName} (Gen {nextGeneration})";
                }
            }
        }

        private void TryProcessPendingWaveSchedule()
        {
            if (HasActiveWave || _bossWaveTriggered) return;
            if (_pendingWave2) { _pendingWave2 = false; StartConfiguredWave(2, _config.wave2SlimeCount, _config.wave2MushroomCount, _config.wave2SkeletonCount); }
            else if (_pendingBoss) { _pendingBoss = false; TriggerBossWave(); }
        }

        private static void ApplyVisualScale(Transform target, Sprite sprite, float desiredSize)
        {
            if (sprite == null) { target.localScale = Vector3.one * desiredSize; return; }
            var s = sprite.bounds.size;
            var maxS = Mathf.Max(s.x, s.y);
            var scale = maxS <= 0.0001f ? desiredSize : desiredSize / maxS;
            target.localScale = new Vector3(scale, scale, 1f);
        }

        private Vector3 ResolveDebugSpawnPosition(Vector3 req, float rad)
        {
            if (IsSpawnClear(req, rad)) return req;
            for (var i = 0; i < 12; i++)
            {
                var cand = ClampToArena(req + (Vector3)(Random.insideUnitCircle.normalized * Random.Range(0.6f, 2.4f)));
                if (IsSpawnClear(cand, rad)) return cand;
            }
            return ClampToArena(req);
        }

        private Vector3 ClampToArena(Vector3 pos)
        {
            if (!_hasArenaBounds) return pos;
            const float edgePadding = 0.8f;
            return new Vector3(
                Mathf.Clamp(pos.x, _arenaBounds.xMin + edgePadding, _arenaBounds.xMax - edgePadding), 
                Mathf.Clamp(pos.y, _arenaBounds.yMin + edgePadding, _arenaBounds.yMax - edgePadding), 
                0f);
        }

        private Vector3? FindSpawnPosition(float rad, float minR, float maxR)
        {
            // 이제 무식하게 수십 번 찍지 않고, 맵 안으로 밀어넣는 방식을 씁니다.
            const int maxTries = 3; 
            var min = minR; 
            var max = maxR; 
            ApplyOffscreenRadiusFloor(rad, ref min, ref max);

            for (var i = 0; i < maxTries; i++)
            {
                var angle = Random.value * Mathf.PI * 2f;
                var rawPos = _target.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Random.Range(min, max);
                
                // [핵심] 일단 좌표를 찍고, 맵 밖이면 맵 경계선 안으로 밀어넣습니다.
                // 이렇게 하면 한 번에 맵 안쪽 자리를 찾을 수 있습니다.
                var cand = ClampToArena(rawPos);

                if (IsSpawnClear(cand, rad))
                {
                    return cand;
                }
            }

            return null;
        }

        private void ApplyOffscreenRadiusFloor(float rad, ref float min, ref float max)
        {
            var offMin = GetMinimumOffscreenRadius(rad);
            if (offMin > 0f) { min = Mathf.Max(min, offMin); max = Mathf.Max(max, min + 0.1f); }
        }

        private float GetMinimumOffscreenRadius(float rad)
        {
            var cam = _spawnReferenceCamera ?? Camera.main;
            if (cam == null || !cam.orthographic) return 0f;
            var h = cam.orthographicSize; var w = h * cam.aspect;
            return Mathf.Sqrt(w * w + h * h) + (_config?.offscreenSpawnPadding ?? 0f) + _playerCollisionRadius + rad;
        }

        private bool IsSpawnClear(Vector3 cand, float rad)
        {
            // [수정] 반드시 풀밭 위여야 함
            if (_groundTilemap != null && !_groundTilemap.HasTile(_groundTilemap.WorldToCell(cand))) return false;

            // [수정] 플레이어와 너무 가까우면 안됨 (최소 거리 보장)
            var distToPlayer = Vector2.Distance(cand, _target.position);
            // 화면 밖에서만 생성되게 하거나, 최소한 플레이어 근처는 피함
            var safeDistance = GetMinimumOffscreenRadius(rad) * 0.85f; // 화면 대각선의 85% 이상 거리 확보
            if (distToPlayer < safeDistance) return false;

            if (_registry?.Enemies == null) return true;
            foreach (var other in _registry.Enemies)
            {
                if (other == null) continue;
                if (Vector2.Distance(cand, other.transform.position) < rad + other.CollisionRadius) return false;
            }
            return true;
        }

        private float CalculateCollisionRadius(EnemyStatProfile p) => Mathf.Max(0.05f, _config.collisionRadius * (p?.collisionRadiusMultiplier ?? 1f));
        private RuntimeSpriteFactory.EnemyVisualKind PickEnemyVisualKind() => SharedEnemyVariantCatalog.PickDynamicVisualKind(_mapId, _config, _elapsedSeconds);
        private float GetBossWaveStartSeconds() => Mathf.Max((_config?.mushroomPhaseStartSeconds ?? 300f) + 1f, _config?.bossWaveStartSeconds ?? 600f);

        private void SpawnDynamicTickEnemies()
        {
            var count = CalculateSpawnCountForTick(GetAliveEnemyCount(), GetTargetAliveCount());
            for (var i = 0; i < count; i++)
            {
                if (IsAtHardAliveCap()) break;
                var visual = PickEnemyVisualKind();
                var def = SharedEnemyVariantCatalog.PickDynamicVariant(_mapId, visual, _elapsedSeconds);
                if (def != null) SpawnVariantEnemy(def); else SpawnEnemy(visual);
            }
        }

        private float CalculateNextSpawnInterval()
        {
            var baseInv = SpawnMath.CalculateSpawnInterval(_elapsedSeconds, _config.initialSpawnInterval, _config.minimumSpawnInterval, _config.spawnRampSeconds);
            if (_config == null || !_config.enableDynamicDensity) return baseInv;
            var ratio = GetAliveEnemyCount() / (float)Mathf.Max(1, GetTargetAliveCount());
            var scale = ratio < 1f ? Mathf.Lerp(Mathf.Clamp(_config.lowDensityIntervalScaleMin, 0.2f, 1f), 1f, ratio) 
                                   : Mathf.Lerp(1f, Mathf.Max(1f, _config.highDensityIntervalScaleMax), Mathf.Clamp01((ratio - 1f) / 0.6f));
            return Mathf.Max(0.03f, baseInv * scale);
        }

        private int CalculateSpawnCountForTick(int alive, int target)
        {
            if (_config == null) return 1;
            if (!_config.enableDynamicDensity) return IsAtHardAliveCap() ? 0 : 1;
            if (IsAtHardAliveCap()) return 0;
            var deficit = target - alive; if (deficit <= 0) return 1;
            var extra = Mathf.Min(Mathf.Max(0, _config.lowDensityExtraSpawnMax), Mathf.CeilToInt(deficit / (float)Mathf.Max(1, Mathf.RoundToInt(target * 0.25f))));
            return 1 + extra;
        }

        private int GetAliveEnemyCount() => _registry?.Enemies?.Count ?? 0;
        private int GetTargetAliveCount()
        {
            if (_config == null) return 12;
            var t = Mathf.Clamp01(_elapsedSeconds / Mathf.Max(1f, _config.targetAliveRampSeconds));
            return Mathf.RoundToInt(Mathf.Lerp(_config.targetAliveStart, _config.targetAliveEnd, Mathf.Pow(t, Mathf.Max(0.1f, _config.targetAliveCurveExponent))));
        }

        private bool IsAtHardAliveCap() => GetAliveEnemyCount() >= Mathf.Max(1, _config?.hardAliveCap ?? 100);
        private WaveRewardChest GetLatestRewardChest() { WaveRewardChest l = null; int s = int.MinValue; foreach (var c in _activeRewardChests) { if (c != null && c.SpawnSequence > s) { s = c.SpawnSequence; l = c; } } return l; }

        private void OnEnable() => EnemyController.Defeated += HandleTrackedEnemyDefeated;
        private void OnDisable() => EnemyController.Defeated -= HandleTrackedEnemyDefeated;
    }
}
