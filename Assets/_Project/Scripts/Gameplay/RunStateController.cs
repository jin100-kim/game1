using System;
using System.Collections.Generic;
using EJR.Game.Audio;
using EJR.Game.Core;
using EJR.Game.Multiplayer;
using EJR.Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EJR.Game.Gameplay
{
    public sealed class RunStateController : MonoBehaviour
    {
        private enum PendingChoiceContext
        {
            None = 0,
            StarterWeapon = 1,
            LevelUp = 2,
            WaveAugment = 3,
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

        [Header("Configs (optional, runtime defaults used if empty)")]
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private WeaponConfig weaponConfig;
        [SerializeField] private EnemyConfig enemyConfig;
        [SerializeField] private LevelUpBalanceConfig levelUpBalanceConfig;

        [Header("Run")]
        [SerializeField] private Rect arenaBounds = new Rect(-12f, -7f, 24f, 14f);
        [SerializeField, Min(30f)] private float runDurationSeconds = 600f;

        [Header("Camera")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float cameraFollowSmoothTime = 0.08f;

        [SerializeField, Min(0.02f)] private float hudRefreshInterval = 0.1f;

        [Header("Debug Hotkeys")]
        [SerializeField] private bool enableDebugTimeSkip = true;
        [SerializeField, Min(1)] private int debugGrantLevelsPerPress = 1;
        [SerializeField, Min(1f)] private float debugAdvanceSeconds = 60f;
        [SerializeField, Min(1)] private int debugSkipBossTargetLevel = 40;

        [Header("Debug Auto Play")]
        [SerializeField] private bool enableDebugAutoPlay = true;
        [SerializeField] private bool startWithAutoPlayEnabled;
        [SerializeField, Min(0.05f)] private float autoPlayChoiceDelay = 0.2f;

        [Header("Debug Weapon Gizmos")]
        [SerializeField] private bool showWeaponAimGizmos = true;
        [SerializeField, Min(0.01f)] private float weaponGizmoPointRadius = 0.06f;
        [SerializeField, Min(45f)] private float weaponAimSmoothingDegreesPerSecond = 540f;

        [Header("Weapon Layering")]
        [SerializeField] private int weaponFrontSortingOffset = 1;
        [SerializeField] private int weaponBackSortingOffset = -1;
        [SerializeField, Range(0f, 0.2f)] private float weaponLayerSwapDeadZone = 0.02f;

        private const string PlayerVisualObjectName = "Visual";
        private const string WeaponVisualObjectName = "WeaponVisual";
        private const float WeaponAimFlipEpsilon = 0.01f;
        private PlayerHealth _playerHealth;
        private PlayerStatsRuntime _playerStats;

        private EnemyRegistry _enemyRegistry;
        private ExperienceSystem _experienceSystem;
        private EnemySpawner _enemySpawner;
        private CameraFollow2D _cameraFollow;
        private WorldHealthBar _playerHealthBar;
        private PlayerMover _playerMover;
        private PlayerSpriteAnimator _playerSpriteAnimator;
        private WeaponSpriteAnimator _weaponSpriteAnimator;
        private Transform _weaponVisualTransform;
        private SpriteRenderer _weaponVisualRenderer;
        private SpriteRenderer _playerVisualRenderer;
        private Transform _playerTransform;
        private AutoWeaponSystem _weaponSystem;
        private Vector2 _lastWeaponAimDirection = Vector2.right;
        private Vector2 _targetWeaponAimDirection = Vector2.right;
        private Vector2 _smoothedWeaponAimDirection = Vector2.right;
        private Vector2 _weaponOrbitCenterLocal = Vector2.zero;
        private bool _weaponDrawBehind;

        private PlayerBuildRuntime _buildRuntime;
        private LevelUpSystem _levelUp;
        private HudController _hud;

        private LevelUpOption[] _currentOptions;
        private readonly Queue<PendingChoiceRequest> _pendingChoices = new();
        private PendingChoiceContext _activeChoiceContext;

        private float _remainingSeconds;
        private bool _isGameOver;
        private bool _isPauseMenuOpen;
        private bool _bossWaveTriggered;
        private bool _lastRunCleared;
        private float _nextHudRefreshAt;
        private bool _usingOwnedMultiplayerPlayer;
        private bool _autoPlayEnabled;
        private float _nextAutoPlayChoiceAt;
        private AutoPlayAgent _autoPlayAgent;
        private string _debugRevealBuffer = string.Empty;
        private int _selectedSingleCharacterId;
        private WeaponUpgradeId _selectedSingleStarterWeaponId = WeaponUpgradeId.Rifle;
        private int _enemiesDefeated;
        private readonly RunCombatTracker _combatTracker = new();
        private float _lastObservedPlayerMaxHealth = -1f;
        private RunMapDefinition _currentMapDefinition;
        private RunDifficultyDefinition _currentDifficultyDefinition;

        private const string DebugRevealCode = "admin";

        private void Awake()
        {
            // Keep simulation running even when the game window loses focus.
            Application.runInBackground = true;
            Time.timeScale = 1f;
            EnsureCamera();
            EnsureConfigs();
            ApplySingleRunSelection();
            _remainingSeconds = Mathf.Max(30f, enemyConfig != null ? enemyConfig.bossWaveStartSeconds : runDurationSeconds);
            _bossWaveTriggered = false;
        }

        private void Start()
        {
            BuildRuntimeGraph();
            HookEvents();
            AudioService.Instance.PlayMusic(AudioCueId.MainTheme);
            _nextHudRefreshAt = 0f;
            UpdateHud();
        }

        private void Update()
        {
            CaptureDebugRevealInput();

            if (!_usingOwnedMultiplayerPlayer)
            {
                HandlePauseMenuInput();
            }

            if (_isPauseMenuOpen)
            {
                TryRefreshHud();
                return;
            }

            if (IsBuildDrawerToggleKeyDown())
            {
                _hud?.ToggleBuildDrawer();
            }

            UpdateWeaponAimSmoothing();
            if (!_isGameOver && _playerSpriteAnimator != null && _playerMover != null)
            {
                _playerSpriteAnimator.SetMotion(_playerMover.CurrentVelocity);
                UpdateFacingPresentation();
            }

            if (!_isGameOver && IsAnyChoiceAwaiting() && _currentOptions != null)
            {
                TryHandleAutoPlayChoice();

                var maxOptions = Mathf.Min(_currentOptions.Length, 10);
                for (var optionIndex = 0; optionIndex < maxOptions; optionIndex++)
                {
                    if (!IsOptionKeyDown(optionIndex))
                    {
                        continue;
                    }

                    SelectLevelUpOption(optionIndex);
                    return;
                }
            }

            if (_isGameOver)
            {
                if (IsRestartKeyDown())
                {
                    ReturnToLobby();
                }

                return;
            }

            if (Time.timeScale > 0f)
            {
                if (_playerStats != null && _playerHealth != null && _playerStats.HealthRegenPerSecond > 0f)
                {
                    _playerHealth.Heal(_playerStats.HealthRegenPerSecond * Time.deltaTime);
                }

                if (!_bossWaveTriggered && _enemySpawner != null && _enemySpawner.IsBossWaveTriggered)
                {
                    _remainingSeconds = 0f;
                    _bossWaveTriggered = true;
                }

                if (!_bossWaveTriggered)
                {
                    _remainingSeconds -= Time.deltaTime;
                    if (_remainingSeconds <= 0f)
                    {
                        _remainingSeconds = 0f;
                        TriggerBossWave();
                    }
                }
                else if (_enemySpawner != null && _enemySpawner.IsBossWaveCleared)
                {
                    FinalizeRun(cleared: true);
                    return;
                }
            }

            TryRefreshHud();
        }

        private static bool IsRestartKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.R);
        }

        private static bool IsPauseToggleKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.Escape);
        }

        private static bool IsBuildDrawerToggleKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.Tab);
        }

        private static bool IsOptionKeyDown(int zeroBasedIndex)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return zeroBasedIndex switch
                {
                    0 => keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame,
                    1 => keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame,
                    2 => keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame,
                    3 => keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame,
                    4 => keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame,
                    5 => keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame,
                    6 => keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame,
                    7 => keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame,
                    8 => keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame,
                    9 => keyboard.digit0Key.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame,
                    _ => false,
                };
            }
