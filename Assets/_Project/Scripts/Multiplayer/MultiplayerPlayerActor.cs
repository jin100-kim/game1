using System.Collections;
using System.Collections.Generic;
using EJR.Game.Core;
using EJR.Game.Gameplay;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(PlayerMover))]
    [RequireComponent(typeof(PlayerSpriteAnimator))]
    public sealed class MultiplayerPlayerActor : NetworkBehaviour
    {
        [SerializeField] private Rect arenaBounds = new Rect(-12f, -7f, 24f, 14f);
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float cameraFollowSmoothTime = 0.08f;
        [SerializeField, Min(1f)] private float cameraOrthographicSize = 5.8f;
        [SerializeField] private Vector3 reviveBarLocalOffset = new(0f, 1.2f, 0f);
        [SerializeField] private Vector2 reviveBarSize = new(1.1f, 0.12f);
        [SerializeField, Min(0.005f)] private float reviveRadiusLineWidth = 0.03f;
        [SerializeField] private Color reviveRadiusColor = new(0.45f, 0.95f, 1f, 0.7f);
        [SerializeField] private Color reviveBarBackgroundColor = new(0f, 0f, 0f, 0.7f);
        [SerializeField] private Color reviveBarFillColor = new(0.2f, 0.95f, 0.45f, 0.95f);
        [SerializeField] private int weaponFrontSortingOffset = 1;
        [SerializeField] private int weaponBackSortingOffset = -1;
        [SerializeField, Range(0f, 0.2f)] private float weaponLayerSwapDeadZone = 0.02f;
        [SerializeField, Min(0.01f)] private float katanaSlashFxFps = 18f;
        [SerializeField, Min(0.05f)] private float katanaSlashFxForwardOffset = 0.72f;
        [SerializeField] private Vector2 katanaSlashFxLocalOffset = new(-0.22f, -2.0f);
        [SerializeField, Min(0.05f)] private float katanaSlashFxScale = 6f;
        [SerializeField, Min(0.01f)] private float chainFxDuration = 0.08f;
        [SerializeField, Min(0.005f)] private float chainFxWidth = 0.05f;
        [SerializeField] private Color chainFxColor = new(0.45f, 0.85f, 1f, 0.95f);
        [SerializeField, Min(0.01f)] private float auraFxDuration = 0.08f;
        [SerializeField, Min(0.005f)] private float auraFxWidth = 0.045f;
        [SerializeField] private Color auraFxColor = new(0.45f, 1f, 0.75f, 0.75f);
        [SerializeField, Min(0.01f)] private float turretTracerFxDuration = 0.06f;
        [SerializeField, Min(0.005f)] private float turretTracerFxWidth = 0.03f;
        [SerializeField] private Color turretTracerFxColor = new(1f, 0.86f, 0.28f, 0.95f);
        [SerializeField] private Color turretRangeFxColor = new(0.55f, 0.9f, 1f, 0.28f);
        [SerializeField, Range(8, 96)] private int ringFxSegments = 28;
        [SerializeField, Min(0.1f)] private float satelliteVisualAnimationFps = 12f;
        [SerializeField, Min(1)] private int satelliteVisualSortOrder = 33;
        [SerializeField, Min(0.1f)] private float turretVisualAnimationFps = 12f;
        [SerializeField, Min(0.05f)] private float turretVisualScale = 3f;
        [SerializeField, Min(0.1f)] private float satelliteBeamVisualFps = 14f;
        [SerializeField, Min(0.05f)] private float satelliteBeamVisualScale = 3f;
        [SerializeField] private float satelliteBeamVisualYOffset = 0f;

        private const string PlayerVisualObjectName = "Visual";
        private const string WeaponVisualObjectName = "WeaponVisual";
        private const string DroneVisualRootObjectName = "DroneVisuals";
        private const string SpecialFxRootObjectName = "SpecialFx";
        private const string ReviveVisualRootObjectName = "ReviveVisuals";
        private const float WeaponAimFlipEpsilon = 0.01f;

        private readonly NetworkVariable<Vector2> _networkVelocity =
            new(
                Vector2.zero,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        private sealed class LocalTurretVisual
        {
            public Transform Root;
            public SpriteRenderer Renderer;
            public Sprite IdleFrame;
            public Sprite[] FireFrames;
            public float ExpiresAt;
            public Coroutine FireAnimationCoroutine;
        }

        private PlayerConfig _playerConfig;
        private PlayerMover _playerMover;
        private PlayerSpriteAnimator _playerSpriteAnimator;
        private SpriteRenderer _rootSpriteRenderer;
        private SpriteRenderer _spriteRenderer;
        private Transform _cachedTransform;
        private Transform _visualRoot;
        private Transform _weaponVisualTransform;
        private SpriteRenderer _weaponVisualRenderer;
        private WeaponSpriteAnimator _weaponSpriteAnimator;
        private Transform _specialFxRoot;
        private Transform _droneVisualRoot;
        private Transform _reviveVisualRoot;
        private LineRenderer _reviveRadiusRenderer;
        private SpriteRenderer _reviveBarBackgroundRenderer;
        private SpriteRenderer _reviveBarFillRenderer;
        private readonly List<Transform> _droneVisuals = new(4);
        private readonly List<LocalTurretVisual> _localTurrets = new(4);
        private Vector2 _weaponAimDirection = Vector2.right;
        private float _droneOrbitRadius;
        private float _droneOrbitSpeedDegrees;
        private int _droneVisualCount;
        private float _droneOrbitSeedDegrees;
        private bool _showReviveVisuals;
        private float _reviveProgress;
        private float _reviveRadius;
        private Vector3 _lastPosition;
        private bool _weaponDrawBehind;
        private bool _initialized;
        private static Material _sharedFxMaterial;

        public SpriteRenderer VisualRenderer
        {
            get
            {
                InitializeRuntime();
                return _spriteRenderer;
            }
        }

        public static MultiplayerPlayerActor FindOwnedLocalPlayer()
        {
            var players = FindObjectsByType<MultiplayerPlayerActor>(FindObjectsSortMode.None);
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
            InitializeRuntime();
            _playerMover.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            InitializeRuntime();

            _playerMover.enabled = IsOwner;
            _lastPosition = _cachedTransform.position;
            _droneOrbitSeedDegrees = (OwnerClientId % 8UL) * 37.5f;
            SetWeaponVisible(false);
            SetWeaponAim(_weaponAimDirection);

            if (IsOwner)
            {
                AttachOwnerCamera();
            }
            else
            {
                _playerSpriteAnimator.SetMotion(_networkVelocity.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            ClearDroneVisuals();
            ClearLocalTurrets();
            ClearTransientFxObjects();

            if (!IsOwner)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            var follow = mainCamera.GetComponent<CameraFollow2D>();
            if (follow != null)
            {
                follow.SetTarget(null);
            }
        }

        private void Update()
        {
            if (!IsSpawned || IsOwner)
            {
                return;
            }

            _playerSpriteAnimator.SetMotion(_networkVelocity.Value);
        }

        private void LateUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                var velocity = CalculateVelocity();
                _networkVelocity.Value = velocity;
                _playerSpriteAnimator.SetMotion(velocity);
            }

            UpdateDroneVisuals();
            UpdateReviveVisuals();
            CleanupExpiredTurrets();
            _lastPosition = _cachedTransform.position;
        }

        public void SetWeaponVisible(bool isVisible)
        {
            if (_weaponVisualRenderer == null)
            {
                return;
            }

            _weaponVisualRenderer.enabled = isVisible;
            if (_weaponSpriteAnimator != null)
            {
                _weaponSpriteAnimator.enabled = isVisible;
            }
        }

        public void SetWeaponAim(Vector2 direction)
        {
            _weaponAimDirection = NormalizeDirection(direction, _weaponAimDirection);
            ApplyWeaponAim(_weaponAimDirection, fromFireEvent: false);
        }

        public void RefreshOwnerCameraBinding()
        {
            if (!IsOwner)
            {
                return;
            }

            InitializeRuntime();
            AttachOwnerCamera();
        }

        public void PlayWeaponAttack(Vector2 direction)
        {
            _weaponAimDirection = NormalizeDirection(direction, _weaponAimDirection);
            ApplyWeaponAim(_weaponAimDirection, fromFireEvent: true);
        }

        public void SetDroneOrbitVisualState(int count, float orbitRadius, float orbitSpeedDegrees)
        {
            if (IsServer)
            {
                return;
            }

            InitializeRuntime();
            _droneVisualCount = Mathf.Clamp(count, 0, 8);
            _droneOrbitRadius = Mathf.Max(0f, orbitRadius);
            _droneOrbitSpeedDegrees = Mathf.Max(0f, orbitSpeedDegrees);
            EnsureDroneVisualCount();
        }

        public void ResetSpecialPresentation()
        {
            if (IsServer)
            {
                return;
            }

            _droneVisualCount = 0;
            _droneOrbitRadius = 0f;
            _droneOrbitSpeedDegrees = 0f;
            ClearDroneVisuals();
            ClearLocalTurrets();
            ClearTransientFxObjects();
        }

        public void SetReviveVisualState(bool isDowned, float progress, float requiredRadius)
        {
            InitializeRuntime();
            _showReviveVisuals = isDowned;
            _reviveProgress = Mathf.Clamp01(progress);
            _reviveRadius = Mathf.Max(0.1f, requiredRadius);
            UpdateReviveVisuals();
        }

        public void PlayKatanaSlashFx(Vector2 origin, Vector2 direction, float range)
        {
            if (IsServer)
            {
                return;
            }

            EnsureSpecialFxRoot();

            var frames = RuntimeSpriteFactory.GetSexySwordAttackAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                return;
            }

            var normalizedDirection = NormalizeDirection(direction, Vector2.right);
            var fxObject = new GameObject("KatanaSlashFx");
            fxObject.transform.SetParent(_specialFxRoot, false);

            var forward = Mathf.Max(0.05f, katanaSlashFxForwardOffset);
            var leftAxis = new Vector2(-normalizedDirection.y, normalizedDirection.x);
            var worldOffset = (normalizedDirection * katanaSlashFxLocalOffset.x) + (leftAxis * katanaSlashFxLocalOffset.y);
            var fxPosition = origin + (normalizedDirection * forward) + worldOffset;
            fxObject.transform.position = new Vector3(fxPosition.x, fxPosition.y, -0.02f);
            fxObject.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg);
            var scale = Mathf.Max(0.05f, katanaSlashFxScale) * Mathf.Max(0.8f, range * 0.4f);
            fxObject.transform.localScale = Vector3.one * scale;

            var renderer = fxObject.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = Color.white;
            renderer.sortingOrder = 35;

            var animator = fxObject.AddComponent<SpriteFxAnimator>();
            animator.Initialize(renderer, frames, katanaSlashFxFps, loop: false, destroyOnComplete: true);
        }

        public void PlayChainFx(Vector3[] points)
        {
            if (IsServer || points == null || points.Length <= 1)
            {
                return;
            }

            SpawnPolylineFx(points, chainFxColor, chainFxWidth, chainFxDuration, loop: false, "ChainFx");
        }

        public void PlayAuraPulseFx(Vector3 center, float radius)
        {
            if (IsServer)
            {
                return;
            }

            SpawnRingFx(center, radius, auraFxColor, auraFxWidth, auraFxDuration, "AuraFx");
        }

        public void PlaySatelliteHitFx(Vector3 center, float radius)
        {
            if (IsServer)
            {
                return;
            }

            SpawnRingFx(center, radius, auraFxColor, auraFxWidth, 0.06f, "SatelliteHitFx");
        }

        public void PlaySatelliteBeamFx(Vector3 targetCenter)
        {
            if (IsServer)
            {
                return;
            }

            EnsureSpecialFxRoot();

            var frames = RuntimeSpriteFactory.GetSexySatelliteBeamAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                return;
            }

            var frame = frames[0];
            var ppu = Mathf.Max(0.0001f, frame.pixelsPerUnit);
            var scale = Mathf.Max(0.05f, satelliteBeamVisualScale);
            var halfHeight = (frame.rect.height / ppu) * 0.5f * scale;
            var yOffset = halfHeight + satelliteBeamVisualYOffset;

            var fxObject = new GameObject("SatelliteBeamFx");
            fxObject.transform.SetParent(_specialFxRoot, false);
            fxObject.transform.position = new Vector3(targetCenter.x, targetCenter.y + yOffset, -0.02f);
            fxObject.transform.localScale = Vector3.one * scale;

            var renderer = fxObject.AddComponent<SpriteRenderer>();
            renderer.sprite = frame;
            renderer.color = Color.white;
            renderer.sortingOrder = 36;

            var animator = fxObject.AddComponent<SpriteFxAnimator>();
            animator.Initialize(renderer, frames, satelliteBeamVisualFps, loop: false, destroyOnComplete: true);
        }

        public void SpawnTurretVisual(Vector3 position, float turretRange, float lifetime)
        {
            if (IsServer)
            {
                return;
            }

            var turretObject = new GameObject("RifleTurretRemote");
            turretObject.transform.SetParent(null, true);
            turretObject.transform.position = new Vector3(position.x, position.y, 0f);
            turretObject.transform.localScale = Vector3.one;

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(turretObject.transform, false);

            var turretRenderer = visualObject.AddComponent<SpriteRenderer>();
            var turretFrames = RuntimeSpriteFactory.GetSexyTurretAnimationFrames();
            var hasTurretAnimation = turretFrames != null && turretFrames.Length > 0;
            var idleFrame = hasTurretAnimation ? turretFrames[0] : RuntimeSpriteFactory.GetSquareSprite();
            var fireFrames = ExtractFireFrames(turretFrames);
            turretRenderer.sprite = idleFrame;
            turretRenderer.color = Color.white;
            turretRenderer.sortingOrder = 34;
            visualObject.transform.localScale = Vector3.one * Mathf.Max(0.05f, turretVisualScale);

            var rangeFxObject = new GameObject("RifleTurretRangeFx");
            rangeFxObject.transform.SetParent(turretObject.transform, false);
            rangeFxObject.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            var rangeRenderer = rangeFxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(rangeRenderer, turretRangeFxColor, 0.03f, loop: true, useWorldSpace: false);
            SetCircleLinePositions(rangeRenderer, Vector2.zero, turretRange, ringFxSegments, 0f);

            _localTurrets.Add(new LocalTurretVisual
            {
                Root = turretObject.transform,
                Renderer = turretRenderer,
                IdleFrame = idleFrame,
                FireFrames = fireFrames,
                ExpiresAt = Time.time + Mathf.Max(0.1f, lifetime),
                FireAnimationCoroutine = null,
            });
        }

        public void PlayTurretTracerFx(Vector3 from, Vector3 to)
        {
            if (IsServer)
            {
                return;
            }

            PlayNearestTurretFireAnimation(from, to);
            SpawnLineFx(from, to, turretTracerFxColor, turretTracerFxWidth, turretTracerFxDuration, "TurretTracerFx");
        }

        private void InitializeRuntime()
        {
            if (_initialized)
            {
                return;
            }

            _cachedTransform = transform;
            _rootSpriteRenderer = GetComponent<SpriteRenderer>();
            _playerMover = GetComponent<PlayerMover>();
            _playerSpriteAnimator = GetComponent<PlayerSpriteAnimator>();
            _playerConfig = ScriptableObject.CreateInstance<PlayerConfig>();

            var frames = RuntimeSpriteFactory.GetPlayerAnimationFrames();
            var baseSprite = frames.Length > 0 ? frames[0] : RuntimeSpriteFactory.GetSquareSprite();
            EnsureVisualRoot();

            _spriteRenderer.sprite = baseSprite;
            _spriteRenderer.sortingOrder = 10;
            var visualWorldSize = Mathf.Max(0.1f, _playerConfig.visualScale * Mathf.Max(0.1f, _playerConfig.visualScaleMultiplier));
            ApplyVisualScale(_visualRoot, baseSprite, visualWorldSize);

            _playerMover.Initialize(_playerConfig, null, arenaBounds);
            _playerSpriteAnimator.Initialize(_spriteRenderer, frames, _playerConfig);
            EnsureWeaponVisual();
            ApplyWeaponAim(_weaponAimDirection, fromFireEvent: false);

            _initialized = true;
        }

        private Vector2 CalculateVelocity()
        {
            var deltaTime = Mathf.Max(0.0001f, Time.deltaTime);
            var delta = _cachedTransform.position - _lastPosition;
            return new Vector2(delta.x, delta.y) / deltaTime;
        }

        private void AttachOwnerCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = cameraOrthographicSize;
            mainCamera.transform.position = cameraOffset;

            var follow = mainCamera.GetComponent<CameraFollow2D>();
            if (follow == null)
            {
                follow = mainCamera.gameObject.AddComponent<CameraFollow2D>();
            }

            follow.Initialize(transform, cameraOffset, cameraFollowSmoothTime);
        }

        private void EnsureWeaponVisual()
        {
            if (_weaponVisualTransform == null)
            {
                var weaponTransform = _cachedTransform.Find(WeaponVisualObjectName);
                if (weaponTransform == null)
                {
                    weaponTransform = new GameObject(WeaponVisualObjectName).transform;
                    weaponTransform.SetParent(_cachedTransform, false);
                }

                _weaponVisualTransform = weaponTransform;
            }

            if (_weaponVisualRenderer == null)
            {
                _weaponVisualRenderer = _weaponVisualTransform.GetComponent<SpriteRenderer>();
                if (_weaponVisualRenderer == null)
                {
                    _weaponVisualRenderer = _weaponVisualTransform.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            var squareSprite = RuntimeSpriteFactory.GetSquareSprite();
            var weaponFrames = RuntimeSpriteFactory.GetWeaponFire1AnimationFrames();
            var weaponSprite = weaponFrames.Length > 0 ? weaponFrames[0] : squareSprite;

            _weaponVisualRenderer.sprite = weaponSprite;
            _weaponVisualRenderer.color = Color.white;
            _weaponVisualRenderer.sortingLayerID = _spriteRenderer.sortingLayerID;
            _weaponVisualRenderer.sortingOrder = _spriteRenderer.sortingOrder + weaponFrontSortingOffset;

            var weaponVisualSize = Mathf.Max(0.05f, _playerConfig.weaponVisualScale);
            ApplyVisualScale(_weaponVisualTransform, weaponSprite, weaponVisualSize);

            if (_weaponSpriteAnimator == null)
            {
                _weaponSpriteAnimator = _weaponVisualTransform.GetComponent<WeaponSpriteAnimator>();
                if (_weaponSpriteAnimator == null)
                {
                    _weaponSpriteAnimator = _weaponVisualTransform.gameObject.AddComponent<WeaponSpriteAnimator>();
                }
            }

            _weaponSpriteAnimator.Initialize(_weaponVisualRenderer, weaponFrames, _playerConfig);
            _weaponVisualRenderer.enabled = false;
            _weaponSpriteAnimator.enabled = false;
        }

        private void EnsureVisualRoot()
        {
            if (_rootSpriteRenderer != null)
            {
                _rootSpriteRenderer.enabled = false;
            }

            if (_visualRoot == null)
            {
                var visualRoot = _cachedTransform.Find(PlayerVisualObjectName);
                if (visualRoot == null)
                {
                    visualRoot = new GameObject(PlayerVisualObjectName).transform;
                    visualRoot.SetParent(_cachedTransform, false);
                }

                _visualRoot = visualRoot;
            }

            _visualRoot.localPosition = new Vector3(0f, _playerConfig.visualYOffset, 0f);

            if (_spriteRenderer == null)
            {
                _spriteRenderer = _visualRoot.GetComponent<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    _spriteRenderer = _visualRoot.gameObject.AddComponent<SpriteRenderer>();
                }
            }
        }

        private void EnsureSpecialFxRoot()
        {
            if (_specialFxRoot != null)
            {
                return;
            }

            var existing = _cachedTransform.Find(SpecialFxRootObjectName);
            if (existing == null)
            {
                existing = new GameObject(SpecialFxRootObjectName).transform;
                existing.SetParent(_cachedTransform, false);
            }

            _specialFxRoot = existing;
        }

        private void EnsureDroneVisualRoot()
        {
            if (_droneVisualRoot != null)
            {
                return;
            }

            var existing = _cachedTransform.Find(DroneVisualRootObjectName);
            if (existing == null)
            {
                existing = new GameObject(DroneVisualRootObjectName).transform;
                existing.SetParent(_cachedTransform, false);
            }

            _droneVisualRoot = existing;
        }

        private void EnsureReviveVisualRoot()
        {
            if (_reviveVisualRoot == null)
            {
                var existing = _cachedTransform.Find(ReviveVisualRootObjectName);
                if (existing == null)
                {
                    existing = new GameObject(ReviveVisualRootObjectName).transform;
                    existing.SetParent(_cachedTransform, false);
                }

                _reviveVisualRoot = existing;
            }

            if (_reviveRadiusRenderer == null)
            {
                var radiusChild = _reviveVisualRoot.Find("ReviveRadius");
                if (radiusChild == null)
                {
                    radiusChild = new GameObject("ReviveRadius").transform;
                    radiusChild.SetParent(_reviveVisualRoot, false);
                }

                _reviveRadiusRenderer = radiusChild.GetComponent<LineRenderer>();
                if (_reviveRadiusRenderer == null)
                {
                    _reviveRadiusRenderer = radiusChild.gameObject.AddComponent<LineRenderer>();
                }
            }

            if (_reviveBarBackgroundRenderer == null)
            {
                var backgroundChild = _reviveVisualRoot.Find("ReviveBarBackground");
                if (backgroundChild == null)
                {
                    backgroundChild = new GameObject("ReviveBarBackground").transform;
                    backgroundChild.SetParent(_reviveVisualRoot, false);
                }

                _reviveBarBackgroundRenderer = backgroundChild.GetComponent<SpriteRenderer>();
                if (_reviveBarBackgroundRenderer == null)
                {
                    _reviveBarBackgroundRenderer = backgroundChild.gameObject.AddComponent<SpriteRenderer>();
                }

                _reviveBarBackgroundRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
                _reviveBarBackgroundRenderer.color = reviveBarBackgroundColor;
                _reviveBarBackgroundRenderer.sortingOrder = 27;
            }

            if (_reviveBarFillRenderer == null)
            {
                var fillChild = _reviveVisualRoot.Find("ReviveBarFill");
                if (fillChild == null)
                {
                    fillChild = new GameObject("ReviveBarFill").transform;
                    fillChild.SetParent(_reviveVisualRoot, false);
                }

                _reviveBarFillRenderer = fillChild.GetComponent<SpriteRenderer>();
                if (_reviveBarFillRenderer == null)
                {
                    _reviveBarFillRenderer = fillChild.gameObject.AddComponent<SpriteRenderer>();
                }

                _reviveBarFillRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
                _reviveBarFillRenderer.color = reviveBarFillColor;
                _reviveBarFillRenderer.sortingOrder = 28;
            }
        }

        private void EnsureDroneVisualCount()
        {
            EnsureDroneVisualRoot();

            while (_droneVisuals.Count < _droneVisualCount)
            {
                _droneVisuals.Add(CreateDroneVisual());
            }

            while (_droneVisuals.Count > _droneVisualCount)
            {
                var lastIndex = _droneVisuals.Count - 1;
                var visual = _droneVisuals[lastIndex];
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }

                _droneVisuals.RemoveAt(lastIndex);
            }
        }

        private Transform CreateDroneVisual()
        {
            EnsureDroneVisualRoot();

            var satelliteRoot = new GameObject("SatelliteVisual");
            satelliteRoot.transform.SetParent(_droneVisualRoot, false);

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(satelliteRoot.transform, false);

            var renderer = visualObject.AddComponent<SpriteRenderer>();
            var frames = RuntimeSpriteFactory.GetSexyDroneAnimationFrames();
            var hasAnimation = frames != null && frames.Length > 0;
            renderer.sprite = hasAnimation ? frames[0] : RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = Color.white;
            renderer.sortingOrder = satelliteVisualSortOrder;
            const float visualScale = 1.5f;
            visualObject.transform.localScale = Vector3.one * visualScale;
            visualObject.transform.localPosition = GetSpriteCenterAlignOffset(renderer.sprite, visualScale);

            if (hasAnimation && frames.Length > 1)
            {
                var animator = visualObject.AddComponent<SpriteFxAnimator>();
                animator.Initialize(renderer, frames, satelliteVisualAnimationFps, loop: true, destroyOnComplete: false);
            }

            return satelliteRoot.transform;
        }

        private void UpdateDroneVisuals()
        {
            if (IsServer)
            {
                return;
            }

            var coop = MultiplayerCoopController.Instance;
            if (coop != null && coop.Phase != MultiplayerRunPhase.Running)
            {
                return;
            }

            EnsureDroneVisualCount();
            if (_droneVisuals.Count <= 0 || _droneOrbitRadius <= 0.0001f)
            {
                return;
            }

            for (var visualIndex = 0; visualIndex < _droneVisuals.Count; visualIndex++)
            {
                var visual = _droneVisuals[visualIndex];
                if (visual == null)
                {
                    continue;
                }

                var phase = (360f / Mathf.Max(1, _droneVisuals.Count)) * visualIndex;
                var angle = (_droneOrbitSeedDegrees + (Time.time * _droneOrbitSpeedDegrees) + phase) * Mathf.Deg2Rad;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _droneOrbitRadius;
                visual.position = new Vector3(
                    _cachedTransform.position.x + offset.x,
                    _cachedTransform.position.y + offset.y,
                    0f);
            }
        }

        private void ClearDroneVisuals()
        {
            for (var i = _droneVisuals.Count - 1; i >= 0; i--)
            {
                var visual = _droneVisuals[i];
                if (visual != null)
                {
                    Destroy(visual.gameObject);
                }
            }

            _droneVisuals.Clear();
        }

        private void ClearLocalTurrets()
        {
            for (var i = _localTurrets.Count - 1; i >= 0; i--)
            {
                DestroyTurretVisualAt(i);
            }

            _localTurrets.Clear();
        }

        private void ClearTransientFxObjects()
        {
            EnsureSpecialFxRoot();
            if (_specialFxRoot == null)
            {
                return;
            }

            for (var i = _specialFxRoot.childCount - 1; i >= 0; i--)
            {
                var child = _specialFxRoot.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void UpdateReviveVisuals()
        {
            EnsureReviveVisualRoot();
            if (_reviveVisualRoot == null)
            {
                return;
            }

            _reviveVisualRoot.gameObject.SetActive(_showReviveVisuals);
            if (!_showReviveVisuals)
            {
                return;
            }

            _reviveVisualRoot.localPosition = Vector3.zero;

            if (_reviveRadiusRenderer != null)
            {
                ConfigureLineRenderer(_reviveRadiusRenderer, reviveRadiusColor, reviveRadiusLineWidth, loop: true, useWorldSpace: false);
                _reviveRadiusRenderer.sortingOrder = 26;
                SetCircleLinePositions(_reviveRadiusRenderer, Vector3.zero, _reviveRadius, ringFxSegments, -0.02f);
                _reviveRadiusRenderer.enabled = true;
            }

            var barWidth = Mathf.Max(0.2f, reviveBarSize.x);
            var barHeight = Mathf.Max(0.03f, reviveBarSize.y);
            var showProgress = _reviveProgress > 0.0001f;

            if (_reviveBarBackgroundRenderer != null)
            {
                _reviveBarBackgroundRenderer.transform.localPosition = reviveBarLocalOffset;
                _reviveBarBackgroundRenderer.transform.localScale = new Vector3(barWidth, barHeight, 1f);
                _reviveBarBackgroundRenderer.enabled = showProgress;
            }

            if (_reviveBarFillRenderer != null)
            {
                var clampedProgress = Mathf.Clamp01(_reviveProgress);
                var fillWidth = Mathf.Max(0.0001f, barWidth * clampedProgress);
                _reviveBarFillRenderer.transform.localPosition = reviveBarLocalOffset + new Vector3((-barWidth + fillWidth) * 0.5f, 0f, -0.01f);
                _reviveBarFillRenderer.transform.localScale = new Vector3(fillWidth, barHeight * 0.72f, 1f);
                _reviveBarFillRenderer.enabled = showProgress && clampedProgress > 0f;
            }
        }

        private void CleanupExpiredTurrets()
        {
            if (IsServer || _localTurrets.Count <= 0)
            {
                return;
            }

            for (var i = _localTurrets.Count - 1; i >= 0; i--)
            {
                if (_localTurrets[i] == null || _localTurrets[i].Root == null || Time.time < _localTurrets[i].ExpiresAt)
                {
                    continue;
                }

                DestroyTurretVisualAt(i);
            }
        }

        private void DestroyTurretVisualAt(int index)
        {
            if (index < 0 || index >= _localTurrets.Count)
            {
                return;
            }

            var turret = _localTurrets[index];
            if (turret?.Root != null)
            {
                if (turret.FireAnimationCoroutine != null)
                {
                    StopCoroutine(turret.FireAnimationCoroutine);
                    turret.FireAnimationCoroutine = null;
                }

                Destroy(turret.Root.gameObject);
            }

            _localTurrets.RemoveAt(index);
        }

        private void PlayNearestTurretFireAnimation(Vector3 from, Vector3 to)
        {
            if (_localTurrets.Count <= 0)
            {
                return;
            }

            LocalTurretVisual best = null;
            var bestDistanceSq = 0.2f * 0.2f;
            for (var i = 0; i < _localTurrets.Count; i++)
            {
                var turret = _localTurrets[i];
                if (turret?.Root == null)
                {
                    continue;
                }

                var distanceSq = (turret.Root.position - from).sqrMagnitude;
                if (best != null && distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                best = turret;
                bestDistanceSq = distanceSq;
            }

            if (best == null || best.Renderer == null)
            {
                return;
            }

            var fireDirection = to - from;
            if (Mathf.Abs(fireDirection.x) > 0.0001f)
            {
                best.Renderer.flipX = fireDirection.x < 0f;
            }

            if (best.FireFrames == null || best.FireFrames.Length <= 0)
            {
                best.Renderer.sprite = best.IdleFrame;
                return;
            }

            if (best.FireAnimationCoroutine != null)
            {
                StopCoroutine(best.FireAnimationCoroutine);
            }

            best.FireAnimationCoroutine = StartCoroutine(PlayTurretFireAnimationRoutine(best));
        }

        private IEnumerator PlayTurretFireAnimationRoutine(LocalTurretVisual turret)
        {
            if (turret == null || turret.Renderer == null)
            {
                yield break;
            }

            var frameDuration = 1f / Mathf.Max(0.1f, turretVisualAnimationFps);
            for (var i = 0; i < turret.FireFrames.Length; i++)
            {
                if (turret.Renderer == null)
                {
                    yield break;
                }

                turret.Renderer.sprite = turret.FireFrames[i];
                yield return new WaitForSeconds(frameDuration);
            }

            if (turret.Renderer != null && turret.IdleFrame != null)
            {
                turret.Renderer.sprite = turret.IdleFrame;
            }

            turret.FireAnimationCoroutine = null;
        }

        private void SpawnLineFx(Vector3 from, Vector3 to, Color color, float width, float duration, string name)
        {
            var points = new[] { from, to };
            SpawnPolylineFx(points, color, width, duration, loop: false, name);
        }

        private void SpawnRingFx(Vector3 center, float radius, Color color, float width, float duration, string name)
        {
            EnsureSpecialFxRoot();
            var fxObject = new GameObject(name);
            fxObject.transform.SetParent(_specialFxRoot, false);
            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, color, width, loop: true, useWorldSpace: true);
            SetCircleLinePositions(lineRenderer, center, radius, ringFxSegments, -0.02f);
            Destroy(fxObject, Mathf.Max(0.02f, duration));
        }

        private void SpawnPolylineFx(IReadOnlyList<Vector3> points, Color color, float width, float duration, bool loop, string name)
        {
            if (points == null || points.Count <= 1)
            {
                return;
            }

            EnsureSpecialFxRoot();
            var fxObject = new GameObject(name);
            fxObject.transform.SetParent(_specialFxRoot, false);
            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, color, width, loop, useWorldSpace: true);
            lineRenderer.positionCount = points.Count;
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                lineRenderer.SetPosition(i, new Vector3(point.x, point.y, -0.02f));
            }

            Destroy(fxObject, Mathf.Max(0.02f, duration));
        }

        private void ConfigureLineRenderer(LineRenderer lineRenderer, Color color, float width, bool loop, bool useWorldSpace)
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.useWorldSpace = useWorldSpace;
            lineRenderer.loop = loop;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.startWidth = Mathf.Max(0.001f, width);
            lineRenderer.endWidth = Mathf.Max(0.001f, width);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.sortingOrder = 500;
            lineRenderer.sharedMaterial = GetOrCreateSharedFxMaterial();
        }

        private static void SetCircleLinePositions(LineRenderer lineRenderer, Vector3 center, float radius, int segments, float z)
        {
            if (lineRenderer == null)
            {
                return;
            }

            var clampedRadius = Mathf.Max(0.01f, radius);
            var clampedSegments = Mathf.Clamp(segments, 8, 96);
            lineRenderer.positionCount = clampedSegments;

            for (var i = 0; i < clampedSegments; i++)
            {
                var t = i / (float)clampedSegments;
                var angle = t * Mathf.PI * 2f;
                var point = new Vector3(
                    center.x + (Mathf.Cos(angle) * clampedRadius),
                    center.y + (Mathf.Sin(angle) * clampedRadius),
                    z);
                lineRenderer.SetPosition(i, point);
            }
        }

        private static Material GetOrCreateSharedFxMaterial()
        {
            if (_sharedFxMaterial != null)
            {
                return _sharedFxMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _sharedFxMaterial = new Material(shader)
            {
                name = "MultiplayerWeaponFxMat",
                hideFlags = HideFlags.HideAndDontSave,
            };

            return _sharedFxMaterial;
        }

        private static Vector3 GetSpriteCenterAlignOffset(Sprite sprite, float uniformScale)
        {
            if (sprite == null)
            {
                return Vector3.zero;
            }

            var centerFromPivot = sprite.bounds.center;
            return new Vector3(
                -centerFromPivot.x * uniformScale,
                -centerFromPivot.y * uniformScale,
                0f);
        }

        private static Sprite[] ExtractFireFrames(Sprite[] allFrames)
        {
            if (allFrames == null || allFrames.Length <= 1)
            {
                return System.Array.Empty<Sprite>();
            }

            var fireFrames = new Sprite[allFrames.Length - 1];
            System.Array.Copy(allFrames, 1, fireFrames, 0, fireFrames.Length);
            return fireFrames;
        }

        private void ApplyWeaponAim(Vector2 aimDirection, bool fromFireEvent)
        {
            if (_weaponVisualTransform == null || _weaponVisualRenderer == null)
            {
                return;
            }

            var normalizedDirection = NormalizeDirection(aimDirection, Vector2.right);
            _playerSpriteAnimator?.SetLookDirection(normalizedDirection);
            var flipX = ResolveWeaponFlipX(normalizedDirection);
            var rotationDegrees = CalculateWeaponRotationDegrees(normalizedDirection, flipX);
            var localPosition = CalculateWeaponLocalPosition(normalizedDirection, flipX, rotationDegrees);
            _weaponVisualTransform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            _weaponVisualRenderer.flipX = flipX;
            UpdateWeaponSorting(normalizedDirection);
            _weaponVisualTransform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);

            if (fromFireEvent)
            {
                _weaponSpriteAnimator?.PlayAttack(normalizedDirection);
            }
        }

        private Vector2 CalculateWeaponLocalPosition(Vector2 normalizedDirection, bool flipX, float rotationDegrees)
        {
            var weaponOffset = _playerConfig.weaponVisualOffset;
            var aimDistance = Mathf.Max(0.05f, _playerConfig.weaponAimDistance);
            if (flipX)
            {
                weaponOffset.x = -weaponOffset.x;
            }

            var rotatedOffset = RotateOffsetByDegrees(weaponOffset, rotationDegrees);
            return ResolveWeaponOrbitCenterLocal() + normalizedDirection * aimDistance + rotatedOffset;
        }

        private Vector2 ResolveWeaponOrbitCenterLocal()
        {
            if (_spriteRenderer != null)
            {
                var worldCenter = _spriteRenderer.bounds.center;
                var localCenter = _cachedTransform.InverseTransformPoint(worldCenter);
                return new Vector2(localCenter.x, localCenter.y);
            }

            return new Vector2(0f, _playerConfig.visualYOffset);
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
            var signedAngleFromHorizontal = Mathf.Atan2(normalizedDirection.y, Mathf.Abs(normalizedDirection.x)) * Mathf.Rad2Deg;
            if (flipX)
            {
                signedAngleFromHorizontal = -signedAngleFromHorizontal;
            }

            return signedAngleFromHorizontal + _playerConfig.weaponAimRotationOffsetDegrees;
        }

        private void UpdateWeaponSorting(Vector2 aimDirection)
        {
            if (_weaponVisualRenderer == null || _spriteRenderer == null)
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
            _weaponVisualRenderer.sortingLayerID = _spriteRenderer.sortingLayerID;
            _weaponVisualRenderer.sortingOrder = _spriteRenderer.sortingOrder + offset;
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

        private static Vector2 NormalizeDirection(Vector2 direction, Vector2 fallback)
        {
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return fallback.sqrMagnitude > 0.000001f ? fallback.normalized : Vector2.right;
            }

            return direction.normalized;
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

            targetTransform.localScale = Vector3.one * (clampedSize / spriteSize);
        }
    }
}
