using System;
using System.Text;
using EJR.Game.Core;
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

        private readonly NetworkVariable<bool> _isReady =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _selectedCharacterId =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _selectedStarterWeaponId =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _selectionComplete =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isDowned =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _reviveProgress =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _moveSpeedMultiplier =
            new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector2> _aimDirection =
            new(Vector2.right, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector2> _fireDirection =
            new(Vector2.right, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _fireSequence =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _showGunWeapon =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _droneVisualCount =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _droneOrbitRadius =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _droneOrbitSpeedDegrees =
            new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private PlayerHealth _playerHealth;
        private PlayerMover _playerMover;
        private PlayerSpriteAnimator _playerSpriteAnimator;
        private SpriteRenderer _spriteRenderer;
        private MultiplayerPlayerActor _playerActor;
        private WorldHealthBar _healthBar;
        private PlayerConfig _playerConfig;
        private WeaponConfig _weaponConfig;
        private AutoWeaponSystem _weaponSystem;
        private PlayerBuildRuntime _buildRuntime;
        private PlayerStatsRuntime _playerStats;
        private LevelUpSystem _levelUp;
        private LevelUpOption[] _serverPendingOptions = Array.Empty<LevelUpOption>();
        private string[] _localPendingLabels = Array.Empty<string>();
        private string _localPendingTitle = string.Empty;
        private string _localWeaponSummary = "Weapons";
        private string _localStatSummary = "Stats";
        private bool _serverChoiceSubmitted;
        private bool _serverInitialized;

        public PlayerHealth ServerPlayerHealth => _playerHealth;
        public bool IsReady => _isReady.Value;
        public bool SelectionComplete => _selectionComplete.Value;
        public bool IsDowned => _isDowned.Value;
        public float ReviveProgress => _reviveProgress.Value;
        public float CurrentHealth => _currentHealth.Value;
        public float MaxHealth => _maxHealth.Value;
        public int SelectedCharacterId => _selectedCharacterId.Value;
        public int SelectedStarterWeaponIndex => _selectedStarterWeaponId.Value;
        public string WeaponSummary => _localWeaponSummary;
        public string StatSummary => _localStatSummary;
        public bool HasLocalPendingChoice => _localPendingLabels.Length > 0;
        public string LocalPendingTitle => _localPendingTitle;
        public int LocalPendingChoiceCount => _localPendingLabels.Length;
        public bool IsAlive => IsSpawned && !_isDowned.Value && _currentHealth.Value > 0.001f;
        public bool IsTargetable => IsSpawned && !_isDowned.Value && _currentHealth.Value > 0.001f;
        public bool HasPendingServerChoice => _levelUp != null && _levelUp.IsAwaitingChoice;
        public bool HasSubmittedServerChoice => _serverChoiceSubmitted;
        public string DisplayName => MultiplayerCatalog.GetPlayerDisplayName(OwnerClientId, _selectedCharacterId.Value);

        public static MultiplayerPlayerCombatant FindOwnedLocalPlayer()
        {
            var players = FindObjectsByType<MultiplayerPlayerCombatant>(FindObjectsSortMode.None);
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player != null && player.IsSpawned && player.IsOwner)
                {
                    return player;
                }
            }

            return null;
        }

        private void Awake()
        {
            _playerHealth = GetComponent<PlayerHealth>();
            _playerMover = GetComponent<PlayerMover>();
            _playerSpriteAnimator = GetComponent<PlayerSpriteAnimator>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _playerActor = GetComponent<MultiplayerPlayerActor>();
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

            ApplyCharacterPresentation(_selectedCharacterId.Value, _isDowned.Value);
            RefreshMoverState();
        }

        public override void OnNetworkSpawn()
        {
            _currentHealth.OnValueChanged += HandleCurrentHealthChanged;
            _maxHealth.OnValueChanged += HandleMaxHealthChanged;
            _selectedCharacterId.OnValueChanged += HandleSelectedCharacterChanged;
            _isDowned.OnValueChanged += HandleDownedChanged;
            _reviveProgress.OnValueChanged += HandleReviveProgressChanged;
            _moveSpeedMultiplier.OnValueChanged += HandleMoveSpeedMultiplierChanged;
            _aimDirection.OnValueChanged += HandleAimDirectionChanged;
            _fireDirection.OnValueChanged += HandleFireDirectionChanged;
            _fireSequence.OnValueChanged += HandleFireSequenceChanged;
            _showGunWeapon.OnValueChanged += HandleShowGunWeaponChanged;
            _droneVisualCount.OnValueChanged += HandleDroneVisualStateChanged;
            _droneOrbitRadius.OnValueChanged += HandleDroneVisualStateChanged;
            _droneOrbitSpeedDegrees.OnValueChanged += HandleDroneVisualStateChanged;

            ApplyHealthPresentation(_currentHealth.Value, Mathf.Max(1f, _maxHealth.Value), false);
            ApplyCharacterPresentation(_selectedCharacterId.Value, _isDowned.Value);
            _playerMover.SetMoveSpeedMultiplier(_moveSpeedMultiplier.Value);
            ApplyWeaponPresentation();
            ApplyDronePresentation();
            ApplyRevivePresentation();
            RefreshMoverState();

            if (IsServer)
            {
                InitializeServerState();
            }
        }

        public override void OnNetworkDespawn()
        {
            _currentHealth.OnValueChanged -= HandleCurrentHealthChanged;
            _maxHealth.OnValueChanged -= HandleMaxHealthChanged;
            _selectedCharacterId.OnValueChanged -= HandleSelectedCharacterChanged;
            _isDowned.OnValueChanged -= HandleDownedChanged;
            _reviveProgress.OnValueChanged -= HandleReviveProgressChanged;
            _moveSpeedMultiplier.OnValueChanged -= HandleMoveSpeedMultiplierChanged;
            _aimDirection.OnValueChanged -= HandleAimDirectionChanged;
            _fireDirection.OnValueChanged -= HandleFireDirectionChanged;
            _fireSequence.OnValueChanged -= HandleFireSequenceChanged;
            _showGunWeapon.OnValueChanged -= HandleShowGunWeaponChanged;
            _droneVisualCount.OnValueChanged -= HandleDroneVisualStateChanged;
            _droneOrbitRadius.OnValueChanged -= HandleDroneVisualStateChanged;
            _droneOrbitSpeedDegrees.OnValueChanged -= HandleDroneVisualStateChanged;

            if (IsServer)
            {
                UnhookServerRuntime();
            }
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            RefreshMoverState();

            if (!IsServer)
            {
                return;
            }

            var coop = MultiplayerCoopController.Instance;
            var canSimulateCombat = coop != null && coop.Phase == MultiplayerRunPhase.Running && !_isDowned.Value;
            if (_weaponSystem != null)
            {
                _weaponSystem.enabled = canSimulateCombat;
            }

            if (!canSimulateCombat)
            {
                return;
            }

            if (_playerStats != null && _playerStats.HealthRegenPerSecond > 0f)
            {
                _playerHealth.Heal(_playerStats.HealthRegenPerSecond * Time.deltaTime);
            }
        }

        public string GetLocalPendingChoiceLabel(int index)
        {
            if (index < 0 || index >= _localPendingLabels.Length)
            {
                return string.Empty;
            }

            return _localPendingLabels[index];
        }

        public void RequestNextCharacterSelection()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            SetLobbyCharacterServerRpc(_selectedCharacterId.Value + 1);
        }

        public void RequestNextStarterWeaponSelection()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            SetLobbyStarterWeaponServerRpc(_selectedStarterWeaponId.Value + 1);
        }

        public void RequestToggleReady()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            SetReadyServerRpc(!_isReady.Value);
        }

        public void SubmitLevelChoice(int optionIndex)
        {
            if (!IsOwner || !IsSpawned || _localPendingLabels.Length <= 0)
            {
                return;
            }

            SubmitLevelChoiceServerRpc(optionIndex);
        }

        public void ServerPrepareForRun(Vector3 spawnPosition, Rect arenaBounds)
        {
            if (!IsServer)
            {
                return;
            }

            InitializeServerState();
            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;
            ResetBuildRuntimeForRun();
            _playerMover.Initialize(_playerConfig, _playerStats, arenaBounds);
            _playerHealth.Initialize(GetCurrentMaxHealth(), _playerConfig.damageInvulnerabilitySeconds);
            _playerHealth.GrantInvulnerability(0.75f);
            _isDowned.Value = false;
            _reviveProgress.Value = 0f;
            _serverChoiceSubmitted = false;
            _weaponSystem?.ClearActiveProjectiles();
            ResetSpecialPresentationClientRpc();
            EnsureWeaponSystem(arenaBounds);
            ApplyBuildToRuntimeSystems();
            ClearPendingChoiceClientRpc(BuildOwnerClientRpcParams());
        }

        public void ServerResetToLobby(Vector3 spawnPosition, Rect arenaBounds)
        {
            if (!IsServer)
            {
                return;
            }

            InitializeServerState();
            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;
            _isReady.Value = false;
            _selectionComplete.Value = true;
            _isDowned.Value = false;
            _reviveProgress.Value = 0f;
            _serverChoiceSubmitted = false;
            ResetBuildRuntimeForLobby();
            _playerMover.Initialize(_playerConfig, _playerStats, arenaBounds);
            _playerHealth.Initialize(_playerConfig.maxHealth, _playerConfig.damageInvulnerabilitySeconds);

            if (_weaponSystem != null)
            {
                _weaponSystem.ClearActiveProjectiles();
                _weaponSystem.enabled = false;
                _weaponSystem.ConfigureLoadout(_buildRuntime, _playerStats);
            }

            UpdateBuildSummaries();
            ResetSpecialPresentationClientRpc();
            ClearPendingChoiceClientRpc(BuildOwnerClientRpcParams());
        }

        public void ServerAddSharedExperience(int amount)
        {
            if (!IsServer || amount <= 0)
            {
                return;
            }

            InitializeServerState();
            _levelUp.AddExperience(amount);
        }

        public void SetReviveProgressServer(float normalizedProgress)
        {
            if (!IsServer)
            {
                return;
            }

            _reviveProgress.Value = Mathf.Clamp01(normalizedProgress);
        }

        public void CompleteReviveServer(float restoredHealthFraction, float invulnerabilitySeconds)
        {
            if (!IsServer || !_isDowned.Value)
            {
                return;
            }

            var restoredHealth = Mathf.Max(1f, GetCurrentMaxHealth() * Mathf.Clamp01(restoredHealthFraction));
            _playerHealth.Restore(restoredHealth, GetCurrentMaxHealth());
            _playerHealth.GrantInvulnerability(invulnerabilitySeconds);
            _isDowned.Value = false;
            _reviveProgress.Value = 0f;
            SyncHealthState();
        }

        public void ResetReviveProgressServer()
        {
            if (!IsServer)
            {
                return;
            }

            if (_reviveProgress.Value > 0.0001f)
            {
                _reviveProgress.Value = 0f;
            }
        }

        private void InitializeServerState()
        {
            if (_serverInitialized)
            {
                return;
            }

            _buildRuntime = new PlayerBuildRuntime();
            _playerStats = new PlayerStatsRuntime();
            RecreateLevelSystem();

            _playerHealth.Changed += HandleServerHealthChanged;
            _playerHealth.Died += HandleServerDied;

            _selectedCharacterId.Value = MultiplayerCatalog.NormalizeCharacterId((int)(OwnerClientId % (ulong)Mathf.Max(1, MultiplayerCatalog.CharacterCount)));
            _selectedStarterWeaponId.Value = 0;
            _selectionComplete.Value = true;
            _isReady.Value = false;
            _isDowned.Value = false;
            _reviveProgress.Value = 0f;
            _droneVisualCount.Value = 0;
            _droneOrbitRadius.Value = 0f;
            _droneOrbitSpeedDegrees.Value = 0f;
            ResetBuildRuntimeForLobby();
            _playerHealth.Initialize(_playerConfig.maxHealth, _playerConfig.damageInvulnerabilitySeconds);
            SyncHealthState();

            _serverInitialized = true;
        }

        private void RecreateLevelSystem()
        {
            if (_levelUp != null)
            {
                _levelUp.OptionsGenerated -= HandleServerOptionsGenerated;
            }

            _levelUp = new LevelUpSystem();
            _levelUp.Initialize(_buildRuntime);
            _levelUp.OptionsGenerated += HandleServerOptionsGenerated;
        }

        private void UnhookServerRuntime()
        {
            if (_levelUp != null)
            {
                _levelUp.OptionsGenerated -= HandleServerOptionsGenerated;
            }

            if (_weaponSystem != null)
            {
                _weaponSystem.AimUpdated -= HandleServerAimUpdated;
                _weaponSystem.Fired -= HandleServerWeaponFired;
                _weaponSystem.ProjectileVisualRequested -= HandleServerProjectileVisualRequested;
                _weaponSystem.KatanaSlashFxRequested -= HandleServerKatanaSlashFxRequested;
                _weaponSystem.ChainFxRequested -= HandleServerChainFxRequested;
                _weaponSystem.AuraPulseFxRequested -= HandleServerAuraPulseFxRequested;
                _weaponSystem.SatelliteHitFxRequested -= HandleServerSatelliteHitFxRequested;
                _weaponSystem.SatelliteBeamFxRequested -= HandleServerSatelliteBeamFxRequested;
                _weaponSystem.TurretDeployed -= HandleServerTurretDeployed;
                _weaponSystem.TurretTracerFxRequested -= HandleServerTurretTracerFxRequested;
            }

            if (_playerHealth != null)
            {
                _playerHealth.Changed -= HandleServerHealthChanged;
                _playerHealth.Died -= HandleServerDied;
            }
        }

        private void ResetBuildRuntimeForLobby()
        {
            _buildRuntime.InitializeDefaults(grantStarterRifle: false);
            _playerStats.RecalculateFromBuild(_buildRuntime);
            RecreateLevelSystem();
            UpdateBuildSummaries();
        }

        private void ResetBuildRuntimeForRun()
        {
            _buildRuntime.InitializeDefaults(grantStarterRifle: false);
            var starterWeapon = MultiplayerCatalog.GetStarterWeaponByIndex(_selectedStarterWeaponId.Value);
            _buildRuntime.Apply(new LevelUpOption(
                UpgradeCategory.Weapon,
                starterWeapon,
                default,
                0,
                1,
                isNewAcquire: true,
                isLockedBySlot: false,
                label: MultiplayerCatalog.GetWeaponDisplayName(starterWeapon)));

            _playerStats.RecalculateFromBuild(_buildRuntime);
            RecreateLevelSystem();
            ApplyBuildToRuntimeSystems();
            UpdateBuildSummaries();
        }

        private void EnsureWeaponSystem(Rect arenaBounds)
        {
            if (_weaponSystem == null)
            {
                _weaponSystem = GetComponent<AutoWeaponSystem>();
                if (_weaponSystem == null)
                {
                    _weaponSystem = gameObject.AddComponent<AutoWeaponSystem>();
                }
            }

            var coop = MultiplayerCoopController.Instance;
            _weaponSystem.Initialize(
                _weaponConfig,
                transform,
                coop != null ? coop.EnemyRegistry : null,
                _playerStats,
                projectileSpawnResolver: ResolveProjectileSpawnPoint,
                projectileCullBounds: arenaBounds);

            _weaponSystem.AimUpdated -= HandleServerAimUpdated;
            _weaponSystem.Fired -= HandleServerWeaponFired;
            _weaponSystem.ProjectileVisualRequested -= HandleServerProjectileVisualRequested;
            _weaponSystem.KatanaSlashFxRequested -= HandleServerKatanaSlashFxRequested;
            _weaponSystem.ChainFxRequested -= HandleServerChainFxRequested;
            _weaponSystem.AuraPulseFxRequested -= HandleServerAuraPulseFxRequested;
            _weaponSystem.SatelliteHitFxRequested -= HandleServerSatelliteHitFxRequested;
            _weaponSystem.SatelliteBeamFxRequested -= HandleServerSatelliteBeamFxRequested;
            _weaponSystem.TurretDeployed -= HandleServerTurretDeployed;
            _weaponSystem.TurretTracerFxRequested -= HandleServerTurretTracerFxRequested;
            _weaponSystem.AimUpdated += HandleServerAimUpdated;
            _weaponSystem.Fired += HandleServerWeaponFired;
            _weaponSystem.ProjectileVisualRequested += HandleServerProjectileVisualRequested;
            _weaponSystem.KatanaSlashFxRequested += HandleServerKatanaSlashFxRequested;
            _weaponSystem.ChainFxRequested += HandleServerChainFxRequested;
            _weaponSystem.AuraPulseFxRequested += HandleServerAuraPulseFxRequested;
            _weaponSystem.SatelliteHitFxRequested += HandleServerSatelliteHitFxRequested;
            _weaponSystem.SatelliteBeamFxRequested += HandleServerSatelliteBeamFxRequested;
            _weaponSystem.TurretDeployed += HandleServerTurretDeployed;
            _weaponSystem.TurretTracerFxRequested += HandleServerTurretTracerFxRequested;
        }

        private Vector3 ResolveProjectileSpawnPoint(Vector2 aimDirection)
        {
            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            if (_playerActor != null)
            {
                return _playerActor.ResolveProjectileSpawnPoint(aimDirection);
            }

            return transform.position;
        }

        private void ApplyBuildToRuntimeSystems()
        {
            _playerStats.RecalculateFromBuild(_buildRuntime);

            if (_weaponSystem != null)
            {
                _weaponSystem.ConfigureLoadout(_buildRuntime, _playerStats);
            }

            if (_playerHealth != null)
            {
                _playerHealth.SetMaxHealth(GetCurrentMaxHealth(), healDelta: true);
            }

            _moveSpeedMultiplier.Value = _playerStats != null ? _playerStats.MoveSpeedMultiplier : 1f;
            _playerMover.SetMoveSpeedMultiplier(_moveSpeedMultiplier.Value);
            _showGunWeapon.Value = HasAnyGunWeapon(_buildRuntime);
            _droneVisualCount.Value = GetDroneVisualCount(_buildRuntime);
            _droneOrbitRadius.Value = GetDroneOrbitRadius(_buildRuntime, _playerStats);
            _droneOrbitSpeedDegrees.Value = GetDroneOrbitSpeedDegrees(_buildRuntime, _playerStats);
            UpdateBuildSummaries();
            SyncHealthState();
        }

        private float GetCurrentMaxHealth()
        {
            var baseMaxHealth = Mathf.Max(1f, _playerConfig != null ? _playerConfig.maxHealth : 100f);
            var bonus = _playerStats != null ? Mathf.Max(0f, _playerStats.MaxHealthBonus) : 0f;
            return baseMaxHealth + bonus;
        }

        private void UpdateBuildSummaries()
        {
            var weaponSummary = BuildWeaponSummary();
            var statSummary = BuildStatSummary();
            _localWeaponSummary = weaponSummary;
            _localStatSummary = statSummary;

            if (IsServer)
            {
                UpdateBuildSummaryClientRpc(weaponSummary, statSummary, BuildOwnerClientRpcParams());
            }
        }

        private string BuildWeaponSummary()
        {
            var builder = new StringBuilder("Weapons");
            var playerLevel = _levelUp != null ? _levelUp.Level : 1;
            var unlockedSlots = _buildRuntime != null ? _buildRuntime.GetUnlockedWeaponSlots(playerLevel) : 1;

            for (var slotIndex = 0; slotIndex < PlayerBuildRuntime.MaxWeaponSlotsAbsolute; slotIndex++)
            {
                var slotNumber = slotIndex + 1;
                if (slotIndex >= unlockedSlots)
                {
                    var requiredLevel = slotIndex == 1
                        ? PlayerBuildRuntime.SecondWeaponUnlockLevel
                        : PlayerBuildRuntime.ThirdWeaponUnlockLevel;
                    builder.Append('\n').Append(slotNumber).Append(") Locked (Lv").Append(requiredLevel).Append(')');
                    continue;
                }

                if (_buildRuntime != null && slotIndex < _buildRuntime.OwnedWeapons.Count)
                {
                    var weaponId = _buildRuntime.OwnedWeapons[slotIndex];
                    var level = _buildRuntime.GetWeaponLevel(weaponId);
                    var coreLevel = _buildRuntime.GetWeaponCoreLevel(weaponId);
                    builder.Append('\n').Append(slotNumber).Append(") ")
                        .Append(MultiplayerCatalog.GetWeaponDisplayName(weaponId))
                        .Append(" Lv").Append(level);

                    if (coreLevel > 0)
                    {
                        builder.Append(" [")
                            .Append(MultiplayerCatalog.GetCoreDisplayName(_buildRuntime.GetWeaponCoreElement(weaponId)))
                            .Append(" C")
                            .Append(coreLevel)
                            .Append(']');
                    }
                }
                else
                {
                    builder.Append('\n').Append(slotNumber).Append(") Empty");
                }
            }

            return builder.ToString();
        }

        private string BuildStatSummary()
        {
            var builder = new StringBuilder("Stats");
            for (var slotIndex = 0; slotIndex < PlayerBuildRuntime.MaxStatSlots; slotIndex++)
            {
                var slotNumber = slotIndex + 1;
                if (_buildRuntime != null && slotIndex < _buildRuntime.OwnedStats.Count)
                {
                    var statId = _buildRuntime.OwnedStats[slotIndex];
                    builder.Append('\n')
                        .Append(slotNumber)
                        .Append(") ")
                        .Append(MultiplayerCatalog.GetStatDisplayName(statId))
                        .Append(" Lv")
                        .Append(_buildRuntime.GetStatLevel(statId));
                }
                else
                {
                    builder.Append('\n').Append(slotNumber).Append(") Empty");
                }
            }

            return builder.ToString();
        }

        private void SyncHealthState()
        {
            _currentHealth.Value = _playerHealth.CurrentHealth;
            _maxHealth.Value = _playerHealth.MaxHealth;
            ApplyHealthPresentation(_currentHealth.Value, Mathf.Max(1f, _maxHealth.Value), false);
        }

        private void HandleServerHealthChanged(float currentHealth, float maxHealth)
        {
            _currentHealth.Value = currentHealth;
            _maxHealth.Value = maxHealth;
            ApplyHealthPresentation(currentHealth, maxHealth, false);
        }

        private void HandleServerDied()
        {
            if (_isDowned.Value)
            {
                return;
            }

            _isDowned.Value = true;
            _reviveProgress.Value = 0f;
            _serverChoiceSubmitted = false;
            SyncHealthState();
        }

        private void HandleServerOptionsGenerated(LevelUpOption[] options)
        {
            if (!IsServer || options == null || options.Length <= 0)
            {
                return;
            }

            _serverPendingOptions = options;
            _serverChoiceSubmitted = false;

            var optionCount = Mathf.Min(options.Length, 3);
            var option0 = optionCount > 0 ? options[0].Label : string.Empty;
            var option1 = optionCount > 1 ? options[1].Label : string.Empty;
            var option2 = optionCount > 2 ? options[2].Label : string.Empty;
            ShowLevelChoiceClientRpc("Level Up - Choose One", optionCount, option0, option1, option2, BuildOwnerClientRpcParams());

            MultiplayerCoopController.Instance?.EnterLevelChoicePauseIfNeeded();
        }

        private void HandleCurrentHealthChanged(float previousValue, float newValue)
        {
            if (!IsServer && newValue < previousValue - 0.001f)
            {
                CombatTextSpawner.SpawnDamage(
                    transform.position + new Vector3(0f, 0.9f, 0f),
                    previousValue - newValue,
                    CombatTextSpawner.PlayerDamagedColor);
            }

            ApplyHealthPresentation(newValue, Mathf.Max(1f, _maxHealth.Value), newValue < previousValue - 0.001f);
        }

        private void HandleMaxHealthChanged(float previousValue, float newValue)
        {
            ApplyHealthPresentation(_currentHealth.Value, Mathf.Max(1f, newValue), false);
        }

        private void HandleSelectedCharacterChanged(int previousValue, int newValue)
        {
            ApplyCharacterPresentation(newValue, _isDowned.Value);
        }

        private void HandleMoveSpeedMultiplierChanged(float previousValue, float newValue)
        {
            _playerMover?.SetMoveSpeedMultiplier(newValue);
        }

        private void HandleReviveProgressChanged(float previousValue, float newValue)
        {
            ApplyRevivePresentation();
        }

        private void HandleAimDirectionChanged(Vector2 previousValue, Vector2 newValue)
        {
            _playerActor?.SetWeaponAim(newValue);
        }

        private void HandleFireDirectionChanged(Vector2 previousValue, Vector2 newValue)
        {
            _playerActor?.SetWeaponAim(newValue);
        }

        private void HandleFireSequenceChanged(int previousValue, int newValue)
        {
            if (newValue == previousValue)
            {
                return;
            }

            _playerActor?.PlayWeaponAttack(_fireDirection.Value);
        }

        private void HandleShowGunWeaponChanged(bool previousValue, bool newValue)
        {
            ApplyWeaponPresentation();
        }

        private void HandleDroneVisualStateChanged(int previousValue, int newValue)
        {
            ApplyDronePresentation();
        }

        private void HandleDroneVisualStateChanged(float previousValue, float newValue)
        {
            ApplyDronePresentation();
        }

        private void HandleDownedChanged(bool previousValue, bool newValue)
        {
            ApplyCharacterPresentation(_selectedCharacterId.Value, newValue);
            ApplyWeaponPresentation();
            ApplyDronePresentation();
            ApplyRevivePresentation();
            RefreshMoverState();
            if (newValue && !previousValue)
            {
                _playerSpriteAnimator?.PlayDie();
            }
            else if (!newValue && previousValue)
            {
                _playerSpriteAnimator?.ResetToAlive();
            }
        }

        private void ApplyHealthPresentation(float currentHealth, float maxHealth, bool playHurt)
        {
            _healthBar?.SetHealth(currentHealth, maxHealth);

            if (playHurt && currentHealth > 0f)
            {
                _playerSpriteAnimator?.PlayHurt();
            }
        }

        private void ApplyCharacterPresentation(int characterId, bool isDowned)
        {
            var targetRenderer = ResolvePresentationRenderer();
            if (targetRenderer == null)
            {
                return;
            }

            var definition = MultiplayerCatalog.GetCharacter(characterId);
            var color = definition.Color;
            color.a = isDowned ? 0.35f : 1f;
            targetRenderer.color = color;
            _playerSpriteAnimator?.SetBaseColor(color);
        }

        private void ApplyWeaponPresentation()
        {
            if (_playerActor == null)
            {
                _playerActor = GetComponent<MultiplayerPlayerActor>();
            }

            var showWeapon = _showGunWeapon.Value && !_isDowned.Value;
            _playerActor?.SetWeaponVisible(showWeapon);
            _playerActor?.SetWeaponAim(_aimDirection.Value);
        }

        private void ApplyDronePresentation()
        {
            if (_playerActor == null)
            {
                _playerActor = GetComponent<MultiplayerPlayerActor>();
            }

            var droneCount = _isDowned.Value ? 0 : _droneVisualCount.Value;
            var orbitRadius = _isDowned.Value ? 0f : _droneOrbitRadius.Value;
            var orbitSpeed = _isDowned.Value ? 0f : _droneOrbitSpeedDegrees.Value;
            _playerActor?.SetDroneOrbitVisualState(droneCount, orbitRadius, orbitSpeed);
        }

        private void ApplyRevivePresentation()
        {
            if (_playerActor == null)
            {
                _playerActor = GetComponent<MultiplayerPlayerActor>();
            }

            var reviveRadius = MultiplayerCoopController.Instance != null
                ? MultiplayerCoopController.Instance.ReviveRadius
                : 1.2f;
            _playerActor?.SetReviveVisualState(_isDowned.Value, _reviveProgress.Value, reviveRadius);
        }

        private void RefreshMoverState()
        {
            if (_playerMover == null)
            {
                return;
            }

            var phase = MultiplayerCoopController.Instance != null
                ? MultiplayerCoopController.Instance.Phase
                : MultiplayerRunPhase.Lobby;

            var canMove = IsOwner && phase != MultiplayerRunPhase.LevelChoice && phase != MultiplayerRunPhase.Result && !_isDowned.Value;
            _playerMover.enabled = canMove;
        }

        private void HandleServerAimUpdated(Vector2 direction)
        {
            if (!IsServer)
            {
                return;
            }

            _aimDirection.Value = NormalizeDirection(direction);
        }

        private void HandleServerWeaponFired(Vector2 direction)
        {
            if (!IsServer)
            {
                return;
            }

            var normalized = NormalizeDirection(direction);
            _aimDirection.Value = normalized;
            _fireDirection.Value = normalized;
            _fireSequence.Value++;
        }

        private void HandleServerProjectileVisualRequested(AutoWeaponSystem.ProjectileSpawnRequest request)
        {
            if (!IsServer)
            {
                return;
            }

            var color = request.Color;
            PlayProjectileVisualClientRpc(
                request.SpawnPosition,
                request.Direction,
                request.Speed,
                request.Lifetime,
                request.VisualScale,
                color.r,
                color.g,
                color.b,
                color.a);
        }

        private void HandleServerKatanaSlashFxRequested(Vector2 origin, Vector2 direction, float range, int slashIndex)
        {
            if (!IsServer)
            {
                return;
            }

            PlayKatanaSlashFxClientRpc(origin, direction, range, slashIndex);
        }

        private void HandleServerChainFxRequested(Vector3[] points)
        {
            if (!IsServer || points == null || points.Length <= 1)
            {
                return;
            }

            PlayChainFxClientRpc(points);
        }

        private void HandleServerAuraPulseFxRequested(Vector3 center, float radius)
        {
            if (!IsServer)
            {
                return;
            }

            PlayAuraPulseFxClientRpc(center, radius);
        }

        private void HandleServerSatelliteHitFxRequested(Vector3 center, float radius)
        {
            if (!IsServer)
            {
                return;
            }

            PlaySatelliteHitFxClientRpc(center, radius);
        }

        private void HandleServerSatelliteBeamFxRequested(Vector3 targetCenter)
        {
            if (!IsServer)
            {
                return;
            }

            PlaySatelliteBeamFxClientRpc(targetCenter);
        }

        private void HandleServerTurretDeployed(Vector3 position, float turretRange, float lifetime)
        {
            if (!IsServer)
            {
                return;
            }

            SpawnTurretVisualClientRpc(position, turretRange, lifetime);
        }

        private void HandleServerTurretTracerFxRequested(Vector3 from, Vector3 to)
        {
            if (!IsServer)
            {
                return;
            }

            PlayTurretTracerFxClientRpc(from, to);
        }

        [ServerRpc]
        private void SetLobbyCharacterServerRpc(int characterId)
        {
            var coop = MultiplayerCoopController.Instance;
            if (coop == null || coop.Phase != MultiplayerRunPhase.Lobby)
            {
                return;
            }

            _selectedCharacterId.Value = MultiplayerCatalog.NormalizeCharacterId(characterId);
            _selectionComplete.Value = true;
            _isReady.Value = false;
        }

        [ServerRpc]
        private void SetLobbyStarterWeaponServerRpc(int starterWeaponIndex)
        {
            var coop = MultiplayerCoopController.Instance;
            if (coop == null || coop.Phase != MultiplayerRunPhase.Lobby)
            {
                return;
            }

            _selectedStarterWeaponId.Value = MultiplayerCatalog.NormalizeStarterWeaponIndex(starterWeaponIndex);
            _selectionComplete.Value = true;
            _isReady.Value = false;
        }

        [ServerRpc]
        private void SetReadyServerRpc(bool ready)
        {
            var coop = MultiplayerCoopController.Instance;
            if (coop == null || coop.Phase != MultiplayerRunPhase.Lobby || !_selectionComplete.Value)
            {
                _isReady.Value = false;
                return;
            }

            _isReady.Value = ready;
        }

        [ServerRpc]
        private void SubmitLevelChoiceServerRpc(int optionIndex)
        {
            if (_serverPendingOptions == null || _serverPendingOptions.Length <= 0 || _levelUp == null || !_levelUp.IsAwaitingChoice)
            {
                return;
            }

            optionIndex = Mathf.Clamp(optionIndex, 0, _serverPendingOptions.Length - 1);
            _serverChoiceSubmitted = true;
            _levelUp.ApplyOption(optionIndex, _serverPendingOptions);
            ApplyBuildToRuntimeSystems();

            if (_levelUp.IsAwaitingChoice)
            {
                return;
            }

            _serverPendingOptions = Array.Empty<LevelUpOption>();
            _serverChoiceSubmitted = false;
            ClearPendingChoiceClientRpc(BuildOwnerClientRpcParams());
            MultiplayerCoopController.Instance?.ResumeRunIfChoicesResolved();
        }

        [ClientRpc]
        private void ShowLevelChoiceClientRpc(
            string title,
            int optionCount,
            string option0,
            string option1,
            string option2,
            ClientRpcParams clientRpcParams = default)
        {
            _localPendingTitle = title ?? string.Empty;

            if (optionCount <= 0)
            {
                _localPendingLabels = Array.Empty<string>();
                return;
            }

            var labels = new string[Mathf.Clamp(optionCount, 1, 3)];
            if (labels.Length > 0)
            {
                labels[0] = option0 ?? string.Empty;
            }

            if (labels.Length > 1)
            {
                labels[1] = option1 ?? string.Empty;
            }

            if (labels.Length > 2)
            {
                labels[2] = option2 ?? string.Empty;
            }

            _localPendingLabels = labels;
        }

        [ClientRpc]
        private void ClearPendingChoiceClientRpc(ClientRpcParams clientRpcParams = default)
        {
            _localPendingTitle = string.Empty;
            _localPendingLabels = Array.Empty<string>();
        }

        [ClientRpc]
        private void UpdateBuildSummaryClientRpc(string weaponSummary, string statSummary, ClientRpcParams clientRpcParams = default)
        {
            _localWeaponSummary = string.IsNullOrWhiteSpace(weaponSummary) ? "Weapons" : weaponSummary;
            _localStatSummary = string.IsNullOrWhiteSpace(statSummary) ? "Stats" : statSummary;
        }

        [ClientRpc]
        private void ResetSpecialPresentationClientRpc()
        {
            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            _playerActor?.ResetSpecialPresentation();
        }

        [ClientRpc]
        private void PlayProjectileVisualClientRpc(
            Vector3 spawnPosition,
            Vector2 direction,
            float speed,
            float lifetime,
            float visualScale,
            float colorR,
            float colorG,
            float colorB,
            float colorA)
        {
            if (IsServer)
            {
                return;
            }

            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            _playerActor?.PlayProjectileVisual(
                spawnPosition,
                direction,
                speed,
                lifetime,
                visualScale,
                new Color(colorR, colorG, colorB, colorA));
        }

        [ClientRpc]
        private void PlayKatanaSlashFxClientRpc(Vector2 origin, Vector2 direction, float range, int slashIndex)
        {
            if (IsServer)
            {
                return;
            }

            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            _playerActor?.PlayKatanaSlashFx(origin, direction, range, slashIndex);
        }

        [ClientRpc]
        private void PlayChainFxClientRpc(Vector3[] points)
        {
            if (IsServer)
            {
                return;
            }

            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            _playerActor?.PlayChainFx(points);
        }

        [ClientRpc]
        private void PlayAuraPulseFxClientRpc(Vector3 center, float radius)
        {
            if (IsServer)
            {
                return;
            }

            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            _playerActor?.PlayAuraPulseFx(center, radius);
        }

        [ClientRpc]
        private void PlaySatelliteHitFxClientRpc(Vector3 center, float radius)
        {
            if (IsServer)
            {
                return;
            }

            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            _playerActor?.PlaySatelliteHitFx(center, radius);
        }

        [ClientRpc]
        private void PlaySatelliteBeamFxClientRpc(Vector3 targetCenter)
        {
            if (IsServer)
            {
                return;
            }

            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            _playerActor?.PlaySatelliteBeamFx(targetCenter);
        }

        [ClientRpc]
        private void SpawnTurretVisualClientRpc(Vector3 position, float turretRange, float lifetime)
        {
            if (IsServer)
            {
                return;
            }

            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            _playerActor?.SpawnTurretVisual(position, turretRange, lifetime);
        }

        [ClientRpc]
        private void PlayTurretTracerFxClientRpc(Vector3 from, Vector3 to)
        {
            if (IsServer)
            {
                return;
            }

            _playerActor ??= GetComponent<MultiplayerPlayerActor>();
            _playerActor?.PlayTurretTracerFx(from, to);
        }

        private ClientRpcParams BuildOwnerClientRpcParams()
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId },
                },
            };
        }

        private static bool HasAnyGunWeapon(PlayerBuildRuntime buildRuntime)
        {
            if (buildRuntime == null)
            {
                return false;
            }

            var ownedWeapons = buildRuntime.OwnedWeapons;
            for (var i = 0; i < ownedWeapons.Count; i++)
            {
                if (ownedWeapons[i] == WeaponUpgradeId.Rifle
                    || ownedWeapons[i] == WeaponUpgradeId.Smg
                    || ownedWeapons[i] == WeaponUpgradeId.SniperRifle
                    || ownedWeapons[i] == WeaponUpgradeId.Shotgun)
                {
                    return true;
                }
            }

            return false;
        }

        private int GetDroneVisualCount(PlayerBuildRuntime buildRuntime)
        {
            if (buildRuntime == null)
            {
                return 0;
            }

            var droneLevel = buildRuntime.GetWeaponLevel(WeaponUpgradeId.Drone);
            if (droneLevel <= 0)
            {
                return 0;
            }

            var baseCount = _weaponConfig != null ? Mathf.Max(1, _weaponConfig.satelliteBaseCount) : 2;
            return Mathf.Clamp(baseCount + GetWeaponExtraCount(WeaponUpgradeId.Drone, droneLevel), 1, 8);
        }

        private float GetDroneOrbitRadius(PlayerBuildRuntime buildRuntime, PlayerStatsRuntime stats)
        {
            if (buildRuntime == null || buildRuntime.GetWeaponLevel(WeaponUpgradeId.Drone) <= 0)
            {
                return 0f;
            }

            var tier = Mathf.Max(0, buildRuntime.GetWeaponLevel(WeaponUpgradeId.Drone) - 1);
            var baseRadius = _weaponConfig != null ? Mathf.Max(0.2f, _weaponConfig.satelliteOrbitRadius) : 1.2f;
            var attackRangeMultiplier = stats != null ? Mathf.Max(0.1f, stats.AttackRangeMultiplier) : 1f;
            return baseRadius * (1f + (0.02f * tier)) * attackRangeMultiplier;
        }

        private float GetDroneOrbitSpeedDegrees(PlayerBuildRuntime buildRuntime, PlayerStatsRuntime stats)
        {
            if (buildRuntime == null || buildRuntime.GetWeaponLevel(WeaponUpgradeId.Drone) <= 0)
            {
                return 0f;
            }

            var tier = Mathf.Max(0, buildRuntime.GetWeaponLevel(WeaponUpgradeId.Drone) - 1);
            var baseSpeed = _weaponConfig != null ? Mathf.Max(30f, _weaponConfig.satelliteAngularSpeed) : 220f;
            var attackSpeedScale = stats != null ? Mathf.Max(0.2f, 1f / stats.AttackIntervalMultiplier) : 1f;
            return baseSpeed * (1f + (0.02f * tier)) * attackSpeedScale;
        }

        private static int GetWeaponExtraCount(WeaponUpgradeId weaponId, int weaponLevel)
        {
            var levelIndex = Mathf.Clamp(weaponLevel - 1, 0, DroneExtraByLevel.Length - 1);
            return weaponId switch
            {
                WeaponUpgradeId.Drone => DroneExtraByLevel[levelIndex],
                _ => 0,
            };
        }

        private static Vector2 NormalizeDirection(Vector2 direction)
        {
            return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
        }

        private SpriteRenderer ResolvePresentationRenderer()
        {
            if (_playerActor == null)
            {
                _playerActor = GetComponent<MultiplayerPlayerActor>();
            }

            if (_playerActor != null && _playerActor.VisualRenderer != null)
            {
                return _playerActor.VisualRenderer;
            }

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            return _spriteRenderer;
        }

        private static readonly int[] DroneExtraByLevel = { 0, 0, 0, 0, 1, 1, 1, 1, 1, 2 };
    }
}
