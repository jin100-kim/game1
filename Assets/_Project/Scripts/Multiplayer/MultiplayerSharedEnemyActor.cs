using EJR.Game.Core;
using EJR.Game.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MultiplayerSharedEnemyActor : NetworkBehaviour
    {
        private const string VisualObjectName = "Visual";

        private readonly NetworkVariable<int> _visualKind =
            new((int)RuntimeSpriteFactory.EnemyVisualKind.Slime, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _currentHealth =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _maxHealth =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private EnemyController _enemyController;
        private EnemyConfig _enemyConfig;
        private Transform _visualRoot;
        private SpriteRenderer _spriteRenderer;
        private EnemySpriteAnimator _spriteAnimator;
        private WorldHealthBar _healthBar;
        private Vector3 _lastPosition;

        public static int CountSpawnedEnemies()
        {
            var enemies = FindObjectsByType<MultiplayerSharedEnemyActor>(FindObjectsSortMode.None);
            var count = 0;
            for (var i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && enemies[i].IsSpawned)
                {
                    count++;
                }
            }

            return count;
        }

        private void Awake()
        {
            _enemyController = GetComponent<EnemyController>();
            _enemyConfig = ScriptableObject.CreateInstance<EnemyConfig>();
            EnsurePresentationObjects();
        }

        public override void OnNetworkSpawn()
        {
            _visualKind.OnValueChanged += HandleVisualKindChanged;
            _currentHealth.OnValueChanged += HandleCurrentHealthChanged;
            _maxHealth.OnValueChanged += HandleMaxHealthChanged;

            ApplyVisualKind((RuntimeSpriteFactory.EnemyVisualKind)_visualKind.Value);
            RefreshHealthBar(_currentHealth.Value, _maxHealth.Value, false);
            _lastPosition = transform.position;
        }

        public override void OnNetworkDespawn()
        {
            _visualKind.OnValueChanged -= HandleVisualKindChanged;
            _currentHealth.OnValueChanged -= HandleCurrentHealthChanged;
            _maxHealth.OnValueChanged -= HandleMaxHealthChanged;

            if (IsServer && _enemyController != null)
            {
                _enemyController.Changed -= HandleServerEnemyHealthChanged;
            }
        }

        private void Update()
        {
            if (!IsSpawned || IsServer || _spriteAnimator == null)
            {
                _lastPosition = transform.position;
                return;
            }

            var deltaTime = Mathf.Max(0.0001f, Time.deltaTime);
            var velocity = (Vector2)(transform.position - _lastPosition) / deltaTime;
            _spriteAnimator.SetMotion(velocity);
            _lastPosition = transform.position;
        }

        public void InitializeServer(
            MultiplayerCoopController coopController,
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            Vector3 spawnPosition,
            float elapsedSeconds)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || coopController == null)
            {
                return;
            }

            EnsurePresentationObjects();
            transform.position = spawnPosition;

            _enemyController.Changed -= HandleServerEnemyHealthChanged;
            _enemyController.Changed += HandleServerEnemyHealthChanged;

            var statProfile = _enemyConfig.GetStatProfile(visualKind);
            var collisionRadius = GetCollisionRadius(statProfile);
            var runtimeMinuteTier = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds / 60f));
            var runtimeMoveSpeedMultiplier = 1f + (runtimeMinuteTier * 0.05f);
            var runtimeHealthMultiplier = 1f + (runtimeMinuteTier * 0.10f);
            var initialTarget = coopController.ResolveClosestPlayerTransform(spawnPosition);
            var initialPlayerHealth = coopController.ResolveClosestPlayerHealth(spawnPosition);

            _visualKind.Value = (int)visualKind;
            ApplyVisualKind(visualKind);

            _enemyController.Initialize(
                _enemyConfig,
                visualKind,
                statProfile,
                initialTarget,
                initialPlayerHealth,
                coopController.EnemyRegistry,
                null,
                coopController.PlayerCollisionRadius,
                collisionRadius,
                runtimeHealthMultiplier,
                runtimeMoveSpeedMultiplier,
                true,
                coopController.ArenaBounds);

            _enemyController.SetTargetResolver(
                () => coopController.ResolveClosestPlayerTransform(transform.position),
                () => coopController.ResolveClosestPlayerHealth(transform.position));
        }

        private void HandleVisualKindChanged(int previousValue, int newValue)
        {
            ApplyVisualKind((RuntimeSpriteFactory.EnemyVisualKind)newValue);
        }

        private void HandleCurrentHealthChanged(float previousValue, float newValue)
        {
            RefreshHealthBar(newValue, _maxHealth.Value, newValue < previousValue - 0.001f);
        }

        private void HandleMaxHealthChanged(float previousValue, float newValue)
        {
            RefreshHealthBar(_currentHealth.Value, newValue, false);
        }

        private void HandleServerEnemyHealthChanged(float currentHealth, float maxHealth)
        {
            _currentHealth.Value = currentHealth;
            _maxHealth.Value = maxHealth;
            RefreshHealthBar(currentHealth, maxHealth, false);
        }

        private void EnsurePresentationObjects()
        {
            if (_visualRoot == null)
            {
                var existingVisual = transform.Find(VisualObjectName);
                if (existingVisual == null)
                {
                    existingVisual = new GameObject(VisualObjectName).transform;
                    existingVisual.SetParent(transform, false);
                }

                _visualRoot = existingVisual;
            }

            if (_spriteRenderer == null)
            {
                _spriteRenderer = _visualRoot.GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = _visualRoot.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            if (_spriteAnimator == null)
            {
                _spriteAnimator = _visualRoot.GetComponent<EnemySpriteAnimator>();
                if (_spriteAnimator == null)
                {
                    _spriteAnimator = _visualRoot.gameObject.AddComponent<EnemySpriteAnimator>();
                }
            }

            if (_healthBar == null)
            {
                _healthBar = GetComponent<WorldHealthBar>();
                if (_healthBar == null)
                {
                    _healthBar = gameObject.AddComponent<WorldHealthBar>();
                }
            }
        }

        private void ApplyVisualKind(RuntimeSpriteFactory.EnemyVisualKind visualKind)
        {
            EnsurePresentationObjects();

            var statProfile = _enemyConfig.GetStatProfile(visualKind);
            var animationProfile = _enemyConfig.GetAnimationProfile(visualKind);
            var enemyFrames = RuntimeSpriteFactory.GetEnemyAnimationFrames(visualKind);
            var baseSprite = enemyFrames.Length > 0 ? enemyFrames[0] : RuntimeSpriteFactory.GetSquareSprite();
            var scaleMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.visualScaleMultiplier) : 1f;
            var visualWorldSize = Mathf.Max(0.1f, _enemyConfig.visualScale * scaleMultiplier);

            _visualRoot.localPosition = new Vector3(0f, _enemyConfig.visualYOffset, 0f);
            _spriteRenderer.sprite = baseSprite;
            _spriteRenderer.color = Color.white;
            _spriteRenderer.sortingOrder = 15;
            ApplyVisualScale(_visualRoot, baseSprite, visualWorldSize);
            _spriteAnimator.Initialize(_spriteRenderer, enemyFrames, animationProfile);

            var healthBarYOffset = _enemyConfig.visualYOffset + Mathf.Max(0.28f, visualWorldSize * 0.36f);
            _healthBar.Initialize(
                new Vector3(0f, healthBarYOffset, 0f),
                0.82f,
                0.1f,
                new Color(1f, 0.3f, 0.35f, 0.95f),
                new Color(0f, 0f, 0f, 0.55f),
                24);
        }

        private void RefreshHealthBar(float currentHealth, float maxHealth, bool playHurt)
        {
            _healthBar?.SetHealth(currentHealth, maxHealth);

            if (playHurt && currentHealth > 0f)
            {
                _spriteAnimator?.PlayHurt();
            }
        }

        private float GetCollisionRadius(EnemyStatProfile statProfile)
        {
            var multiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.collisionRadiusMultiplier) : 1f;
            return Mathf.Max(0.05f, _enemyConfig.collisionRadius * multiplier);
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
    }
}
