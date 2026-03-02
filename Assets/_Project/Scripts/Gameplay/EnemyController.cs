using System;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class EnemyController : MonoBehaviour
    {
        public event Action<float, float> Changed;

        private EnemyConfig _config;
        private Transform _target;
        private PlayerHealth _playerHealth;
        private EnemyRegistry _registry;
        private ExperienceSystem _experienceSystem;
        private RuntimeSpriteFactory.EnemyVisualKind _visualKind;

        private float _health;
        private float _maxHealth;
        private float _moveSpeed;
        private float _contactDamage;
        private float _contactDamageCooldown;
        private float _contactCooldown;
        private float _playerCollisionRadius;
        private float _collisionRadius = 0.3f;
        private int _experienceOnDeath = 1;
        private EnemySpriteAnimator _spriteAnimator;
        private bool _isDead;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _health;
        public float CollisionRadius => _collisionRadius;
        public RuntimeSpriteFactory.EnemyVisualKind VisualKind => _visualKind;
        public bool IsBoss => _visualKind == RuntimeSpriteFactory.EnemyVisualKind.Boss;

        public void Initialize(
            EnemyConfig config,
            RuntimeSpriteFactory.EnemyVisualKind visualKind,
            EnemyStatProfile statProfile,
            Transform target,
            PlayerHealth playerHealth,
            EnemyRegistry registry,
            ExperienceSystem experienceSystem,
            float playerCollisionRadius,
            float collisionRadius)
        {
            _config = config;
            _visualKind = visualKind;
            _target = target;
            _playerHealth = playerHealth;
            _registry = registry;
            _experienceSystem = experienceSystem;
            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
            _collisionRadius = Mathf.Max(0.05f, collisionRadius);
            _spriteAnimator = GetComponentInChildren<EnemySpriteAnimator>();

            var healthMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.healthMultiplier) : 1f;
            var moveMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.moveSpeedMultiplier) : 1f;
            var contactDamageMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.contactDamageMultiplier) : 1f;
            var experienceMultiplier = statProfile != null ? Mathf.Max(0.1f, statProfile.experienceMultiplier) : 1f;

            _maxHealth = Mathf.Max(1f, config.maxHealth * healthMultiplier);
            _moveSpeed = Mathf.Max(0.1f, config.moveSpeed * moveMultiplier);
            _contactDamage = Mathf.Max(0f, config.contactDamage * contactDamageMultiplier);
            _contactDamageCooldown = Mathf.Max(0.05f, config.contactDamageCooldown);
            _experienceOnDeath = Mathf.Max(1, Mathf.RoundToInt(config.experienceOnDeath * experienceMultiplier));

            _health = _maxHealth;
            _registry.Register(this);
            Changed?.Invoke(_health, _maxHealth);
        }

        private void OnDisable()
        {
            if (_registry != null)
            {
                _registry.Unregister(this);
            }
        }

        private void Update()
        {
            if (_isDead || _target == null || _config == null || _playerHealth == null)
            {
                return;
            }

            var previousPosition = transform.position;
            var toPlayer = _target.position - transform.position;
            var distance = toPlayer.magnitude;
            var direction = distance > 0.001f ? toPlayer / distance : Vector3.zero;
            var minimumSeparation = CollisionRadius + _playerCollisionRadius;
            var separation = ComputeSeparationVector((Vector2)transform.position) * Mathf.Max(0f, _config.separationWeight);

            var desired = separation;
            if (distance > minimumSeparation)
            {
                desired += (Vector2)direction;
            }

            if (desired.sqrMagnitude > 1f)
            {
                desired.Normalize();
            }

            var moveBudget = _moveSpeed * Time.deltaTime;
            var next = transform.position + (Vector3)(desired * moveBudget);
            next = ResolvePlayerOverlap(next, minimumSeparation, (Vector2)direction);
            next = ResolveCrowdOverlaps(next);
            transform.position = next;
            if (_spriteAnimator != null)
            {
                var velocity = ((Vector2)(transform.position - previousPosition)) / Mathf.Max(0.0001f, Time.deltaTime);
                _spriteAnimator.SetMotion(velocity);
            }

            _contactCooldown -= Time.deltaTime;
            var currentDistance = (_target.position - transform.position).magnitude;
            if (currentDistance <= minimumSeparation + 0.02f && _contactCooldown <= 0f)
            {
                _contactCooldown = _contactDamageCooldown;
                _playerHealth.TakeDamage(_contactDamage);
            }
        }

        private Vector3 ResolvePlayerOverlap(Vector3 candidatePosition, float minimumSeparation, Vector2 fallbackDirection)
        {
            var toPlayer = (Vector2)_target.position - (Vector2)candidatePosition;
            var distance = toPlayer.magnitude;
            if (distance >= minimumSeparation)
            {
                return candidatePosition;
            }

            var away = distance > 0.0001f ? -toPlayer / distance : -fallbackDirection;
            if (away.sqrMagnitude <= 0.0001f)
            {
                away = Vector2.right;
            }

            var corrected = (Vector2)_target.position + away * minimumSeparation;
            return new Vector3(corrected.x, corrected.y, 0f);
        }

        private Vector2 ComputeSeparationVector(Vector2 selfPosition)
        {
            if (_registry == null || _config == null)
            {
                return Vector2.zero;
            }

            var separation = Vector2.zero;
            var rangeMultiplier = Mathf.Max(1f, _config.separationRangeMultiplier);
            var overlapPadding = Mathf.Max(0f, _config.overlapResolvePadding);
            var neighbors = _registry.Enemies;

            for (var i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                if (neighbor == null || ReferenceEquals(neighbor, this))
                {
                    continue;
                }

                var toNeighbor = (Vector2)neighbor.transform.position - selfPosition;
                var centerDistance = toNeighbor.magnitude;
                if (centerDistance <= 0.0001f)
                {
                    separation += (Vector2)UnityEngine.Random.insideUnitCircle.normalized;
                    continue;
                }

                var combinedRadius = CollisionRadius + neighbor.CollisionRadius;
                var influenceRadius = combinedRadius * rangeMultiplier;
                if (centerDistance > influenceRadius)
                {
                    continue;
                }

                var away = -toNeighbor / centerDistance;
                var minimumSpacing = combinedRadius + overlapPadding;
                var weight = 0f;

                if (centerDistance < minimumSpacing)
                {
                    var overlap = minimumSpacing - centerDistance;
                    weight += Mathf.Clamp01(overlap / Mathf.Max(0.0001f, combinedRadius)) * 2.5f;
                }
                else if (rangeMultiplier > 1f)
                {
                    var t = Mathf.InverseLerp(influenceRadius, combinedRadius, centerDistance);
                    weight += t * t * 0.25f;
                }

                if (weight > 0f)
                {
                    separation += away * weight;
                }
            }

            return separation;
        }

        private Vector3 ResolveCrowdOverlaps(Vector3 candidatePosition)
        {
            if (_registry == null || _config == null)
            {
                return candidatePosition;
            }

            var resolved = candidatePosition;
            var padding = Mathf.Max(0f, _config.overlapResolvePadding);
            var neighbors = _registry.Enemies;

            for (var pass = 0; pass < 2; pass++)
            {
                var adjusted = false;
                for (var i = 0; i < neighbors.Count; i++)
                {
                    var neighbor = neighbors[i];
                    if (neighbor == null || ReferenceEquals(neighbor, this))
                    {
                        continue;
                    }

                    var toSelf = (Vector2)resolved - (Vector2)neighbor.transform.position;
                    var distance = toSelf.magnitude;
                    var minimum = CollisionRadius + neighbor.CollisionRadius + padding;
                    if (distance >= minimum)
                    {
                        continue;
                    }

                    var away = distance > 0.0001f ? toSelf / distance : (Vector2)(transform.position - _target.position);
                    if (away.sqrMagnitude <= 0.0001f)
                    {
                        away = Vector2.right;
                    }

                    var corrected = (Vector2)neighbor.transform.position + away.normalized * minimum;
                    resolved = new Vector3(corrected.x, corrected.y, 0f);
                    adjusted = true;
                }

                if (!adjusted)
                {
                    break;
                }
            }

            return resolved;
        }

        public void ReceiveDamage(float damage)
        {
            if (_isDead)
            {
                return;
            }

            var appliedDamage = Mathf.Max(0f, damage);
            if (appliedDamage <= 0f)
            {
                return;
            }

            _health = Mathf.Max(0f, _health - appliedDamage);
            if (_health > 0f)
            {
                _spriteAnimator?.PlayHurt();
            }

            CombatTextSpawner.SpawnDamage(transform.position + new Vector3(0f, 0.8f, 0f), appliedDamage, CombatTextSpawner.EnemyDamagedColor);
            Changed?.Invoke(_health, MaxHealth);

            if (_health <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;

            if (_experienceSystem != null)
            {
                _experienceSystem.SpawnOrb(transform.position, _experienceOnDeath);
            }

            if (_registry != null)
            {
                _registry.Unregister(this);
            }

            var destroyDelay = _spriteAnimator != null ? _spriteAnimator.PlayDie() : 0f;
            if (destroyDelay > 0f)
            {
                Destroy(gameObject, destroyDelay);
                return;
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.95f);
            Gizmos.DrawWireSphere(transform.position, CollisionRadius);
        }
    }
}
