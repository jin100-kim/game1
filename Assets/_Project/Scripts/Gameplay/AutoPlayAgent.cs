using System;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class AutoPlayAgent
    {
        private readonly List<EnemyController> _nearbyEnemies = new(24);

        private Vector2 _cachedMove = Vector2.zero;
        private Vector2 _wanderDirection = Vector2.right;
        private float _nextDecisionAt;
        private float _nextWanderAt;

        public Vector2 EvaluateMove(
            Vector3 playerPosition,
            Rect movementBounds,
            float healthRatio,
            EnemyRegistry registry,
            Func<Vector3, Vector3?> nearestOrbResolver = null)
        {
            if (Time.unscaledTime < _nextDecisionAt)
            {
                return _cachedMove;
            }

            _nextDecisionAt = Time.unscaledTime + 0.06f;
            healthRatio = Mathf.Clamp01(healthRatio);

            var playerPosition2D = (Vector2)playerPosition;
            var preferredDistance = Mathf.Lerp(3.4f, 2.2f, healthRatio);
            var dangerDistance = Mathf.Lerp(4.4f, 2.6f, healthRatio);
            var searchDistance = Mathf.Max(preferredDistance + 4f, dangerDistance + 2f);

            var evade = Vector2.zero;
            var approach = Vector2.zero;
            var nearestEnemyDistance = float.MaxValue;
            Vector2 nearestEnemyDirection = Vector2.zero;

            if (registry != null)
            {
                registry.GetNearby(playerPosition2D, searchDistance + registry.GetMaxCollisionRadius(), _nearbyEnemies);
                for (var i = 0; i < _nearbyEnemies.Count; i++)
                {
                    var enemy = _nearbyEnemies[i];
                    if (enemy == null || enemy.IsDead)
                    {
                        continue;
                    }

                    var toEnemy = (Vector2)enemy.transform.position - playerPosition2D;
                    var centerDistance = toEnemy.magnitude;
                    if (centerDistance <= 0.0001f)
                    {
                        evade += UnityEngine.Random.insideUnitCircle.normalized;
                        continue;
                    }

                    var surfaceDistance = Mathf.Max(0.01f, centerDistance - enemy.CollisionRadius);
                    if (surfaceDistance < nearestEnemyDistance)
                    {
                        nearestEnemyDistance = surfaceDistance;
                        nearestEnemyDirection = toEnemy / centerDistance;
                    }

                    if (surfaceDistance <= dangerDistance)
                    {
                        var weight = 1f - (surfaceDistance / Mathf.Max(0.1f, dangerDistance));
                        evade -= nearestEnemyDirection * (weight * weight);
                        continue;
                    }

                    if (surfaceDistance > preferredDistance * 1.15f && surfaceDistance <= searchDistance)
                    {
                        var weight = Mathf.Clamp01((surfaceDistance - preferredDistance) / Mathf.Max(0.5f, searchDistance - preferredDistance));
                        approach += nearestEnemyDirection * (weight * 0.75f);
                    }
                }
            }

            var collect = Vector2.zero;
            if (nearestOrbResolver != null && evade.sqrMagnitude < 0.45f)
            {
                var nearestOrbPosition = nearestOrbResolver(playerPosition);
                if (nearestOrbPosition.HasValue)
                {
                    var toOrb = (Vector2)(nearestOrbPosition.Value - playerPosition);
                    var orbDistance = toOrb.magnitude;
                    if (orbDistance > 0.05f)
                    {
                        var orbWeight = Mathf.Clamp01(1f - (orbDistance / 9f));
                        collect = (toOrb / orbDistance) * Mathf.Lerp(0.4f, 1.1f, orbWeight);
                    }
                }
            }

            var inward = ComputeBoundsInwardBias(playerPosition2D, movementBounds);
            var centerBias = (-playerPosition2D) * 0.08f;
            centerBias = centerBias.sqrMagnitude > 1f ? centerBias.normalized : centerBias;

            RefreshWanderDirection();
            var wander = _wanderDirection * 0.25f;

            var move = (evade * 1.8f) + (collect * 1f) + (approach * 0.55f) + (inward * 1.25f) + (centerBias * 0.25f) + wander;
            if (move.sqrMagnitude <= 0.0001f && nearestEnemyDistance < float.MaxValue)
            {
                move = -nearestEnemyDirection;
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            _cachedMove = move;
            return _cachedMove;
        }

        private void RefreshWanderDirection()
        {
            if (Time.unscaledTime < _nextWanderAt && _wanderDirection.sqrMagnitude > 0.0001f)
            {
                return;
            }

            _nextWanderAt = Time.unscaledTime + UnityEngine.Random.Range(0.8f, 1.6f);
            _wanderDirection = UnityEngine.Random.insideUnitCircle;
            if (_wanderDirection.sqrMagnitude <= 0.0001f)
            {
                _wanderDirection = Vector2.right;
            }
            else
            {
                _wanderDirection.Normalize();
            }
        }

        private static Vector2 ComputeBoundsInwardBias(Vector2 position, Rect movementBounds)
        {
            const float margin = 1.35f;
            var inward = Vector2.zero;

            if (position.x < movementBounds.xMin + margin)
            {
                inward.x += Mathf.InverseLerp(movementBounds.xMin, movementBounds.xMin + margin, position.x);
            }
            else if (position.x > movementBounds.xMax - margin)
            {
                inward.x -= Mathf.InverseLerp(movementBounds.xMax, movementBounds.xMax - margin, position.x);
            }

            if (position.y < movementBounds.yMin + margin)
            {
                inward.y += Mathf.InverseLerp(movementBounds.yMin, movementBounds.yMin + margin, position.y);
            }
            else if (position.y > movementBounds.yMax - margin)
            {
                inward.y -= Mathf.InverseLerp(movementBounds.yMax, movementBounds.yMax - margin, position.y);
            }

            return inward;
        }
    }
}
