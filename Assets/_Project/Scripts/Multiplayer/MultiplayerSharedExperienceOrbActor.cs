using Unity.Netcode;
using UnityEngine;

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MultiplayerSharedExperienceOrbActor : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _value =
            new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private SpriteRenderer _spriteRenderer;
        private float _pickupRadius;
        private float _attractRadius;
        private float _attractSpeed;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _spriteRenderer.sprite = EJR.Game.Core.RuntimeSpriteFactory.GetSquareSprite();
            _spriteRenderer.color = new Color(0.35f, 1f, 0.4f, 0.95f);
            _spriteRenderer.sortingOrder = 20;
            transform.localScale = Vector3.one * 0.2f;
        }

        public void InitializeServer(int value, float pickupRadius, float attractRadius, float attractSpeed)
        {
            _value.Value = Mathf.Max(1, value);
            _pickupRadius = Mathf.Max(0.1f, pickupRadius);
            _attractRadius = Mathf.Max(_pickupRadius, attractRadius);
            _attractSpeed = Mathf.Max(0.1f, attractSpeed);
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            var coop = MultiplayerCoopController.Instance;
            if (coop == null || coop.Phase != MultiplayerRunPhase.Running)
            {
                return;
            }

            if (!coop.TryResolveExperienceCollector(transform.position, _attractRadius, out var collector, out var distance))
            {
                return;
            }

            if (distance <= _pickupRadius)
            {
                coop.CollectSharedExperience(_value.Value);
                NetworkObject.Despawn(true);
                return;
            }

            var toCollector = collector.transform.position - transform.position;
            if (toCollector.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var direction = toCollector.normalized;
            transform.position += direction * (_attractSpeed * Time.deltaTime);
        }
    }
}
