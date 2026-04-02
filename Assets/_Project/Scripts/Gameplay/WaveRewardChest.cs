using System;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class WaveRewardChest : MonoBehaviour
    {
        private const float DefaultPickupRadius = 0.7f;
        private const float BobAmplitude = 0.08f;
        private const float BobFrequency = 2.6f;

        private Transform _player;
        private float _pickupRadius;
        private Action<WaveRewardChest> _collectedCallback;
        private Action<WaveRewardChest> _releasedCallback;
        private Vector3 _basePosition;
        private bool _isActive;

        public int WaveIndex { get; private set; }
        public int SpawnSequence { get; private set; }

        private void Awake()
        {
            EnsurePresentation();
        }

        private void OnDestroy()
        {
            _releasedCallback?.Invoke(this);
        }

        public void Initialize(
            Transform player,
            int waveIndex,
            int spawnSequence,
            float pickupRadius,
            Action<WaveRewardChest> collectedCallback,
            Action<WaveRewardChest> releasedCallback)
        {
            _player = player;
            WaveIndex = Mathf.Max(1, waveIndex);
            SpawnSequence = Mathf.Max(1, spawnSequence);
            _pickupRadius = Mathf.Max(0.1f, pickupRadius > 0f ? pickupRadius : DefaultPickupRadius);
            _collectedCallback = collectedCallback;
            _releasedCallback = releasedCallback;
            _basePosition = transform.position;
            _isActive = true;
            EnsurePresentation();
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            transform.position = _basePosition + new Vector3(0f, Mathf.Sin(Time.time * BobFrequency) * BobAmplitude, 0f);

            if (_player == null)
            {
                return;
            }

            if ((_player.position - transform.position).sqrMagnitude > _pickupRadius * _pickupRadius)
            {
                return;
            }

            _isActive = false;
            _collectedCallback?.Invoke(this);
            Destroy(gameObject);
        }

        private void EnsurePresentation()
        {
            if (transform.Find("Base") == null)
            {
                var baseObject = new GameObject("Base");
                baseObject.transform.SetParent(transform, false);
                var baseRenderer = baseObject.AddComponent<SpriteRenderer>();
                baseRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
                baseRenderer.color = new Color(0.16f, 0.64f, 0.24f, 0.96f);
                baseRenderer.sortingOrder = 24;
                baseObject.transform.localScale = new Vector3(0.34f, 0.26f, 1f);
                baseObject.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            }

            if (transform.Find("Trim") == null)
            {
                var trimObject = new GameObject("Trim");
                trimObject.transform.SetParent(transform, false);
                var trimRenderer = trimObject.AddComponent<SpriteRenderer>();
                trimRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
                trimRenderer.color = new Color(0.62f, 1f, 0.68f, 0.96f);
                trimRenderer.sortingOrder = 25;
                trimObject.transform.localScale = new Vector3(0.18f, 0.08f, 1f);
                trimObject.transform.localPosition = new Vector3(0f, 0.08f, -0.01f);
            }

            if (transform.Find("Glow") == null)
            {
                var glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(transform, false);
                var glowRenderer = glowObject.AddComponent<SpriteRenderer>();
                glowRenderer.sprite = RuntimeSpriteFactory.GetSquareSprite();
                glowRenderer.color = new Color(0.30f, 1f, 0.38f, 0.22f);
                glowRenderer.sortingOrder = 23;
                glowObject.transform.localScale = new Vector3(0.58f, 0.48f, 1f);
            }
        }
    }
}
