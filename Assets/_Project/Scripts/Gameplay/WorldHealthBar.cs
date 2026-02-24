using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private const string BackgroundObjectName = "HPBar_Background";
        private const string FillObjectName = "HPBar_Fill";

        private SpriteRenderer _backgroundRenderer;
        private SpriteRenderer _fillRenderer;

        private Vector3 _localOffset = new Vector3(0f, 0.7f, 0f);
        private float _width = 1f;
        private float _height = 0.1f;
        private int _sortingOrder = 20;

        public void Initialize(
            Vector3 localOffset,
            float width,
            float height,
            Color fillColor,
            Color backgroundColor,
            int sortingOrder)
        {
            _localOffset = localOffset;
            _width = Mathf.Max(0.1f, width);
            _height = Mathf.Max(0.02f, height);
            _sortingOrder = sortingOrder;

            EnsureRenderers();

            _backgroundRenderer.color = backgroundColor;
            _fillRenderer.color = fillColor;

            _backgroundRenderer.sortingOrder = _sortingOrder;
            _fillRenderer.sortingOrder = _sortingOrder + 1;
            _backgroundRenderer.transform.localScale = new Vector3(_width, _height, 1f);
        }

        public void SetHealth(float currentHealth, float maxHealth)
        {
            EnsureRenderers();

            var ratio = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
            var fillWidth = Mathf.Max(0.0001f, _width * ratio);

            _fillRenderer.transform.localScale = new Vector3(fillWidth, _height * 0.72f, 1f);
            _fillRenderer.transform.localPosition = _localOffset + new Vector3(-_width * (1f - ratio) * 0.5f, 0f, -0.01f);
            _fillRenderer.enabled = ratio > 0f;
            _backgroundRenderer.transform.localPosition = _localOffset;
        }

        private void EnsureRenderers()
        {
            if (_backgroundRenderer == null)
            {
                _backgroundRenderer = GetOrCreateRenderer(BackgroundObjectName);
            }

            if (_fillRenderer == null)
            {
                _fillRenderer = GetOrCreateRenderer(FillObjectName);
            }

            var sprite = RuntimeSpriteFactory.GetSquareSprite();
            _backgroundRenderer.sprite = sprite;
            _fillRenderer.sprite = sprite;
        }

        private SpriteRenderer GetOrCreateRenderer(string objectName)
        {
            var child = transform.Find(objectName);
            if (child == null)
            {
                child = new GameObject(objectName).transform;
                child.SetParent(transform, false);
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            return renderer;
        }
    }
}
