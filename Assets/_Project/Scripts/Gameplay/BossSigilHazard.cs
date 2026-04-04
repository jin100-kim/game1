using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class BossSigilHazard : MonoBehaviour
    {
        private const int RingSegments = 40;
        private const float RingWidth = 0.06f;
        private const float ExplosionFxDuration = 0.32f;

        private static readonly Color WarningColor = new(0.95f, 0.72f, 0.24f, 0.95f);
        private static readonly Color DangerColor = new(1f, 0.22f, 0.45f, 0.95f);

        private PlayerHealth _targetPlayer;
        private float _playerCollisionRadius;
        private float _delay;
        private float _radius;
        private float _damage;
        private bool _visualOnly;
        private float _remaining;
        private LineRenderer _ringRenderer;
        private bool _initialized;
        private static readonly System.Collections.Generic.List<BossSigilHazard> s_activeHazards = new();

        public static System.Collections.Generic.IReadOnlyList<BossSigilHazard> ActiveHazards => s_activeHazards;
        public Vector2 WorldPosition => transform.position;
        public float Radius => _radius;
        public float RemainingTime => Mathf.Max(0f, _remaining);

        public void Initialize(
            PlayerHealth targetPlayer,
            float playerCollisionRadius,
            float delay,
            float radius,
            float damage,
            bool visualOnly = false)
        {
            _targetPlayer = targetPlayer;
            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
            _delay = Mathf.Max(0.05f, delay);
            _remaining = _delay;
            _radius = Mathf.Max(0.1f, radius);
            _damage = Mathf.Max(0f, damage);
            _visualOnly = visualOnly;
            EnsureVisual();
            _initialized = true;
        }

        private void OnEnable()
        {
            if (!s_activeHazards.Contains(this))
            {
                s_activeHazards.Add(this);
            }
        }

        private void OnDisable()
        {
            s_activeHazards.Remove(this);
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            _remaining -= Time.deltaTime;
            UpdateVisual();
            if (_remaining > 0f)
            {
                return;
            }

            Explode();
        }

        private void EnsureVisual()
        {
            if (_ringRenderer != null)
            {
                return;
            }

            var ringObject = new GameObject("Ring");
            ringObject.transform.SetParent(transform, false);
            var lineRenderer = ringObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            lineRenderer.positionCount = RingSegments;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.startWidth = RingWidth;
            lineRenderer.endWidth = RingWidth;
            lineRenderer.startColor = WarningColor;
            lineRenderer.endColor = WarningColor;
            lineRenderer.sortingOrder = 520;
            lineRenderer.sharedMaterial = GetOrCreateMaterial();
            WeaponFxRenderer.SetCircleLinePositions(lineRenderer, transform.position, _radius, RingSegments, -0.03f);
            _ringRenderer = lineRenderer;
        }

        private void UpdateVisual()
        {
            if (_ringRenderer == null)
            {
                return;
            }

            WeaponFxRenderer.SetCircleLinePositions(_ringRenderer, transform.position, _radius, RingSegments, -0.03f);
            var progress = 1f - Mathf.Clamp01(_remaining / Mathf.Max(0.05f, _delay));
            var color = Color.Lerp(WarningColor, DangerColor, progress);
            _ringRenderer.startColor = color;
            _ringRenderer.endColor = color;
        }

        private void Explode()
        {
            if (!_visualOnly && _targetPlayer != null)
            {
                var distance = Vector2.Distance(transform.position, _targetPlayer.transform.position);
                if (distance <= _radius + _playerCollisionRadius)
                {
                    _targetPlayer.TakeDamage(_damage);
                }
            }

            WeaponFxRenderer.SpawnRingFx(
                transform.parent,
                transform.position,
                _radius,
                RingSegments,
                DangerColor,
                RingWidth,
                ExplosionFxDuration,
                "BossSigilExplosion",
                530);
            Destroy(gameObject);
        }

        private static Material s_material;

        private static Material GetOrCreateMaterial()
        {
            if (s_material != null)
            {
                return s_material;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            s_material = new Material(shader)
            {
                name = "BossSigilRingMat",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return s_material;
        }
    }
}
