using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class DamageNumberPopup : MonoBehaviour
    {
        private TextMesh _textMesh;
        private Action<DamageNumberPopup> _releaseToPool;
        private float _lifetime;
        private float _riseSpeed;
        private float _elapsed;
        private Color _baseColor;
        private bool _isShowing;

        public void Initialize(TextMesh textMesh, Action<DamageNumberPopup> releaseToPool)
        {
            _textMesh = textMesh;
            _releaseToPool = releaseToPool;
            _isShowing = false;
        }

        public void Show(Vector3 worldPosition, string text, Color color, float lifetime, float riseSpeed)
        {
            if (_textMesh == null)
            {
                _releaseToPool?.Invoke(this);
                return;
            }

            transform.position = worldPosition;
            _lifetime = Mathf.Max(0.1f, lifetime);
            _riseSpeed = Mathf.Max(0.1f, riseSpeed);
            _elapsed = 0f;
            _baseColor = color;
            _textMesh.text = text;
            _textMesh.color = color;
            _isShowing = true;
        }

        private void Update()
        {
            if (!_isShowing)
            {
                return;
            }

            if (_textMesh == null)
            {
                Release();
                return;
            }

            var deltaTime = Time.unscaledDeltaTime;
            _elapsed += deltaTime;
            transform.position += new Vector3(0f, _riseSpeed * deltaTime, 0f);

            var normalizedLife = Mathf.Clamp01(_elapsed / _lifetime);
            var color = _baseColor;
            color.a = 1f - normalizedLife;
            _textMesh.color = color;

            if (_elapsed >= _lifetime)
            {
                Release();
            }
        }

        private void OnDisable()
        {
            _isShowing = false;
        }

        private void Release()
        {
            if (!_isShowing)
            {
                return;
            }

            _isShowing = false;
            _releaseToPool?.Invoke(this);
        }
    }
}
