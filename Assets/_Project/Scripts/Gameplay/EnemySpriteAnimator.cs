using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemySpriteAnimator : MonoBehaviour
    {
        private enum AnimationState
        {
            Idle,
            Move,
            Die,
        }

        private readonly struct FrameRange
        {
            public readonly int Start;
            public readonly int End;

            public FrameRange(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Length => End >= Start ? End - Start + 1 : 0;
        }

        private const float MoveThreshold = 0.02f;

        private SpriteRenderer _targetRenderer;
        private Sprite[] _frames;
        private EnemyAnimationProfile _profile;
        private float _framesPerSecond = 9f;
        private bool _flipByMoveDirection = true;

        private FrameRange _idleRange;
        private FrameRange _moveRange;
        private FrameRange _dieRange;
        private bool _hasDieRange;

        private AnimationState _state = AnimationState.Idle;
        private int _currentFrameIndex;
        private float _frameCursor;
        private bool _isDying;

        public void Initialize(SpriteRenderer targetRenderer, Sprite[] frames, EnemyAnimationProfile profile)
        {
            _targetRenderer = targetRenderer;
            _frames = frames ?? Array.Empty<Sprite>();
            _profile = profile;
            _framesPerSecond = Mathf.Max(0.1f, profile != null ? profile.animationFps : 9f);
            _flipByMoveDirection = profile == null || profile.flipByMoveDirection;
            ConfigureRanges(profile);

            _state = AnimationState.Idle;
            _frameCursor = 0f;
            _isDying = false;
            _currentFrameIndex = _idleRange.Start;
            ApplyFrame(_currentFrameIndex);
        }

        public bool TryGetClipRange(string clipName, out int startFrame, out int endFrame)
        {
            startFrame = 0;
            endFrame = 0;

            if (_profile == null || !_profile.TryGetClipRange(clipName, out var clipRange))
            {
                return false;
            }

            startFrame = clipRange.startFrame;
            endFrame = clipRange.endFrame;
            return true;
        }

        public void SetMotion(Vector2 velocity)
        {
            if (_isDying || _targetRenderer == null || _frames.Length == 0)
            {
                return;
            }

            if (_flipByMoveDirection && Mathf.Abs(velocity.x) > 0.01f)
            {
                _targetRenderer.flipX = velocity.x < 0f;
            }

            var shouldMove = velocity.sqrMagnitude > MoveThreshold * MoveThreshold;
            SetState(shouldMove ? AnimationState.Move : AnimationState.Idle, reset: false);
        }

        public float PlayDie()
        {
            if (_isDying || !_hasDieRange || _targetRenderer == null || _frames.Length == 0)
            {
                return 0f;
            }

            _isDying = true;
            SetState(AnimationState.Die, reset: true);
            return _dieRange.Length / Mathf.Max(0.1f, _framesPerSecond);
        }

        private void Update()
        {
            if (_targetRenderer == null || _frames == null || _frames.Length == 0)
            {
                return;
            }

            var range = GetRange(_state);
            if (range.Length <= 0 || _framesPerSecond <= 0f)
            {
                return;
            }

            _frameCursor += Time.deltaTime * _framesPerSecond;
            if (_state == AnimationState.Die)
            {
                AdvanceOneShot(range);
                return;
            }

            AdvanceLoop(range);
        }

        private void ConfigureRanges(EnemyAnimationProfile profile)
        {
            var max = Mathf.Max(0, _frames.Length - 1);

            var idleStart = profile != null ? profile.idleStartFrame : 0;
            var idleEnd = profile != null ? profile.idleEndFrame : 0;
            var moveStart = profile != null ? profile.moveStartFrame : 0;
            var moveEnd = profile != null ? profile.moveEndFrame : max;
            var dieStart = profile != null ? profile.dieStartFrame : 0;
            var dieEnd = profile != null ? profile.dieEndFrame : 0;

            if (profile != null)
            {
                if (TryResolveNamedRange(profile, out var idleRange, "Idle"))
                {
                    idleStart = idleRange.startFrame;
                    idleEnd = idleRange.endFrame;
                }

                if (TryResolveNamedRange(profile, out var moveRange, "Move", "Walk", "Fly", "Jump"))
                {
                    moveStart = moveRange.startFrame;
                    moveEnd = moveRange.endFrame;
                }

                if (TryResolveNamedRange(profile, out var dieRange, "Die", "Death"))
                {
                    dieStart = dieRange.startFrame;
                    dieEnd = dieRange.endFrame;
                }
            }

            _idleRange = BuildRange(idleStart, idleEnd, max);
            _moveRange = BuildRange(moveStart, moveEnd, max);
            _dieRange = BuildRange(dieStart, dieEnd, max);
            _hasDieRange = _dieRange.Length > 0;

            if (_idleRange.Length == 0)
            {
                _idleRange = new FrameRange(0, Mathf.Min(0, max));
            }

            if (_moveRange.Length == 0)
            {
                _moveRange = _idleRange;
            }
        }

        private static bool TryResolveNamedRange(
            EnemyAnimationProfile profile,
            out EnemyAnimationClipRange clipRange,
            params string[] candidateNames)
        {
            clipRange = default;
            if (profile == null || candidateNames == null || candidateNames.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < candidateNames.Length; i++)
            {
                if (!profile.TryGetClipRange(candidateNames[i], out clipRange))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static FrameRange BuildRange(int start, int end, int maxFrameIndex)
        {
            if (maxFrameIndex < 0)
            {
                return new FrameRange(0, -1);
            }

            var clampedStart = Mathf.Clamp(start, 0, maxFrameIndex);
            var clampedEnd = Mathf.Clamp(end, 0, maxFrameIndex);
            if (clampedEnd < clampedStart)
            {
                (clampedStart, clampedEnd) = (clampedEnd, clampedStart);
            }

            return new FrameRange(clampedStart, clampedEnd);
        }

        private FrameRange GetRange(AnimationState state)
        {
            return state switch
            {
                AnimationState.Move => _moveRange,
                AnimationState.Die => _hasDieRange ? _dieRange : _moveRange,
                _ => _idleRange,
            };
        }

        private void SetState(AnimationState nextState, bool reset)
        {
            if (!reset && _state == nextState)
            {
                return;
            }

            _state = nextState;
            _frameCursor = 0f;
            var range = GetRange(_state);
            if (range.Length <= 0)
            {
                return;
            }

            _currentFrameIndex = range.Start;
            ApplyFrame(_currentFrameIndex);
        }

        private void AdvanceLoop(FrameRange range)
        {
            var nextOffset = Mathf.FloorToInt(_frameCursor) % range.Length;
            var nextFrame = range.Start + nextOffset;
            if (nextFrame == _currentFrameIndex)
            {
                return;
            }

            _currentFrameIndex = nextFrame;
            ApplyFrame(_currentFrameIndex);
        }

        private void AdvanceOneShot(FrameRange range)
        {
            var steps = Mathf.FloorToInt(_frameCursor);
            if (steps <= 0)
            {
                return;
            }

            _frameCursor -= steps;
            var nextFrame = Mathf.Min(range.End, _currentFrameIndex + steps);
            if (nextFrame == _currentFrameIndex)
            {
                return;
            }

            _currentFrameIndex = nextFrame;
            ApplyFrame(_currentFrameIndex);
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
