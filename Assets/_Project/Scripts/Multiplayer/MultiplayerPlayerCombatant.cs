using EJR.Game.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    [RequireComponent(typeof(PlayerMover))]
    [RequireComponent(typeof(PlayerSpriteAnimator))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MultiplayerPlayerCombatant : NetworkBehaviour
    {
        private readonly NetworkVariable<float> _currentHealth =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _maxHealth =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _alive =
            new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private PlayerHealth _playerHealth;
        private PlayerMover _playerMover;
        private PlayerSpriteAnimator _playerSpriteAnimator;
        private SpriteRenderer _spriteRenderer;
        private WorldHealthBar _healthBar;
        private PlayerConfig _playerConfig;
        private WeaponConfig _weaponConfig;
        private float _nextAttackAt;

        public PlayerHealth ServerPlayerHealth => _playerHealth;
        public bool IsAlive => _alive.Value;

        public static int CountAlivePlayers()
        {
            var players = FindObjectsByType<MultiplayerPlayerCombatant>(FindObjectsSortMode.None);
            var count = 0;
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player != null && player.IsSpawned && player.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }

        private void Awake()
        {
            _playerHealth = GetComponent<PlayerHealth>();
            _playerMover = GetComponent<PlayerMover>();
            _playerSpriteAnimator = GetComponent<PlayerSpriteAnimator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _healthBar = GetComponent<WorldHealthBar>();
            if (_healthBar == null)
            {
                _healthBar = gameObject.AddComponent<WorldHealthBar>();
            }

            _playerConfig = ScriptableObject.CreateInstance<PlayerConfig>();
            _weaponConfig = ScriptableObject.CreateInstance<WeaponConfig>();

            _healthBar.Initialize(
                new Vector3(0f, 0.82f, 0f),
                1.15f,
                0.14f,
                new Color(0.25f, 0.95f, 0.4f, 0.95f),
                new Color(0f, 0f, 0f, 0.55f),
                25);
        }

        public override void OnNetworkSpawn()
        {
            _currentHealth.OnValueChanged += HandleCurrentHealthChanged;
            _maxHealth.OnValueChanged += HandleMaxHealthChanged;
            _alive.OnValueChanged += HandleAliveChanged;

            if (IsServer)
            {
                _playerHealth.Changed += HandleServerHealthChanged;
                _playerHealth.Died += HandleServerDied;
                _playerHealth.Initialize(_playerConfig.maxHealth, _playerConfig.damageInvulnerabilitySeconds);
                SyncServerHealth();
            }
            else
            {
                ApplyHealthPresentation(_currentHealth.Value, Mathf.Max(1f, _maxHealth.Value), false);
                ApplyAlivePresentation(_alive.Value, false);
            }
        }

        public override void OnNetworkDespawn()
        {
            _currentHealth.OnValueChanged -= HandleCurrentHealthChanged;
            _maxHealth.OnValueChanged -= HandleMaxHealthChanged;
            _alive.OnValueChanged -= HandleAliveChanged;

            if (IsServer)
            {
                _playerHealth.Changed -= HandleServerHealthChanged;
                _playerHealth.Died -= HandleServerDied;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !_alive.Value)
            {
                return;
            }

            if (Time.time < _nextAttackAt)
            {
                return;
            }

            var coop = MultiplayerCoopController.Instance;
            if (coop == null)
            {
                return;
            }

            if (!coop.TryFindNearestEnemy(transform.position, Mathf.Max(0.5f, _weaponConfig.rifleRange), out var enemy))
            {
                return;
            }

            var toEnemy = enemy.transform.position - transform.position;
            if (toEnemy.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var direction = ((Vector2)toEnemy).normalized;
            var origin = transform.position + new Vector3(direction.x, direction.y, 0f) * 0.45f;
            if (coop.SpawnPlayerProjectile(origin, direction, OwnerClientId))
            {
                _nextAttackAt = Time.time + Mathf.Max(0.05f, _weaponConfig.rifleAttackInterval);
            }
        }

        public void ApplyServerDamage(float damage)
        {
            if (!IsServer || !_alive.Value)
            {
                return;
            }

            _playerHealth.TakeDamage(damage);
        }

        private void HandleServerHealthChanged(float currentHealth, float maxHealth)
        {
            _currentHealth.Value = currentHealth;
            _maxHealth.Value = maxHealth;
            _alive.Value = currentHealth > 0.0001f;
            ApplyHealthPresentation(currentHealth, maxHealth, false);
            ApplyAlivePresentation(_alive.Value, false);
        }

        private void HandleServerDied()
        {
            _alive.Value = false;
            ApplyAlivePresentation(false, true);
        }

        private void HandleCurrentHealthChanged(float previousValue, float newValue)
        {
            ApplyHealthPresentation(newValue, Mathf.Max(1f, _maxHealth.Value), newValue < previousValue - 0.001f);
        }

        private void HandleMaxHealthChanged(float previousValue, float newValue)
        {
            ApplyHealthPresentation(_currentHealth.Value, Mathf.Max(1f, newValue), false);
        }

        private void HandleAliveChanged(bool previousValue, bool newValue)
        {
            ApplyAlivePresentation(newValue, previousValue && !newValue);
        }

        private void SyncServerHealth()
        {
            HandleServerHealthChanged(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
        }

        private void ApplyHealthPresentation(float currentHealth, float maxHealth, bool playHurt)
        {
            _healthBar?.SetHealth(currentHealth, maxHealth);

            if (playHurt && currentHealth > 0f)
            {
                _playerSpriteAnimator?.PlayHurt();
            }
        }

        private void ApplyAlivePresentation(bool isAlive, bool playDie)
        {
            if (_playerMover != null)
            {
                _playerMover.enabled = IsOwner && isAlive;
            }

            if (_spriteRenderer != null)
            {
                var color = _spriteRenderer.color;
                color.a = isAlive ? 1f : 0.3f;
                _spriteRenderer.color = color;
            }

            if (playDie)
            {
                _playerSpriteAnimator?.PlayDie();
            }
        }
    }
}