#endif
            return zeroBasedIndex switch
            {
                0 => Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1),
                1 => Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2),
                2 => Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3),
                3 => Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4),
                4 => Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5),
                5 => Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6),
                6 => Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7),
                7 => Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8),
                8 => Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9),
                9 => Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0),
                _ => false,
            };
        }

        private void OnDestroy()
        {
            if (_autoPlayEnabled && _playerMover != null)
            {
                _playerMover.SetMoveInputReader(null);
            }

            if (_playerHealth != null)
            {
                _playerHealth.Changed -= OnPlayerHealthChanged;
                _playerHealth.Died -= OnPlayerDied;
            }

            if (_weaponSystem != null)
            {
                _weaponSystem.AimUpdated -= OnWeaponAimUpdated;
                _weaponSystem.Fired -= OnWeaponFired;
                _weaponSystem.WeaponSoundRequested -= OnWeaponSoundRequested;
            }

            EnemyController.Defeated -= HandleEnemyDefeated;

            Time.timeScale = 1f;
        }

        private void EnsureCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 6f;
            mainCamera.transform.position = cameraOffset;

            _cameraFollow = mainCamera.GetComponent<CameraFollow2D>();
            if (_cameraFollow == null)
            {
                _cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow2D>();
            }
        }

        private void EnsureConfigs()
        {
            playerConfig ??= ScriptableObject.CreateInstance<PlayerConfig>();
            weaponConfig ??= ScriptableObject.CreateInstance<WeaponConfig>();
            enemyConfig ??= ScriptableObject.CreateInstance<EnemyConfig>();
            levelUpBalanceConfig ??= LevelUpBalanceConfig.CreateRuntimeDefault();
        }

        private void ApplySingleRunSelection()
        {
            _currentMapDefinition = RunSelectionService.SingleMapDefinition;
            _currentDifficultyDefinition = RunSelectionService.SingleDifficultyDefinition;
            enemyConfig = SharedRunCatalog.CreateRuntimeEnemyConfig(
                enemyConfig,
                _currentMapDefinition.Id,
                _currentDifficultyDefinition.Id);
            arenaBounds = _currentMapDefinition.ArenaBounds;
            ApplyArenaPresentation();
        }

        private void ApplyArenaPresentation()
        {
            var mapDefinition = _currentMapDefinition ?? SharedRunCatalog.GetMap(SharedRunCatalog.DefaultMapId);
            ArenaVisualPresenter.Apply(arenaBounds, mapDefinition.CameraBackgroundColor, mapDefinition.BoundaryColor, Camera.main);
        }

        private void CaptureDebugRevealInput()
        {
            if (_hud == null)
            {
                return;
            }

            var typed = Input.inputString;
            if (string.IsNullOrEmpty(typed))
            {
                return;
            }

            for (var i = 0; i < typed.Length; i++)
            {
                var character = typed[i];
                if (character == '\b')
                {
                    if (_debugRevealBuffer.Length > 0)
                    {
                        _debugRevealBuffer = _debugRevealBuffer.Substring(0, _debugRevealBuffer.Length - 1);
                    }

                    continue;
                }

                if (!char.IsLetter(character))
                {
                    if (!char.IsWhiteSpace(character))
                    {
                        _debugRevealBuffer = string.Empty;
                    }

                    continue;
                }

                _debugRevealBuffer += char.ToLowerInvariant(character);
                if (_debugRevealBuffer.Length > DebugRevealCode.Length)
                {
                    _debugRevealBuffer = _debugRevealBuffer.Substring(_debugRevealBuffer.Length - DebugRevealCode.Length);
                }

                if (!string.Equals(_debugRevealBuffer, DebugRevealCode, StringComparison.Ordinal))
                {
                    continue;
                }

                _hud.SetDebugAccessVisible(true);
                _debugRevealBuffer = string.Empty;
                break;
            }
        }

        private void HandlePauseMenuInput()
        {
            if (_isGameOver)
            {
                return;
            }

            if (!_isPauseMenuOpen && IsAnyChoiceAwaiting())
            {
                return;
            }

            if (!IsPauseToggleKeyDown())
            {
                return;
            }

            if (_isPauseMenuOpen)
            {
                ResumeFromPauseMenu();
            }
            else
            {
                OpenPauseMenu();
            }
        }

        private void OpenPauseMenu()
        {
            if (_hud == null || _isPauseMenuOpen)
            {
                return;
            }

            AudioService.Instance.PlayUi(AudioCueId.UiConfirm);
            _isPauseMenuOpen = true;
            Time.timeScale = 0f;
            _hud.ShowPauseMenu(ResumeFromPauseMenu, ReturnToLobbyFromPauseMenu);
        }

        private void ResumeFromPauseMenu()
        {
            if (!_isPauseMenuOpen)
            {
                return;
            }

            AudioService.Instance.PlayUi(AudioCueId.UiBack);
            _isPauseMenuOpen = false;
            _hud?.HidePauseMenu();
            if (!_isGameOver && !IsAnyChoiceAwaiting())
            {
                Time.timeScale = 1f;
            }

            UpdateHud();
        }

        private void ReturnToLobbyFromPauseMenu()
        {
            AudioService.Instance.PlayUi(AudioCueId.UiBack);
            _isPauseMenuOpen = false;
            _hud?.HidePauseMenu();
            Time.timeScale = 1f;
            SceneManager.LoadScene(MultiplayerSessionController.TitleSceneName);
        }

        private void GrantDebugLevels(int levelsToGrant)
        {
            if (_levelUp == null)
            {
                return;
            }

            var grantCount = Mathf.Max(1, levelsToGrant);
            for (var i = 0; i < grantCount; i++)
            {
                var required = Mathf.Max(1, _levelUp.RequiredExperience - _levelUp.CurrentExperience);
                _levelUp.AddExperience(required);
            }

            UpdateHud();
        }

        private void DebugRandomLevelUpToTarget(int targetLevel)
        {
            if (_levelUp == null)
            {
                return;
            }

            var desiredLevel = Mathf.Max(_levelUp.Level, targetLevel);
            var iterationGuard = 0;
            const int maxIterations = 8192;

            while (iterationGuard++ < maxIterations)
            {
                if (_activeChoiceContext == PendingChoiceContext.StarterWeapon)
                {
                    if (_currentOptions == null || _currentOptions.Length <= 0)
                    {
                        break;
                    }

                    SelectLevelUpOption(UnityEngine.Random.Range(0, _currentOptions.Length));
                    continue;
                }

                if (_levelUp.IsAwaitingChoice)
                {
                    if (_currentOptions == null || _currentOptions.Length <= 0)
                    {
                        if (!_levelUp.RerollCurrentChoice())
                        {
                            break;
                        }

                        continue;
                    }

                    SelectLevelUpOption(UnityEngine.Random.Range(0, _currentOptions.Length));
                    continue;
                }

                if (_levelUp.Level >= desiredLevel)
                {
                    break;
                }

                var required = Mathf.Max(1, _levelUp.RequiredExperience - _levelUp.CurrentExperience);
                _levelUp.AddExperience(required);
            }

            UpdateHud();
        }

        private void DebugRerollLevelUpOptions()
        {
            if (_levelUp == null || !_levelUp.IsAwaitingChoice)
            {
                return;
            }

            if (_levelUp.RerollCurrentChoice())
            {
                UpdateHud();
            }
        }

        private void SyncRemainingTimeFromSpawner()
        {
            if (_enemySpawner == null)
            {
                return;
            }

            if (_enemySpawner.IsBossWaveTriggered)
            {
                _bossWaveTriggered = true;
                _remainingSeconds = 0f;
                return;
            }

            _remainingSeconds = Mathf.Max(0f, _enemySpawner.BossWaveStartSeconds - _enemySpawner.ElapsedSeconds);
        }

        private void BuildRuntimeGraph()
        {
            MetaProgressionService.EnsureLoaded();
            _autoPlayAgent = new AutoPlayAgent();
            _buildRuntime = new PlayerBuildRuntime();
            _buildRuntime.InitializeDefaults(grantStarterRifle: false);
            _selectedSingleCharacterId = MetaProgressionService.GetSingleSelectedCharacterId();
            _selectedSingleStarterWeaponId = MetaProgressionService.GetCharacterStarterWeapon(_selectedSingleCharacterId);
            _buildRuntime.ApplyMetaBonuses(MetaProgressionService.GetPurchasedUpgradeBonuses());
            _buildRuntime.ApplyCharacterBaseBonuses(MetaProgressionService.GetCharacterBaseBonuses(_selectedSingleCharacterId));

            _playerStats = new PlayerStatsRuntime();
            _playerStats.RecalculateFromBuild(_buildRuntime);
            _levelUp = new LevelUpSystem();
            _levelUp.Initialize(_buildRuntime, levelUpBalanceConfig, _ => true);
            _hud = new HudController();
            _hud.Initialize();
            _hud.ConfigureDebugTools(
                enableDebugTimeSkip
                    ? () =>
                    {
                        GrantDebugLevels(Mathf.Max(1, debugGrantLevelsPerPress));
                        UpdateHud();
                    }
                    : null,
                enableDebugTimeSkip
                    ? () =>
                    {
                        if (_enemySpawner != null)
                        {
                            _enemySpawner.DebugAdvanceSeconds(debugAdvanceSeconds);
                            SyncRemainingTimeFromSpawner();
                            UpdateHud();
                        }
                    }
                    : null,
                enableDebugTimeSkip ? () => DebugRerollLevelUpOptions() : null,
                enableDebugTimeSkip
                    ? () =>
                    {
                        if (_enemySpawner != null)
                        {
                            _enemySpawner.DebugSkipToBossWave();
                            _bossWaveTriggered = true;
                            _remainingSeconds = 0f;
                        }

                        DebugRandomLevelUpToTarget(debugSkipBossTargetLevel);
                        UpdateHud();
                    }
                    : null,
                () =>
                {
                    SetAutoPlayEnabled(!_autoPlayEnabled);
                    UpdateHud();
                });
            _hud.SetDebugAccessVisible(false);

            var ownedMultiplayerPlayer = MultiplayerPlayerActor.FindOwnedLocalPlayer();
            _usingOwnedMultiplayerPlayer = ownedMultiplayerPlayer != null;

            var player = _usingOwnedMultiplayerPlayer
                ? ownedMultiplayerPlayer.gameObject
                : GameObject.Find("Player");

            if (!_usingOwnedMultiplayerPlayer && player == null)
            {
                player = new GameObject("Player");
                player.transform.position = Vector3.zero;
            }

            var rootRenderer = player.GetComponent<SpriteRenderer>();

            Transform visualTransform;
            SpriteRenderer playerRenderer;
            if (_usingOwnedMultiplayerPlayer)
            {
                if (rootRenderer == null)
                {
                    rootRenderer = player.AddComponent<SpriteRenderer>();
                }

                visualTransform = player.transform;
                playerRenderer = rootRenderer;
                _weaponOrbitCenterLocal = new Vector2(0f, playerConfig.visualYOffset);
            }
            else
            {
                if (rootRenderer != null)
                {
                    Destroy(rootRenderer);
                }

                visualTransform = player.transform.Find(PlayerVisualObjectName);
                if (visualTransform == null)
                {
                    visualTransform = new GameObject(PlayerVisualObjectName).transform;
                    visualTransform.SetParent(player.transform, false);
                }

                visualTransform.localPosition = new Vector3(0f, playerConfig.visualYOffset, 0f);
                _weaponOrbitCenterLocal = new Vector2(visualTransform.localPosition.x, visualTransform.localPosition.y);

                playerRenderer = visualTransform.GetComponent<SpriteRenderer>();
                if (playerRenderer == null)
                {
                    playerRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            var squareSprite = RuntimeSpriteFactory.GetSquareSprite();
            var playerFrames = RuntimeSpriteFactory.GetPlayerAnimationFrames();
            var playerSprite = playerFrames.Length > 0 ? playerFrames[0] : squareSprite;
            var hasPlayerAnimation = playerFrames.Length > 1 && !ReferenceEquals(playerSprite, squareSprite);

            playerRenderer.sprite = playerSprite;
            playerRenderer.color = hasPlayerAnimation ? SharedGameCatalog.GetCharacter(_selectedSingleCharacterId).Color : new Color(0.35f, 0.75f, 1f);
            var visualWorldSize = Mathf.Max(0.1f, playerConfig.visualScale * Mathf.Max(0.1f, playerConfig.visualScaleMultiplier));
            ApplyVisualScale(visualTransform, playerSprite, visualWorldSize);
            _playerTransform = player.transform;
            _playerVisualRenderer = playerRenderer;

            var playerSpriteAnimator = player.GetComponent<PlayerSpriteAnimator>();
            if (hasPlayerAnimation)
            {
                if (playerSpriteAnimator == null)
                {
                    playerSpriteAnimator = player.AddComponent<PlayerSpriteAnimator>();
                }

                playerSpriteAnimator.enabled = true;
                playerSpriteAnimator.Initialize(playerRenderer, playerFrames, playerConfig);
                _playerSpriteAnimator = playerSpriteAnimator;
            }
            else
            {
                if (playerSpriteAnimator != null)
                {
                    playerSpriteAnimator.enabled = false;
                }

                _playerSpriteAnimator = null;
            }

            EnsureWeaponVisual(player.transform, playerRenderer);

            _playerHealth = player.GetComponent<PlayerHealth>();
            if (_playerHealth == null)
            {
                _playerHealth = player.AddComponent<PlayerHealth>();
            }
            _playerHealth.Initialize(GetCurrentMaxHealth(), playerConfig.damageInvulnerabilitySeconds);

            _playerHealthBar = player.GetComponent<WorldHealthBar>();
            if (_playerHealthBar == null)
            {
                _playerHealthBar = player.AddComponent<WorldHealthBar>();
            }

            _playerHealthBar.Initialize(
                new Vector3(0f, 0.82f, 0f),
                1.15f,
                0.14f,
                new Color(0.25f, 0.95f, 0.4f, 0.95f),
                new Color(0f, 0f, 0f, 0.55f),
                25);
            _playerHealthBar.SetHealth(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);

            var playerMover = player.GetComponent<PlayerMover>();
            if (playerMover == null)
            {
                playerMover = player.AddComponent<PlayerMover>();
            }

            _playerMover = playerMover;

            playerMover.Initialize(playerConfig, _playerStats, arenaBounds);
            SetAutoPlayEnabled(startWithAutoPlayEnabled);
            _cameraFollow?.Initialize(player.transform, cameraOffset, cameraFollowSmoothTime);
            ApplyArenaPresentation();

            var systems = new GameObject("Systems");
            _enemyRegistry = systems.AddComponent<EnemyRegistry>();
            _experienceSystem = systems.AddComponent<ExperienceSystem>();
            _experienceSystem.Initialize(player.transform, playerConfig, _levelUp, _playerStats);

            var enemySpawner = systems.AddComponent<EnemySpawner>();
            enemySpawner.Initialize(
                enemyConfig,
                player.transform,
                _playerHealth,
                _enemyRegistry,
                _experienceSystem,
                playerConfig.collisionRadius,
                arenaBounds,
                (_currentMapDefinition ?? RunSelectionService.SingleMapDefinition).BossVisualKind);
            enemySpawner.WaveStarted += HandleWaveStarted;
            enemySpawner.WaveCleared += HandleWaveCleared;
            _enemySpawner = enemySpawner;
            if (enemySpawner.BossWaveStartSeconds > 0f)
            {
                _remainingSeconds = enemySpawner.BossWaveStartSeconds;
            }

            var weaponSystem = systems.AddComponent<AutoWeaponSystem>();
            weaponSystem.Initialize(
                weaponConfig,
                player.transform,
                _enemyRegistry,
                _playerStats,
                _playerHealth,
                ResolveProjectileSpawnPoint,
                projectileSpawnOverride: null,
                projectileCullBounds: arenaBounds,
                facingDirectionResolver: () => _playerMover != null ? _playerMover.CurrentFacingDirection : Vector2.right);
            weaponSystem.ConfigureLoadout(_buildRuntime, _playerStats);
            weaponSystem.AimUpdated += OnWeaponAimUpdated;
            weaponSystem.Fired += OnWeaponFired;
            weaponSystem.WeaponSoundRequested += OnWeaponSoundRequested;
            _weaponSystem = weaponSystem;
            _targetWeaponAimDirection = Vector2.right;
            _smoothedWeaponAimDirection = Vector2.right;
            _buildRuntime.Apply(LevelUpOption.CreateWeaponAcquire(
                _selectedSingleStarterWeaponId,
                $"{SharedGameCatalog.GetWeaponDisplayName(_selectedSingleStarterWeaponId)} 레벨 1",
                "무기 획득",
                SharedGameCatalog.GetWeaponDisplayName(_selectedSingleStarterWeaponId)));
            _combatTracker.Reset();
            RefreshCharacterPassiveBonuses();
            ApplySelectedCharacterPresentation(isDowned: false);
            _enemiesDefeated = 0;
            _pendingChoices.Clear();
            _activeChoiceContext = PendingChoiceContext.None;
            _currentOptions = null;
            _lastObservedPlayerMaxHealth = _playerHealth != null ? _playerHealth.MaxHealth : -1f;
        }

        private void SetAutoPlayEnabled(bool enabled)
        {
            _autoPlayEnabled = enabled && enableDebugAutoPlay;
            if (_playerMover != null)
            {
                _playerMover.SetMoveInputReader(_autoPlayEnabled ? ReadAutoPlayMoveInput : null);
            }

            _nextAutoPlayChoiceAt = Time.unscaledTime + Mathf.Max(0.05f, autoPlayChoiceDelay);
            _hud?.SetDebugAutoPlayState(_autoPlayEnabled);
            UpdateHud();
        }

        private Vector2 ReadAutoPlayMoveInput()
        {
            if (!_autoPlayEnabled
                || _isGameOver
                || _isPauseMenuOpen
                || IsAnyChoiceAwaiting()
                || _playerTransform == null)
            {
                return Vector2.zero;
            }

            var healthRatio = _playerHealth != null && _playerHealth.MaxHealth > 0f
                ? _playerHealth.CurrentHealth / _playerHealth.MaxHealth
                : 1f;

            return _autoPlayAgent != null
                ? _autoPlayAgent.EvaluateMove(_playerTransform.position, arenaBounds, healthRatio, _enemyRegistry, ResolveNearestSinglePlayerOrbPosition)
                : Vector2.zero;
        }

        private Vector3? ResolveNearestSinglePlayerOrbPosition(Vector3 fromPosition)
        {
            var activeOrbs = ExperienceOrb.ActiveOrbs;
            var bestDistanceSq = 9f * 9f;
            Vector3? bestPosition = null;

            for (var i = 0; i < activeOrbs.Count; i++)
            {
                var orb = activeOrbs[i];
                if (orb == null || !orb.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var distanceSq = (orb.transform.position - fromPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                bestPosition = orb.transform.position;
            }

            return bestPosition;
        }

        private void TryHandleAutoPlayChoice()
        {
            if (!_autoPlayEnabled || _currentOptions == null || _currentOptions.Length <= 0)
            {
                return;
            }

            if (Time.unscaledTime < _nextAutoPlayChoiceAt)
            {
                return;
            }

            var selectedIndex = _activeChoiceContext == PendingChoiceContext.StarterWeapon
                ? ChooseAutoPlayStarterWeaponIndex(_currentOptions)
                : ChooseAutoPlayLevelChoiceIndex(_currentOptions);

            _nextAutoPlayChoiceAt = Time.unscaledTime + Mathf.Max(0.05f, autoPlayChoiceDelay);
            SelectLevelUpOption(selectedIndex);
        }

        private int ChooseAutoPlayStarterWeaponIndex(LevelUpOption[] options)
        {
            var bestScore = int.MinValue;
            var bestIndex = 0;

            for (var i = 0; i < options.Length; i++)
            {
                var score = options[i].WeaponId switch
                {
                    WeaponUpgradeId.Rifle => 48,
                    WeaponUpgradeId.BfSword => 47,
                    WeaponUpgradeId.Smg => 44,
                    WeaponUpgradeId.Drone => 43,
                    WeaponUpgradeId.SatelliteBeam => 42,
                    WeaponUpgradeId.RifleTurret => 41,
                    WeaponUpgradeId.Shotgun => 40,
                    WeaponUpgradeId.SniperRifle => 39,
                    WeaponUpgradeId.Aura => 38,
                    WeaponUpgradeId.ChainAttack => 37,
                    WeaponUpgradeId.Katana => 36,
                    _ => 30,
                };

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestIndex = i;
            }

            return bestIndex;
        }

        private int ChooseAutoPlayLevelChoiceIndex(LevelUpOption[] options)
        {
            var healthRatio = _playerHealth != null && _playerHealth.MaxHealth > 0f
                ? _playerHealth.CurrentHealth / _playerHealth.MaxHealth
                : 1f;

            var bestScore = int.MinValue;
            var bestIndex = 0;
            for (var i = 0; i < options.Length; i++)
            {
                var option = options[i];
                var rarityScore = option.Rarity switch
                {
                    OptionRarity.Legendary => 24,
                    OptionRarity.Epic => 16,
                    OptionRarity.Rare => 8,
                    OptionRarity.Special => 20,
                    _ => 0,
                };

                var score = option.Domain switch
                {
                    LevelUpOptionDomain.Augment => option.AugmentId switch
                    {
                        RunAugmentId.Berserk => 58,
                        RunAugmentId.Overclock => 56,
                        RunAugmentId.LongReach => 50,
                        RunAugmentId.Fleetfoot => 46,
                        RunAugmentId.VitalCore => 44 + Mathf.RoundToInt((1f - healthRatio) * 18f),
                        _ => 40,
                    },
                    LevelUpOptionDomain.WeaponMilestone => 64 + option.NextLevel,
                    LevelUpOptionDomain.WeaponAcquire => 48,
                    LevelUpOptionDomain.WeaponLevelRoll => 40 + option.NextLevel + rarityScore + (option.WeaponRollKind switch
                    {
                        WeaponRollKind.DamagePercent => 8,
                        WeaponRollKind.AttackSpeedPercent => 7,
                        WeaponRollKind.RangePercent => 4,
                        _ => 0,
                    }),
                    LevelUpOptionDomain.GlobalStatRoll => option.StatId switch
                    {
                        StatUpgradeId.AttackPower => 26 + rarityScore,
                        StatUpgradeId.AttackSpeed => 24 + rarityScore,
                        StatUpgradeId.AttackRange => 18 + rarityScore,
                        StatUpgradeId.MaxHealth => 14 + rarityScore + Mathf.RoundToInt((1f - healthRatio) * 18f),
                        StatUpgradeId.HealthRegen => 12 + rarityScore + Mathf.RoundToInt((1f - healthRatio) * 10f),
                        StatUpgradeId.MoveSpeed => 10 + rarityScore,
                        StatUpgradeId.Luck => 8 + rarityScore,
                        _ => 10,
                    },
                    _ => 0,
                };

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestIndex = i;
            }

            return bestIndex;
        }


        private void EnsureWeaponVisual(Transform playerTransform, SpriteRenderer playerRenderer)
        {
            if (playerTransform == null || playerRenderer == null)
            {
                _weaponSpriteAnimator = null;
                _weaponVisualTransform = null;
                _weaponVisualRenderer = null;
                return;
            }

            var weaponTransform = playerTransform.Find(WeaponVisualObjectName);
            if (weaponTransform == null)
            {
                weaponTransform = new GameObject(WeaponVisualObjectName).transform;
                weaponTransform.SetParent(playerTransform, false);
            }

            var weaponRenderer = weaponTransform.GetComponent<SpriteRenderer>();
            if (weaponRenderer == null)
            {
                weaponRenderer = weaponTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            _weaponVisualTransform = weaponTransform;
            _weaponVisualRenderer = weaponRenderer;
            _weaponDrawBehind = false;

            weaponRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            weaponRenderer.sortingOrder = playerRenderer.sortingOrder + weaponFrontSortingOffset;

            var squareSprite = RuntimeSpriteFactory.GetSquareSprite();
            var weaponFrames = RuntimeSpriteFactory.GetSexyBfSwordAnimationFrames();
            var weaponSprite = weaponFrames.Length > 0 ? weaponFrames[0] : squareSprite;

            weaponRenderer.sprite = weaponSprite;
            weaponRenderer.color = Color.white;

            var weaponVisualSize = weaponConfig != null ? Mathf.Max(0.05f, weaponConfig.bfSwordVisualScale) : 0.95f;
            ApplyVisualScale(weaponTransform, weaponSprite, weaponVisualSize);
            RefreshHeldWeaponVisualScale();
            ApplyHeldWeaponFacing(_playerMover != null ? _playerMover.CurrentFacingDirection : Vector2.right);

            var weaponAnimator = playerTransform.GetComponent<WeaponSpriteAnimator>();
            if (weaponAnimator != null)
            {
                weaponAnimator.enabled = false;
            }

            _weaponSpriteAnimator = null;
        }

        private void EnsureArenaBoundaryVisual()
        {
            ApplyArenaPresentation();
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

        private void ApplyBfSwordVisualWidthScale(Transform targetTransform)
        {
            if (targetTransform == null)
            {
                return;
            }

            var widthMultiplier = weaponConfig != null ? Mathf.Max(0.05f, weaponConfig.bfSwordVisualWidthMultiplier) : 0.5f;
            if (_buildRuntime != null)
            {
                widthMultiplier *= Mathf.Max(1f, _buildRuntime.GetBfSwordWidthMultiplier());
            }

            var localScale = targetTransform.localScale;
            targetTransform.localScale = new Vector3(localScale.x, localScale.y * widthMultiplier, localScale.z);
        }

        private void RefreshHeldWeaponVisualScale()
        {
            if (_weaponVisualTransform == null || _weaponVisualRenderer == null)
            {
                return;
            }

            var visualScale = weaponConfig != null ? Mathf.Max(0.05f, weaponConfig.bfSwordVisualScale) : 0.95f;
            var lengthMultiplier = _playerStats != null ? Mathf.Max(0.1f, _playerStats.AttackRangeMultiplier) : 1f;
            if (_buildRuntime != null)
            {
                lengthMultiplier *= 1f + (Mathf.Max(0f, _buildRuntime.GetWeaponRangeBonusPercentTotal(WeaponUpgradeId.BfSword)) / 100f);
                lengthMultiplier *= Mathf.Max(1f, _buildRuntime.GetBfSwordLengthMultiplier());
            }

            ApplyVisualScale(_weaponVisualTransform, _weaponVisualRenderer.sprite, visualScale * lengthMultiplier);
            ApplyBfSwordVisualWidthScale(_weaponVisualTransform);
        }

        private void OnWeaponAimUpdated(Vector2 direction)
        {
            _targetWeaponAimDirection = NormalizeAimDirection(direction, _targetWeaponAimDirection);
        }

        private void OnWeaponFired(Vector2 direction)
        {
            var normalized = NormalizeAimDirection(direction, _targetWeaponAimDirection);
            _targetWeaponAimDirection = normalized;
            _smoothedWeaponAimDirection = normalized;
            _lastWeaponAimDirection = normalized;
        }

        private void UpdateFacingPresentation()
        {
            if (_playerMover == null)
            {
                return;
            }

            var facingDirection = NormalizeAimDirection(_playerMover.CurrentFacingDirection, Vector2.right);
            _playerSpriteAnimator?.SetLookDirection(facingDirection);
            ApplyHeldWeaponFacing(facingDirection);
        }

        private void ApplyHeldWeaponFacing(Vector2 direction)
        {
            if (_weaponVisualTransform == null || _weaponVisualRenderer == null)
            {
                return;
            }

            var normalizedDirection = NormalizeAimDirection(direction, Vector2.right);
            var flipX = ResolveWeaponFlipX(normalizedDirection);
            var rotationDegrees = CalculateWeaponRotationDegrees(normalizedDirection, flipX);
            var localPosition = CalculateHeldWeaponLocalPosition(_playerTransform, normalizedDirection, flipX, rotationDegrees);
            _weaponVisualTransform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            _weaponVisualRenderer.flipX = flipX;
            UpdateWeaponSorting(normalizedDirection);
            _weaponVisualTransform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        }

        private Vector3 ResolveProjectileSpawnPoint(Vector2 aimDirection)
        {
            if (_playerTransform == null)
            {
                return Vector3.zero;
            }

            var normalizedDirection = NormalizeAimDirection(aimDirection, _lastWeaponAimDirection);
            var localPosition = CalculateProjectileSpawnLocalPosition(_playerTransform, normalizedDirection);
            return _playerTransform.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0f));
        }

        private Vector2 CalculateHeldWeaponLocalPosition(Transform playerRoot, Vector2 normalizedDirection, bool flipX, float rotationDegrees)
        {
            var weaponOffset = weaponConfig != null ? weaponConfig.bfSwordVisualLocalOffset : new Vector2(0f, -0.08f);
            var aimDistance = weaponConfig != null ? Mathf.Max(0f, weaponConfig.bfSwordForwardOffset) : 0.48f;
            var orbitCenterLocal = ResolveWeaponOrbitCenterLocal(playerRoot);
            var sprite = _weaponVisualRenderer != null ? _weaponVisualRenderer.sprite : null;
            return WeaponVisualLayoutUtility.CalculateWeaponLocalPosition(
                orbitCenterLocal,
                normalizedDirection,
                aimDistance,
                weaponOffset,
                flipX,
                rotationDegrees,
                sprite);
        }

        private Vector2 CalculateProjectileSpawnLocalPosition(Transform playerRoot, Vector2 normalizedDirection)
        {
            var orbitCenterLocal = ResolveWeaponOrbitCenterLocal(playerRoot);
            var aimDistance = playerConfig != null ? Mathf.Max(0.05f, playerConfig.weaponAimDistance) : 0.4f;
            return orbitCenterLocal + (normalizedDirection * aimDistance);
        }

        private bool ResolveWeaponFlipX(Vector2 normalizedDirection)
        {
            var previousFlip = _weaponVisualRenderer != null && _weaponVisualRenderer.flipX;
            if (normalizedDirection.x > WeaponAimFlipEpsilon)
            {
                return false;
            }

            if (normalizedDirection.x < -WeaponAimFlipEpsilon)
            {
                return true;
            }

            return previousFlip;
        }

        private float CalculateWeaponRotationDegrees(Vector2 normalizedDirection, bool flipX)
        {
            // Base authored direction is 3 o'clock. Mirrored side must reverse signed angle.
            var signedAngleFromHorizontal = Mathf.Atan2(normalizedDirection.y, Mathf.Abs(normalizedDirection.x)) * Mathf.Rad2Deg;
            if (flipX)
            {
                signedAngleFromHorizontal = -signedAngleFromHorizontal;
            }

            var rotationOffset = playerConfig != null ? playerConfig.weaponAimRotationOffsetDegrees : 0f;
            return signedAngleFromHorizontal + rotationOffset;
        }

        private void UpdateWeaponSorting(Vector2 aimDirection)
        {
            if (_weaponVisualRenderer == null || _playerVisualRenderer == null)
            {
                return;
            }

            var deadZone = Mathf.Max(0f, weaponLayerSwapDeadZone);
            if (aimDirection.y > deadZone)
            {
                _weaponDrawBehind = true;
            }
            else if (aimDirection.y < -deadZone)
            {
                _weaponDrawBehind = false;
            }

            var offset = _weaponDrawBehind ? weaponBackSortingOffset : weaponFrontSortingOffset;
            _weaponVisualRenderer.sortingLayerID = _playerVisualRenderer.sortingLayerID;
            _weaponVisualRenderer.sortingOrder = _playerVisualRenderer.sortingOrder + offset;
        }

        private Vector2 ResolveWeaponOrbitCenterLocal(Transform playerRoot)
        {
            if (playerRoot != null)
            {
                var visual = playerRoot.Find(PlayerVisualObjectName);
                if (visual != null)
                {
                    var visualRenderer = visual.GetComponent<SpriteRenderer>();
                    if (visualRenderer != null)
                    {
                        // Use the rendered sprite center so orbit/gizmo center overlaps the visible character.
                        var worldCenter = visualRenderer.bounds.center;
                        var localCenter = playerRoot.InverseTransformPoint(worldCenter);
                        _weaponOrbitCenterLocal = new Vector2(localCenter.x, localCenter.y);
                        return _weaponOrbitCenterLocal;
                    }

                    _weaponOrbitCenterLocal = new Vector2(visual.localPosition.x, visual.localPosition.y);
                    return _weaponOrbitCenterLocal;
                }
            }

            if (playerConfig != null)
            {
                _weaponOrbitCenterLocal = new Vector2(0f, playerConfig.visualYOffset);
            }

            return _weaponOrbitCenterLocal;
        }

        private static Vector2 NormalizeAimDirection(Vector2 direction, Vector2 fallbackDirection)
        {
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return fallbackDirection.sqrMagnitude > 0.000001f ? fallbackDirection.normalized : Vector2.right;
            }

            return direction.normalized;
        }

        private void UpdateWeaponAimSmoothing()
        {
            var from = NormalizeAimDirection(_smoothedWeaponAimDirection, _lastWeaponAimDirection);
            var to = NormalizeAimDirection(_targetWeaponAimDirection, from);
            var maxRadiansDelta = Mathf.Max(1f, weaponAimSmoothingDegreesPerSecond) * Mathf.Deg2Rad * Time.deltaTime;
            var next3 = Vector3.RotateTowards(
                new Vector3(from.x, from.y, 0f),
                new Vector3(to.x, to.y, 0f),
                maxRadiansDelta,
                0f);
            var next = new Vector2(next3.x, next3.y);
            _smoothedWeaponAimDirection = NormalizeAimDirection(next, to);
            _lastWeaponAimDirection = _smoothedWeaponAimDirection;
        }

        private void OnDrawGizmos()
        {
            if (!showWeaponAimGizmos)
            {
                return;
            }

            var player = _playerTransform;
            if (player == null)
            {
                var playerObject = GameObject.Find("Player");
                player = playerObject != null ? playerObject.transform : null;
            }

            if (player == null)
            {
                return;
            }

            var orbitCenterLocal = ResolveWeaponOrbitCenterLocal(player);
            var aimDirection = NormalizeAimDirection(_lastWeaponAimDirection, Vector2.right);
            var aimDistance = playerConfig != null ? Mathf.Max(0.05f, playerConfig.weaponAimDistance) : 0.55f;
            var flipX = ResolveWeaponFlipX(aimDirection);
            var rotationDegrees = CalculateWeaponRotationDegrees(aimDirection, flipX);
            var weaponLocal = CalculateProjectileSpawnLocalPosition(player, aimDirection);

            var orbitCenterWorld = player.TransformPoint(new Vector3(orbitCenterLocal.x, orbitCenterLocal.y, 0f));
            var radiusEndWorld = player.TransformPoint(new Vector3(
                orbitCenterLocal.x + aimDirection.x * aimDistance,
                orbitCenterLocal.y + aimDirection.y * aimDistance,
                0f));
            var weaponWorld = _weaponVisualTransform != null
                ? _weaponVisualTransform.position
                : player.TransformPoint(new Vector3(weaponLocal.x, weaponLocal.y, 0f));

            var pointRadius = Mathf.Max(0.01f, weaponGizmoPointRadius);

            Gizmos.color = new Color(0.1f, 0.95f, 1f, 0.95f);
            Gizmos.DrawSphere(orbitCenterWorld, pointRadius);
            Gizmos.DrawWireSphere(orbitCenterWorld, aimDistance);

            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.95f);
            Gizmos.DrawLine(orbitCenterWorld, radiusEndWorld);

            Gizmos.color = new Color(1f, 0.3f, 0.9f, 0.95f);
            Gizmos.DrawLine(radiusEndWorld, weaponWorld);

            Gizmos.color = new Color(1f, 1f, 1f, 0.95f);
            Gizmos.DrawSphere(weaponWorld, pointRadius * 0.9f);
            Gizmos.DrawLine(orbitCenterWorld, weaponWorld);

            var projectileSpawnWorld = ResolveProjectileSpawnPoint(aimDirection);
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.95f);
            Gizmos.DrawSphere(projectileSpawnWorld, pointRadius * 0.85f);
            Gizmos.DrawLine(weaponWorld, projectileSpawnWorld);

        }

        private void DrawWeaponSpriteRectGizmo(Transform playerRoot)
        {
            if (playerRoot == null)
            {
                return;
            }

            var weaponTransform = _weaponVisualTransform;
            if (weaponTransform == null)
            {
                weaponTransform = playerRoot.Find(WeaponVisualObjectName);
            }

            if (weaponTransform == null)
            {
                return;
            }

            var weaponRenderer = _weaponVisualRenderer;
            if (weaponRenderer == null)
            {
                weaponRenderer = weaponTransform.GetComponent<SpriteRenderer>();
            }

            if (weaponRenderer == null || weaponRenderer.sprite == null)
            {
                return;
            }

            var sprite = weaponRenderer.sprite;
            var pixelsPerUnit = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
            var rect = sprite.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            // Use sprite rect dimensions (includes transparent pixels inside the sprite frame).
            var size = new Vector2(rect.width / pixelsPerUnit, rect.height / pixelsPerUnit);
            var pivotNormalized = new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);

            var bottomLeft = new Vector2(-pivotNormalized.x * size.x, -pivotNormalized.y * size.y);
            var bottomRight = bottomLeft + new Vector2(size.x, 0f);
            var topRight = bottomLeft + size;
            var topLeft = bottomLeft + new Vector2(0f, size.y);

            bottomLeft = ApplySpriteFlip(bottomLeft, weaponRenderer);
            bottomRight = ApplySpriteFlip(bottomRight, weaponRenderer);
            topRight = ApplySpriteFlip(topRight, weaponRenderer);
            topLeft = ApplySpriteFlip(topLeft, weaponRenderer);

            var p0 = weaponTransform.TransformPoint(new Vector3(bottomLeft.x, bottomLeft.y, 0f));
            var p1 = weaponTransform.TransformPoint(new Vector3(bottomRight.x, bottomRight.y, 0f));
            var p2 = weaponTransform.TransformPoint(new Vector3(topRight.x, topRight.y, 0f));
            var p3 = weaponTransform.TransformPoint(new Vector3(topLeft.x, topLeft.y, 0f));

            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.95f);
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);
        }

        private static Vector2 ApplySpriteFlip(Vector2 point, SpriteRenderer renderer)
        {
            var x = renderer.flipX ? -point.x : point.x;
            var y = renderer.flipY ? -point.y : point.y;
            return new Vector2(x, y);
        }

        private void HookEvents()
        {
            _playerHealth.Changed += OnPlayerHealthChanged;
            _playerHealth.Died += OnPlayerDied;
            _playerHealth.Damaged += OnPlayerDamaged;
            _playerHealth.Healed += OnPlayerHealed;
            _levelUp.ExperienceChanged += OnExperienceChanged;
            _levelUp.OptionsGenerated += OnLevelUpRequested;
            EnemyController.Defeated += HandleEnemyDefeated;
            EnemyController.Damaged += HandleEnemyDamaged;
        }

        private void OnWeaponSoundRequested(WeaponSoundRequest request)
        {
            AudioService.Instance.PlayWeaponSound(request);
        }

        private void OnPlayerHealthChanged(float currentHealth, float maxHealth)
        {
            _playerHealthBar?.SetHealth(currentHealth, maxHealth);
            if (!Mathf.Approximately(_lastObservedPlayerMaxHealth, maxHealth))
            {
                _lastObservedPlayerMaxHealth = maxHealth;
                RefreshCharacterPassiveBonuses();
            }

            UpdateHud();
        }

        private void OnPlayerDamaged(float damage)
        {
            _combatTracker.RecordDamageTaken(damage);
        }

        private void OnPlayerHealed(float amount)
        {
            _combatTracker.RecordHealing(amount);
        }

        private void OnExperienceChanged(int currentExperience, int requiredExperience, int level)
        {
            RefreshCharacterPassiveBonuses();
            UpdateHud();
        }

        private void OnPlayerDied()
        {
            _playerSpriteAnimator?.PlayDie();
            ApplySelectedCharacterPresentation(isDowned: true);
            FinalizeRun(cleared: false);
        }

        private void HandleEnemyDefeated(EnemyController enemy)
        {
            if (enemy == null || _isGameOver)
            {
                return;
            }

            _enemiesDefeated++;
        }

        private void HandleEnemyDamaged(EnemyController enemy, WeaponUpgradeId weaponId, float damage)
        {
            if (_isGameOver || enemy == null)
            {
                return;
            }

            _combatTracker.RecordDamageDealt(weaponId, damage, enemy.IsBoss, enemy.CurrentHealth, enemy.MaxHealth);
        }

        private void HandleWaveStarted(int waveIndex)
        {
            _hud?.ShowWaveBanner($"웨이브 {waveIndex} 시작\n보상: 증강 선택");
            UpdateHud();
        }

        private void HandleWaveCleared(int waveIndex)
        {
            if (_isGameOver || _buildRuntime == null)
            {
                return;
            }

            _hud?.ShowWaveBanner("웨이브 정리 완료");
            var options = SharedAugmentCatalog.BuildRandomOptions(_buildRuntime.ActiveAugments);
            if (options.Length > 0)
            {
                EnqueueChoice(PendingChoiceContext.WaveAugment, options, $"웨이브 {waveIndex} 보상 - 증강 선택");
            }

            UpdateHud();
        }

        private void OnLevelUpRequested(LevelUpOption[] options)
        {
            if (_isGameOver)
            {
                return;
            }

            EnqueueChoice(PendingChoiceContext.LevelUp, options, "레벨 업 - 하나 선택");
        }

        private void SelectLevelUpOption(int optionIndex)
        {
            if (_currentOptions == null || _currentOptions.Length <= 0)
            {
                return;
            }

            AudioService.Instance.PlaySfx(AudioCueId.LevelUpSelect);
            optionIndex = Mathf.Clamp(optionIndex, 0, _currentOptions.Length - 1);
            var currentContext = _activeChoiceContext;
            var currentOptions = _currentOptions;

            _currentOptions = null;
            _activeChoiceContext = PendingChoiceContext.None;
            _hud.HideLevelUpOptions();

            switch (currentContext)
            {
                case PendingChoiceContext.StarterWeapon:
                case PendingChoiceContext.WaveAugment:
                    if (_buildRuntime == null)
                    {
                        return;
                    }

                    _buildRuntime.Apply(currentOptions[optionIndex]);
                    RefreshCharacterPassiveBonuses();
                    break;

                case PendingChoiceContext.LevelUp:
                    if (_levelUp == null)
                    {
                        return;
                    }

                    _levelUp.ApplyOption(optionIndex, currentOptions);
                    RefreshCharacterPassiveBonuses();
                    break;
            }

            if (_isGameOver)
            {
                return;
            }

            if (_pendingChoices.Count > 0)
            {
                TryOpenNextQueuedChoice();
                return;
            }

            if (Time.timeScale <= 0f)
            {
                Time.timeScale = 1f;
            }

            UpdateHud();
        }

        private void BeginStarterWeaponChoiceIfNeeded()
        {
            if (_buildRuntime == null || _hud == null || _buildRuntime.OwnedWeapons.Count > 0)
            {
                return;
            }

            var options = CreateStarterWeaponOptions();
            if (options.Length <= 0)
            {
                return;
            }

            EnqueueChoice(PendingChoiceContext.StarterWeapon, options, "시작 무기 선택");
        }

        private LevelUpOption[] CreateStarterWeaponOptions()
        {
            var options = new LevelUpOption[SharedGameCatalog.StarterWeaponCount];
            for (var i = 0; i < SharedGameCatalog.StarterWeaponCount; i++)
            {
                var weaponId = SharedGameCatalog.GetStarterWeaponByIndex(i);
                var title = $"시작: {GetWeaponDisplayName(weaponId)} 레벨 1";
                options[i] = LevelUpOption.CreateWeaponAcquire(
                    weaponId,
                    title,
                    "무기 획득",
                    $"{title}\n무기 획득");
            }

            return options;
        }

        private void EnqueueChoice(PendingChoiceContext context, LevelUpOption[] options, string title)
        {
            if (options == null || options.Length <= 0)
            {
                return;
            }

            _pendingChoices.Enqueue(new PendingChoiceRequest(context, options, title));
            TryOpenNextQueuedChoice();
        }

        private void TryOpenNextQueuedChoice()
        {
            if (_isGameOver || _hud == null || _activeChoiceContext != PendingChoiceContext.None || _pendingChoices.Count <= 0)
            {
                return;
            }

            var nextChoice = _pendingChoices.Dequeue();
            _activeChoiceContext = nextChoice.Context;
            _currentOptions = nextChoice.Options;
            Time.timeScale = 0f;
            AudioService.Instance.PlaySfx(AudioCueId.LevelUpAppear);
            _hud.ShowLevelUpOptions(nextChoice.Options, SelectLevelUpOption, nextChoice.Title);
            UpdateHud();
        }

        private void FinalizeRun(bool cleared)
        {
            if (_isGameOver)
            {
                return;
            }

            _isGameOver = true;
            _lastRunCleared = cleared;
            _isPauseMenuOpen = false;
            _pendingChoices.Clear();
            _activeChoiceContext = PendingChoiceContext.None;
            _currentOptions = null;
            Time.timeScale = 0f;
#if false
            MetaProgressionService.RecordRunSummary(MetaProgressionService.BuildRunRewardSummary(
                "싱글",
                cleared,
                _levelUp != null ? _levelUp.Level : 1,
                _enemySpawner != null ? _enemySpawner.ElapsedSeconds : 0f,
                _enemiesDefeated,
                _bossWaveTriggered));
            _hud.HideLevelUpOptions();
            _hud.HidePauseMenu();
            _hud.HideBossBar();
            _hud.ShowResult(
                cleared,
                ReturnToLobby,
                "타이틀로");
        }

