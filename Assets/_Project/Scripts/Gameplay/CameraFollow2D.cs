using UnityEngine;

namespace EJR.Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float smoothTime = 0.08f;

        private Transform _target;
        private Vector3 _offset = new Vector3(0f, 0f, -10f);
        private Vector3 _velocity;

        public Transform Target => _target;

        public void Initialize(Transform target, Vector3 offset, float followSmoothTime)
        {
            _target = target;
            _offset = offset;
            smoothTime = Mathf.Max(0f, followSmoothTime);
            SnapToTarget();
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            var desired = new Vector3(
                _target.position.x + _offset.x,
                _target.position.y + _offset.y,
                _offset.z);

            if (smoothTime <= 0f)
            {
                transform.position = desired;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }

        private void SnapToTarget()
        {
            if (_target == null)
            {
                return;
            }

            transform.position = new Vector3(
                _target.position.x + _offset.x,
                _target.position.y + _offset.y,
                _offset.z);
        }
    }
}
