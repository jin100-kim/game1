using UnityEngine;

namespace EJR.Game.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ArenaBoundaryVisualizer : MonoBehaviour
    {
        [SerializeField] private Color boundaryColor = new Color(0.75f, 0.9f, 1f, 0.75f);
        [SerializeField, Min(0.01f)] private float thickness = 0.08f;
        [SerializeField] private int sortingOrder = 20;

        private LineRenderer _lineRenderer;
        private Material _material;

        public void Initialize(Rect bounds)
        {
            EnsureRenderer();
            ApplyBounds(bounds);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_material);
                }
                else
                {
                    DestroyImmediate(_material);
                }
            }
        }

        private void EnsureRenderer()
        {
            if (_lineRenderer == null)
            {
                _lineRenderer = GetComponent<LineRenderer>();
            }

            _lineRenderer.useWorldSpace = true;
            _lineRenderer.loop = false;
            _lineRenderer.positionCount = 5;
            _lineRenderer.startWidth = thickness;
            _lineRenderer.endWidth = thickness;
            _lineRenderer.numCornerVertices = 2;
            _lineRenderer.numCapVertices = 2;
            _lineRenderer.sortingOrder = sortingOrder;

            if (_material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _material = new Material(shader);
                    _material.color = boundaryColor;
                    _lineRenderer.material = _material;
                }
            }

            _lineRenderer.startColor = boundaryColor;
            _lineRenderer.endColor = boundaryColor;
        }

        private void ApplyBounds(Rect bounds)
        {
            var z = 0f;
            var p0 = new Vector3(bounds.xMin, bounds.yMin, z);
            var p1 = new Vector3(bounds.xMax, bounds.yMin, z);
            var p2 = new Vector3(bounds.xMax, bounds.yMax, z);
            var p3 = new Vector3(bounds.xMin, bounds.yMax, z);

            _lineRenderer.SetPosition(0, p0);
            _lineRenderer.SetPosition(1, p1);
            _lineRenderer.SetPosition(2, p2);
            _lineRenderer.SetPosition(3, p3);
            _lineRenderer.SetPosition(4, p0);
        }
    }
}
