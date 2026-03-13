using System.Collections.Generic;
using EJR.Game.Core;
using EJR.Game.Gameplay;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MultiplayerCoopController : NetworkBehaviour
    {
        [SerializeField] private Rect arenaBounds = new Rect(-12f, -7f, 24f, 14f);
        [SerializeField, Min(1f)] private float lobbySpawnRadius = 3.5f;
        [SerializeField, Min(1f)] private float runSpawnRadius = 2.2f;
        [SerializeField, Min(0.25f)] private float defeatDelaySeconds = 2f;
        [SerializeField, Min(0.5f)] private float resultReturnDelaySeconds = 4f;
        [SerializeField, Min(0.25f)] private float reviveRadius = 1.2f;
        [SerializeField, Min(0.25f)] private float reviveDurationSeconds = 1.25f;
        [SerializeField, Range(0.1f, 1f)] private float reviveHealthFraction = 0.4f;
        [SerializeField, Min(0f)] private float reviveInvulnerabilitySeconds = 1.5f;

        private readonly NetworkVariable<int> _phase =
            new((int)MultiplayerRunPhase.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _teamLevel =
            new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _teamExperience =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _teamRequiredExperience =
            new(ProgressionMath.RequiredExperienceForLevel(1), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _remainingSeconds =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _bossCurrentHealth =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _bossMaxHealth =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _resultCleared =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly List<MultiplayerPlayerCombatant> _spawnedPlayers = new(4);
        private readonly List<MultiplayerPlayerCombatant> _combatPlayers = new(4);

        private EnemyRegistry _enemyRegistry;
        private EnemyConfig _enemyConfig;
        private PlayerConfig _playerConfig;
        private WeaponConfig _weaponConfig;
        private GameObject _enemyPrefab;
        private GameObject _projectilePrefab;
        private GameObject _experienceOrbPrefab;
        private MultiplayerSharedEnemyActor _currentBoss;
        private float _elapsedSeconds;
        private float _nextSpawnAt;
        private float _allDownAt = -1f;
        private float _resultReturnAt = -1f;
        private bool _bossWaveTriggered;

        public static MultiplayerCoopController Instance { get; private set; }

        public EnemyRegistry EnemyRegistry => _enemyRegistry;
        public Rect ArenaBounds => arenaBounds;
        public float PlayerCollisionRadius => _playerConfig != null ? Mathf.Max(0.05f, _playerConfig.collisionRadius) : 0.35f;
        public MultiplayerRunPhase Phase => (MultiplayerRunPhase)_phase.Value;
        public int TeamLevel => _teamLevel.Value;
        public int TeamExperience => _teamExperience.Value;
        public int TeamRequiredExperience => _teamRequiredExperience.Value;
        public float RemainingSeconds => _remainingSeconds.Value;
        public bool BossActive => _bossMaxHealth.Value > 0.0001f;
        public float BossCurrentHealth => _bossCurrentHealth.Value;
        public float BossMaxHealth => _bossMaxHealth.Value;
        public bool ResultCleared => _resultCleared.Value;
        public bool AllowsJoin => Phase == MultiplayerRunPhase.Lobby;
        public float ReviveRadius => reviveRadius;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureConfigs();
            EnsureRuntime();
            LoadPrefabs();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;

            if (!IsServer)
            {
                return;
            }

            Time.timeScale = 1f;
            ReturnPlayersToLobby();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                Time.timeScale = 1f;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public override void OnDestroy()
        {
            if (IsServer)
            {
                Time.timeScale = 1f;
            }

            if (Instance == this)
            {
                Instance = null;
            }

            base.OnDestroy();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            EnsureRuntime();
            LoadPrefabs();

            switch (Phase)
            {
                case MultiplayerRunPhase.Lobby:
                    UpdateBossState();
                    break;

                case MultiplayerRunPhase.Running:
                    UpdateRunning();
                    break;

                case MultiplayerRunPhase.LevelChoice:
                    UpdateBossState();
                    ResumeRunIfChoicesResolved();
                    break;

                case MultiplayerRunPhase.Result:
                    if (_resultReturnAt > 0f && Time.unscaledTime >= _resultReturnAt)
                    {
                        ReturnPlayersToLobby();
                    }
                    break;
            }
        }

        public void RequestStartGame()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return;
            }

            RequestStartGameServerRpc();
        }

        public string GetStartBlockReason()
        {
            if (Phase != MultiplayerRunPhase.Lobby)
            {
                return "Run already in progress.";
            }

            CollectSpawnedPlayers(_spawnedPlayers);
            if (_spawnedPlayers.Count <= 0)
            {
                return "Waiting for players.";
            }

            for (var i = 0; i < _spawnedPlayers.Count; i++)
            {
                var player = _spawnedPlayers[i];
                if (player == null || !player.IsSpawned)
                {
                    continue;
                }

                if (!player.SelectionComplete)
                {
                    return "Everyone must choose character and weapon.";
                }

                if (!player.IsReady)
                {
                    return "Everyone must be ready.";
                }
            }

            return string.Empty;
        }

        public void EnterLevelChoicePauseIfNeeded()
        {
            if (!IsServer || Phase != MultiplayerRunPhase.Running)
            {
                return;
            }

            CollectSpawnedPlayers(_spawnedPlayers);
            for (var i = 0; i < _spawnedPlayers.Count; i++)
            {
                var player = _spawnedPlayers[i];
                if (player != null && player.HasPendingServerChoice)
                {
                    _phase.Value = (int)MultiplayerRunPhase.LevelChoice;
                    Time.timeScale = 0f;
                    return;
                }
            }
        }

        public void ResumeRunIfChoicesResolved()
        {
            if (!IsServer || Phase != MultiplayerRunPhase.LevelChoice)
            {
                return;
            }

            CollectSpawnedPlayers(_spawnedPlayers);
            for (var i = 0; i < _spawnedPlayers.Count; i++)
            {
                var player = _spawnedPlayers[i];
                if (player != null && player.HasPendingServerChoice)
                {
                    return;
                }
            }

            Time.timeScale = 1f;
            _phase.Value = (int)MultiplayerRunPhase.Running;
        }

        public bool TryResolveExperienceCollector(Vector3 fromPosition, float maxDistance, out MultiplayerPlayerCombatant collector, out float distance)
        {
            collector = null;
            distance = float.MaxValue;

            CollectCombatPlayers(_combatPlayers);
            var maxDistanceSq = Mathf.Max(0.1f, maxDistance) * Mathf.Max(0.1f, maxDistance);
            for (var i = 0; i < _combatPlayers.Count; i++)
            {
                var player = _combatPlayers[i];
                if (player == null)
                {
                    continue;
                }

                var distanceSq = (player.transform.position - fromPosition).sqrMagnitude;
                if (distanceSq > maxDistanceSq || distanceSq >= distance * distance)
                {
                    continue;
                }

                collector = player;
                distance = Mathf.Sqrt(distanceSq);
            }

            return collector != null;
        }

        public void CollectSharedExperience(int amount)
        {
            if (!IsServer || Phase != MultiplayerRunPhase.Running || amount <= 0)
            {
                return;
            }

            CollectSpawnedPlayers(_spawnedPlayers);
            for (var i = 0; i < _spawnedPlayers.Count; i++)
            {
                _spawnedPlayers[i]?.ServerAddSharedExperience(amount);
            }

            _teamExperience.Value += amount;
            while (_teamExperience.Value >= _teamRequiredExperience.Value)
            {
                _teamExperience.Value -= _teamRequiredExperience.Value;
                _teamLevel.Value++;
                _teamRequiredExperience.Value = ProgressionMath.RequiredExperienceForLevel(_teamLevel.Value);
            }

            EnterLevelChoicePauseIfNeeded();
        }

        public Transform ResolveClosestPlayerTransform(Vector3 fromPosition)
        {
            return TryGetNearestCombatPlayer(fromPosition, out var player) ? player.transform : null;
        }

        public PlayerHealth ResolveClosestPlayerHealth(Vector3 fromPosition)
        {
            return TryGetNearestCombatPlayer(fromPosition, out var player) ? player.ServerPlayerHealth : null;
        }

        public bool SpawnExperienceOrb(Vector3 position, int value)
        {
            if (!IsServer || _experienceOrbPrefab == null)
            {
                return false;
            }

            var orbObject = Instantiate(_experienceOrbPrefab, position, Quaternion.identity);
            var orbActor = orbObject.GetComponent<MultiplayerSharedExperienceOrbActor>();
            if (orbActor == null)
            {
                Destroy(orbObject);
                return false;
            }

            orbActor.InitializeServer(
                value,
                _playerConfig != null ? _playerConfig.pickupRadius : 1.2f,
                _playerConfig != null ? _playerConfig.xpAttractRadius : 4f,
                _playerConfig != null ? _playerConfig.xpAttractSpeed : 6f);

            orbActor.NetworkObject.Spawn(true);
            return true;
        }

        public bool SpawnPlayerProjectile(AutoWeaponSystem.ProjectileSpawnRequest request, ulong ownerClientId)
        {
            if (!IsServer || _projectilePrefab == null)
            {
                return false;
            }

            var projectileObject = Instantiate(_projectilePrefab, request.SpawnPosition, Quaternion.identity);
            var projectileActor = projectileObject.GetComponent<MultiplayerSharedProjectileActor>();
            if (projectileActor == null)
            {
                Destroy(projectileObject);
                return false;
            }

            projectileActor.InitializeServer(this, request);
            projectileActor.NetworkObject.Spawn(true);
            return true;
        }

        private void UpdateRunning()
        {
            CollectSpawnedPlayers(_spawnedPlayers);
            CollectCombatPlayers(_combatPlayers);
            HandleAutomaticRevives(_spawnedPlayers, _combatPlayers);
            UpdateBossState();

            if (_combatPlayers.Count <= 0)
            {
                HandleAllPlayersDown();
                return;
            }

            _allDownAt = -1f;
            _elapsedSeconds += Time.deltaTime;

            if (!_bossWaveTriggered)
            {
                _remainingSeconds.Value = Mathf.Max(0f, Mathf.Max(30f, _enemyConfig.bossWaveStartSeconds) - _elapsedSeconds);
                if (_remainingSeconds.Value <= 0.001f)
                {
                    TriggerBossWave(_combatPlayers);
                }
            }

            if (_bossWaveTriggered)
            {
                if (_currentBoss == null || !_currentBoss.IsSpawned)
                {
                    EnterResult(true);
                    return;
                }
            }
            else
            {
                TryMaintainEnemyPressure(_combatPlayers);
            }

            UpdateBossState();
        }

        private void HandleAutomaticRevives(IReadOnlyList<MultiplayerPlayerCombatant> allPlayers, IReadOnlyList<MultiplayerPlayerCombatant> combatPlayers)
        {
            if (Phase != MultiplayerRunPhase.Running)
            {
                return;
            }

            var reviveRadiusSq = reviveRadius * reviveRadius;
            for (var i = 0; i < allPlayers.Count; i++)
            {
                var downedPlayer = allPlayers[i];
                if (downedPlayer == null || !downedPlayer.IsSpawned || !downedPlayer.IsDowned)
                {
                    continue;
                }

                var hasReviver = false;
                for (var j = 0; j < combatPlayers.Count; j++)
                {
                    var reviver = combatPlayers[j];
                    if (reviver == null || ReferenceEquals(reviver, downedPlayer))
                    {
                        continue;
                    }

                    if ((reviver.transform.position - downedPlayer.transform.position).sqrMagnitude > reviveRadiusSq)
                    {
                        continue;
                    }

                    hasReviver = true;
                    break;
                }

                if (!hasReviver)
                {
                    downedPlayer.ResetReviveProgressServer();
                    continue;
                }

                var nextProgress = downedPlayer.ReviveProgress + (Time.deltaTime / Mathf.Max(0.1f, reviveDurationSeconds));
                if (nextProgress >= 1f)
                {
                    downedPlayer.CompleteReviveServer(reviveHealthFraction, reviveInvulnerabilitySeconds);
                    continue;
                }

                downedPlayer.SetReviveProgressServer(nextProgress);
            }
        }

        private void HandleAllPlayersDown()
        {
            if (_allDownAt < 0f)
            {
                _allDownAt = Time.unscaledTime + defeatDelaySeconds;
                return;
            }

            if (Time.unscaledTime < _allDownAt)
            {
                return;
            }

            EnterResult(false);
        }

        private void EnterResult(bool cleared)
        {
            if (!IsServer || Phase == MultiplayerRunPhase.Result)
            {
                return;
            }

            _resultCleared.Value = cleared;
            _phase.Value = (int)MultiplayerRunPhase.Result;
            _resultReturnAt = Time.unscaledTime + Mathf.Max(1f, resultReturnDelaySeconds);
            Time.timeScale = 0f;
        }

        private void ReturnPlayersToLobby()
        {
            if (!IsServer)
            {
                return;
            }

            Time.timeScale = 1f;
            CleanupSharedWorld();
            CollectSpawnedPlayers(_spawnedPlayers);

            for (var i = 0; i < _spawnedPlayers.Count; i++)
            {
                var spawnPosition = GetLobbySpawnPosition(i, _spawnedPlayers.Count);
                _spawnedPlayers[i]?.ServerResetToLobby(spawnPosition, arenaBounds);
            }

            _elapsedSeconds = 0f;
            _nextSpawnAt = 0f;
            _allDownAt = -1f;
            _resultReturnAt = -1f;
            _bossWaveTriggered = false;
            _currentBoss = null;
            _teamLevel.Value = 1;
            _teamExperience.Value = 0;
            _teamRequiredExperience.Value = ProgressionMath.RequiredExperienceForLevel(1);
            _remainingSeconds.Value = Mathf.Max(30f, _enemyConfig.bossWaveStartSeconds);
            _bossCurrentHealth.Value = 0f;
            _bossMaxHealth.Value = 0f;
            _resultCleared.Value = false;
            _phase.Value = (int)MultiplayerRunPhase.Lobby;
        }

        private void BeginRun()
        {
            CleanupSharedWorld();
            CollectSpawnedPlayers(_spawnedPlayers);
            for (var i = 0; i < _spawnedPlayers.Count; i++)
            {
                var spawnPosition = GetRunSpawnPosition(i, _spawnedPlayers.Count);
                _spawnedPlayers[i]?.ServerPrepareForRun(spawnPosition, arenaBounds);
            }

            _elapsedSeconds = 0f;
            _nextSpawnAt = 0f;
            _allDownAt = -1f;
            _resultReturnAt = -1f;
            _bossWaveTriggered = false;
            _currentBoss = null;
            _teamLevel.Value = 1;
            _teamExperience.Value = 0;
            _teamRequiredExperience.Value = ProgressionMath.RequiredExperienceForLevel(1);
            _remainingSeconds.Value = Mathf.Max(30f, _enemyConfig.bossWaveStartSeconds);
            _bossCurrentHealth.Value = 0f;
            _bossMaxHealth.Value = 0f;
            _resultCleared.Value = false;
            _phase.Value = (int)MultiplayerRunPhase.Running;
            Time.timeScale = 1f;
        }

        private void CleanupSharedWorld()
        {
            CleanupNetworkObjects(FindObjectsByType<MultiplayerSharedProjectileActor>(FindObjectsSortMode.None));
            CleanupNetworkObjects(FindObjectsByType<MultiplayerSharedExperienceOrbActor>(FindObjectsSortMode.None));
            CleanupNetworkObjects(FindObjectsByType<MultiplayerSharedEnemyActor>(FindObjectsSortMode.None));
        }

        private void CleanupNetworkObjects<T>(T[] actors) where T : NetworkBehaviour
        {
            for (var i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor == null)
                {
                    continue;
                }

                if (actor.NetworkObject != null && actor.NetworkObject.IsSpawned)
                {
                    actor.NetworkObject.Despawn(true);
                }
                else
                {
                    Destroy(actor.gameObject);
                }
            }
        }

        private void EnsureConfigs()
        {
            _enemyConfig ??= ScriptableObject.CreateInstance<EnemyConfig>();
            _playerConfig ??= ScriptableObject.CreateInstance<PlayerConfig>();
            _weaponConfig ??= ScriptableObject.CreateInstance<WeaponConfig>();
        }

        private void EnsureRuntime()
        {
            if (_enemyRegistry != null)
            {
                return;
            }

            var systemsRoot = new GameObject("SharedCoopSystems");
            systemsRoot.transform.SetParent(transform, false);
            _enemyRegistry = systemsRoot.AddComponent<EnemyRegistry>();
        }

        private void LoadPrefabs()
        {
            _enemyPrefab ??= Resources.Load<GameObject>(MultiplayerSessionController.SharedEnemyPrefabResourcePath);
            _projectilePrefab ??= Resources.Load<GameObject>(MultiplayerSessionController.SharedProjectilePrefabResourcePath);
            _experienceOrbPrefab ??= Resources.Load<GameObject>(MultiplayerSessionController.SharedExperienceOrbPrefabResourcePath);
        }

        private void CollectSpawnedPlayers(List<MultiplayerPlayerCombatant> results)
        {
            results.Clear();
            var players = FindObjectsByType<MultiplayerPlayerCombatant>(FindObjectsSortMode.None);
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !player.IsSpawned)
                {
                    continue;
                }

                results.Add(player);
            }
        }

        private void CollectCombatPlayers(List<MultiplayerPlayerCombatant> results)
        {
            results.Clear();
            var players = FindObjectsByType<MultiplayerPlayerCombatant>(FindObjectsSortMode.None);
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !player.IsTargetable)
                {
                    continue;
                }

                results.Add(player);
            }
        }

        private bool TryGetNearestCombatPlayer(Vector3 fromPosition, out MultiplayerPlayerCombatant bestPlayer)
        {
            bestPlayer = null;
            var bestDistanceSq = float.MaxValue;
            CollectCombatPlayers(_combatPlayers);

            for (var i = 0; i < _combatPlayers.Count; i++)
            {
                var player = _combatPlayers[i];
                if (player == null)
                {
                    continue;
                }

                var distanceSq = (player.transform.position - fromPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                bestPlayer = player;
            }

            return bestPlayer != null;
        }

        private void TryMaintainEnemyPressure(IReadOnlyList<MultiplayerPlayerCombatant> alivePlayers)
        {
            if (_enemyRegistry == null || _enemyPrefab == null)
            {
                return;
            }

            var aliveEnemyCount = CountAliveEnemies();
            var targetAliveEnemyCount = GetTargetAliveEnemyCount(alivePlayers.Count);
            if (aliveEnemyCount >= targetAliveEnemyCount || Time.time < _nextSpawnAt)
            {
                return;
            }

            var spawnCount = Mathf.Clamp(targetAliveEnemyCount - aliveEnemyCount, 1, Mathf.Max(1, alivePlayers.Count));
            for (var i = 0; i < spawnCount; i++)
            {
                SpawnSharedEnemy(alivePlayers, PickEnemyVisualKind(), isBoss: false);
            }

            _nextSpawnAt = Time.time + GetSpawnInterval(aliveEnemyCount, targetAliveEnemyCount);
        }

        private void TriggerBossWave(IReadOnlyList<MultiplayerPlayerCombatant> alivePlayers)
        {
            if (_bossWaveTriggered)
            {
                return;
            }

            _bossWaveTriggered = true;
            _remainingSeconds.Value = 0f;
            _currentBoss = SpawnSharedEnemy(alivePlayers, RuntimeSpriteFactory.EnemyVisualKind.Boss, isBoss: true);

            var skeletonCount = Mathf.Max(3, _enemyConfig.bossWaveSkeletonCount);
            for (var i = 0; i < skeletonCount; i++)
            {
                SpawnSharedEnemy(alivePlayers, RuntimeSpriteFactory.EnemyVisualKind.Skeleton, isBoss: false);
            }
        }

        private MultiplayerSharedEnemyActor SpawnSharedEnemy(
            IReadOnlyList<MultiplayerPlayerCombatant> alivePlayers,
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            bool isBoss)
        {
            var spawnPosition = FindSpawnPosition(alivePlayers, visualKind, isBoss);
            var enemyObject = Instantiate(_enemyPrefab, spawnPosition, Quaternion.identity);
            var enemyActor = enemyObject.GetComponent<MultiplayerSharedEnemyActor>();
            if (enemyActor == null)
            {
                Destroy(enemyObject);
                return null;
            }

            enemyActor.InitializeServer(this, visualKind, spawnPosition, _elapsedSeconds);
            enemyActor.NetworkObject.Spawn(true);
            return enemyActor;
        }

        private Vector3 FindSpawnPosition(
            IReadOnlyList<MultiplayerPlayerCombatant> alivePlayers,
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            bool isBoss)
        {
            var statProfile = _enemyConfig.GetStatProfile(visualKind);
            var collisionRadius = GetCollisionRadius(statProfile);
            var minRadius = isBoss
                ? Mathf.Max(2.5f, _enemyConfig.bossSpawnRadius * 0.9f)
                : Mathf.Max(0.75f, _enemyConfig.minSpawnRadius);
            var maxRadius = isBoss
                ? Mathf.Max(minRadius + 0.5f, _enemyConfig.bossSpawnRadius * 1.15f)
                : Mathf.Max(minRadius + 0.5f, _enemyConfig.maxSpawnRadius);

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var anchor = alivePlayers[Random.Range(0, alivePlayers.Count)].transform.position;
                var angle = Random.value * Mathf.PI * 2f;
                var radius = Random.Range(minRadius, maxRadius);
                var candidate = anchor + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                candidate.x = Mathf.Clamp(candidate.x, arenaBounds.xMin + collisionRadius, arenaBounds.xMax - collisionRadius);
                candidate.y = Mathf.Clamp(candidate.y, arenaBounds.yMin + collisionRadius, arenaBounds.yMax - collisionRadius);
                candidate.z = 0f;

                if (IsSpawnClear(candidate, collisionRadius, alivePlayers))
                {
                    return candidate;
                }
            }

            var fallbackAnchor = alivePlayers[0].transform.position;
            var fallback = fallbackAnchor + new Vector3(minRadius, 0f, 0f);
            fallback.x = Mathf.Clamp(fallback.x, arenaBounds.xMin + collisionRadius, arenaBounds.xMax - collisionRadius);
            fallback.y = Mathf.Clamp(fallback.y, arenaBounds.yMin + collisionRadius, arenaBounds.yMax - collisionRadius);
            fallback.z = 0f;
            return fallback;
        }

        private bool IsSpawnClear(Vector3 position, float collisionRadius, IReadOnlyList<MultiplayerPlayerCombatant> alivePlayers)
        {
            var minimumPlayerDistance = collisionRadius + PlayerCollisionRadius + 0.35f;
            var minimumPlayerDistanceSq = minimumPlayerDistance * minimumPlayerDistance;
            for (var i = 0; i < alivePlayers.Count; i++)
            {
                var player = alivePlayers[i];
                if (player == null)
                {
                    continue;
                }

                if ((player.transform.position - position).sqrMagnitude < minimumPlayerDistanceSq)
                {
                    return false;
                }
            }

            if (_enemyRegistry == null)
            {
                return true;
            }

            var nearbyEnemy = _enemyRegistry.FindNearest(position, collisionRadius + _enemyRegistry.GetMaxCollisionRadius() + 0.4f);
            if (nearbyEnemy == null)
            {
                return true;
            }

            var minimumEnemyDistance = collisionRadius + nearbyEnemy.CollisionRadius + 0.2f;
            return (nearbyEnemy.transform.position - position).sqrMagnitude >= minimumEnemyDistance * minimumEnemyDistance;
        }

        private int CountAliveEnemies()
        {
            if (_enemyRegistry == null)
            {
                return 0;
            }

            var enemies = _enemyRegistry.Enemies;
            var count = 0;
            for (var i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && !enemies[i].IsDead)
                {
                    count++;
                }
            }

            return count;
        }

        private int GetTargetAliveEnemyCount(int alivePlayerCount)
        {
            var ramp = Mathf.Clamp01(_elapsedSeconds / Mathf.Max(1f, _enemyConfig.targetAliveRampSeconds));
            var baseTarget = Mathf.RoundToInt(Mathf.Lerp(_enemyConfig.targetAliveStart, _enemyConfig.targetAliveEnd, ramp));
            var scaledTarget = baseTarget + Mathf.Max(0, alivePlayerCount - 1) * 4;
            return Mathf.Clamp(scaledTarget, Mathf.Max(2, alivePlayerCount * 2), _enemyConfig.hardAliveCap);
        }

        private float GetSpawnInterval(int aliveEnemyCount, int targetAliveEnemyCount)
        {
            var ramp = Mathf.Clamp01(_elapsedSeconds / Mathf.Max(1f, _enemyConfig.spawnRampSeconds));
            var baseInterval = Mathf.Lerp(_enemyConfig.initialSpawnInterval, _enemyConfig.minimumSpawnInterval, ramp);
            var density = targetAliveEnemyCount <= 0 ? 1f : Mathf.Clamp01(aliveEnemyCount / (float)targetAliveEnemyCount);
            return baseInterval * Mathf.Lerp(0.8f, 1.2f, density);
        }

        private RuntimeSpriteFactory.EnemyVisualKind PickEnemyVisualKind()
        {
            if (_elapsedSeconds >= 150f && Random.value < 0.15f)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Skeleton;
            }

            if (_elapsedSeconds >= 45f && Random.value < 0.35f)
            {
                return RuntimeSpriteFactory.EnemyVisualKind.Mushroom;
            }

            return RuntimeSpriteFactory.EnemyVisualKind.Slime;
        }

        private float GetCollisionRadius(EnemyStatProfile statProfile)
        {
            var multiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.collisionRadiusMultiplier) : 1f;
            return Mathf.Max(0.05f, _enemyConfig.collisionRadius * multiplier);
        }

        private void UpdateBossState()
        {
            if (_currentBoss == null || !_currentBoss.IsSpawned)
            {
                _bossCurrentHealth.Value = 0f;
                _bossMaxHealth.Value = 0f;
                return;
            }

            var enemy = _currentBoss.GetComponent<EnemyController>();
            if (enemy == null || enemy.IsDead)
            {
                _bossCurrentHealth.Value = 0f;
                _bossMaxHealth.Value = 0f;
                return;
            }

            _bossCurrentHealth.Value = enemy.CurrentHealth;
            _bossMaxHealth.Value = enemy.MaxHealth;
        }

        private Vector3 GetLobbySpawnPosition(int index, int totalCount)
        {
            return GetCircleSpawnPosition(index, totalCount, lobbySpawnRadius);
        }

        private Vector3 GetRunSpawnPosition(int index, int totalCount)
        {
            return GetCircleSpawnPosition(index, totalCount, runSpawnRadius);
        }

        private static Vector3 GetCircleSpawnPosition(int index, int totalCount, float radius)
        {
            if (totalCount <= 0)
            {
                return Vector3.zero;
            }

            var angle = (Mathf.PI * 2f * index) / Mathf.Max(1, totalCount);
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestStartGameServerRpc(ServerRpcParams rpcParams = default)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || rpcParams.Receive.SenderClientId != manager.LocalClientId)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(GetStartBlockReason()))
            {
                return;
            }

            BeginRun();
        }
    }
}
