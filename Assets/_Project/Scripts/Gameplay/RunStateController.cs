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
        [Header("Configs (optional, runtime defaults used if empty)")]
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private WeaponConfig weaponConfig;
        [SerializeField] private EnemyConfig enemyConfig;

        [Header("Run")]
        [SerializeField] private Rect arenaBounds = new Rect(-12f, -7f, 24f, 14f);
        [SerializeField, Min(30f)] private float runDurationSeconds = 600f;

        [Header("Camera")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float cameraFollowSmoothTime = 0.08f;

        [SerializeField, Min(0.02f)] private float hudRefreshInterval = 0.1f;

        [Header("Debug Hotkeys (F5~F8)")]
        [SerializeField] private bool enableDebugTimeSkip = true;
        [SerializeField, Min(1)] private int debugGrantLevelsPerPress = 1;
        [SerializeField, Min(1f)] private float debugAdvanceSeconds = 60f;
        [SerializeField, Min(1)] private int debugSkipBossTargetLevel = 40;

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
        private static readonly WeaponUpgradeId[] StarterWeaponIds =
        {
            WeaponUpgradeId.Rifle,
            WeaponUpgradeId.Smg,
            WeaponUpgradeId.SniperRifle,
            WeaponUpgradeId.Shotgun,
            WeaponUpgradeId.Katana,
            WeaponUpgradeId.ChainAttack,
            WeaponUpgradeId.SatelliteBeam,
            WeaponUpgradeId.Drone,
            WeaponUpgradeId.RifleTurret,
            WeaponUpgradeId.Aura,
        };

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

        private float _remainingSeconds;
        private bool _isGameOver;
        private bool _isAwaitingStarterWeaponChoice;
        private bool _isPauseMenuOpen;
        private bool _bossWaveTriggered;
        private float _nextHudRefreshAt;
        private bool _usingOwnedMultiplayerPlayer;

        private void Awake()
        {
            // Keep simulation running even when the game window loses focus.
            Application.runInBackground = true;
            Time.timeScale = 1f;
            EnsureCamera();
            EnsureConfigs();
            _remainingSeconds = Mathf.Max(30f, enemyConfig != null ? enemyConfig.bossWaveStartSeconds : runDurationSeconds);
            _bossWaveTriggered = false;
        }

        private void Start()
        {
            BuildRuntimeGraph();
            HookEvents();
            _nextHudRefreshAt = 0f;
            UpdateHud();
        }

        private void Update()
        {
            if (!_usingOwnedMultiplayerPlayer)
            {
                HandlePauseMenuInput();
            }

            if (_isPauseMenuOpen)
            {
                TryRefreshHud();
                return;
            }

            HandleDebugTimeSkipInput();
            UpdateWeaponAimSmoothing();
            if (!_isGameOver && _playerSpriteAnimator != null && _playerMover != null)
            {
                _playerSpriteAnimator.SetMotion(_playerMover.CurrentVelocity);
            }

            if (!_isGameOver && IsAnyChoiceAwaiting() && _currentOptions != null)
            {
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
                    RestartRun();
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
                    EndRun(cleared: true);
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

        private static bool IsDebugGrantLevelKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.F5);
        }

        private static bool IsDebugAdvanceKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f6Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.F6);
        }

        private static bool IsDebugRerollOptionsKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f7Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.F7);
        }

        private static bool IsDebugSkipBossKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.F8);
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
            {
                _playerHealth.Changed -= OnPlayerHealthChanged;
                _playerHealth.Died -= OnPlayerDied;
            }

            if (_weaponSystem != null)
            {
                _weaponSystem.AimUpdated -= OnWeaponAimUpdated;
                _weaponSystem.Fired -= OnWeaponFired;
            }

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
        }

        private void HandleDebugTimeSkipInput()
        {
            if (!enableDebugTimeSkip || _isGameOver)
            {
                return;
            }

            if (IsDebugGrantLevelKeyDown())
            {
                var grantLevels = Mathf.Max(1, debugGrantLevelsPerPress);
                GrantDebugLevels(grantLevels);
            }

            if (IsDebugAdvanceKeyDown())
            {
                if (_enemySpawner != null)
                {
                    _enemySpawner.DebugAdvanceSeconds(debugAdvanceSeconds);
                    SyncRemainingTimeFromSpawner();
                }
            }

            if (IsDebugRerollOptionsKeyDown())
            {
                DebugRerollLevelUpOptions();
            }

            if (IsDebugSkipBossKeyDown())
            {
                if (_enemySpawner != null)
                {
                    _enemySpawner.DebugSkipToBossWave();
                    _bossWaveTriggered = true;
                    _remainingSeconds = 0f;
                }

                DebugRandomLevelUpToTarget(debugSkipBossTargetLevel);
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

            _isPauseMenuOpen = true;
            Time.timeScale = 0f;
            _hud.ShowPauseMenu(ResumeFromPauseMenu, QuitFromPauseMenu);
        }

        private void ResumeFromPauseMenu()
        {
            if (!_isPauseMenuOpen)
            {
                return;
            }

            _isPauseMenuOpen = false;
            _hud?.HidePauseMenu();
            if (!_isGameOver && !IsAnyChoiceAwaiting())
            {
                Time.timeScale = 1f;
            }

            UpdateHud();
        }

        private void QuitFromPauseMenu()
        {
            _isPauseMenuOpen = false;
            _hud?.HidePauseMenu();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
                if (_isAwaitingStarterWeaponChoice)
                {
                    if (_currentOptions == null || _currentOptions.Length <= 0)
                    {
                        break;
                    }

                    SelectStarterWeaponOption(UnityEngine.Random.Range(0, _currentOptions.Length));
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
            _buildRuntime = new PlayerBuildRuntime();
            _buildRuntime.InitializeDefaults(grantStarterRifle: false);

            _playerStats = new PlayerStatsRuntime();
            _playerStats.RecalculateFromBuild(_buildRuntime);
            _levelUp = new LevelUpSystem();
            _levelUp.Initialize(_buildRuntime);
            _hud = new HudController();
            _hud.Initialize();

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
            playerRenderer.color = hasPlayerAnimation ? Color.white : new Color(0.35f, 0.75f, 1f);
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
            _cameraFollow?.Initialize(player.transform, cameraOffset, cameraFollowSmoothTime);
            EnsureArenaBoundaryVisual();

            var systems = new GameObject("Systems");
            _enemyRegistry = systems.AddComponent<EnemyRegistry>();
            _experienceSystem = systems.AddComponent<ExperienceSystem>();
            _experienceSystem.Initialize(player.transform, playerConfig, _levelUp);

            var enemySpawner = systems.AddComponent<EnemySpawner>();
            enemySpawner.Initialize(enemyConfig, player.transform, _playerHealth, _enemyRegistry, _experienceSystem, playerConfig.collisionRadius, arenaBounds);
            _enemySpawner = enemySpawner;
            if (enemySpawner.BossWaveStartSeconds > 0f)
            {
                _remainingSeconds = enemySpawner.BossWaveStartSeconds;
            }

            var weaponSystem = systems.AddComponent<AutoWeaponSystem>();
            weaponSystem.Initialize(weaponConfig, player.transform, _enemyRegistry, _playerStats, ResolveProjectileSpawnPoint, arenaBounds);
            weaponSystem.ConfigureLoadout(_buildRuntime, _playerStats);
            weaponSystem.AimUpdated += OnWeaponAimUpdated;
            weaponSystem.Fired += OnWeaponFired;
            _weaponSystem = weaponSystem;
            _targetWeaponAimDirection = Vector2.right;
            _smoothedWeaponAimDirection = Vector2.right;
            ApplyBuildToRuntimeSystems();
            BeginStarterWeaponChoiceIfNeeded();
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
            var weaponFrames = RuntimeSpriteFactory.GetWeaponFire1AnimationFrames();
            var weaponSprite = weaponFrames.Length > 0 ? weaponFrames[0] : squareSprite;
            var hasWeaponAnimation = weaponFrames.Length > 1 && !ReferenceEquals(weaponSprite, squareSprite);

            weaponRenderer.sprite = weaponSprite;
            weaponRenderer.color = hasWeaponAnimation ? Color.white : new Color(1f, 0.95f, 0.35f);

            var weaponVisualSize = playerConfig != null ? Mathf.Max(0.05f, playerConfig.weaponVisualScale) : 0.45f;
            ApplyVisualScale(weaponTransform, weaponSprite, weaponVisualSize);
            ApplyWeaponAim(_lastWeaponAimDirection, fromFireEvent: false);

            var weaponAnimator = playerTransform.GetComponent<WeaponSpriteAnimator>();
            if (hasWeaponAnimation)
            {
                if (weaponAnimator == null)
                {
                    weaponAnimator = playerTransform.gameObject.AddComponent<WeaponSpriteAnimator>();
                }

                weaponAnimator.enabled = true;
                weaponAnimator.Initialize(weaponRenderer, weaponFrames, playerConfig);
                _weaponSpriteAnimator = weaponAnimator;
            }
            else
            {
                if (weaponAnimator != null)
                {
                    weaponAnimator.enabled = false;
                }

                _weaponSpriteAnimator = null;
            }
        }

        private void EnsureArenaBoundaryVisual()
        {
            var boundaryObject = GameObject.Find("ArenaBoundary");
            if (boundaryObject == null)
            {
                boundaryObject = new GameObject("ArenaBoundary");
            }

            var visualizer = boundaryObject.GetComponent<ArenaBoundaryVisualizer>();
            if (visualizer == null)
            {
                visualizer = boundaryObject.AddComponent<ArenaBoundaryVisualizer>();
            }

            visualizer.Initialize(arenaBounds);
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

        private void OnWeaponAimUpdated(Vector2 direction)
        {
            _targetWeaponAimDirection = NormalizeAimDirection(direction, _targetWeaponAimDirection);
        }

        private void OnWeaponFired(Vector2 direction)
        {
            var normalized = NormalizeAimDirection(direction, _targetWeaponAimDirection);
            _targetWeaponAimDirection = normalized;
            _smoothedWeaponAimDirection = normalized;
            ApplyWeaponAim(normalized, fromFireEvent: true);
        }

        private void ApplyWeaponAim(Vector2 direction, bool fromFireEvent)
        {
            if (_weaponVisualTransform == null)
            {
                return;
            }

            var normalizedDirection = NormalizeAimDirection(direction, _lastWeaponAimDirection);
            _lastWeaponAimDirection = normalizedDirection;
            _playerSpriteAnimator?.SetLookDirection(normalizedDirection);
            var flipX = ResolveWeaponFlipX(normalizedDirection);
            var rotationDegrees = CalculateWeaponRotationDegrees(normalizedDirection, flipX);
            var localPosition = CalculateWeaponLocalPosition(_playerTransform, normalizedDirection, flipX, rotationDegrees);
            _weaponVisualTransform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            if (_weaponVisualRenderer != null)
            {
                _weaponVisualRenderer.flipX = flipX;
            }

            UpdateWeaponSorting(normalizedDirection);
            _weaponVisualTransform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            if (fromFireEvent)
            {
                _weaponSpriteAnimator?.PlayAttack(normalizedDirection);
            }
        }

        private Vector3 ResolveProjectileSpawnPoint(Vector2 aimDirection)
        {
            if (_playerTransform == null)
            {
                return Vector3.zero;
            }

            if (_weaponVisualRenderer != null
                && _weaponVisualRenderer.enabled
                && _weaponVisualRenderer.sprite != null)
            {
                // Spawn from the rendered weapon sprite area (green gizmo rectangle), not transform pivot.
                return _weaponVisualRenderer.bounds.center;
            }

            var normalizedDirection = NormalizeAimDirection(aimDirection, _lastWeaponAimDirection);
            var flipX = ResolveWeaponFlipX(normalizedDirection);
            var rotationDegrees = CalculateWeaponRotationDegrees(normalizedDirection, flipX);
            var localPosition = CalculateWeaponLocalPosition(_playerTransform, normalizedDirection, flipX, rotationDegrees);
            return _playerTransform.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0f));
        }

        private Vector2 CalculateWeaponLocalPosition(Transform playerRoot, Vector2 normalizedDirection, bool flipX, float rotationDegrees)
        {
            var weaponOffset = playerConfig != null ? playerConfig.weaponVisualOffset : new Vector2(0.42f, -0.08f);
            var aimDistance = playerConfig != null ? Mathf.Max(0.05f, playerConfig.weaponAimDistance) : 0.55f;
            if (flipX)
            {
                weaponOffset.x = -weaponOffset.x;
            }

            var orbitCenterLocal = ResolveWeaponOrbitCenterLocal(playerRoot);
            var rotatedOffset = RotateOffsetByDegrees(weaponOffset, rotationDegrees);
            return orbitCenterLocal + normalizedDirection * aimDistance + rotatedOffset;
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

        private static Vector2 RotateOffsetByDegrees(Vector2 offset, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cosine = Mathf.Cos(radians);
            var sine = Mathf.Sin(radians);
            return new Vector2(
                offset.x * cosine - offset.y * sine,
                offset.x * sine + offset.y * cosine);
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
            if (_weaponVisualTransform == null)
            {
                return;
            }

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
            ApplyWeaponAim(_smoothedWeaponAimDirection, fromFireEvent: false);
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
            var weaponLocal = CalculateWeaponLocalPosition(player, aimDirection, flipX, rotationDegrees);

            var orbitCenterWorld = player.TransformPoint(new Vector3(orbitCenterLocal.x, orbitCenterLocal.y, 0f));
            var radiusEndWorld = player.TransformPoint(new Vector3(
                orbitCenterLocal.x + aimDirection.x * aimDistance,
                orbitCenterLocal.y + aimDirection.y * aimDistance,
                0f));
            var weaponWorld = player.TransformPoint(new Vector3(weaponLocal.x, weaponLocal.y, 0f));

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

            DrawWeaponSpriteRectGizmo(player);
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
            _levelUp.ExperienceChanged += (_, _, _) => UpdateHud();
            _levelUp.OptionsGenerated += OnLevelUpRequested;
        }

        private void OnPlayerHealthChanged(float currentHealth, float maxHealth)
        {
            _playerHealthBar?.SetHealth(currentHealth, maxHealth);
            UpdateHud();
        }

        private void OnPlayerDied()
        {
            _playerSpriteAnimator?.PlayDie();
            EndRun(cleared: false);
        }

        private void OnLevelUpRequested(LevelUpOption[] options)
        {
            if (_isGameOver || _isAwaitingStarterWeaponChoice)
            {
                return;
            }

            _currentOptions = options;
            Time.timeScale = 0f;
            _hud.ShowLevelUpOptions(options, SelectLevelUpOption);
        }

        private void SelectLevelUpOption(int optionIndex)
        {
            if (_isAwaitingStarterWeaponChoice)
            {
                SelectStarterWeaponOption(optionIndex);
                return;
            }

            _hud.HideLevelUpOptions();
            _levelUp.ApplyOption(optionIndex, _currentOptions);
            ApplyBuildToRuntimeSystems();

            if (_isGameOver)
            {
                return;
            }

            if (_levelUp.IsAwaitingChoice)
            {
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

            _isAwaitingStarterWeaponChoice = true;
            _currentOptions = options;
            Time.timeScale = 0f;
            _hud.ShowLevelUpOptions(options, SelectLevelUpOption, "\uC2DC\uC791 \uBB34\uAE30 \uC120\uD0DD");
        }

        private LevelUpOption[] CreateStarterWeaponOptions()
        {
            var options = new LevelUpOption[StarterWeaponIds.Length];
            for (var i = 0; i < StarterWeaponIds.Length; i++)
            {
                var weaponId = StarterWeaponIds[i];
                options[i] = new LevelUpOption(
                    UpgradeCategory.Weapon,
                    weaponId,
                    default,
                    0,
                    1,
                    isNewAcquire: true,
                    isLockedBySlot: false,
                    label: $"\uC2DC\uC791: {GetWeaponDisplayName(weaponId)} Lv1");
            }

            return options;
        }

        private void SelectStarterWeaponOption(int optionIndex)
        {
            if (_buildRuntime == null || _currentOptions == null || _currentOptions.Length <= 0)
            {
                return;
            }

            optionIndex = Mathf.Clamp(optionIndex, 0, _currentOptions.Length - 1);
            _buildRuntime.Apply(_currentOptions[optionIndex]);
            _isAwaitingStarterWeaponChoice = false;
            _currentOptions = null;
            _hud.HideLevelUpOptions();
            ApplyBuildToRuntimeSystems();

            if (!_isGameOver && Time.timeScale <= 0f)
            {
                Time.timeScale = 1f;
            }

            UpdateHud();
        }

        private void EndRun(bool cleared)
        {
            if (_isGameOver)
            {
                return;
            }

            _isGameOver = true;
            _isPauseMenuOpen = false;
            Time.timeScale = 0f;
            _hud.HideLevelUpOptions();
            _hud.HidePauseMenu();
            _hud.HideBossBar();
            _hud.ShowResult(cleared, RestartRun);
        }

        private void TriggerBossWave()
        {
            if (_bossWaveTriggered)
            {
                return;
            }

            _bossWaveTriggered = true;
            _enemySpawner?.TriggerBossWave();
        }

        private void RestartRun()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

            _hud.SetBuildInfo(BuildWeaponSummary(), BuildStatSummary());
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

            _hud.SetBossBar(boss.CurrentHealth, boss.MaxHealth, "BOSS");
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
                return $"Weapons\n1) Empty\n2) Locked (Lv{PlayerBuildRuntime.SecondWeaponUnlockLevel})\n3) Locked (Lv{PlayerBuildRuntime.ThirdWeaponUnlockLevel})";
            }

            var unlockedSlots = _buildRuntime.GetUnlockedWeaponSlots(_levelUp.Level);
            var lines = "Weapons";

            for (var slotIndex = 0; slotIndex < PlayerBuildRuntime.MaxWeaponSlotsAbsolute; slotIndex++)
            {
                var slotNumber = slotIndex + 1;
                if (slotIndex >= unlockedSlots)
                {
                    var requiredLevel = slotIndex == 1
                        ? PlayerBuildRuntime.SecondWeaponUnlockLevel
                        : PlayerBuildRuntime.ThirdWeaponUnlockLevel;
                    lines += $"\n{slotNumber}) Locked (Lv{requiredLevel})";
                    continue;
                }

                if (slotIndex < _buildRuntime.OwnedWeapons.Count)
                {
                    var weaponId = _buildRuntime.OwnedWeapons[slotIndex];
                    var level = _buildRuntime.GetWeaponLevel(weaponId);
                    var coreLevel = _buildRuntime.GetWeaponCoreLevel(weaponId);
                    var coreElement = _buildRuntime.GetWeaponCoreElement(weaponId);
                    var coreSuffix = coreLevel > 0
                        ? $" [{GetCoreDisplayName(coreElement)} C{coreLevel}]"
                        : string.Empty;
                    lines += $"\n{slotNumber}) {GetWeaponDisplayName(weaponId)} Lv{level}{coreSuffix}";
                }
                else
                {
                    lines += $"\n{slotNumber}) Empty";
                }
            }

            return lines;
        }

        private string BuildStatSummary()
        {
            if (_buildRuntime == null)
            {
                var emptyLines = "Stats";
                for (var slotIndex = 0; slotIndex < PlayerBuildRuntime.MaxStatSlots; slotIndex++)
                {
                    emptyLines += $"\n{slotIndex + 1}) Empty";
                }

                return emptyLines;
            }

            var lines = "Stats";
            for (var slotIndex = 0; slotIndex < PlayerBuildRuntime.MaxStatSlots; slotIndex++)
            {
                var slotNumber = slotIndex + 1;
                if (slotIndex < _buildRuntime.OwnedStats.Count)
                {
                    var statId = _buildRuntime.OwnedStats[slotIndex];
                    var level = _buildRuntime.GetStatLevel(statId);
                    lines += $"\n{slotNumber}) {GetStatDisplayName(statId)} Lv{level}";
                }
                else
                {
                    lines += $"\n{slotNumber}) Empty";
                }
            }

            return lines;
        }

        private static string GetWeaponDisplayName(WeaponUpgradeId weaponId)
        {
            return weaponId switch
            {
                WeaponUpgradeId.Smg => "\uAE30\uAD00\uB2E8\uCD1D",
                WeaponUpgradeId.SniperRifle => "\uC800\uACA9\uC18C\uCD1D",
                WeaponUpgradeId.Shotgun => "\uC0B0\uD0C4\uCD1D",
                WeaponUpgradeId.Katana => "\uCE74\uD0C0\uB098",
                WeaponUpgradeId.ChainAttack => "\uCCB4\uC778\uC5B4\uD0DD",
                WeaponUpgradeId.SatelliteBeam => "\uC704\uC131\uBE54",
                WeaponUpgradeId.Drone => "\uB4DC\uB860",
                WeaponUpgradeId.RifleTurret => "\uB77C\uC774\uD50C\uD3EC\uD0D1",
                WeaponUpgradeId.Aura => "\uC624\uB77C",
                _ => "\uB77C\uC774\uD50C",
            };
        }

        private static string GetStatDisplayName(StatUpgradeId statId)
        {
            return statId switch
            {
                StatUpgradeId.AttackPower => "Attack Power",
                StatUpgradeId.AttackSpeed => "Attack Speed",
                StatUpgradeId.MaxHealth => "Max Health",
                StatUpgradeId.HealthRegen => "Health Regen",
                StatUpgradeId.MoveSpeed => "Move Speed",
                StatUpgradeId.AttackRange => "Attack Range",
                _ => statId.ToString(),
            };
        }

        private static string GetCoreDisplayName(WeaponCoreElement coreElement)
        {
            return coreElement switch
            {
                WeaponCoreElement.Fire => "Fire",
                WeaponCoreElement.Wind => "Wind",
                WeaponCoreElement.Light => "Light",
                WeaponCoreElement.Water => "Water",
                _ => "Core",
            };
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
            return _isAwaitingStarterWeaponChoice || (_levelUp != null && _levelUp.IsAwaitingChoice);
        }

        private void UpdateWeaponVisualActivation()
        {
            if (_weaponVisualRenderer == null || _buildRuntime == null)
            {
                return;
            }

            var hasGunWeapon = HasAnyGunWeapon(_buildRuntime);
            _weaponVisualRenderer.enabled = hasGunWeapon;
            if (_weaponSpriteAnimator != null)
            {
                _weaponSpriteAnimator.enabled = hasGunWeapon;
            }
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
                if (IsGunWeapon(ownedWeapons[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGunWeapon(WeaponUpgradeId weaponId)
        {
            return weaponId == WeaponUpgradeId.Rifle
                   || weaponId == WeaponUpgradeId.Smg
                   || weaponId == WeaponUpgradeId.SniperRifle
                   || weaponId == WeaponUpgradeId.Shotgun;
        }
    }
}