#endif
            var mapDefinition = _currentMapDefinition ?? RunSelectionService.SingleMapDefinition;
            var difficultyDefinition = _currentDifficultyDefinition ?? RunSelectionService.SingleDifficultyDefinition;
            var summary = MetaProgressionService.BuildRunRewardSummary(
                "\uC2F1\uAE00",
                cleared,
                _levelUp != null ? _levelUp.Level : 1,
                _enemySpawner != null ? _enemySpawner.ElapsedSeconds : 0f,
                _enemiesDefeated,
                _combatTracker.BuildSummary(),
                _combatTracker.BossThresholdsReached,
                mapDefinition.Id,
                mapDefinition.DisplayName,
                difficultyDefinition.DisplayName,
                _playerStats != null ? _playerStats.CreditGainPercent : 0f);

            MetaProgressionService.RecordRunSummary(summary);
            _hud.HideLevelUpOptions();
            _hud.HidePauseMenu();
            _hud.HideBossBar();
            _hud.HideWaveStatus();
            _hud.HideWaveBanner();
            _hud.ShowResult(summary, ReturnToLobby, "\uD0C0\uC774\uD2C0\uB85C");
        }

        private void TriggerBossWave()
        {
            if (_bossWaveTriggered)
            {
                return;
            }

            AudioService.Instance.PlaySfx(AudioCueId.BossWarning);
            _bossWaveTriggered = true;
            _enemySpawner?.TriggerBossWave();
        }

        private void RestartRun()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ReturnToLobby()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MultiplayerSessionController.TitleSceneName);
        }

        private void UpdateHud()
        {
            if (_hud == null || _playerHealth == null || _levelUp == null)
            {
                return;
            }

            _hud.SetTopBar(
                _playerHealth.CurrentHealth,
                _playerHealth.MaxHealth,
                _levelUp.Level,
                _levelUp.CurrentExperience,
                _levelUp.RequiredExperience,
                _remainingSeconds);

            _hud.SetModeHint(string.Empty);
            _hud.SetBuildInfo(BuildWeaponSummary(), BuildStatSummary());
            if (_enemySpawner != null && _enemySpawner.HasActiveWave)
            {
                _hud.SetWaveStatus(_enemySpawner.ActiveWaveIndex, _enemySpawner.ActiveWaveRemainingCount);
            }
            else
            {
                _hud.HideWaveStatus();
            }
            UpdateBossHud();
        }

        private void UpdateBossHud()
        {
            if (_hud == null || _enemySpawner == null || !_enemySpawner.IsBossWaveTriggered)
            {
                _hud?.HideBossBar();
                return;
            }

            var boss = _enemySpawner.CurrentBoss;
            if (boss == null)
            {
                _hud.HideBossBar();
                return;
            }

            _hud.SetBossBar(boss.CurrentHealth, boss.MaxHealth, "보스");
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

            UpdateWeaponVisualActivation();

            if (_playerHealth != null)
            {
                _playerHealth.SetMaxHealth(GetCurrentMaxHealth(), healDelta: true);
            }

            if (_playerMover != null)
            {
                _playerMover.SetMoveSpeedMultiplier(_playerStats.MoveSpeedMultiplier);
            }
        }

        private void RefreshCharacterPassiveBonuses()
        {
            if (_buildRuntime == null)
            {
                return;
            }

            var passiveId = MetaProgressionService.GetCharacterPassiveId(_selectedSingleCharacterId);
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
                    var baseMaxHealth = Mathf.Max(1f, playerConfig != null ? playerConfig.maxHealth : 100f);
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

            _buildRuntime.ApplyCharacterDynamicBonuses(dynamicBonuses);
            _buildRuntime.SetChainAttackModifiers(ignoreChainDecay, bonusChains);
            ApplyBuildToRuntimeSystems();
        }

        private float GetCurrentMaxHealth()
        {
            var baseMaxHealth = playerConfig != null ? Mathf.Max(1f, playerConfig.maxHealth) : 100f;
            var bonus = _playerStats != null ? Mathf.Max(0f, _playerStats.MaxHealthBonus) : 0f;
            return baseMaxHealth + bonus;
        }

        private string BuildWeaponSummary()
        {
            if (_buildRuntime == null || _levelUp == null)
            {
                return $"무기\n1) 비어 있음\n2) 잠김 (레벨 {PlayerBuildRuntime.SecondWeaponUnlockLevel})\n3) 잠김 (레벨 {PlayerBuildRuntime.ThirdWeaponUnlockLevel})";
            }

            var unlockedSlots = _buildRuntime.GetUnlockedWeaponSlots(_levelUp.Level);
            var lines = "무기";

            for (var slotIndex = 0; slotIndex < PlayerBuildRuntime.MaxWeaponSlotsAbsolute; slotIndex++)
            {
                var slotNumber = slotIndex + 1;
                if (slotIndex >= unlockedSlots)
                {
                    var requiredLevel = slotIndex == 1
                        ? PlayerBuildRuntime.SecondWeaponUnlockLevel
                        : PlayerBuildRuntime.ThirdWeaponUnlockLevel;
                    lines += $"\n{slotNumber}) 잠김 (레벨 {requiredLevel})";
                    continue;
                }

                if (slotIndex < _buildRuntime.OwnedWeapons.Count)
                {
                    var weaponId = _buildRuntime.OwnedWeapons[slotIndex];
                    var level = _buildRuntime.GetWeaponLevel(weaponId);
                    var damageBonus = _buildRuntime.GetWeaponDamageBonusPercentTotal(weaponId);
                    var attackSpeedBonus = _buildRuntime.GetWeaponAttackSpeedBonusPercentTotal(weaponId);
                    var rangeBonus = _buildRuntime.GetWeaponRangeBonusPercentTotal(weaponId);
                    var milestoneCount = _buildRuntime.GetWeaponMilestoneCount(weaponId);
                    var bonusSummary = $" [피해량+{damageBonus:0.#} 공속+{attackSpeedBonus:0.#} 범위+{rangeBonus:0.#}";
                    if (milestoneCount > 0)
                    {
                        bonusSummary += $" 특수+{milestoneCount}";
                    }

                    bonusSummary += "]";
                    lines += $"\n{slotNumber}) {GetWeaponDisplayName(weaponId)} 레벨 {level}{bonusSummary}";
                }
                else
                {
                    lines += $"\n{slotNumber}) 비어 있음";
                }
            }

            return lines;
        }

        private string BuildStatSummary()
        {
            if (_buildRuntime == null)
            {
                return "전역 능력치";
            }

            var lines = "전역 능력치";
            lines += $"\n피해량 +{_buildRuntime.GlobalAttackPowerPercentTotal:0.#}%";
            lines += $"\n공격 속도 +{_buildRuntime.GlobalAttackSpeedPercentTotal:0.#}%";
            lines += $"\n최대 체력 +{_buildRuntime.GlobalMaxHealthFlatTotal:0}";
            lines += $"\n체력 재생 +{_buildRuntime.GlobalHealthRegenPerSecondTotal:0.##}/초";
            lines += $"\n이동 속도 +{_buildRuntime.GlobalMoveSpeedPercentTotal:0.#}%";
            lines += $"\n공격 범위 +{_buildRuntime.GlobalAttackRangePercentTotal:0.#}%";
            lines += $"\n행운 {_buildRuntime.GlobalLuckTotal:0}";
            if (!Mathf.Approximately(_buildRuntime.GlobalExperienceGainPercentTotal, 0f))
            {
                lines += $"\nXP +{_buildRuntime.GlobalExperienceGainPercentTotal:0.#}%";
            }

            if (!Mathf.Approximately(_buildRuntime.GlobalCreditGainPercentTotal, 0f))
            {
                lines += $"\n코인 +{_buildRuntime.GlobalCreditGainPercentTotal:0.#}%";
            }

            return lines;
        }

        private static string GetWeaponDisplayName(WeaponUpgradeId weaponId)
        {
            return SharedGameCatalog.GetWeaponDisplayName(weaponId);
        }

        private static string GetStatDisplayName(StatUpgradeId statId)
        {
            return SharedGameCatalog.GetStatDisplayName(statId);
        }

        private void ApplySelectedCharacterPresentation(bool isDowned)
        {
            if (_playerVisualRenderer == null)
            {
                return;
            }

            var color = SharedGameCatalog.GetCharacter(_selectedSingleCharacterId).Color;
            color.a = isDowned ? 0.35f : 1f;
            _playerVisualRenderer.color = color;
            _playerSpriteAnimator?.SetBaseColor(color);
        }

        private void TryRefreshHud()
        {
            if (Time.unscaledTime < _nextHudRefreshAt)
            {
                return;
            }

            _nextHudRefreshAt = Time.unscaledTime + Mathf.Max(0.02f, hudRefreshInterval);
            UpdateHud();
        }

        private bool IsAnyChoiceAwaiting()
        {
            return _activeChoiceContext != PendingChoiceContext.None || _pendingChoices.Count > 0;
        }

        private void UpdateWeaponVisualActivation()
        {
            if (_weaponVisualRenderer == null || _buildRuntime == null)
            {
                return;
            }

            var showHeldWeapon = _buildRuntime.HasWeapon(WeaponUpgradeId.BfSword);
            _weaponVisualRenderer.enabled = showHeldWeapon;
            if (_weaponSpriteAnimator != null)
            {
                _weaponSpriteAnimator.enabled = false;
            }

            if (showHeldWeapon)
            {
                RefreshHeldWeaponVisualScale();
                ApplyHeldWeaponFacing(_playerMover != null ? _playerMover.CurrentFacingDirection : Vector2.right);
            }
        }
    }
}

