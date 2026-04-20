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
        }

        private UnityEngine.Tilemaps.Tilemap _groundTilemap;
        private UnityEngine.Tilemaps.Tilemap _wallTilemap;

        private void Start()
        {
            // Try to find the ground and wall tilemaps in the scene
            var allTilemaps = GameObject.FindObjectsOfType<UnityEngine.Tilemaps.Tilemap>();
            foreach (var tm in allTilemaps)
            {
                string lowerName = tm.name.ToLower();
                if (lowerName.Contains("ground") || lowerName.Contains("floor") || lowerName.Contains("grass"))
                {
                    _groundTilemap = tm;
                }
                else if (lowerName.Contains("wall") || lowerName.Contains("object") || lowerName.Contains("obstacle"))
                {
                    _wallTilemap = tm;
                }
            }
            
            // Fallback for ground if not found by name
            if (_groundTilemap == null && allTilemaps.Length > 0) _groundTilemap = allTilemaps[0];
        }

        private void Update()
        {
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

            var previous = transform.position;
            var delta = ((Vector3)move * (moveSpeed * _speedMultiplier * Time.deltaTime))
                + ((Vector3)externalVelocity * Time.deltaTime);
            var next = previous + delta;

            // --- TILE-BASED MOVEMENT RESTRICTION ---
            if (_groundTilemap != null)
            {
                Vector3Int cellPos = _groundTilemap.WorldToCell(next);
                
                // 1. MUST have a ground tile
                bool hasGround = _groundTilemap.HasTile(cellPos);
                // 2. MUST NOT have a wall tile
                bool hasWall = _wallTilemap != null && _wallTilemap.HasTile(_wallTilemap.WorldToCell(next));

                if (!hasGround || hasWall)
                {
                    // Try to at least allow moving in one axis (sliding)
                    Vector3 nextX = previous + new Vector3(delta.x, 0, 0);
                    Vector3 nextY = previous + new Vector3(0, delta.y, 0);
                    
                    bool canMoveX = _groundTilemap.HasTile(_groundTilemap.WorldToCell(nextX)) && 
                                   (_wallTilemap == null || !_wallTilemap.HasTile(_wallTilemap.WorldToCell(nextX)));
                    bool canMoveY = _groundTilemap.HasTile(_groundTilemap.WorldToCell(nextY)) && 
                                   (_wallTilemap == null || !_wallTilemap.HasTile(_wallTilemap.WorldToCell(nextY)));

                    if (canMoveX) next = nextX;
                    else if (canMoveY) next = nextY;
                    else next = previous;
                }
            }
            else if (clampToBounds)
            {
                // Fallback to rectangle bounds if no tilemap is found
                next.x = Mathf.Clamp(next.x, movementBounds.xMin, movementBounds.xMax);
                next.y = Mathf.Clamp(next.y, movementBounds.yMin, movementBounds.yMax);
            }

            next.z = 0f;
            transform.position = next;
            CurrentVelocity = ((Vector2)(next - previous)) / Mathf.Max(0.0001f, Time.deltaTime);
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
