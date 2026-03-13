using System.Collections.Generic;
using EJR.Game.Core;
using EJR.Game.Gameplay;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerCoopController : MonoBehaviour
    {
        [SerializeField] private Rect arenaBounds = new Rect(-12f, -7f, 24f, 14f);
        [SerializeField, Min(0.25f)] private float defeatReturnDelaySeconds = 2f;

        private readonly List<MultiplayerPlayerCombatant> _alivePlayers = new(4);

        private EnemyRegistry _enemyRegistry;
        private EnemyConfig _enemyConfig;
        private PlayerConfig _playerConfig;
        private WeaponConfig _weaponConfig;
        private GameObject _enemyPrefab;
        private GameObject _projectilePrefab;
        private float _elapsedSeconds;
        private float _nextSpawnAt;
        private bool _defeatTriggered;
        private float _defeatReturnAt;

        public static MultiplayerCoopController Instance { get; private set; }

        public EnemyRegistry EnemyRegistry => _enemyRegistry;
        public Rect ArenaBounds => arenaBounds;
        public float PlayerCollisionRadius => _playerConfig != null ? Mathf.Max(0.05f, _playerConfig.collisionRadius) : 0.35f;

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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer)
            {
                return;
            }

            EnsureRuntime();
            _elapsedSeconds += Time.deltaTime;

            if (!CollectAlivePlayers(_alivePlayers))
            {
                HandleSharedDefeat();
                return;
            }

            _defeatTriggered = false;
            TryMaintainEnemyPressure(_alivePlayers);
        }

        public bool TryFindNearestEnemy(Vector3 fromPosition, float maxDistance, out EnemyController enemy)
        {
            enemy = _enemyRegistry != null ? _enemyRegistry.FindNearest(fromPosition, Mathf.Max(0.1f, maxDistance)) : null;
            return enemy != null;
        }

        public Transform ResolveClosestPlayerTransform(Vector3 fromPosition)
        {
            return TryGetNearestAlivePlayer(fromPosition, out var player) ? player.transform : null;
        }

        public PlayerHealth ResolveClosestPlayerHealth(Vector3 fromPosition)
        {
            return TryGetNearestAlivePlayer(fromPosition, out var player) ? player.ServerPlayerHealth : null;
        }

        public bool SpawnPlayerProjectile(Vector3 origin, Vector2 direction, ulong ownerClientId)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || !manager.IsServer || _projectilePrefab == null)
            {
                return false;
            }

            var projectileObject = Instantiate(_projectilePrefab, origin, Quaternion.identity);
            var projectileActor = projectileObject.GetComponent<MultiplayerSharedProjectileActor>();
            if (projectileActor == null)
            {
                Destroy(projectileObject);
                return false;
            }

            projectileActor.InitializeServer(
                this,
                direction,
                Mathf.Max(0.1f, _weaponConfig.projectileSpeed),
                Mathf.Max(0.05f, _weaponConfig.projectileLifetime),
                Mathf.Max(0f, _weaponConfig.rifleBaseDamage),
                Mathf.Max(0.05f, _weaponConfig.projectileHitRadius),
                ownerClientId);

            projectileActor.NetworkObject.Spawn(true);
            return true;
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
        }

        private bool CollectAlivePlayers(List<MultiplayerPlayerCombatant> results)
        {
            results.Clear();
            var players = FindObjectsByType<MultiplayerPlayerCombatant>(FindObjectsSortMode.None);
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !player.IsSpawned || !player.IsAlive)
                {
                    continue;
                }

                results.Add(player);
            }

            return results.Count > 0;
        }

        private bool TryGetNearestAlivePlayer(Vector3 fromPosition, out MultiplayerPlayerCombatant bestPlayer)
        {
            bestPlayer = null;
            var bestDistanceSq = float.MaxValue;
            var players = FindObjectsByType<MultiplayerPlayerCombatant>(FindObjectsSortMode.None);

            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !player.IsSpawned || !player.IsAlive)
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
                SpawnSharedEnemy(alivePlayers);
            }

            _nextSpawnAt = Time.time + GetSpawnInterval(aliveEnemyCount, targetAliveEnemyCount);
        }

        private void SpawnSharedEnemy(IReadOnlyList<MultiplayerPlayerCombatant> alivePlayers)
        {
            var visualKind = PickEnemyVisualKind();
            var spawnPosition = FindSpawnPosition(alivePlayers, visualKind);
            var enemyObject = Instantiate(_enemyPrefab, spawnPosition, Quaternion.identity);
            var enemyActor = enemyObject.GetComponent<MultiplayerSharedEnemyActor>();
            if (enemyActor == null)
            {
                Destroy(enemyObject);
                return;
            }

            enemyActor.InitializeServer(this, visualKind, spawnPosition, _elapsedSeconds);
            enemyActor.NetworkObject.Spawn(true);
        }

        private Vector3 FindSpawnPosition(IReadOnlyList<MultiplayerPlayerCombatant> alivePlayers, RuntimeSpriteFactory.EnemyVisualKind visualKind)
        {
            var statProfile = _enemyConfig.GetStatProfile(visualKind);
            var collisionRadius = GetCollisionRadius(statProfile);
            var minRadius = Mathf.Max(0.75f, _enemyConfig.minSpawnRadius);
            var maxRadius = Mathf.Max(minRadius + 0.5f, _enemyConfig.maxSpawnRadius);

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
                if (enemies[i] != null)
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

        private void HandleSharedDefeat()
        {
            if (!_defeatTriggered)
            {
                _defeatTriggered = true;
                _defeatReturnAt = Time.time + defeatReturnDelaySeconds;
                return;
            }

            if (Time.time < _defeatReturnAt)
            {
                return;
            }

            enabled = false;
            MultiplayerSessionController.EnsureInstance().LeaveSession("All players were defeated.");
        }
    }
}
