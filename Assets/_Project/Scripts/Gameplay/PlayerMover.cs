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
        private Func<Vector2> _moveInputReader;

        public Vector2 CurrentVelocity { get; private set; }

        public void Initialize(PlayerConfig config, EJR.Game.Core.PlayerStatsRuntime stats, Rect bounds)
        {
            if (config != null)
            {
                moveSpeed = Mathf.Max(0.1f, config.moveSpeed);
                _collisionRadius = Mathf.Max(0.05f, config.collisionRadius);
            }

            _speedMultiplier = stats != null ? stats.MoveSpeedMultiplier : 1f;
            movementBounds = bounds;
        }

        public void SetMoveInputReader(Func<Vector2> moveInputReader)
        {
            _moveInputReader = moveInputReader;
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
        }

        private void Update()
        {
            var move = _moveInputReader != null ? _moveInputReader.Invoke() : ReadMovementInput();
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y))
            {
                move = Vector2.zero;
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            var previous = transform.position;
            var delta = (Vector3)move * (moveSpeed * _speedMultiplier * Time.deltaTime);
            var next = previous + delta;

            if (clampToBounds)
            {
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

        private void OnDrawGizmos()
        {
            var radius = Mathf.Max(0.05f, _collisionRadius);
            Gizmos.color = new Color(0.2f, 1f, 1f, 0.95f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
