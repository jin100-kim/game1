using EJR.Game.Core;
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

        [Header("Automation")]
        [SerializeField] private bool enableAutoPlay;
        [SerializeField] private bool autoRestartOnGameOver;
        [SerializeField, Min(0f)] private float autoRestartDelay = 1f;
        [SerializeField, Min(0f)] private float autoPickDelay = 0.2f;

        private PlayerHealth _playerHealth;
        private PlayerStatsRuntime _playerStats;

        private EnemyRegistry _enemyRegistry;
        private ExperienceSystem _experienceSystem;
        private EnemySpawner _enemySpawner;
        private CameraFollow2D _cameraFollow;
        private WorldHealthBar _playerHealthBar;
        private AutoPlayController _autoPlay;
        private PlayerMover _playerMover;
        private Transform _playerTransform;

        private LevelUpSystem _levelUp;
        private HudController _hud;

        private LevelUpOption[] _currentOptions;

        private float _remainingSeconds;
        private float _autoPickAt = -1f;
        private float _autoRestartAt = -1f;
        private bool _isGameOver;
        private bool _bossWaveTriggered;

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
            UpdateHud();
        }

        private void Update()
        {
            if (!_isGameOver && enableAutoPlay && _levelUp != null && _levelUp.IsAwaitingChoice && _currentOptions != null && _currentOptions.Length > 0 && _autoPlay != null)
            {
                if (_autoPickAt < 0f)
                {
                    _autoPickAt = Time.unscaledTime + autoPickDelay;
                }

                if (Time.unscaledTime >= _autoPickAt)
                {
                    SelectLevelUpOption(_autoPlay.PickLevelUpOption(_currentOptions));
                    return;
                }
            }

            if (!_isGameOver && _levelUp != null && _levelUp.IsAwaitingChoice)
            {
                if (IsOptionKeyDown(0))
                {
                    SelectLevelUpOption(0);
                    return;
                }

                if (IsOptionKeyDown(1))
                {
                    SelectLevelUpOption(1);
                    return;
                }

                if (IsOptionKeyDown(2))
                {
                    SelectLevelUpOption(2);
                    return;
                }
            }

            if (_isGameOver)
            {
                if (enableAutoPlay && autoRestartOnGameOver && _autoRestartAt >= 0f && Time.unscaledTime >= _autoRestartAt)
                {
                    RestartRun();
                    return;
                }

                if (IsRestartKeyDown())
                {
                    RestartRun();
                }

                return;
            }

            if (Time.timeScale > 0f)
            {
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

            UpdateHud();
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
                    _ => false,
                };
            }
