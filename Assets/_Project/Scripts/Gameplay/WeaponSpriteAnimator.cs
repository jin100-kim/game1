using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class WeaponSpriteAnimator : MonoBehaviour
    {
        private SpriteRenderer _targetRenderer;
        private Sprite[] _frames = Array.Empty<Sprite>();
        private float _framesPerSecond = 18f;

        private int _idleFrameIndex;
        private int _attackStartFrame;
        private int _attackEndFrame;
        private bool _hasAttackFrames;

        private bool _isAttacking;
        private int _currentFrameIndex;
        private float _frameCursor;

        public void Initialize(SpriteRenderer targetRenderer, Sprite[] frames, PlayerConfig config)
        {
            _targetRenderer = targetRenderer;
            _frames = frames ?? Array.Empty<Sprite>();
            _framesPerSecond = Mathf.Max(1f, config != null ? config.weaponAnimationFps : 18f);

            ConfigureFrameRanges(config);

            _isAttacking = false;
            _frameCursor = 0f;
            _currentFrameIndex = _idleFrameIndex;
            ApplyFrame(_currentFrameIndex);
        }

        public void PlayAttack(Vector2 direction)
        {
            if (_targetRenderer == null || _frames.Length == 0)
            {
                return;
            }

            if (Mathf.Abs(direction.x) > 0.01f)
            {
                _targetRenderer.flipX = direction.x < 0f;
            }

            if (!_hasAttackFrames)
            {
                ApplyFrame(_idleFrameIndex);
                return;
            }

            _isAttacking = true;
            _frameCursor = 0f;
            _currentFrameIndex = _attackStartFrame;
            ApplyFrame(_currentFrameIndex);
        }

        private void Update()
        {
            if (!_isAttacking || _targetRenderer == null || _frames.Length == 0)
            {
                return;
            }

            _frameCursor += Time.deltaTime * _framesPerSecond;
            var steps = Mathf.FloorToInt(_frameCursor);
            if (steps <= 0)
            {
                return;
            }

            _frameCursor -= steps;
            _currentFrameIndex += steps;
            if (_currentFrameIndex > _attackEndFrame)
            {
                _isAttacking = false;
                _currentFrameIndex = _idleFrameIndex;
            }

            ApplyFrame(_currentFrameIndex);
        }

        private void ConfigureFrameRanges(PlayerConfig config)
        {
            var maxFrameIndex = Mathf.Max(0, _frames.Length - 1);
            var requestedIdle = config != null ? config.weaponIdleFrame : 0;
            var requestedAttackStart = config != null ? config.weaponAttackStartFrame : 1;
            var requestedAttackEnd = config != null ? config.weaponAttackEndFrame : 3;

            if (config != null && requestedAttackStart == 0 && requestedAttackEnd == 0 && maxFrameIndex >= 3)
            {
                requestedAttackStart = 1;
                requestedAttackEnd = 3;
            }

            _idleFrameIndex = Mathf.Clamp(requestedIdle, 0, maxFrameIndex);
            _attackStartFrame = Mathf.Clamp(requestedAttackStart, 0, maxFrameIndex);
            _attackEndFrame = Mathf.Clamp(requestedAttackEnd, 0, maxFrameIndex);

            if (_attackEndFrame < _attackStartFrame)
            {
                (_attackStartFrame, _attackEndFrame) = (_attackEndFrame, _attackStartFrame);
            }

            _hasAttackFrames = _frames.Length > 1 && _attackEndFrame >= _attackStartFrame;
            if (!_hasAttackFrames)
            {
                _attackStartFrame = _idleFrameIndex;
                _attackEndFrame = _idleFrameIndex;
            }
        }

        private void ApplyFrame(int frameIndex)
        {
            if (_targetRenderer == null || _frames == null || frameIndex < 0 || frameIndex >= _frames.Length)
            {
                return;
            }

            _targetRenderer.sprite = _frames[frameIndex];
        }
    }
}
