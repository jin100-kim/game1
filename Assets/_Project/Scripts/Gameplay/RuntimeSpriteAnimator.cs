using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class RuntimeSpriteAnimator : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private float _fps;
        private bool _loop;
        private bool _destroyOnComplete;
        private float _timer;
        private int _index;
        private bool _playing;

        public void Initialize(SpriteRenderer renderer, Sprite[] frames, float fps, bool loop = true, bool destroyOnComplete = false)
        {
            _renderer = renderer;
            _frames = frames;
            _fps = fps;
            _loop = loop;
            _destroyOnComplete = destroyOnComplete;
            _timer = 0f;
            _index = 0;
            _playing = frames != null && frames.Length > 0;

            if (_playing && _renderer != null)
            {
                _renderer.sprite = _frames[0];
            }
        }

        private void Update()
        {
            if (!_playing || _renderer == null || _frames == null || _frames.Length == 0) return;

            _timer += Time.deltaTime;
            if (_timer >= 1f / _fps)
            {
                _timer = 0f;
                _index++;
                if (_index >= _frames.Length)
                {
                    if (_loop)
                    {
                        _index = 0;
                    }
                    else
                    {
                        _playing = false;
                        if (_destroyOnComplete)
                        {
                            Destroy(gameObject);
                        }
                        return;
                    }
                }
                _renderer.sprite = _frames[_index];
            }
        }
    }
}
