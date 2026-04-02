using System;
using System.Collections.Generic;
using System.Text;
using EJR.Game.Audio;
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
        private enum PendingChoiceContext
        {
            None = 0,
            LevelUp = 1,
            WaveAugment = 2,
        }

        private readonly struct PendingChoiceRequest
        {
            public PendingChoiceRequest(PendingChoiceContext context, LevelUpOption[] options, string title)
            {
                Context = context;
                Options = options ?? Array.Empty<LevelUpOption>();
                Title = title ?? string.Empty;
            }

            public PendingChoiceContext Context { get; }
            public LevelUpOption[] Options { get; }
            public string Title { get; }
        }

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

        private readonly NetworkVariable<Vector2> _facingDirection =
            new(Vector2.right, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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

        private readonly NetworkVariable<float> _auraRadius =
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
        private LevelUpBalanceConfig _levelUpBalanceConfig;
        private LevelUpOption[] _serverPendingOptions = Array.Empty<LevelUpOption>();
        private readonly Queue<PendingChoiceRequest> _serverQueuedChoices = new();
        private PendingChoiceContext _serverPendingChoiceContext;
        private string[] _localPendingLabels = Array.Empty<string>();
        private string _localPendingTitle = string.Empty;
        private string _localWeaponSummary = "臾닿린";
        private string _localStatSummary = "능력치";
        private bool _serverChoiceSubmitted;
        private bool _serverInitialized;
        private readonly RunCombatTracker _combatTracker = new();
        private float _lastObservedPlayerMaxHealth = -1f;
        private int _unlockedCharacterMask;
        private MetaBonusValues _metaRunStartBonuses;
        private float _pendingRemoteHealPopupAmount;

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
        public bool HasPendingServerChoice => _serverPendingOptions.Length > 0 || _serverQueuedChoices.Count > 0;
        public bool HasSubmittedServerChoice => _serverChoiceSubmitted;
        public string DisplayName => MultiplayerCatalog.GetPlayerDisplayName(OwnerClientId, _selectedCharacterId.Value);
        public float CurrentCreditGainPercent => _playerStats != null ? _playerStats.CreditGainPercent : 0f;
        private int optionCount => Mathf.Min(_serverPendingOptions.Length, 3);
        private string option0 => optionCount > 0 ? _serverPendingOptions[0].Label : string.Empty;
        private string option1 => optionCount > 1 ? _serverPendingOptions[1].Label : string.Empty;
        private string option2 => optionCount > 2 ? _serverPendingOptions[2].Label : string.Empty;

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
            _levelUpBalanceConfig = LevelUpBalanceConfig.CreateRuntimeDefault();

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
            _pendingRemoteHealPopupAmount = 0f;
            _currentHealth.OnValueChanged += HandleCurrentHealthChanged;
            _maxHealth.OnValueChanged += HandleMaxHealthChanged;
            _selectedCharacterId.OnValueChanged += HandleSelectedCharacterChanged;
            _isDowned.OnValueChanged += HandleDownedChanged;
            _reviveProgress.OnValueChanged += HandleReviveProgressChanged;
            _moveSpeedMultiplier.OnValueChanged += HandleMoveSpeedMultiplierChanged;
            _aimDirection.OnValueChanged += HandleAimDirectionChanged;
            _facingDirection.OnValueChanged += HandleFacingDirectionChanged;
            _fireDirection.OnValueChanged += HandleFireDirectionChanged;
            _fireSequence.OnValueChanged += HandleFireSequenceChanged;
            _showGunWeapon.OnValueChanged += HandleShowGunWeaponChanged;
            _droneVisualCount.OnValueChanged += HandleDroneVisualStateChanged;
            _droneOrbitRadius.OnValueChanged += HandleDroneVisualStateChanged;
            _droneOrbitSpeedDegrees.OnValueChanged += HandleDroneVisualStateChanged;
            _auraRadius.OnValueChanged += HandleAuraRadiusChanged;

            ApplyHealthPresentation(_currentHealth.Value, Mathf.Max(1f, _maxHealth.Value), false);
            ApplyCharacterPresentation(_selectedCharacterId.Value, _isDowned.Value);
            _playerMover.SetMoveSpeedMultiplier(_moveSpeedMultiplier.Value);
            ApplyWeaponPresentation();
            _playerActor?.SetFacingDirection(_facingDirection.Value);
            ApplyDronePresentation();
            ApplyAuraPresentation();
            ApplyRevivePresentation();
            RefreshMoverState();

            if (IsServer)
            {
                InitializeServerState();
            }

            if (IsOwner)
            {
                PushLocalMetaProfileToServer();
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
            _facingDirection.OnValueChanged -= HandleFacingDirectionChanged;
            _fireDirection.OnValueChanged -= HandleFireDirectionChanged;
            _fireSequence.OnValueChanged -= HandleFireSequenceChanged;
            _showGunWeapon.OnValueChanged -= HandleShowGunWeaponChanged;
            _droneVisualCount.OnValueChanged -= HandleDroneVisualStateChanged;
            _droneOrbitRadius.OnValueChanged -= HandleDroneVisualStateChanged;
            _droneOrbitSpeedDegrees.OnValueChanged -= HandleDroneVisualStateChanged;
            _auraRadius.OnValueChanged -= HandleAuraRadiusChanged;

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

            if (IsOwner)
            {
                var facingDirection = NormalizeDirection(_playerMover != null ? _playerMover.CurrentFacingDirection : Vector2.right);
                if ((_facingDirection.Value - facingDirection).sqrMagnitude > 0.000001f)
                {
                    _facingDirection.Value = facingDirection;
                }

                _playerActor?.SetFacingDirection(facingDirection);
            }

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

            var nextCharacterId = MetaProgressionService.GetNextUnlockedCharacterId(_selectedCharacterId.Value);
            MetaProgressionService.SetSingleSelectedCharacterId(nextCharacterId);
            SetLobbyCharacterServerRpc(nextCharacterId);
        }

        public void RequestCharacterSelection(int characterId)
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            var normalizedCharacterId = MultiplayerCatalog.NormalizeCharacterId(characterId);
            if (!MetaProgressionService.IsCharacterUnlocked(normalizedCharacterId))
            {
                return;
            }

            MetaProgressionService.SetSingleSelectedCharacterId(normalizedCharacterId);
            SetLobbyCharacterServerRpc(normalizedCharacterId);
        }

        public void RequestNextStarterWeaponSelection()
        {
            // Starter weapon selection was removed. Character selection now determines the starter weapon.
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

            AudioService.Instance.PlaySfx(AudioCueId.LevelUpSelect);
            SubmitLevelChoiceServerRpc(optionIndex);
        }

        public bool QueueWaveAugmentChoiceServer(int waveIndex)
        {
            if (!IsServer || _buildRuntime == null)
            {
                return false;
            }

            var options = SharedAugmentCatalog.BuildRandomOptions(_buildRuntime.ActiveAugments);
            if (options.Length <= 0)
            {
                return false;
            }

            EnqueueServerChoice(PendingChoiceContext.WaveAugment, options, $"웨이브 {Mathf.Max(1, waveIndex)} 보상 - 증강 선택");
            return true;
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
            _combatTracker.Reset();
            _isDowned.Value = false;
            _reviveProgress.Value = 0f;
            _serverPendingOptions = Array.Empty<LevelUpOption>();
            _serverQueuedChoices.Clear();
            _serverPendingChoiceContext = PendingChoiceContext.None;
            _serverChoiceSubmitted = false;
            _lastObservedPlayerMaxHealth = _playerHealth.MaxHealth;
            _weaponSystem?.ClearActiveProjectiles();
            ResetSpecialPresentationClientRpc();
            EnsureWeaponSystem(arenaBounds);
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
            _serverPendingOptions = Array.Empty<LevelUpOption>();
            _serverQueuedChoices.Clear();
            _serverPendingChoiceContext = PendingChoiceContext.None;
            _serverChoiceSubmitted = false;
            ResetBuildRuntimeForLobby();
            _playerMover.Initialize(_playerConfig, _playerStats, arenaBounds);
            _playerHealth.Initialize(GetCurrentMaxHealth(), _playerConfig.damageInvulnerabilitySeconds);
            _combatTracker.Reset();
            _lastObservedPlayerMaxHealth = _playerHealth.MaxHealth;

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
            _playerHealth.Damaged += HandleServerDamaged;
            _playerHealth.Healed += HandleServerHealed;

            _selectedCharacterId.Value = MultiplayerCatalog.NormalizeCharacterId((int)(OwnerClientId % (ulong)Mathf.Max(1, MultiplayerCatalog.CharacterCount)));
            _selectedStarterWeaponId.Value = MultiplayerCatalog.GetStarterWeaponIndex(
                MetaProgressionService.GetCharacterStarterWeapon(_selectedCharacterId.Value));
            _selectionComplete.Value = true;
            _isReady.Value = false;
            _isDowned.Value = false;
            _reviveProgress.Value = 0f;
            _facingDirection.Value = Vector2.right;
            _unlockedCharacterMask = 0;
            _metaRunStartBonuses = default;
            _droneVisualCount.Value = 0;
            _droneOrbitRadius.Value = 0f;
            _droneOrbitSpeedDegrees.Value = 0f;
            _auraRadius.Value = 0f;
            _combatTracker.Reset();
            ResetBuildRuntimeForLobby();
            _playerHealth.Initialize(GetCurrentMaxHealth(), _playerConfig.damageInvulnerabilitySeconds);
            _lastObservedPlayerMaxHealth = _playerHealth.MaxHealth;
            SyncHealthState();

            _serverInitialized = true;
        }

        private void RecreateLevelSystem()
        {
            if (_levelUp != null)
            {
                _levelUp.OptionsGenerated -= HandleServerOptionsGenerated;
                _levelUp.ExperienceChanged -= HandleServerExperienceChanged;
            }

            _levelUp = new LevelUpSystem();
            _levelUp.Initialize(_buildRuntime, _levelUpBalanceConfig, IsWeaponUnlockedForThisPlayer);
            _levelUp.OptionsGenerated += HandleServerOptionsGenerated;
            _levelUp.ExperienceChanged += HandleServerExperienceChanged;
        }

        private void UnhookServerRuntime()
        {
            if (_levelUp != null)
            {
                _levelUp.OptionsGenerated -= HandleServerOptionsGenerated;
                _levelUp.ExperienceChanged -= HandleServerExperienceChanged;
            }

            if (_weaponSystem != null)
            {
                _weaponSystem.AimUpdated -= HandleServerAimUpdated;
                _weaponSystem.Fired -= HandleServerWeaponFired;
                _weaponSystem.WeaponSoundRequested -= HandleServerWeaponSoundRequested;
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
                _playerHealth.Damaged -= HandleServerDamaged;
                _playerHealth.Healed -= HandleServerHealed;
            }
        }

        private void ResetBuildRuntimeForLobby()
        {
            _buildRuntime.InitializeDefaults(grantStarterRifle: false);
            _buildRuntime.ApplyMetaBonuses(_metaRunStartBonuses);
            _buildRuntime.ApplyCharacterBaseBonuses(MetaProgressionService.GetCharacterBaseBonuses(_selectedCharacterId.Value));
            RecreateLevelSystem();
            RefreshCharacterPassiveBonuses();
        }

        private void ResetBuildRuntimeForRun()
        {
            _buildRuntime.InitializeDefaults(grantStarterRifle: false);
            _buildRuntime.ApplyMetaBonuses(_metaRunStartBonuses);
            _buildRuntime.ApplyCharacterBaseBonuses(MetaProgressionService.GetCharacterBaseBonuses(_selectedCharacterId.Value));
            var starterWeapon = MetaProgressionService.GetCharacterStarterWeapon(_selectedCharacterId.Value);
            _selectedStarterWeaponId.Value = MultiplayerCatalog.GetStarterWeaponIndex(starterWeapon);
            _buildRuntime.Apply(LevelUpOption.CreateWeaponAcquire(
                starterWeapon,
                $"{MultiplayerCatalog.GetWeaponDisplayName(starterWeapon)} ?덈꺼 1",
                "臾닿린 ?띾뱷",
                MultiplayerCatalog.GetWeaponDisplayName(starterWeapon)));

            RecreateLevelSystem();
            RefreshCharacterPassiveBonuses();
        }

        private bool IsWeaponUnlockedForThisPlayer(WeaponUpgradeId weaponId)
        {
            return weaponId != WeaponUpgradeId.Drone;
        }

        private bool IsCharacterUnlockedForThisPlayer(int characterId)
        {
            return _unlockedCharacterMask == 0 || SharedGameCatalog.IsCharacterInMask(_unlockedCharacterMask, characterId);
        }

        private void PushLocalMetaProfileToServer()
        {
            MetaProgressionService.EnsureLoaded();

            var selectedCharacterId = MetaProgressionService.GetSingleSelectedCharacterId();
            var bonuses = MetaProgressionService.GetPurchasedUpgradeBonuses();

            SubmitMetaProfileServerRpc(
                MetaProgressionService.GetUnlockedCharacterMask(),
                bonuses.attackPowerPercent,
                bonuses.attackSpeedPercent,
                bonuses.maxHealthFlat,
                bonuses.healthRegenPerSecond,
                bonuses.moveSpeedPercent,
                bonuses.attackRangePercent,
                bonuses.luck,
                bonuses.experienceGainPercent,
                bonuses.creditGainPercent,
                selectedCharacterId);
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
                _playerHealth,
                projectileSpawnResolver: ResolveProjectileSpawnPoint,
                projectileCullBounds: arenaBounds,
                facingDirectionResolver: () => NormalizeDirection(_facingDirection.Value));

            _weaponSystem.AimUpdated -= HandleServerAimUpdated;
            _weaponSystem.Fired -= HandleServerWeaponFired;
            _weaponSystem.WeaponSoundRequested -= HandleServerWeaponSoundRequested;
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
            _weaponSystem.WeaponSoundRequested += HandleServerWeaponSoundRequested;
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
            if (_buildRuntime == null || _playerStats == null)
            {
                return;
            }

            _playerStats.RecalculateFromBuild(_buildRuntime);

            if (_weaponSystem != null)
            {
                _weaponSystem.ConfigureLoadout(_buildRuntime, _playerStats);
            }

            if (_playerHealth != null)
            {
                var preserveCurrentRatio = _buildRuntime != null && !Mathf.Approximately(_buildRuntime.GlobalMaxHealthScale, 1f);
                _playerHealth.SetMaxHealth(GetCurrentMaxHealth(), healDelta: !preserveCurrentRatio, preserveCurrentRatio: preserveCurrentRatio);
            }

            _moveSpeedMultiplier.Value = _playerStats != null ? _playerStats.MoveSpeedMultiplier : 1f;
            _playerMover.SetMoveSpeedMultiplier(_moveSpeedMultiplier.Value);
            _showGunWeapon.Value = _buildRuntime != null && _buildRuntime.HasWeapon(WeaponUpgradeId.BfSword);
            _droneVisualCount.Value = GetDroneVisualCount(_buildRuntime);
            _droneOrbitRadius.Value = GetDroneOrbitRadius(_buildRuntime, _playerStats);
            _droneOrbitSpeedDegrees.Value = GetDroneOrbitSpeedDegrees(_buildRuntime, _playerStats);
            _auraRadius.Value = GetAuraVisualRadius(_buildRuntime, _playerStats);
            ApplyAuraPresentation();
            UpdateBuildSummaries();
            SyncHealthState();
        }

        private void RefreshCharacterPassiveBonuses()
        {
            if (_buildRuntime == null)
            {
                return;
            }

            var passiveId = MetaProgressionService.GetCharacterPassiveId(_selectedCharacterId.Value);
            var currentLevel = _levelUp != null ? Mathf.Max(1, _levelUp.Level) : 1;
            var dynamicBonuses = default(MetaBonusValues);
            var ignoreChainDecay = false;
            var bonusChains = 0;

            switch (passiveId)
            {
                case CharacterPassiveId.SoldierLevelAttackSpeed:
                    dynamicBonuses.attackSpeedPercent = currentLevel;
                    break;
                case CharacterPassiveId.VampireMaxHealthDamage:
                {
                    var baseMaxHealth = Mathf.Max(1f, _playerConfig != null ? _playerConfig.maxHealth : 100f);
                    var currentMaxHealth = _playerHealth != null ? _playerHealth.MaxHealth : GetCurrentMaxHealth();
                    var bonusMaxHealth = Mathf.Max(0f, currentMaxHealth - baseMaxHealth);
                    dynamicBonuses.attackPowerPercent = Mathf.Floor(bonusMaxHealth / 3f);
                    break;
                }
                case CharacterPassiveId.SwordsmanLevelMoveSpeed:
                    dynamicBonuses.moveSpeedPercent = currentLevel;
                    break;
                case CharacterPassiveId.WizardLevelDamage:
                    dynamicBonuses.attackPowerPercent = currentLevel;
                    break;
                case CharacterPassiveId.PriestLevelRange:
                    dynamicBonuses.attackRangePercent = currentLevel;
                    break;
                case CharacterPassiveId.LightningMageChainMastery:
                    ignoreChainDecay = true;
                    bonusChains = 2;
                    break;
            }

            dynamicBonuses += _buildRuntime.GetLowHealthDynamicBonuses(GetCurrentHealthRatio());
            _buildRuntime.ApplyCharacterDynamicBonuses(dynamicBonuses);
            _buildRuntime.SetChainAttackModifiers(ignoreChainDecay, bonusChains);
            ApplyBuildToRuntimeSystems();
        }

        private float GetCurrentMaxHealth()
        {
            var baseMaxHealth = Mathf.Max(1f, _playerConfig != null ? _playerConfig.maxHealth : 100f);
            var bonus = _playerStats != null ? Mathf.Max(0f, _playerStats.MaxHealthBonus) : 0f;
            var scale = _playerStats != null ? Mathf.Max(0.05f, _playerStats.MaxHealthScale) : 1f;
            return Mathf.Max(1f, (baseMaxHealth + bonus) * scale);
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
            var builder = new StringBuilder("무기");
            var playerLevel = _levelUp != null ? _levelUp.Level : 1;
            var unlockedSlots = _buildRuntime != null ? _buildRuntime.GetUnlockedWeaponSlots(playerLevel) : 1;

            for (var slotIndex = 0; slotIndex < PlayerBuildRuntime.MaxWeaponSlotsAbsolute; slotIndex++)
            {
                var slotNumber = slotIndex + 1;
                if (slotIndex >= unlockedSlots)
                {
                    builder.Append('\n').Append(slotNumber).Append(") ").Append(GetLockedWeaponSlotText(slotIndex));
                    continue;
                }

                if (_buildRuntime != null && slotIndex < _buildRuntime.OwnedWeapons.Count)
                {
                    var weaponId = _buildRuntime.OwnedWeapons[slotIndex];
                    var level = _buildRuntime.GetWeaponLevel(weaponId);
                    var damageBonus = _buildRuntime.GetWeaponDamageBonusPercentTotal(weaponId);
                    var attackSpeedBonus = _buildRuntime.GetWeaponAttackSpeedBonusPercentTotal(weaponId);
                    var rangeBonus = _buildRuntime.GetWeaponRangeBonusPercentTotal(weaponId);
                    var milestoneCount = _buildRuntime.GetWeaponMilestoneCount(weaponId);
                    builder.Append('\n').Append(slotNumber).Append(") ")
                        .Append(MultiplayerCatalog.GetWeaponDisplayName(weaponId))
                        .Append(" 레벨 ").Append(level)
                        .Append(" [피해+").Append(damageBonus.ToString("0.#"))
                        .Append(" 공속").Append(FormatSignedPercent(attackSpeedBonus))
                        .Append(" 범위+").Append(rangeBonus.ToString("0.#"));

                    if (milestoneCount > 0)
                    {
                        builder.Append(" 특수+").Append(milestoneCount);
                    }

                    builder.Append(']');
                }
                else
                {
                    builder.Append('\n').Append(slotNumber).Append(") 비어 있음");
                }
            }

            return builder.ToString();
        }

        private string BuildStatSummary()
        {
            if (_buildRuntime == null)
            {
                return "전체 능력치";
            }

            var builder = new StringBuilder("전체 능력치");
            builder.Append('\n').Append("피해량 +").Append(_buildRuntime.GlobalAttackPowerPercentTotal.ToString("0.#")).Append('%');
            builder.Append('\n').Append("공격 속도 ").Append(FormatSignedPercent(_buildRuntime.GlobalAttackSpeedPercentTotal));
            builder.Append('\n').Append("최대 체력 +").Append(_buildRuntime.GlobalMaxHealthFlatTotal.ToString("0"));
            if (_buildRuntime.SuppressesPassiveRegen)
            {
                builder.Append('\n').Append("체력 재생 0/초 (흡혈)");
            }
            else
            {
                var regenPerSecond = _playerStats != null ? _playerStats.HealthRegenPerSecond : _buildRuntime.GlobalHealthRegenPerSecondTotal;
                builder.Append('\n').Append("체력 재생 +").Append(regenPerSecond.ToString("0.##")).Append("/초");
            }

            builder.Append('\n').Append("이동 속도 ").Append(FormatSignedPercent(_buildRuntime.GlobalMoveSpeedPercentTotal));
            builder.Append('\n').Append("공격 범위 +").Append(_buildRuntime.GlobalAttackRangePercentTotal.ToString("0.#")).Append('%');
            builder.Append('\n').Append("행운 ").Append(_buildRuntime.GlobalLuckTotal.ToString("0"));
            if (!Mathf.Approximately(_buildRuntime.GlobalMaxHealthScale, 1f))
            {
                builder.Append('\n').Append("최대 체력 배율 x").Append(_buildRuntime.GlobalMaxHealthScale.ToString("0.##"));
            }

            if (_buildRuntime.LifestealHealPerHit > 0)
            {
                builder.Append('\n').Append("흡혈 ").Append(_buildRuntime.LifestealHealPerHit).Append("/타격");
            }

            return builder.ToString();
        }

        private float GetCurrentHealthRatio()
        {
            if (_playerHealth == null || _playerHealth.MaxHealth <= 0.0001f)
            {
                return 1f;
            }

            return Mathf.Clamp01(_playerHealth.CurrentHealth / _playerHealth.MaxHealth);
        }

        private static string GetLockedWeaponSlotText(int slotIndex)
        {
            return slotIndex switch
            {
                1 => $"잠김 (레벨 {PlayerBuildRuntime.SecondWeaponUnlockLevel})",
                2 => $"잠김 (레벨 {PlayerBuildRuntime.ThirdWeaponUnlockLevel})",
                3 => "잠김 (양손잡이 필요)",
                _ => "잠김",
            };
        }

        private static string FormatSignedPercent(float value)
        {
            var prefix = value >= 0f ? "+" : string.Empty;
            return $"{prefix}{value:0.#}%";
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

            var maxHealthChanged = !Mathf.Approximately(_lastObservedPlayerMaxHealth, maxHealth);
            if (maxHealthChanged)
            {
                _lastObservedPlayerMaxHealth = maxHealth;
            }

            if (maxHealthChanged || (_buildRuntime != null && _buildRuntime.HasLowHealthBonuses))
            {
                RefreshCharacterPassiveBonuses();
            }
        }

        private void HandleServerDamaged(float damage)
        {
            _combatTracker.RecordDamageTaken(damage);
        }

        private void HandleServerHealed(float amount)
        {
            _combatTracker.RecordHealing(amount);
        }

        private void HandleServerExperienceChanged(int currentExperience, int requiredExperience, int level)
        {
            RefreshCharacterPassiveBonuses();
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

            EnqueueServerChoice(PendingChoiceContext.LevelUp, options, "레벨 업 - 하나 선택");
        }

        private void EnqueueServerChoice(PendingChoiceContext context, LevelUpOption[] options, string title)
        {
            if (!IsServer || options == null || options.Length <= 0)
            {
                return;
            }

            if (_serverPendingOptions.Length <= 0)
            {
                _serverPendingOptions = options;
                _serverPendingChoiceContext = context;
                _serverChoiceSubmitted = false;
                ShowActiveChoiceToOwner(title);
                return;
            }

            _serverQueuedChoices.Enqueue(new PendingChoiceRequest(context, options, title));
            MultiplayerCoopController.Instance?.EnterLevelChoicePauseIfNeeded();
        }

        private void TryShowNextQueuedChoice()
        {
            if (!IsServer || _serverPendingOptions.Length > 0 || _serverQueuedChoices.Count <= 0)
            {
                return;
            }

            var nextChoice = _serverQueuedChoices.Dequeue();
            _serverPendingOptions = nextChoice.Options;
            _serverPendingChoiceContext = nextChoice.Context;
            _serverChoiceSubmitted = false;
            ShowActiveChoiceToOwner(nextChoice.Title);
        }

        private void ShowActiveChoiceToOwner(string title)
        {
            if (!IsServer || _serverPendingOptions.Length <= 0)
            {
                return;
            }

            ShowLevelChoiceClientRpc(
                title,
                optionCount,
                option0,
                option1,
                option2,
                BuildOwnerClientRpcParams());

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
                if (IsOwner)
                {
                    AudioService.Instance.PlaySfx(AudioCueId.PlayerHurt);
                }
            }
            else if (!IsServer && newValue > previousValue + 0.001f)
            {
                if (previousValue <= 0.001f)
                {
                    _pendingRemoteHealPopupAmount = 0f;
                }
                else
                {
                    TrySpawnRemoteHealingPopup(newValue - previousValue);
                }
            }

            ApplyHealthPresentation(newValue, Mathf.Max(1f, _maxHealth.Value), newValue < previousValue - 0.001f);
        }

        private void HandleMaxHealthChanged(float previousValue, float newValue)
        {
            ApplyHealthPresentation(_currentHealth.Value, Mathf.Max(1f, newValue), false);
        }

        private void TrySpawnRemoteHealingPopup(float healingAmount)
        {
            _pendingRemoteHealPopupAmount += healingAmount;
            var displayAmount = Mathf.FloorToInt(_pendingRemoteHealPopupAmount + 0.0001f);
            if (displayAmount <= 0)
            {
                return;
            }

            _pendingRemoteHealPopupAmount = Mathf.Max(0f, _pendingRemoteHealPopupAmount - displayAmount);
            CombatTextSpawner.SpawnHealing(
                transform.position + new Vector3(0f, 0.9f, 0f),
                displayAmount);
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

        private void HandleFacingDirectionChanged(Vector2 previousValue, Vector2 newValue)
        {
            _playerActor?.SetFacingDirection(newValue);
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

        private void HandleAuraRadiusChanged(float previousValue, float newValue)
        {
            ApplyAuraPresentation();
        }

        private void HandleDownedChanged(bool previousValue, bool newValue)
        {
            ApplyCharacterPresentation(_selectedCharacterId.Value, newValue);
            ApplyWeaponPresentation();
            ApplyDronePresentation();
            ApplyAuraPresentation();
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
            _playerActor?.SetFacingDirection(_facingDirection.Value);
        }

        private void ApplyWeaponPresentation()
        {
            if (_playerActor == null)
            {
                _playerActor = GetComponent<MultiplayerPlayerActor>();
            }

            var showWeapon = _showGunWeapon.Value && !_isDowned.Value;
            _playerActor?.SetWeaponVisible(showWeapon);
            _playerActor?.SetFacingDirection(_facingDirection.Value);
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

        private void ApplyAuraPresentation()
        {
            if (_playerActor == null)
            {
                _playerActor = GetComponent<MultiplayerPlayerActor>();
            }

            var auraRadius = _isDowned.Value ? 0f : _auraRadius.Value;
            _playerActor?.SetAuraVisualState(auraRadius);
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

        private void HandleServerWeaponSoundRequested(WeaponSoundRequest request)
        {
            if (!IsServer)
            {
                return;
            }

            AudioService.Instance.PlayWeaponSound(request);
            PlayWeaponSoundClientRpc((int)request.WeaponId, (int)request.Kind);
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
        private void SubmitMetaProfileServerRpc(
            int unlockedCharacterMask,
            float attackPowerPercent,
            float attackSpeedPercent,
            float maxHealthFlat,
            float healthRegenPerSecond,
            float moveSpeedPercent,
            float attackRangePercent,
            float luck,
            float experienceGainPercent,
            float creditGainPercent,
            int preferredCharacterId)
        {
            _unlockedCharacterMask = unlockedCharacterMask;
            _metaRunStartBonuses = new MetaBonusValues
            {
                attackPowerPercent = attackPowerPercent,
                attackSpeedPercent = attackSpeedPercent,
                maxHealthFlat = maxHealthFlat,
                healthRegenPerSecond = healthRegenPerSecond,
                moveSpeedPercent = moveSpeedPercent,
                attackRangePercent = attackRangePercent,
                luck = luck,
                experienceGainPercent = experienceGainPercent,
                creditGainPercent = creditGainPercent,
            };

            if (MultiplayerCoopController.Instance != null && MultiplayerCoopController.Instance.Phase == MultiplayerRunPhase.Lobby)
            {
                if (IsCharacterUnlockedForThisPlayer(preferredCharacterId))
                {
                    _selectedCharacterId.Value = MultiplayerCatalog.NormalizeCharacterId(preferredCharacterId);
                }

                _selectedStarterWeaponId.Value = MultiplayerCatalog.GetStarterWeaponIndex(
                    MetaProgressionService.GetCharacterStarterWeapon(_selectedCharacterId.Value));
                _selectionComplete.Value = true;
                _isReady.Value = false;
            }

            if (_serverInitialized)
            {
                ResetBuildRuntimeForLobby();
            }
        }

        [ServerRpc]
        private void SetLobbyCharacterServerRpc(int characterId)
        {
            var coop = MultiplayerCoopController.Instance;
            if (coop == null || coop.Phase != MultiplayerRunPhase.Lobby)
            {
                return;
            }

            characterId = MultiplayerCatalog.NormalizeCharacterId(characterId);
            if (!IsCharacterUnlockedForThisPlayer(characterId))
            {
                return;
            }

            _selectedCharacterId.Value = characterId;
            _selectedStarterWeaponId.Value = MultiplayerCatalog.GetStarterWeaponIndex(
                MetaProgressionService.GetCharacterStarterWeapon(characterId));
            _selectionComplete.Value = true;
            _isReady.Value = false;

            if (_serverInitialized)
            {
                ResetBuildRuntimeForLobby();
            }
        }

        [ServerRpc]
        private void SetLobbyStarterWeaponServerRpc(int starterWeaponIndex)
        {
            _selectedStarterWeaponId.Value = MultiplayerCatalog.GetStarterWeaponIndex(
                MetaProgressionService.GetCharacterStarterWeapon(_selectedCharacterId.Value));
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
            if (_serverPendingOptions == null || _serverPendingOptions.Length <= 0)
            {
                return;
            }

            optionIndex = Mathf.Clamp(optionIndex, 0, _serverPendingOptions.Length - 1);
            _serverChoiceSubmitted = true;
            var selectedOption = _serverPendingOptions[optionIndex];
            if (_serverPendingChoiceContext == PendingChoiceContext.WaveAugment)
            {
                _buildRuntime?.Apply(selectedOption);
                RefreshCharacterPassiveBonuses();
            }
            else
            {
                if (_levelUp == null || !_levelUp.IsAwaitingChoice)
                {
                    return;
                }

                _levelUp.ApplyOption(optionIndex, _serverPendingOptions);
                RefreshCharacterPassiveBonuses();
            }

            _serverPendingOptions = Array.Empty<LevelUpOption>();
            _serverPendingChoiceContext = PendingChoiceContext.None;
            _serverChoiceSubmitted = false;
            ClearPendingChoiceClientRpc(BuildOwnerClientRpcParams());
            TryShowNextQueuedChoice();
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
            if (!IsServer)
            {
                AudioService.Instance.PlaySfx(AudioCueId.LevelUpAppear);
            }

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
            _localWeaponSummary = string.IsNullOrWhiteSpace(weaponSummary) ? "臾닿린" : weaponSummary;
            _localStatSummary = string.IsNullOrWhiteSpace(statSummary) ? "능력치" : statSummary;
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
        private void PlayWeaponSoundClientRpc(int weaponId, int kind)
        {
            if (IsServer)
            {
                return;
            }

            AudioService.Instance.PlayWeaponSound(
                new WeaponSoundRequest(
                    (WeaponUpgradeId)weaponId,
                    (WeaponSoundKind)kind,
                    transform.position));
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
            return Mathf.Clamp(baseCount + buildRuntime.GetWeaponExtraCountBonus(WeaponUpgradeId.Drone), 1, 8);
        }

        private float GetDroneOrbitRadius(PlayerBuildRuntime buildRuntime, PlayerStatsRuntime stats)
        {
            if (buildRuntime == null || buildRuntime.GetWeaponLevel(WeaponUpgradeId.Drone) <= 0)
            {
                return 0f;
            }

            var baseRadius = _weaponConfig != null ? Mathf.Max(0.2f, _weaponConfig.satelliteOrbitRadius) : 1.2f;
            var attackRangeMultiplier = stats != null ? Mathf.Max(0.1f, stats.AttackRangeMultiplier) : 1f;
            var weaponRangeMultiplier = 1f + (Mathf.Max(0f, buildRuntime.GetWeaponRangeBonusPercentTotal(WeaponUpgradeId.Drone)) / 100f);
            return baseRadius * weaponRangeMultiplier * attackRangeMultiplier;
        }

        private float GetDroneOrbitSpeedDegrees(PlayerBuildRuntime buildRuntime, PlayerStatsRuntime stats)
        {
            if (buildRuntime == null || buildRuntime.GetWeaponLevel(WeaponUpgradeId.Drone) <= 0)
            {
                return 0f;
            }

            var baseSpeed = _weaponConfig != null ? Mathf.Max(30f, _weaponConfig.satelliteAngularSpeed) : 220f;
            var attackSpeedScale = stats != null ? Mathf.Max(0.2f, 1f / stats.AttackIntervalMultiplier) : 1f;
            var weaponAttackSpeedScale = 1f + (Mathf.Max(0f, buildRuntime.GetWeaponAttackSpeedBonusPercentTotal(WeaponUpgradeId.Drone)) / 100f);
            return baseSpeed * weaponAttackSpeedScale * attackSpeedScale;
        }

        private float GetAuraVisualRadius(PlayerBuildRuntime buildRuntime, PlayerStatsRuntime stats)
        {
            if (buildRuntime == null || buildRuntime.GetWeaponLevel(WeaponUpgradeId.Aura) <= 0)
            {
                return 0f;
            }

            var baseRadius = _weaponConfig != null ? Mathf.Max(0.2f, _weaponConfig.auraRadius) : 1.5f;
            var attackRangeMultiplier = stats != null ? Mathf.Max(0.1f, stats.AttackRangeMultiplier) : 1f;
            var weaponRangeMultiplier = 1f + (Mathf.Max(0f, buildRuntime.GetWeaponRangeBonusPercentTotal(WeaponUpgradeId.Aura)) / 100f);
            weaponRangeMultiplier *= Mathf.Max(1f, buildRuntime.GetAuraMilestoneRangeMultiplier());
            return Mathf.Max(0.2f, baseRadius * attackRangeMultiplier * weaponRangeMultiplier);
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
    }
}



