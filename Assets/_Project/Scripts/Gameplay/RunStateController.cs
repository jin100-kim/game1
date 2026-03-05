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
        [SerializeField, Min(0.02f)] private float hudRefreshInterval = 0.1f;

        [Header("Debug Time Skip")]
        [SerializeField] private bool enableDebugTimeSkip = true;
        [SerializeField, Min(1f)] private float debugAdvanceSeconds = 60f;

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
        private AutoPlayController _autoPlay;
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

        private LevelUpSystem _levelUp;
        private HudController _hud;

        private LevelUpOption[] _currentOptions;

        private float _remainingSeconds;
        private float _autoPickAt = -1f;
        private float _autoRestartAt = -1f;
        private bool _isGameOver;
        private bool _bossWaveTriggered;
        private float _nextHudRefreshAt;

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
            HandleDebugTimeSkipInput();
            UpdateWeaponAimSmoothing();
            if (!_isGameOver && _playerSpriteAnimator != null && _playerMover != null)
            {
                _playerSpriteAnimator.SetMotion(_playerMover.CurrentVelocity);
            }

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

        private static bool IsDebugSkipMushroomKeyDown()
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
            if (!enableDebugTimeSkip || _isGameOver || _enemySpawner == null)
            {
                return;
            }

            if (IsDebugAdvanceKeyDown())
            {
                _enemySpawner.DebugAdvanceSeconds(debugAdvanceSeconds);
                SyncRemainingTimeFromSpawner();
            }

            if (IsDebugSkipMushroomKeyDown())
            {
                var mushroomPhaseTime = enemyConfig != null
                    ? Mathf.Max(0f, enemyConfig.mushroomPhaseStartSeconds + 1f)
                    : 301f;
                _enemySpawner.DebugSetElapsedSeconds(mushroomPhaseTime);
                SyncRemainingTimeFromSpawner();
            }

            if (IsDebugSkipBossKeyDown())
            {
                _enemySpawner.DebugSkipToBossWave();
                _bossWaveTriggered = true;
                _remainingSeconds = 0f;
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

            var rootRenderer = player.GetComponent<SpriteRenderer>();
            if (rootRenderer != null)
            {
                Destroy(rootRenderer);
            }

            var visualTransform = player.transform.Find(PlayerVisualObjectName);
            if (visualTransform == null)
            {
                visualTransform = new GameObject(PlayerVisualObjectName).transform;
                visualTransform.SetParent(player.transform, false);
            }

            visualTransform.localPosition = new Vector3(0f, playerConfig.visualYOffset, 0f);
            _weaponOrbitCenterLocal = new Vector2(visualTransform.localPosition.x, visualTransform.localPosition.y);

            var playerRenderer = visualTransform.GetComponent<SpriteRenderer>();
            if (playerRenderer == null)
            {
                playerRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
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
            _playerHealth.Initialize(playerConfig.maxHealth, playerConfig.damageInvulnerabilitySeconds);

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
            enemySpawner.Initialize(enemyConfig, player.transform, _playerHealth, _enemyRegistry, _experienceSystem, playerConfig.collisionRadius);
            _enemySpawner = enemySpawner;
            if (enemySpawner.BossWaveStartSeconds > 0f)
            {
                _remainingSeconds = enemySpawner.BossWaveStartSeconds;
            }

            var weaponSystem = systems.AddComponent<AutoWeaponSystem>();
            weaponSystem.Initialize(weaponConfig, player.transform, _enemyRegistry, _playerStats, ResolveProjectileSpawnPoint);
            weaponSystem.AimUpdated += OnWeaponAimUpdated;
            weaponSystem.Fired += OnWeaponFired;
            _weaponSystem = weaponSystem;
            _targetWeaponAimDirection = Vector2.right;
            _smoothedWeaponAimDirection = Vector2.right;
            ConfigureAutoPlay(playerMover, player.transform);
            _hud.BindAutoPlayToggle(enableAutoPlay, ToggleAutoPlayFromHud);
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

            if (_weaponVisualRenderer != null && _weaponVisualRenderer.sprite != null)
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

        private void TryRefreshHud()
        {
            if (Time.unscaledTime < _nextHudRefreshAt)
            {
                return;
            }

            _nextHudRefreshAt = Time.unscaledTime + Mathf.Max(0.02f, hudRefreshInterval);
            UpdateHud();
        }
    }
}
