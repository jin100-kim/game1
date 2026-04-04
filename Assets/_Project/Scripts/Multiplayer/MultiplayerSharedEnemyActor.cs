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
        public enum HudTargetKind
        {
            None = 0,
            Boss = 1,
            WaveTarget = 2,
        }

        private const string VisualObjectName = "Visual";
        private const float WaveTargetVisualScaleMultiplier = 2f;
        private static readonly System.Collections.Generic.List<MultiplayerSharedEnemyActor> ActiveActors = new();

        private readonly NetworkVariable<int> _visualKind =
            new((int)RuntimeSpriteFactory.EnemyVisualKind.Slime, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _variantId =
            new((int)EnemyVariantId.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _currentHealth =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _maxHealth =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _hudTargetKind =
            new((int)HudTargetKind.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _hudSpawnSequence =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _bossPullActive =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector2> _bossPullCenter =
            new(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _bossPullRadius =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _bossPullSpeed =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private EnemyController _enemyController;
        private EnemyConfig _enemyConfig;
        private Transform _visualRoot;
        private SpriteRenderer _spriteRenderer;
        private EnemySpriteAnimator _spriteAnimator;
        private WorldHealthBar _healthBar;
        private SpriteRenderer _waveTargetGlowRenderer;
        private Vector3 _lastPosition;

        public static System.Collections.Generic.IReadOnlyList<MultiplayerSharedEnemyActor> SpawnedActors => ActiveActors;
        public float CurrentHealthValue => _currentHealth.Value;
        public float MaxHealthValue => _maxHealth.Value;
        public RuntimeSpriteFactory.EnemyVisualKind VisualKindValue => (RuntimeSpriteFactory.EnemyVisualKind)_visualKind.Value;
        public EnemyVariantId VariantIdValue => (EnemyVariantId)_variantId.Value;
        public HudTargetKind CurrentHudTargetKind => (HudTargetKind)_hudTargetKind.Value;
        public int HudSpawnSequence => _hudSpawnSequence.Value;
        public bool IsHudBossTarget => CurrentHudTargetKind == HudTargetKind.Boss;
        public bool IsHudWaveTarget => CurrentHudTargetKind == HudTargetKind.WaveTarget;
        public string HudLabel => IsHudWaveTarget
            ? (VisualKindValue == RuntimeSpriteFactory.EnemyVisualKind.Mushroom ? "거대 버섯" : "거대 슬라임")
            : "보스";

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

        public static bool TryGetCurrentBossActor(out MultiplayerSharedEnemyActor actor)
        {
            actor = null;
            var bestSequence = int.MinValue;
            for (var i = 0; i < ActiveActors.Count; i++)
            {
                var candidate = ActiveActors[i];
                if (candidate == null || !candidate.IsSpawned || !candidate.IsHudBossTarget)
                {
                    continue;
                }

                if (candidate.HudSpawnSequence < bestSequence)
                {
                    continue;
                }

                bestSequence = candidate.HudSpawnSequence;
                actor = candidate;
            }

            return actor != null;
        }

        public bool TryGetBossPullState(out Vector2 center, out float radius, out float speed)
        {
            center = _bossPullCenter.Value;
            radius = _bossPullRadius.Value;
            speed = _bossPullSpeed.Value;
            return _bossPullActive.Value && radius > 0.0001f && speed > 0.0001f;
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
            _variantId.OnValueChanged += HandleVariantIdChanged;
            _currentHealth.OnValueChanged += HandleCurrentHealthChanged;
            _maxHealth.OnValueChanged += HandleMaxHealthChanged;
            _hudTargetKind.OnValueChanged += HandleHudTargetKindChanged;

            if (!ActiveActors.Contains(this))
            {
                ActiveActors.Add(this);
            }

            ApplyVisualKind((RuntimeSpriteFactory.EnemyVisualKind)_visualKind.Value);
            RefreshHealthBar(_currentHealth.Value, _maxHealth.Value, false);
            _lastPosition = transform.position;
        }

        public override void OnNetworkDespawn()
        {
            _visualKind.OnValueChanged -= HandleVisualKindChanged;
            _variantId.OnValueChanged -= HandleVariantIdChanged;
            _currentHealth.OnValueChanged -= HandleCurrentHealthChanged;
            _maxHealth.OnValueChanged -= HandleMaxHealthChanged;
            _hudTargetKind.OnValueChanged -= HandleHudTargetKindChanged;
            ActiveActors.Remove(this);
            ClearBossPullState();

            if (IsServer && _enemyController != null)
            {
                _enemyController.Changed -= HandleServerEnemyHealthChanged;
                _enemyController.BossProjectileSpawned -= HandleServerBossProjectileSpawned;
                _enemyController.BossSigilSpawned -= HandleServerBossSigilSpawned;
            }
        }

        private void SyncBossPullState()
        {
            if (_enemyController == null || !_enemyController.IsBoss)
            {
                ClearBossPullState();
                return;
            }

            if (!_enemyController.TryGetBossPullState(out var center, out var radius, out var speed))
            {
                ClearBossPullState();
                return;
            }

            _bossPullActive.Value = true;
            _bossPullCenter.Value = center;
            _bossPullRadius.Value = radius;
            _bossPullSpeed.Value = speed;
        }

        private void ClearBossPullState()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer)
            {
                return;
            }

            _bossPullActive.Value = false;
            _bossPullCenter.Value = Vector2.zero;
            _bossPullRadius.Value = 0f;
            _bossPullSpeed.Value = 0f;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                _lastPosition = transform.position;
                return;
            }

            if (IsServer)
            {
                SyncBossPullState();
            }

            if (IsServer || _spriteAnimator == null)
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
            float elapsedSeconds,
            bool isBoss,
            EnemyVariantId variantId = EnemyVariantId.None,
            BossArchetypeId bossArchetype = BossArchetypeId.Final,
            RunDifficultyDefinition bossDifficulty = null,
            System.Action<EnemyController, EnemyVariantDefinition> splitSpawnHandler = null,
            HudTargetKind hudTargetKind = HudTargetKind.None,
            int hudSpawnSequence = 0)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || coopController == null)
            {
                return;
            }

            EnsurePresentationObjects();
            transform.position = spawnPosition;

            _enemyController.Changed -= HandleServerEnemyHealthChanged;
            _enemyController.Changed += HandleServerEnemyHealthChanged;
            _enemyController.BossProjectileSpawned -= HandleServerBossProjectileSpawned;
            _enemyController.BossProjectileSpawned += HandleServerBossProjectileSpawned;
            _enemyController.BossSigilSpawned -= HandleServerBossSigilSpawned;
            _enemyController.BossSigilSpawned += HandleServerBossSigilSpawned;
            SharedRunCatalog.CopyEnemyConfig(_enemyConfig, coopController.CurrentEnemyConfig);

            var variantDefinition = !isBoss ? SharedEnemyVariantCatalog.Get(variantId) : null;
            var statProfile = variantDefinition != null
                ? SharedEnemyVariantCatalog.CreateVariantStatProfile(_enemyConfig, variantDefinition)
                : _enemyConfig.GetStatProfile(isBoss ? RuntimeSpriteFactory.EnemyVisualKind.Boss : visualKind);
            var collisionRadius = GetCollisionRadius(statProfile);
            var runtimeMinuteTier = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds / 60f));
            var runtimeMoveSpeedMultiplier = 1f + (runtimeMinuteTier * 0.05f);
            var runtimeHealthMultiplier = 1f + (runtimeMinuteTier * 0.10f);
            var runtimeContactDamageMultiplier = 1f + (Mathf.Min(runtimeMinuteTier, 10) * 0.10f);
            var initialTarget = coopController.ResolveClosestPlayerTransform(spawnPosition);
            var initialPlayerHealth = coopController.ResolveClosestPlayerHealth(spawnPosition);

            _visualKind.Value = (int)visualKind;
            _variantId.Value = (int)(variantDefinition?.Id ?? EnemyVariantId.None);
            _hudTargetKind.Value = (int)hudTargetKind;
            _hudSpawnSequence.Value = Mathf.Max(0, hudSpawnSequence);
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
                runtimeContactDamageMultiplier,
                isBoss,
                true,
                coopController.ArenaBounds,
                bossArchetype,
                bossDifficulty);

            if (variantDefinition != null)
            {
                _enemyController.ConfigureVariant(variantDefinition, splitSpawnHandler);
            }

            _enemyController.SetTargetResolver(
                () => coopController.ResolveClosestPlayerTransform(transform.position),
                () => coopController.ResolveClosestPlayerHealth(transform.position));
            _enemyController.SetExperienceOrbSpawner((position, value) => coopController.SpawnExperienceOrb(position, value));
        }

        private void HandleVisualKindChanged(int previousValue, int newValue)
        {
            ApplyVisualKind((RuntimeSpriteFactory.EnemyVisualKind)newValue);
        }

        private void HandleVariantIdChanged(int previousValue, int newValue)
        {
            ApplyVisualKind((RuntimeSpriteFactory.EnemyVisualKind)_visualKind.Value);
        }

        private void HandleHudTargetKindChanged(int previousValue, int newValue)
        {
            ApplyVisualKind((RuntimeSpriteFactory.EnemyVisualKind)_visualKind.Value);
        }

        private void HandleCurrentHealthChanged(float previousValue, float newValue)
        {
            if (!IsServer && newValue < previousValue - 0.001f)
            {
                var popupPosition = transform.position + new Vector3(0f, 0.8f, 0f);
                CombatTextSpawner.SpawnDamage(
                    popupPosition,
                    previousValue - newValue,
                    CombatTextSpawner.EnemyDamagedColor);
            }

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

        private void HandleServerBossProjectileSpawned(Vector3 spawnPosition, Vector2 direction, float speed, float lifetime, float visualScale)
        {
            if (!IsServer)
            {
                return;
            }

            SpawnBossProjectileVisualClientRpc(spawnPosition, direction, speed, lifetime, visualScale);
        }

        private void HandleServerBossSigilSpawned(Vector3 spawnPosition, float delay, float radius)
        {
            if (!IsServer)
            {
                return;
            }

            SpawnBossSigilVisualClientRpc(spawnPosition, delay, radius);
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
            var variantDefinition = SharedEnemyVariantCatalog.Get((EnemyVariantId)_variantId.Value);
            if (variantDefinition != null)
            {
                statProfile = SharedEnemyVariantCatalog.CreateVariantStatProfile(_enemyConfig, variantDefinition);
            }

            var animationProfile = _enemyConfig.GetAnimationProfile(visualKind);
            var enemyFrames = RuntimeSpriteFactory.GetEnemyAnimationFrames(visualKind);
            var baseSprite = enemyFrames.Length > 0 ? enemyFrames[0] : RuntimeSpriteFactory.GetSquareSprite();
            var scaleMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.visualScaleMultiplier) : 1f;
            if (IsHudWaveTarget)
            {
                scaleMultiplier *= WaveTargetVisualScaleMultiplier;
            }
            var visualWorldSize = Mathf.Max(0.1f, _enemyConfig.visualScale * scaleMultiplier);

            _visualRoot.localPosition = new Vector3(0f, _enemyConfig.visualYOffset, 0f);
            _spriteRenderer.sprite = baseSprite;
            var baseColor = variantDefinition != null ? variantDefinition.TintColor : Color.white;
            _spriteRenderer.color = baseColor;
            _spriteRenderer.sortingOrder = 15;
            ApplyVisualScale(_visualRoot, baseSprite, visualWorldSize);
            _spriteAnimator.Initialize(_spriteRenderer, enemyFrames, animationProfile);
            _spriteAnimator.SetBaseColor(baseColor);
            RefreshWaveTargetGlow(visualWorldSize);

            var healthBarYOffset = _enemyConfig.visualYOffset + Mathf.Max(0.28f, visualWorldSize * 0.36f);
            _healthBar.Initialize(
                new Vector3(0f, healthBarYOffset, 0f),
                0.82f,
                0.1f,
                new Color(1f, 0.3f, 0.35f, 0.95f),
                new Color(0f, 0f, 0f, 0.55f),
                24);
        }

        private void RefreshWaveTargetGlow(float visualWorldSize)
        {
            if (_visualRoot == null)
            {
                return;
            }

            if (_waveTargetGlowRenderer == null)
            {
                var glowTransform = _visualRoot.Find("WaveTargetGlow");
                if (glowTransform == null)
                {
                    glowTransform = new GameObject("WaveTargetGlow").transform;
                    glowTransform.SetParent(_visualRoot, false);
                }

                _waveTargetGlowRenderer = glowTransform.GetComponent<SpriteRenderer>();
                if (_waveTargetGlowRenderer == null)
                {
                    _waveTargetGlowRenderer = glowTransform.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            var glowObject = _waveTargetGlowRenderer.gameObject;
            if (!IsHudWaveTarget)
            {
                glowObject.SetActive(false);
                return;
            }

            glowObject.SetActive(false);
        }

        private void RefreshHealthBar(float currentHealth, float maxHealth, bool playHurt)
        {
            _healthBar?.SetHealth(currentHealth, maxHealth);

            if (playHurt && currentHealth > 0f)
            {
                _spriteAnimator?.PlayHurt();
            }
        }

        [ClientRpc]
        private void SpawnBossProjectileVisualClientRpc(Vector3 spawnPosition, Vector2 direction, float speed, float lifetime, float visualScale)
        {
            if (IsServer)
            {
                return;
            }

            var projectileObject = new GameObject("BossProjectileVisual");
            projectileObject.transform.position = spawnPosition;

            var renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = new Color(1f, 0.32f, 0.24f, 1f);
            renderer.sortingOrder = 38;
            projectileObject.transform.localScale = Vector3.one * Mathf.Max(0.05f, visualScale);

            var projectile = projectileObject.AddComponent<BossProjectile>();
            projectile.Initialize(
                direction,
                speed,
                lifetime,
                0f,
                0.14f,
                null,
                0.05f);
        }

        [ClientRpc]
        private void SpawnBossSigilVisualClientRpc(Vector3 spawnPosition, float delay, float radius)
        {
            if (IsServer)
            {
                return;
            }

            var sigilObject = new GameObject("BossSigilVisual");
            sigilObject.transform.position = spawnPosition;
            var sigil = sigilObject.AddComponent<BossSigilHazard>();
            sigil.Initialize(null, 0.05f, delay, radius, 0f, visualOnly: true);
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
