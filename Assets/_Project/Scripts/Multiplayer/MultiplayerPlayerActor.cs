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

        private static readonly Color[] PlayerPalette =
        {
            new Color(0.97f, 0.95f, 0.70f, 1f),
            new Color(0.62f, 0.90f, 1f, 1f),
            new Color(1f, 0.67f, 0.74f, 1f),
            new Color(0.67f, 1f, 0.77f, 1f),
        };

        private readonly NetworkVariable<Vector2> _networkVelocity =
            new NetworkVariable<Vector2>(
                Vector2.zero,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Owner);

        private PlayerConfig _playerConfig;
        private PlayerMover _playerMover;
        private PlayerSpriteAnimator _playerSpriteAnimator;
        private SpriteRenderer _spriteRenderer;
        private Transform _cachedTransform;
        private Vector3 _lastPosition;
        private bool _initialized;

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
            _spriteRenderer.color = PlayerPalette[(int)(OwnerClientId % (ulong)PlayerPalette.Length)];
            _lastPosition = _cachedTransform.position;

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

            _lastPosition = _cachedTransform.position;
        }

        private void InitializeRuntime()
        {
            if (_initialized)
            {
                return;
            }

            _cachedTransform = transform;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _playerMover = GetComponent<PlayerMover>();
            _playerSpriteAnimator = GetComponent<PlayerSpriteAnimator>();
            _playerConfig = ScriptableObject.CreateInstance<PlayerConfig>();

            var frames = RuntimeSpriteFactory.GetPlayerAnimationFrames();
            var baseSprite = frames.Length > 0 ? frames[0] : RuntimeSpriteFactory.GetSquareSprite();

            _spriteRenderer.sprite = baseSprite;
            _spriteRenderer.sortingOrder = 10;
            _cachedTransform.localScale = Vector3.one * Mathf.Max(0.1f, _playerConfig.visualScale);

            _playerMover.Initialize(_playerConfig, null, arenaBounds);
            _playerSpriteAnimator.Initialize(_spriteRenderer, frames, _playerConfig);

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
            mainCamera.orthographicSize = 6f;
            mainCamera.transform.position = cameraOffset;

            var follow = mainCamera.GetComponent<CameraFollow2D>();
            if (follow == null)
            {
                follow = mainCamera.gameObject.AddComponent<CameraFollow2D>();
            }

            follow.Initialize(transform, cameraOffset, cameraFollowSmoothTime);
        }
    }
}
