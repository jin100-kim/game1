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
            float bossPullRadius = 0f,
            System.Func<Vector2, bool> isWalkable = null)
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
            var projectilePressure = ComputeProjectilePressure(playerPosition2D);
            var bossProjectilePressure = ComputeBossProjectilePressure(playerPosition2D);
            var combinedProjectilePressure = Mathf.Clamp01(Mathf.Max(projectilePressure, bossProjectilePressure));
            var dangerPressure = Mathf.Clamp01((evade.magnitude * 0.38f) + (hazardAvoid.magnitude * 0.6f) + (cornerPressure * 0.65f) + (combinedProjectilePressure * 0.95f));
            var escape = ComputeEscapeObjective(
                playerPosition2D,
                movementBounds,
                Mathf.Lerp(2.6f, 3.6f, 1f - healthRatio),
                dangerPressure);

            var objective = Vector2.zero;
            var hasPriorityObjective = false;
            if (nearestRewardChestPosition.HasValue && combinedProjectilePressure < 0.35f)
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
                objective += ComputeBossObjective(playerPosition2D, bossPosition.Value, healthRatio, Mathf.Max(bossProjectilePressure, combinedProjectilePressure * 0.65f));
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

            objective *= Mathf.Lerp(1f, 0.28f, Mathf.Clamp01(dangerPressure + (combinedProjectilePressure * 0.55f)));

            var collect = Vector2.zero;
            if (!hasPriorityObjective && nearestOrbPosition.HasValue && dangerPressure < 0.15f && combinedProjectilePressure < 0.15f && cornerPressure < 0.35f)
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
                + (approach * (hasPriorityObjective ? 0.05f : Mathf.Lerp(0.06f, 0.36f, 1f - Mathf.Clamp01(dangerPressure + (combinedProjectilePressure * 0.5f)))))
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

            move = ResolveWalkableMove(playerPosition2D, move, movementBounds, isWalkable);
            _cachedMove = move;
            return _cachedMove;
        }

        private Vector2 ResolveWalkableMove(Vector2 playerPosition, Vector2 move, Rect movementBounds, System.Func<Vector2, bool> isWalkable)
        {
            if (isWalkable == null || move.sqrMagnitude <= 0.000001f)
            {
                return move;
            }

            var moveMagnitude = Mathf.Clamp01(move.magnitude);
            var moveDirection = move / Mathf.Max(0.0001f, move.magnitude);
            const float sampleDistance = 0.9f;
            if (IsWalkableSample(playerPosition + (moveDirection * sampleDistance), movementBounds, isWalkable))
            {
                return move;
            }

            var bestScore = float.NegativeInfinity;
            var bestDirection = Vector2.zero;
            for (var i = 0; i < EscapeSampleDirections.Length; i++)
            {
                var candidate = EscapeSampleDirections[i];
                var sample = playerPosition + (candidate * sampleDistance);
                if (!IsWalkableSample(sample, movementBounds, isWalkable))
                {
                    continue;
                }

                var score = (Vector2.Dot(candidate, moveDirection) * 2.1f)
                    + (EvaluateSampleSafety(sample, movementBounds) * 0.12f)
                    + (Vector2.Dot(candidate, _wanderDirection) * 0.15f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDirection = candidate;
                }
            }

            return bestDirection.sqrMagnitude > 0.000001f
                ? bestDirection * moveMagnitude
                : Vector2.zero;
        }

        private static bool IsWalkableSample(Vector2 sample, Rect movementBounds, System.Func<Vector2, bool> isWalkable)
        {
            if (sample.x < movementBounds.xMin || sample.x > movementBounds.xMax ||
                sample.y < movementBounds.yMin || sample.y > movementBounds.yMax)
            {
                return false;
            }

            return isWalkable.Invoke(sample);
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

                var horizon = Mathf.Min(projectile.RemainingLifetime, 0.95f);
                var travelPadding = Mathf.Min(projectile.Speed * horizon, 5.5f) + projectile.HitRadius + 0.35f;
                var paddedRadius = searchRadius + travelPadding;
                if ((projectile.WorldPosition - playerPosition).sqrMagnitude <= paddedRadius * paddedRadius)
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

                var horizon = Mathf.Min(projectile.RemainingLifetime, 0.9f);
                var travelPadding = Mathf.Min(projectile.Speed * horizon, 5f) + projectile.HitRadius + 0.35f;
                var paddedRadius = searchRadius + travelPadding;
                if ((projectile.WorldPosition - playerPosition).sqrMagnitude <= paddedRadius * paddedRadius)
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

        private Vector2 ComputeBossObjective(Vector2 playerPosition, Vector3 bossPosition, float healthRatio, float projectilePressure)
        {
            var clampedPressure = Mathf.Clamp01(projectilePressure);
            var preferredDistance = Mathf.Lerp(4.4f, 3.4f, healthRatio) + Mathf.Lerp(0f, 1.4f, clampedPressure);
            var dangerDistance = Mathf.Lerp(2.6f, 1.9f, healthRatio) + Mathf.Lerp(0f, 0.5f, clampedPressure);
            var objective = ComputeCombatObjective(
                playerPosition,
                bossPosition,
                preferredDistance,
                dangerDistance,
                Mathf.Lerp(1.35f, 2.05f, clampedPressure));

            var toBoss = (Vector2)bossPosition - playerPosition;
            var bossDistance = toBoss.magnitude;
            if (bossDistance > preferredDistance + 1.2f && bossDistance <= 8.5f)
            {
                objective += (toBoss / Mathf.Max(0.001f, bossDistance)) * 0.45f;
            }

            if (clampedPressure > 0.01f && bossDistance < preferredDistance)
            {
                objective -= (toBoss / Mathf.Max(0.001f, bossDistance)) * Mathf.Lerp(0.2f, 0.75f, clampedPressure);
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

            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null || enemy.IsDead || !enemy.IsBoss)
                {
                    continue;
                }

                if (enemy.TryGetBossDashHazard(out var center, out var direction, out var length, out var width, out _))
                {
                    AddDashHazardAvoidance(
                        playerPosition,
                        center,
                        direction,
                        length,
                        width + 0.55f,
                        ref avoid);
                }

                if (enemy.TryGetBossRadialHazard(out center, out var radialRadius, out var radialRemainingTime))
                {
                    var toPlayer = playerPosition - center;
                    var distance = toPlayer.magnitude;
                    var safeRadius = radialRadius + clearancePadding + Mathf.Lerp(0.15f, 0.65f, Mathf.Clamp01(radialRemainingTime));
                    if (distance >= safeRadius)
                    {
                        continue;
                    }

                    var awayDirection = distance > 0.0001f ? toPlayer / distance : Random.insideUnitCircle.normalized;
                    var weight = 1f - (distance / Mathf.Max(0.1f, safeRadius));
                    avoid += awayDirection * Mathf.Lerp(1.15f, 2.25f, weight);
                }
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
                    0.95f,
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
                    1.05f,
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

                if (!enemy.IsBoss)
                {
                    continue;
                }

                if (enemy.TryGetBossDashHazard(out center, out var direction, out var length, out var width, out _))
                {
                    score -= EvaluateDashThreat(samplePosition, center, direction, length, width + 0.55f);
                }

                if (enemy.TryGetBossRadialHazard(out center, out radius, out _))
                {
                    var radialDistance = Vector2.Distance(samplePosition, center) - radius;
                    if (radialDistance <= 0.15f)
                    {
                        score -= 9.5f;
                    }
                    else if (radialDistance <= 1.3f)
                    {
                        score -= Mathf.Lerp(5.8f, 0f, radialDistance / 1.3f);
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
                    1.05f);
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
                    1.1f);
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

            var horizon = Mathf.Min(remainingLifetime, 0.95f);
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
            var approachDistance = Mathf.Max(0f, Vector2.Dot(closestPoint - projectilePosition, direction.normalized));
            var impactTime = speed > 0.001f ? approachDistance / speed : horizon;
            var timeWeight = 1f - Mathf.Clamp01(impactTime / Mathf.Max(0.05f, horizon));
            var weight = (1f - (distance / Mathf.Max(0.05f, safeRadius))) * Mathf.Lerp(0.8f, 1.3f, timeWeight);
            avoid += awayDirection * Mathf.Lerp(1.1f, 2.55f, weight);
        }

        private static void AddDashHazardAvoidance(
            Vector2 playerPosition,
            Vector2 dashOrigin,
            Vector2 dashDirection,
            float dashLength,
            float hazardWidth,
            ref Vector2 avoid)
        {
            var projectedEnd = dashOrigin + (dashDirection.normalized * Mathf.Max(0.25f, dashLength));
            var closestPoint = ClosestPointOnSegment(playerPosition, dashOrigin, projectedEnd);
            var offset = playerPosition - closestPoint;
            var distance = offset.magnitude;
            var safeWidth = Mathf.Max(0.15f, hazardWidth);
            if (distance >= safeWidth)
            {
                return;
            }

            var lateralDirection = distance > 0.0001f
                ? offset / distance
                : new Vector2(-dashDirection.y, dashDirection.x).normalized;
            var longitudinal = playerPosition - dashOrigin;
            var aheadFactor = Mathf.Clamp01(Vector2.Dot(dashDirection.normalized, longitudinal) / Mathf.Max(0.5f, dashLength));
            var weight = (1f - (distance / safeWidth)) * Mathf.Lerp(0.65f, 1.2f, aheadFactor);
            avoid += lateralDirection * Mathf.Lerp(1.25f, 2.4f, weight);
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

            var horizon = Mathf.Min(remainingLifetime, 0.95f);
            var projectedEnd = projectilePosition + (direction * speed * horizon);
            var closestPoint = ClosestPointOnSegment(samplePosition, projectilePosition, projectedEnd);
            var distance = Vector2.Distance(samplePosition, closestPoint);
            var safeRadius = hitRadius + avoidancePadding;
            if (distance >= safeRadius)
            {
                return 0f;
            }

            var approachDistance = Mathf.Max(0f, Vector2.Dot(closestPoint - projectilePosition, direction.normalized));
            var impactTime = speed > 0.001f ? approachDistance / speed : horizon;
            var timeWeight = 1f - Mathf.Clamp01(impactTime / Mathf.Max(0.05f, horizon));
            var weight = (1f - (distance / Mathf.Max(0.05f, safeRadius))) * Mathf.Lerp(0.8f, 1.3f, timeWeight);
            return Mathf.Lerp(0.85f, 4.6f, weight);
        }

        private float ComputeProjectilePressure(Vector2 playerPosition)
        {
            var pressure = 0f;

            for (var i = 0; i < _nearbyVariantProjectiles.Count; i++)
            {
                var projectile = _nearbyVariantProjectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                var threat = EvaluateProjectileThreat(
                    playerPosition,
                    projectile.WorldPosition,
                    projectile.Direction,
                    projectile.Speed,
                    projectile.RemainingLifetime,
                    projectile.HitRadius,
                    1.05f);
                pressure = Mathf.Max(pressure, Mathf.Clamp01(threat / 4.2f));
            }

            for (var i = 0; i < _nearbyBossProjectiles.Count; i++)
            {
                var projectile = _nearbyBossProjectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                var threat = EvaluateProjectileThreat(
                    playerPosition,
                    projectile.WorldPosition,
                    projectile.Direction,
                    projectile.Speed,
                    projectile.RemainingLifetime,
                    projectile.HitRadius,
                    1.1f);
                pressure = Mathf.Max(pressure, Mathf.Clamp01(threat / 4.6f));
            }

            return pressure;
        }

        private static float EvaluateDashThreat(
            Vector2 samplePosition,
            Vector2 dashOrigin,
            Vector2 dashDirection,
            float dashLength,
            float hazardWidth)
        {
            var projectedEnd = dashOrigin + (dashDirection.normalized * Mathf.Max(0.25f, dashLength));
            var closestPoint = ClosestPointOnSegment(samplePosition, dashOrigin, projectedEnd);
            var distance = Vector2.Distance(samplePosition, closestPoint);
            var safeWidth = Mathf.Max(0.15f, hazardWidth);
            if (distance >= safeWidth)
            {
                return 0f;
            }

            var longitudinal = samplePosition - dashOrigin;
            var aheadFactor = Mathf.Clamp01(Vector2.Dot(dashDirection.normalized, longitudinal) / Mathf.Max(0.5f, dashLength));
            var weight = (1f - (distance / safeWidth)) * Mathf.Lerp(0.75f, 1.2f, aheadFactor);
            return Mathf.Lerp(1.25f, 4.25f, weight);
        }

        private float ComputeBossProjectilePressure(Vector2 playerPosition)
        {
            var pressure = 0f;
            for (var i = 0; i < _nearbyEnemies.Count; i++)
            {
                var enemy = _nearbyEnemies[i];
                if (enemy == null || enemy.IsDead || !enemy.IsBoss || !enemy.HasBossProjectilePressure())
                {
                    continue;
                }

                var distance = Vector2.Distance(playerPosition, enemy.transform.position);
                var distanceWeight = 1f - Mathf.Clamp01(distance / 8.5f);
                pressure = Mathf.Max(pressure, Mathf.Lerp(0.35f, 1f, distanceWeight));
            }

            return pressure;
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
