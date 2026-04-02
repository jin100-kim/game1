using Unity.Netcode;
using UnityEngine;

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MultiplayerSharedExperienceOrbActor : NetworkBehaviour
    {
        public enum PickupKind
        {
            Experience = 0,
            WaveRewardChest = 1,
        }

        private static readonly System.Collections.Generic.List<MultiplayerSharedExperienceOrbActor> ActiveActors = new();

        private readonly NetworkVariable<int> _value =
            new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _pickupKind =
            new((int)PickupKind.Experience, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _waveIndex =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _spawnSequence =
            new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private SpriteRenderer _spriteRenderer;
        private float _pickupRadius;
        private float _attractRadius;
        private float _attractSpeed;

        public static System.Collections.Generic.IReadOnlyList<MultiplayerSharedExperienceOrbActor> SpawnedActors => ActiveActors;
        public bool IsWaveRewardChest => (PickupKind)_pickupKind.Value == PickupKind.WaveRewardChest;
        public int WaveIndex => _waveIndex.Value;
        public int SpawnSequence => _spawnSequence.Value;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            ApplyPresentation();
        }

        public override void OnNetworkSpawn()
        {
            _pickupKind.OnValueChanged += HandlePickupKindChanged;
            if (!ActiveActors.Contains(this))
            {
                ActiveActors.Add(this);
            }

            ApplyPresentation();
        }

        public override void OnNetworkDespawn()
        {
            _pickupKind.OnValueChanged -= HandlePickupKindChanged;
            ActiveActors.Remove(this);
        }

        public void InitializeServer(int value, float pickupRadius, float attractRadius, float attractSpeed)
        {
            _pickupKind.Value = (int)PickupKind.Experience;
            _waveIndex.Value = 0;
            _spawnSequence.Value = 0;
            _value.Value = Mathf.Max(1, value);
            _pickupRadius = Mathf.Max(0.1f, pickupRadius);
            _attractRadius = Mathf.Max(_pickupRadius, attractRadius);
            _attractSpeed = Mathf.Max(0.1f, attractSpeed);
            ApplyPresentation();
        }

        public void InitializeWaveRewardChestServer(int waveIndex, int spawnSequence, float pickupRadius)
        {
            _pickupKind.Value = (int)PickupKind.WaveRewardChest;
            _waveIndex.Value = Mathf.Max(1, waveIndex);
            _spawnSequence.Value = Mathf.Max(1, spawnSequence);
            _value.Value = 0;
            _pickupRadius = Mathf.Max(0.1f, pickupRadius);
            _attractRadius = _pickupRadius;
            _attractSpeed = 0f;
            ApplyPresentation();
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

            if (IsWaveRewardChest)
            {
                if (!coop.TryResolveExperienceCollector(transform.position, _pickupRadius, out _, out var chestDistance))
                {
                    return;
                }

                if (chestDistance <= _pickupRadius)
                {
                    coop.CollectWaveRewardChest(_waveIndex.Value);
                    NetworkObject.Despawn(true);
                }

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

        private void HandlePickupKindChanged(int previousValue, int newValue)
        {
            ApplyPresentation();
        }

        private void ApplyPresentation()
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            _spriteRenderer.sprite = EJR.Game.Core.RuntimeSpriteFactory.GetSquareSprite();
            _spriteRenderer.sortingOrder = 20;
            if ((PickupKind)_pickupKind.Value == PickupKind.WaveRewardChest)
            {
                _spriteRenderer.color = new Color(0.20f, 0.88f, 0.30f, 0.96f);
                transform.localScale = new Vector3(0.28f, 0.22f, 1f);
                return;
            }

            _spriteRenderer.color = new Color(0.35f, 1f, 0.4f, 0.95f);
            transform.localScale = Vector3.one * 0.2f;
        }
    }
}
