using UnityEngine;

namespace EJR.Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class DamageNumberPopup : MonoBehaviour
    {
        private TextMesh _textMesh;
        private float _lifetime;
        private float _riseSpeed;
        private float _elapsed;
        private Color _baseColor;

        public void Initialize(TextMesh textMesh, float lifetime, float riseSpeed)
        {
            _textMesh = textMesh;
            _lifetime = Mathf.Max(0.1f, lifetime);
            _riseSpeed = Mathf.Max(0.1f, riseSpeed);

            if (_textMesh != null)
            {
                _baseColor = _textMesh.color;
            }
        }

        private void Update()
        {
            if (_textMesh == null)
            {
                Destroy(gameObject);
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
                Destroy(gameObject);
            }
        }
    }
}
