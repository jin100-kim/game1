using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EJR.Game.Gameplay
{
    public sealed class PlayerMover : MonoBehaviour
    {
        private const float DefaultCollisionRadius = 0.35f;

        [SerializeField, Min(0.1f)] private float moveSpeed = 5f;
        [SerializeField] private Rect movementBounds = new Rect(-12f, -7f, 24f, 14f);
        [SerializeField] private bool clampToBounds = true;

        private float _speedMultiplier = 1f;
        private float _collisionRadius = DefaultCollisionRadius;
        private float _facingTurnSpeedDegreesPerSecond = 1080f;
        private Func<Vector2> _moveInputReader;
        private Func<Vector2> _externalVelocityReader;
        private EJR.Game.Core.PlayerStatsRuntime _stats;
        private Vector2 _externalVelocity;
        private Rigidbody2D _rb;

        public Vector2 CurrentVelocity { get; private set; }
        public Vector2 LastFacingDirection { get; private set; } = Vector2.right;
        public Vector2 CurrentFacingDirection { get; private set; } = Vector2.right;

        public void Initialize(PlayerConfig config, EJR.Game.Core.PlayerStatsRuntime stats, Rect bounds)
        {
            if (config != null)
            {
                moveSpeed = Mathf.Max(0.1f, config.moveSpeed);
                _collisionRadius = Mathf.Max(0.05f, config.collisionRadius);
                _facingTurnSpeedDegreesPerSecond = Mathf.Max(90f, config.facingTurnSpeedDegreesPerSecond);
            }

            _stats = stats;
            _speedMultiplier = stats != null ? stats.MoveSpeedMultiplier : 1f;
            movementBounds = bounds;
        }

        public void SetMoveInputReader(Func<Vector2> moveInputReader)
        {
            _moveInputReader = moveInputReader;
        }

        public void SetExternalVelocityReader(Func<Vector2> externalVelocityReader)
        {
            _externalVelocityReader = externalVelocityReader;
        }

        public void SetExternalDisplacement(Vector2 velocityLike)
        {
            _externalVelocity = SanitizeVector(velocityLike);
        }

        public void SetMoveSpeedMultiplier(float speedMultiplier)
        {
            _speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
        }

        private void Awake()
        {
            if (moveSpeed <= 0f)
            {
                moveSpeed = 5f;
            }

            if (movementBounds.width <= 0f || movementBounds.height <= 0f)
            {
                movementBounds = new Rect(-12f, -7f, 24f, 14f);
            }

            if (_speedMultiplier <= 0f)
            {
                _speedMultiplier = 1f;
            }

            if (_collisionRadius <= 0f)
            {
                _collisionRadius = DefaultCollisionRadius;
            }

            if (_facingTurnSpeedDegreesPerSecond <= 0f)
            {
                _facingTurnSpeedDegreesPerSecond = 1080f;
            }

            _rb = GetComponent<Rigidbody2D>();
        }

        private UnityEngine.Tilemaps.Tilemap _groundTilemap;
        private UnityEngine.Tilemaps.Tilemap _propsTilemap;
        private bool _tilemapsResolved = false;

        public void SetColliders(Collider2D ground, Collider2D props) { } // 미사용, 호환용

        public void SetTilemaps(UnityEngine.Tilemaps.Tilemap ground, UnityEngine.Tilemaps.Tilemap props)
        {
            _groundTilemap = ground;
            _propsTilemap  = props;
            _tilemapsResolved = ground != null;
        }

        private void TryResolveTilemaps()
        {
            if (_tilemapsResolved) return;

            var groundObj = GameObject.Find("Tilemap_Ground");
            var propsObj  = GameObject.Find("Tilemap_Props");

            if (groundObj != null) _groundTilemap = groundObj.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            if (propsObj  != null) _propsTilemap  = propsObj.GetComponent<UnityEngine.Tilemaps.Tilemap>();

            if (_groundTilemap != null)
                _tilemapsResolved = true;
        }

        private bool IsWalkable(Vector3 position)
        {
            // Ground 타일이 없으면 이동 불가
            if (_groundTilemap != null && !_groundTilemap.HasTile(_groundTilemap.WorldToCell(position)))
                return false;
            // Props 타일이 있으면 이동 불가
            if (_propsTilemap != null && _propsTilemap.HasTile(_propsTilemap.WorldToCell(position)))
                return false;
            return true;
        }

        private void FixedUpdate()
        {
            // Rigidbody2D가 Awake 이후에 붙는 경우 재탐색
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();

            TryResolveTilemaps();

            if (_stats != null)
            {
                _speedMultiplier = Mathf.Max(0.1f, _stats.MoveSpeedMultiplier);
            }

            var move = _moveInputReader != null ? _moveInputReader.Invoke() : ReadMovementInput();
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y))
            {
                move = Vector2.zero;
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            var externalVelocity = ReadExternalVelocity();

            if (move.sqrMagnitude > 0.000001f)
            {
                LastFacingDirection = move.normalized;
            }

            CurrentFacingDirection = RotateDirectionTowards(
                CurrentFacingDirection,
                LastFacingDirection,
                _facingTurnSpeedDegreesPerSecond * Time.deltaTime);

            var previous = (Vector2)transform.position;
            var velocity = (move * (moveSpeed * _speedMultiplier)) + externalVelocity;
            var next = previous + velocity * Time.fixedDeltaTime;

            if (_rb != null)
            {
                _rb.MovePosition(next); // 물리 엔진이 TilemapCollider2D 충돌 자동 처리
                CurrentVelocity = velocity;
            }
            else
            {
                // Rigidbody2D 없으면 기존 방식 폴백
                next.x = Mathf.Clamp(next.x, movementBounds.xMin, movementBounds.xMax);
                next.y = Mathf.Clamp(next.y, movementBounds.yMin, movementBounds.yMax);
                transform.position = new Vector3(next.x, next.y, 0f);
                CurrentVelocity = (next - previous) / Mathf.Max(0.0001f, Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            CurrentVelocity = Vector2.zero;
            _externalVelocity = Vector2.zero;
        }

        private Vector2 ReadExternalVelocity()
        {
            if (_externalVelocityReader == null)
            {
                return _externalVelocity;
            }

            return SanitizeVector(_externalVelocityReader.Invoke());
        }

        private static Vector2 RotateDirectionTowards(Vector2 current, Vector2 target, float maxDegreesDelta)
        {
            var normalizedCurrent = current.sqrMagnitude > 0.000001f ? current.normalized : Vector2.right;
            var normalizedTarget = target.sqrMagnitude > 0.000001f ? target.normalized : normalizedCurrent;

            var currentAngle = Mathf.Atan2(normalizedCurrent.y, normalizedCurrent.x) * Mathf.Rad2Deg;
            var targetAngle = Mathf.Atan2(normalizedTarget.y, normalizedTarget.x) * Mathf.Rad2Deg;
            var nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Mathf.Max(0f, maxDegreesDelta));
            var nextRadians = nextAngle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(nextRadians), Mathf.Sin(nextRadians));
        }

        private static Vector2 ReadMovementInput()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var xInput = 0f;
                var yInput = 0f;

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) xInput -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) xInput += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) yInput -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) yInput += 1f;

                return new Vector2(xInput, yInput);
            }
#endif

            var x = 0f;
            var y = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;

            if (Mathf.Approximately(x, 0f)) x = Input.GetAxisRaw("Horizontal");
            if (Mathf.Approximately(y, 0f)) y = Input.GetAxisRaw("Vertical");

            return new Vector2(x, y);
        }

        private static Vector2 SanitizeVector(Vector2 value)
        {
            if (!float.IsFinite(value.x) || !float.IsFinite(value.y))
            {
                return Vector2.zero;
            }

            return value;
        }

        private void OnDrawGizmos()
        {
            var radius = Mathf.Max(0.05f, _collisionRadius);
            Gizmos.color = new Color(0.2f, 1f, 1f, 0.95f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