#endif
            return zeroBasedIndex switch
            {
                0 => Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1),
                1 => Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2),
                2 => Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3),
                _ => false,
            };
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
            {
                _playerHealth.Changed -= OnPlayerHealthChanged;
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

        private void BuildRuntimeGraph()
        {
            _playerStats = new PlayerStatsRuntime();
            _levelUp = new LevelUpSystem();
            _hud = new HudController();
            _hud.Initialize();

            var player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.transform.position = Vector3.zero;
            }

            var playerRenderer = player.GetComponent<SpriteRenderer>();
            if (playerRenderer == null)
            {
                playerRenderer = player.AddComponent<SpriteRenderer>();
            }

            playerRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
            playerRenderer.color = new Color(0.35f, 0.75f, 1f);
            player.transform.localScale = Vector3.one * Mathf.Max(0.1f, playerConfig.visualScale);

            _playerHealth = player.GetComponent<PlayerHealth>();
            if (_playerHealth == null)
            {
                _playerHealth = player.AddComponent<PlayerHealth>();
            }
            _playerHealth.Initialize(playerConfig.maxHealth);

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
            _playerTransform = player.transform;

            playerMover.Initialize(playerConfig, _playerStats, arenaBounds);
            _cameraFollow?.Initialize(player.transform, cameraOffset, cameraFollowSmoothTime);
            EnsureArenaBoundaryVisual();

            var systems = new GameObject("Systems");
            _enemyRegistry = systems.AddComponent<EnemyRegistry>();
            _experienceSystem = systems.AddComponent<ExperienceSystem>();
            _experienceSystem.Initialize(player.transform, playerConfig, _levelUp);

            var enemySpawner = systems.AddComponent<EnemySpawner>();
            enemySpawner.Initialize(enemyConfig, player.transform, _playerHealth, _enemyRegistry, _experienceSystem, playerConfig.collisionRadius);
            _enemySpawner = enemySpawner;
            if (enemySpawner.BossWaveStartSeconds > 0f)
            {
                _remainingSeconds = enemySpawner.BossWaveStartSeconds;
            }

            var weaponSystem = systems.AddComponent<AutoWeaponSystem>();
            weaponSystem.Initialize(weaponConfig, player.transform, _enemyRegistry, _playerStats);
            ConfigureAutoPlay(playerMover, player.transform);
            _hud.BindAutoPlayToggle(enableAutoPlay, ToggleAutoPlayFromHud);
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

        private void ConfigureAutoPlay(PlayerMover playerMover, Transform playerTransform)
        {
            _autoPickAt = -1f;
            _autoRestartAt = -1f;

            if (!enableAutoPlay || playerMover == null || playerTransform == null)
            {
                playerMover?.SetMoveInputReader(null);
                if (_autoPlay != null)
                {
                    Destroy(_autoPlay);
                    _autoPlay = null;
                }

                return;
            }

            if (_autoPlay == null)
            {
                _autoPlay = GetComponent<AutoPlayController>();
                if (_autoPlay == null)
                {
                    _autoPlay = gameObject.AddComponent<AutoPlayController>();
                }
            }

            var preferredRange = weaponConfig != null ? weaponConfig.attackRange : 6f;
            var playerRadius = playerConfig != null ? playerConfig.collisionRadius : 0.35f;
            _autoPlay.Initialize(playerTransform, _enemyRegistry, arenaBounds, preferredRange, playerRadius);
            playerMover.SetMoveInputReader(_autoPlay.ReadMove);
            _hud?.SetAutoPlayState(enableAutoPlay);
        }

        private void ToggleAutoPlayFromHud()
        {
            enableAutoPlay = !enableAutoPlay;
            ConfigureAutoPlay(_playerMover, _playerTransform);
            _hud?.SetAutoPlayState(enableAutoPlay);
        }

        private void HookEvents()
        {
            _playerHealth.Changed += OnPlayerHealthChanged;
            _playerHealth.Died += () => EndRun(cleared: false);
            _levelUp.ExperienceChanged += (_, _, _) => UpdateHud();
            _levelUp.OptionsGenerated += OnLevelUpRequested;
        }

        private void OnPlayerHealthChanged(float currentHealth, float maxHealth)
        {
            _playerHealthBar?.SetHealth(currentHealth, maxHealth);
            UpdateHud();
        }

        private void OnLevelUpRequested(LevelUpOption[] options)
        {
            if (_isGameOver)
            {
                return;
            }

            _currentOptions = options;
            _autoPickAt = -1f;
            Time.timeScale = 0f;
            _hud.ShowLevelUpOptions(options, SelectLevelUpOption);
        }

        private void SelectLevelUpOption(int optionIndex)
        {
            _hud.HideLevelUpOptions();
            _levelUp.ApplyOption(optionIndex, _currentOptions, _playerStats);
            _autoPickAt = -1f;

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

        private void EndRun(bool cleared)
        {
            if (_isGameOver)
            {
                return;
            }

            _isGameOver = true;
            Time.timeScale = 0f;
            _hud.HideLevelUpOptions();
            _hud.ShowResult(cleared, RestartRun);
            _autoRestartAt = enableAutoPlay && autoRestartOnGameOver
                ? Time.unscaledTime + autoRestartDelay
                : -1f;
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
            _autoRestartAt = -1f;
            _autoPickAt = -1f;
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
        }
    }
}
