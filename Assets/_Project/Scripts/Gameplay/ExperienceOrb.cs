using System.Collections.Generic;
using System;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class ExperienceOrb : MonoBehaviour
    {
        private static readonly List<ExperienceOrb> ActiveOrbList = new();

        private Transform _player;
        private ExperienceSystem _system;
        private float _pickupRadius;
        private float _attractRadius;
        private float _attractSpeed;
        private Action<ExperienceOrb> _releaseToPool;
        private bool _isActive;

        public static IReadOnlyList<ExperienceOrb> ActiveOrbs => ActiveOrbList;
        public int Value { get; private set; }

        private void OnEnable()
        {
            if (!ActiveOrbList.Contains(this))
            {
                ActiveOrbList.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveOrbList.Remove(this);
            _isActive = false;
        }

        public void Initialize(
            Transform player,
            ExperienceSystem system,
            int value,
            float pickupRadius,
            float attractRadius,
            float attractSpeed,
            Action<ExperienceOrb> releaseToPool)
        {
            _player = player;
            _system = system;
            Value = value;
            _pickupRadius = pickupRadius;
            _attractRadius = attractRadius;
            _attractSpeed = attractSpeed;
            _releaseToPool = releaseToPool;
            _isActive = true;
        }

        private void Update()
        {
            if (!_isActive || _player == null || _system == null)
            {
                return;
            }

            var toPlayer = _player.position - transform.position;
            var distance = toPlayer.magnitude;

            if (distance <= _pickupRadius)
            {
                _system.Collect(Value);
                Release();
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

        private void Release()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _releaseToPool?.Invoke(this);
        }
    }
}
