using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public sealed class AutoPlayAgent
    {
        private static readonly Vector2[] EscapeSampleDirections =
        {
            Vector2.right,
            new Vector2(0.866f, 0.5f).normalized,
            new Vector2(0.5f, 0.866f).normalized,
            Vector2.up,
            new Vector2(-0.5f, 0.866f).normalized,
            new Vector2(-0.866f, 0.5f).normalized,
            Vector2.left,
            new Vector2(-0.866f, -0.5f).normalized,
            new Vector2(-0.5f, -0.866f).normalized,
            Vector2.down,
            new Vector2(0.5f, -0.866f).normalized,
            new Vector2(0.866f, -0.5f).normalized,
        };

        private readonly List<EnemyController> _nearbyEnemies = new(24);
        private readonly List<BossSigilHazard> _nearbySigils = new(12);
        private readonly List<EnemyVariantProjectile> _nearbyVariantProjectiles = new(20);
        private readonly List<BossProjectile> _nearbyBossProjectiles = new(24);

        private Vector2 _cachedMove = Vector2.zero;
        private Vector2 _wanderDirection = Vector2.right;
        private float _nextDecisionAt;
        private float _nextWanderAt;

        public Vector2 EvaluateMove(
            Vector3 playerPosition,
            Rect movementBounds,
            float healthRatio,
            EnemyRegistry registry,
            Vector3? nearestOrbPosition = null,
            Vector3? nearestRewardChestPosition = null,
            Vector3? waveTargetPosition = null,
            Vector3? bossPosition = null,
            bool bossPullActive = false,
            Vector2 bossPullCenter = default,
            float bossPullRadius = 0f)
        {
            if (Time.unscaledTime < _nextDecisionAt)
            {
                return _cachedMove;
            }

            _nextDecisionAt = Time.unscaledTime + 0.04f;
            healthRatio = Mathf.Clamp01(healthRatio);

            var playerPosition2D = (Vector2)playerPosition;
            var preferredDistance = Mathf.Lerp(3.4f, 2.2f, healthRatio);
            var dangerDistance = Mathf.Lerp(4.4f, 2.6f, healthRatio);
            var searchDistance = Mathf.Max(preferredDistance + 4f, dangerDistance + 2f);

            var evade = Vector2.zero;
            var approach = Vector2.zero;
            var nearestEnemyDistance = float.MaxValue;
            var nearestEnemyDirection = Vector2.zero;

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
                        evade += Random.insideUnitCircle.normalized;
                        continue;
                    }

                    var surfaceDistance = Mathf.Max(0.01f, centerDistance - enemy.CollisionRadius);
                    var enemyDirection = toEnemy / centerDistance;
                    if (surfaceDistance < nearestEnemyDistance)
                    {
                        nearestEnemyDistance = surfaceDistance;
                        nearestEnemyDirection = enemyDirection;
                    }

                    if (surfaceDistance <= dangerDistance)
                    {
                        var weight = 1f - (surfaceDistance / Mathf.Max(0.1f, dangerDistance));
                        evade -= enemyDirection * (weight * weight);
                        continue;
                    }

                    if (surfaceDistance > preferredDistance * 1.15f && surfaceDistance <= searchDistance)
                    {
                        var weight = Mathf.Clamp01((surfaceDistance - preferredDistance) / Mathf.Max(0.5f, searchDistance - preferredDistance));
                        approach += enemyDirection * (weight * 0.75f);
                    }
                }
            }
            else
            {
                _nearbyEnemies.Clear();
            }

            CollectNearbyHazards(playerPosition2D, Mathf.Max(searchDistance, 8.5f));
            var hazardAvoid = ComputeHazardAvoidance(playerPosition2D, healthRatio);
            var cornerPressure = ComputeCornerPressure(playerPosition2D, movementBounds);
            var dangerPressure = Mathf.Clamp01((evade.magnitude * 0.45f) + (hazardAvoid.magnitude * 0.55f) + (cornerPressure * 0.6f));
            var escape = ComputeEscapeObjective(
                playerPosition2D,
                movementBounds,
                Mathf.Lerp(2.6f, 3.6f, 1f - healthRatio),
                dangerPressure);

            var objective = Vector2.zero;
            var hasPriorityObjective = false;
            if (nearestRewardChestPosition.HasValue)
            {
                objective += ComputeChestObjective(
                    playerPosition2D,
                    nearestRewardChestPosition.Value,
                    evade.sqrMagnitude,
                    dangerPressure);
                hasPriorityObjective = true;
            }

            if (waveTargetPosition.HasValue)
            {
                objective += ComputeCombatObjective(
                    playerPosition2D,
                    waveTargetPosition.Value,
                    Mathf.Lerp(2.8f, 2.1f, healthRatio),
                    Mathf.Lerp(1.35f, 1.05f, healthRatio),
                    1.1f);
                hasPriorityObjective = true;
            }

            if (bossPosition.HasValue)
            {
                objective += ComputeBossObjective(playerPosition2D, bossPosition.Value, healthRatio);
                hasPriorityObjective = true;
            }

            if (bossPullActive && bossPullRadius > 0.01f)
            {
                var awayFromPull = playerPosition2D - bossPullCenter;
                var pullDistance = awayFromPull.magnitude;
                if (pullDistance < bossPullRadius && pullDistance > 0.001f)
                {
                    var pullWeight = 1f - (pullDistance / Mathf.Max(0.1f, bossPullRadius));
                    objective += (awayFromPull / pullDistance) * Mathf.Lerp(0.7f, 1.7f, pullWeight);
                    hasPriorityObjective = true;
                }
            }

            objective *= Mathf.Lerp(1f, 0.52f, dangerPressure);

            var collect = Vector2.zero;
            if (!hasPriorityObjective && nearestOrbPosition.HasValue && dangerPressure < 0.25f && cornerPressure < 0.45f)
            {
                var toOrb = (Vector2)(nearestOrbPosition.Value - playerPosition);
                var orbDistance = toOrb.magnitude;
                if (orbDistance > 0.05f)
                {
                    var orbWeight = Mathf.Clamp01(1f - (orbDistance / 9f));
                    collect = (toOrb / orbDistance) * Mathf.Lerp(0.4f, 1.1f, orbWeight);
                }
            }

            var inward = ComputeBoundsInwardBias(playerPosition2D, movementBounds);
            var centerBias = (-playerPosition2D) * 0.08f;
            centerBias = centerBias.sqrMagnitude > 1f ? centerBias.normalized : centerBias;

            RefreshWanderDirection();
            var wander = _wanderDirection * (hasPriorityObjective ? 0.08f : Mathf.Lerp(0.08f, 0.24f, 1f - dangerPressure));

            var move = (evade * 2.05f)
                + (hazardAvoid * 2.35f)
                + (escape * Mathf.Lerp(0.65f, 2.35f, Mathf.Clamp01(dangerPressure + (cornerPressure * 0.5f))))
                + (objective * 1.35f)
                + (collect * 1f)
                + (approach * (hasPriorityObjective ? 0.12f : Mathf.Lerp(0.18f, 0.55f, 1f - dangerPressure)))
                + (inward * Mathf.Lerp(1.25f, 3f, cornerPressure))
                + (centerBias * 0.25f)
                + wander;
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

        private void CollectNearbyHazards(Vector2 playerPosition, float searchRadius)
        {
            _nearbySigils.Clear();
            _nearbyVariantProjectiles.Clear();
            _nearbyBossProjectiles.Clear();

            var searchRadiusSq = searchRadius * searchRadius;
            var sigils = BossSigilHazard.ActiveHazards;
            for (var i = 0; i < sigils.Count; i++)
            {
                var sigil = sigils[i];
                if (sigil == null)
                {
                    continue;
                }

                var delta = sigil.WorldPosition - playerPosition;
                var paddedRadius = searchRadius + sigil.Radius;
                if (delta.sqrMagnitude <= paddedRadius * paddedRadius)
                {
                    _nearbySigils.Add(sigil);
                }
            }

            var variantProjectiles = EnemyVariantProjectile.ActiveProjectiles;
            for (var i = 0; i < variantProjectiles.Count; i++)
            {
                var projectile = variantProjectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                if ((projectile.WorldPosition - playerPosition).sqrMagnitude <= searchRadiusSq)
                {
                    _nearbyVariantProjectiles.Add(projectile);
                }
            }

            var bossProjectiles = BossProjectile.ActiveProjectiles;
            for (var i = 0; i < bossProjectiles.Count; i++)
            {
                var projectile = bossProjectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                if ((projectile.WorldPosition - playerPosition).sqrMagnitude <= searchRadiusSq)
                {
                    _nearbyBossProjectiles.Add(projectile);
                }
            }
        }

        private Vector2 ComputeChestObjective(Vector2 playerPosition, Vector3 chestPosition, float evadeMagnitudeSq, float dangerPressure)
        {
            var toChest = (Vector2)chestPosition - playerPosition;
            var chestDistance = toChest.magnitude;
            if (chestDistance <= 0.05f)
            {
                return Vector2.zero;
            }

            var safePriority = evadeMagnitudeSq < 0.35f ? 1.85f : 0.85f;
            var urgency = chestDistance > 2.6f ? 1f : Mathf.Lerp(1.3f, 0.85f, chestDistance / 2.6f);
            var commitment = Mathf.Lerp(1f, 0.15f, dangerPressure);
            return (toChest / chestDistance) * (safePriority * urgency * commitment);
        }

        private Vector2 ComputeCombatObjective(
            Vector2 playerPosition,
            Vector3 targetPosition,
            float preferredDistance,
            float dangerDistance,
            float orbitWeight)
        {
            var toTarget = (Vector2)targetPosition - playerPosition;
            var distance = toTarget.magnitude;
            if (distance <= 0.001f)
            {
                return Random.insideUnitCircle.normalized;
            }

            var direction = toTarget / distance;
            var objective = Vector2.zero;
            if (distance < dangerDistance)
            {
                objective -= direction * 1.25f;
            }
            else if (distance > preferredDistance + 0.45f)
            {
                objective += direction * 1.15f;
            }
            else if (distance < preferredDistance - 0.3f)
            {
                objective -= direction * 0.75f;
            }

            var orbitSign = _wanderDirection.x >= 0f ? 1f : -1f;
            var orbitDirection = new Vector2(-direction.y, direction.x) * orbitSign;
            objective += orbitDirection * orbitWeight;
            return objective;
        }

        private Vector2 ComputeBossObjective(Vector2 playerPosition, Vector3 bossPosition, float healthRatio)
        {
            var preferredDistance = Mathf.Lerp(4.4f, 3.4f, healthRatio);
            var dangerDistance = Mathf.Lerp(2.6f, 1.9f, healthRatio);
            var objective = ComputeCombatObjective(
                playerPosition,
                bossPosition,
                preferredDistance,
                dangerDistance,
                1.35f);

            var toBoss = (Vector2)bossPosition - playerPosition;
            var bossDistance = toBoss.magnitude;
            if (bossDistance > preferredDistance + 1.2f && bossDistance <= 8.5f)
            {
                objective += (toBoss / Mathf.Max(0.001f, bossDistance)) * 0.45f;
            }

            return objective;
        }

        private Vector2 ComputeHazardAvoidance(Vector2 playerPosition, float healthRatio)
        {
            var avoid = Vector2.zero;
            var clearancePadding = Mathf.Lerp(0.95f, 0.55f, healthRatio);

            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null || enemy.IsDead || !enemy.TryGetVariantExplosionHazard(out var center, out var radius, out var remainingTime))
                {
                    continue;
                }

                var toPlayer = playerPosition - center;
                var distance = toPlayer.magnitude;
                var safeRadius = radius + clearancePadding + (Mathf.Clamp01(remainingTime / 1f) * 0.35f);
                if (distance >= safeRadius)
                {
                    continue;
                }

                var direction = distance > 0.0001f ? toPlayer / distance : Random.insideUnitCircle.normalized;
                var weight = 1f - (distance / Mathf.Max(0.1f, safeRadius));
                avoid += direction * Mathf.Lerp(1.1f, 2.2f, weight);
            }

            for (var i = 0; i < _nearbySigils.Count; i++)
            {
                var sigil = _nearbySigils[i];
                if (sigil == null)
                {
                    continue;
                }

                var toPlayer = playerPosition - sigil.WorldPosition;
                var distance = toPlayer.magnitude;
                var safeRadius = sigil.Radius + clearancePadding + 0.55f;
                if (distance >= safeRadius)
                {
                    continue;
                }

                var direction = distance > 0.0001f ? toPlayer / distance : Random.insideUnitCircle.normalized;
                var weight = 1f - (distance / Mathf.Max(0.1f, safeRadius));
                avoid += direction * Mathf.Lerp(0.95f, 1.8f, weight);
            }

            for (var i = 0; i < _nearbyVariantProjectiles.Count; i++)
            {
                var projectile = _nearbyVariantProjectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                AddProjectileAvoidance(
                    playerPosition,
                    projectile.WorldPosition,
                    projectile.Direction,
                    projectile.Speed,
                    projectile.RemainingLifetime,
                    projectile.HitRadius,
                    0.65f,
                    ref avoid);
            }

            for (var i = 0; i < _nearbyBossProjectiles.Count; i++)
            {
                var projectile = _nearbyBossProjectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                AddProjectileAvoidance(
                    playerPosition,
                    projectile.WorldPosition,
                    projectile.Direction,
                    projectile.Speed,
                    projectile.RemainingLifetime,
                    projectile.HitRadius,
                    0.9f,
                    ref avoid);
            }

            return avoid;
        }

        private Vector2 ComputeEscapeObjective(Vector2 playerPosition, Rect movementBounds, float sampleDistance, float dangerPressure)
        {
            var bestScore = float.NegativeInfinity;
            var bestDirection = Vector2.zero;

            for (var i = 0; i < EscapeSampleDirections.Length; i++)
            {
                var direction = EscapeSampleDirections[i];
                var sample = playerPosition + (direction * sampleDistance);
                sample.x = Mathf.Clamp(sample.x, movementBounds.xMin, movementBounds.xMax);
                sample.y = Mathf.Clamp(sample.y, movementBounds.yMin, movementBounds.yMax);

                var score = EvaluateSampleSafety(sample, movementBounds);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestDirection = (sample - playerPosition).normalized;
            }

            if (bestDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            return bestDirection * Mathf.Lerp(0.55f, 1.35f, dangerPressure);
        }

        private float EvaluateSampleSafety(Vector2 samplePosition, Rect movementBounds)
        {
            var edgeClearance = GetBoundsClearance(samplePosition, movementBounds);
            var score = edgeClearance * 2.4f;

            if (edgeClearance < 0.9f)
            {
                score -= Mathf.Lerp(5.5f, 1f, edgeClearance / 0.9f);
            }

            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                var surfaceDistance = Vector2.Distance(samplePosition, enemy.transform.position) - enemy.CollisionRadius;
                if (surfaceDistance <= 0.2f)
                {
                    score -= 8f;
                    continue;
                }

                if (surfaceDistance <= 3.2f)
                {
                    score -= Mathf.Lerp(4.8f, 0f, surfaceDistance / 3.2f);
                }

                if (enemy.TryGetVariantExplosionHazard(out var center, out var radius, out _))
                {
                    var blastDistance = Vector2.Distance(samplePosition, center) - radius;
                    if (blastDistance <= 0.15f)
                    {
                        score -= 10f;
                    }
                    else if (blastDistance <= 1.2f)
                    {
                        score -= Mathf.Lerp(5.5f, 0f, blastDistance / 1.2f);
                    }
                }
            }

            for (var i = 0; i < _nearbySigils.Count; i++)
            {
                var sigil = _nearbySigils[i];
                if (sigil == null)
                {
                    continue;
                }

                var sigilDistance = Vector2.Distance(samplePosition, sigil.WorldPosition) - sigil.Radius;
                if (sigilDistance <= 0.15f)
                {
                    score -= 10f;
                }
                else if (sigilDistance <= 1.3f)
                {
                    score -= Mathf.Lerp(5.2f, 0f, sigilDistance / 1.3f);
                }
            }

            for (var i = 0; i < _nearbyVariantProjectiles.Count; i++)
            {
                var projectile = _nearbyVariantProjectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                score -= EvaluateProjectileThreat(
                    samplePosition,
                    projectile.WorldPosition,
                    projectile.Direction,
                    projectile.Speed,
                    projectile.RemainingLifetime,
                    projectile.HitRadius,
                    0.65f);
            }

            for (var i = 0; i < _nearbyBossProjectiles.Count; i++)
            {
                var projectile = _nearbyBossProjectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                score -= EvaluateProjectileThreat(
                    samplePosition,
                    projectile.WorldPosition,
                    projectile.Direction,
                    projectile.Speed,
                    projectile.RemainingLifetime,
                    projectile.HitRadius,
                    0.9f);
            }

            return score;
        }

        private static void AddProjectileAvoidance(
            Vector2 playerPosition,
            Vector2 projectilePosition,
            Vector2 direction,
            float speed,
            float remainingLifetime,
            float hitRadius,
            float avoidancePadding,
            ref Vector2 avoid)
        {
            var toPlayer = playerPosition - projectilePosition;
            if (Vector2.Dot(direction, toPlayer) < -0.15f && toPlayer.sqrMagnitude > 0.5f * 0.5f)
            {
                return;
            }

            var horizon = Mathf.Min(remainingLifetime, 0.65f);
            var projectedEnd = projectilePosition + (direction * speed * horizon);
            var closestPoint = ClosestPointOnSegment(playerPosition, projectilePosition, projectedEnd);
            var offset = playerPosition - closestPoint;
            var distance = offset.magnitude;
            var safeRadius = hitRadius + avoidancePadding;
            if (distance >= safeRadius)
            {
                return;
            }

            var awayDirection = distance > 0.0001f
                ? offset / distance
                : new Vector2(-direction.y, direction.x).normalized;
            var weight = 1f - (distance / Mathf.Max(0.05f, safeRadius));
            avoid += awayDirection * Mathf.Lerp(0.8f, 1.85f, weight);
        }

        private static float EvaluateProjectileThreat(
            Vector2 samplePosition,
            Vector2 projectilePosition,
            Vector2 direction,
            float speed,
            float remainingLifetime,
            float hitRadius,
            float avoidancePadding)
        {
            var toSample = samplePosition - projectilePosition;
            if (Vector2.Dot(direction, toSample) < -0.15f && toSample.sqrMagnitude > 0.5f * 0.5f)
            {
                return 0f;
            }

            var horizon = Mathf.Min(remainingLifetime, 0.65f);
            var projectedEnd = projectilePosition + (direction * speed * horizon);
            var closestPoint = ClosestPointOnSegment(samplePosition, projectilePosition, projectedEnd);
            var distance = Vector2.Distance(samplePosition, closestPoint);
            var safeRadius = hitRadius + avoidancePadding;
            if (distance >= safeRadius)
            {
                return 0f;
            }

            var weight = 1f - (distance / Mathf.Max(0.05f, safeRadius));
            return Mathf.Lerp(0.6f, 3.2f, weight);
        }

        private void RefreshWanderDirection()
        {
            if (Time.unscaledTime < _nextWanderAt && _wanderDirection.sqrMagnitude > 0.0001f)
            {
                return;
            }

            _nextWanderAt = Time.unscaledTime + Random.Range(0.8f, 1.6f);
            _wanderDirection = Random.insideUnitCircle;
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
            const float margin = 2.1f;
            var inward = Vector2.zero;

            if (position.x < movementBounds.xMin + margin)
            {
                var t = Mathf.InverseLerp(movementBounds.xMin + margin, movementBounds.xMin, position.x);
                inward.x += Mathf.Clamp01(t);
            }
            else if (position.x > movementBounds.xMax - margin)
            {
                var t = Mathf.InverseLerp(movementBounds.xMax - margin, movementBounds.xMax, position.x);
                inward.x -= Mathf.Clamp01(t);
            }

            if (position.y < movementBounds.yMin + margin)
            {
                var t = Mathf.InverseLerp(movementBounds.yMin + margin, movementBounds.yMin, position.y);
                inward.y += Mathf.Clamp01(t);
            }
            else if (position.y > movementBounds.yMax - margin)
            {
                var t = Mathf.InverseLerp(movementBounds.yMax - margin, movementBounds.yMax, position.y);
                inward.y -= Mathf.Clamp01(t);
            }

            return inward;
        }

        private static float ComputeCornerPressure(Vector2 position, Rect movementBounds)
        {
            const float margin = 2.2f;
            var xPressure = 0f;
            var yPressure = 0f;

            if (position.x < movementBounds.xMin + margin)
            {
                xPressure = Mathf.InverseLerp(movementBounds.xMin + margin, movementBounds.xMin, position.x);
            }
            else if (position.x > movementBounds.xMax - margin)
            {
                xPressure = Mathf.InverseLerp(movementBounds.xMax - margin, movementBounds.xMax, position.x);
            }

            if (position.y < movementBounds.yMin + margin)
            {
                yPressure = Mathf.InverseLerp(movementBounds.yMin + margin, movementBounds.yMin, position.y);
            }
            else if (position.y > movementBounds.yMax - margin)
            {
                yPressure = Mathf.InverseLerp(movementBounds.yMax - margin, movementBounds.yMax, position.y);
            }

            return Mathf.Clamp01((xPressure * 0.55f) + (yPressure * 0.55f));
        }

        private static float GetBoundsClearance(Vector2 position, Rect movementBounds)
        {
            var left = position.x - movementBounds.xMin;
            var right = movementBounds.xMax - position.x;
            var bottom = position.y - movementBounds.yMin;
            var top = movementBounds.yMax - position.y;
            return Mathf.Min(left, right, bottom, top);
        }

        private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            var segment = segmentEnd - segmentStart;
            var lengthSq = segment.sqrMagnitude;
            if (lengthSq <= 0.000001f)
            {
                return segmentStart;
            }

            var t = Vector2.Dot(point - segmentStart, segment) / lengthSq;
            t = Mathf.Clamp01(t);
            return segmentStart + (segment * t);
        }
    }
}
