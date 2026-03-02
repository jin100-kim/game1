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
            Hurt,
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
        private const float HurtFlashDuration = 0.08f;
        private const float HurtMinStateDuration = 0.08f;
        private static readonly Color HurtFlashColor = new(1f, 0.62f, 0.62f, 1f);

        private SpriteRenderer _targetRenderer;
        private Sprite[] _frames;
        private EnemyAnimationProfile _profile;
        private float _framesPerSecond = 9f;
        private bool _flipByMoveDirection = true;
        private Color _defaultColor = Color.white;

        private FrameRange _idleRange;
        private FrameRange _moveRange;
        private FrameRange _hurtRange;
        private FrameRange _dieRange;
        private bool _hasHurtRange;
        private bool _hasDieRange;

        private AnimationState _state = AnimationState.Idle;
        private int _currentFrameIndex;
        private float _frameCursor;
        private bool _isDying;
        private float _hurtFlashTimer;
        private float _hurtStateTimer;
        private Vector2 _lastVelocity;

        public void Initialize(SpriteRenderer targetRenderer, Sprite[] frames, EnemyAnimationProfile profile)
        {
            _targetRenderer = targetRenderer;
            _frames = frames ?? Array.Empty<Sprite>();
            _profile = profile;
            _framesPerSecond = Mathf.Max(0.1f, profile != null ? profile.animationFps : 9f);
            _flipByMoveDirection = profile == null || profile.flipByMoveDirection;
            _defaultColor = targetRenderer != null ? targetRenderer.color : Color.white;
            ConfigureRanges(profile);

            _state = AnimationState.Idle;
            _frameCursor = 0f;
            _isDying = false;
            _hurtFlashTimer = 0f;
            _hurtStateTimer = 0f;
            _lastVelocity = Vector2.zero;
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

            _lastVelocity = velocity;

            if (_flipByMoveDirection && Mathf.Abs(velocity.x) > 0.01f)
            {
                _targetRenderer.flipX = velocity.x < 0f;
            }

            if (_state == AnimationState.Hurt)
            {
                return;
            }

            var shouldMove = velocity.sqrMagnitude > MoveThreshold * MoveThreshold;
            SetState(shouldMove ? AnimationState.Move : AnimationState.Idle, reset: false);
        }

        public void PlayHurt()
        {
            if (_isDying || _targetRenderer == null || _frames.Length == 0)
            {
                return;
            }

            TriggerHurtFlash();
            if (!_hasHurtRange)
            {
                return;
            }

            _hurtStateTimer = HurtMinStateDuration;
            SetState(AnimationState.Hurt, reset: true);
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
            UpdateHurtFlash();

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

            if (_state == AnimationState.Hurt)
            {
                var finished = AdvanceOneShot(range);
                _hurtStateTimer = Mathf.Max(0f, _hurtStateTimer - Time.deltaTime);
                if (finished && _hurtStateTimer <= 0f)
                {
                    var shouldMove = _lastVelocity.sqrMagnitude > MoveThreshold * MoveThreshold;
                    SetState(shouldMove ? AnimationState.Move : AnimationState.Idle, reset: true);
                }

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
            var hurtStart = profile != null ? profile.hurtStartFrame : 0;
            var hurtEnd = profile != null ? profile.hurtEndFrame : 0;
            var hasHurtRange = profile != null && profile.useHurtAnimation;
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

                if (TryResolveNamedRange(profile, out var hurtRange, "Hurt", "Hit", "Damage"))
                {
                    hurtStart = hurtRange.startFrame;
                    hurtEnd = hurtRange.endFrame;
                    hasHurtRange = true;
                }
            }

            _idleRange = BuildRange(idleStart, idleEnd, max);
            _moveRange = BuildRange(moveStart, moveEnd, max);
            _hurtRange = BuildRange(hurtStart, hurtEnd, max);
            _dieRange = BuildRange(dieStart, dieEnd, max);
            _hasHurtRange = hasHurtRange && _hurtRange.Length > 0;
            _hasDieRange = _dieRange.Length > 0;

            ExcludeRangeFrom(ref _moveRange, _hurtRange, _hasHurtRange);
            ExcludeRangeFrom(ref _moveRange, _dieRange, _hasDieRange);

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
                AnimationState.Hurt => _hasHurtRange ? _hurtRange : _moveRange,
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

        private bool AdvanceOneShot(FrameRange range)
        {
            var steps = Mathf.FloorToInt(_frameCursor);
            if (steps <= 0)
            {
                return _currentFrameIndex >= range.End;
            }

            _frameCursor -= steps;
            var nextFrame = Mathf.Min(range.End, _currentFrameIndex + steps);
            if (nextFrame == _currentFrameIndex)
            {
                return _currentFrameIndex >= range.End;
            }

            _currentFrameIndex = nextFrame;
            ApplyFrame(_currentFrameIndex);
            return _currentFrameIndex >= range.End;
        }

        private void ApplyFrame(int frameIndex)
        {
            if (_targetRenderer == null || _frames == null || frameIndex < 0 || frameIndex >= _frames.Length)
            {
                return;
            }

            _targetRenderer.sprite = _frames[frameIndex];
        }

        private static void ExcludeRangeFrom(ref FrameRange source, FrameRange blocked, bool blockedEnabled)
        {
            if (!blockedEnabled || source.Length <= 0 || blocked.Length <= 0)
            {
                return;
            }

            if (blocked.End < source.Start || blocked.Start > source.End)
            {
                return;
            }

            var beforeLength = blocked.Start - source.Start;
            var afterLength = source.End - blocked.End;

            if (beforeLength <= 0 && afterLength <= 0)
            {
                return;
            }

            if (beforeLength >= afterLength && beforeLength > 0)
            {
                source = new FrameRange(source.Start, blocked.Start - 1);
                return;
            }

            if (afterLength > 0)
            {
                source = new FrameRange(blocked.End + 1, source.End);
            }
        }

        private void TriggerHurtFlash()
        {
            _hurtFlashTimer = HurtFlashDuration;
            if (_targetRenderer != null)
            {
                _targetRenderer.color = HurtFlashColor;
            }
        }

        private void UpdateHurtFlash()
        {
            if (_targetRenderer == null || _hurtFlashTimer <= 0f)
            {
                return;
            }

            _hurtFlashTimer -= Time.deltaTime;
            if (_hurtFlashTimer <= 0f)
            {
                _targetRenderer.color = _defaultColor;
            }
        }
    }
}
