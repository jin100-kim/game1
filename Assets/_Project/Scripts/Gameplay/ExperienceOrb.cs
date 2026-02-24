using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class ExperienceOrb : MonoBehaviour
    {
        private Transform _player;
        private ExperienceSystem _system;
        private float _pickupRadius;
        private float _attractRadius;
        private float _attractSpeed;

        public int Value { get; private set; }

        public void Initialize(Transform player, ExperienceSystem system, int value, float pickupRadius, float attractRadius, float attractSpeed)
        {
            _player = player;
            _system = system;
            Value = value;
            _pickupRadius = pickupRadius;
            _attractRadius = attractRadius;
            _attractSpeed = attractSpeed;
        }

        private void Update()
        {
            if (_player == null || _system == null)
            {
                return;
            }

            var toPlayer = _player.position - transform.position;
            var distance = toPlayer.magnitude;

            if (distance <= _pickupRadius)
            {
                _system.Collect(Value);
                Destroy(gameObject);
                return;
            }

            if (distance <= _attractRadius)
            {
                var move = toPlayer.normalized * _attractSpeed * Time.deltaTime;
                if (move.sqrMagnitude >= toPlayer.sqrMagnitude)
                {
                    transform.position = _player.position;
                }
                else
                {
                    transform.position += move;
                }
            }
        }
    }
}
