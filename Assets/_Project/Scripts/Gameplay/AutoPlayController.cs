using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class AutoPlayController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float edgeBuffer = 2f;
        [SerializeField, Min(0.1f)] private float dangerRadius = 4.8f;
        [SerializeField, Min(0.1f)] private float emergencyRadius = 2.2f;
        [SerializeField, Min(0.1f)] private float escapeProbeDistance = 2.2f;
        [SerializeField, Range(6, 24)] private int escapeProbeDirections = 14;
        [SerializeField, Min(0f)] private float escapeDangerWeight = 1.3f;
        [SerializeField, Min(0f)] private float escapeBoundsWeight = 3f;
        [SerializeField, Min(0f)] private float escapeCenterWeight = 0.65f;
        [SerializeField, Min(0f)] private float escapeMomentumWeight = 0.35f;
        [SerializeField, Min(0f)] private float wallJamEscapeWeight = 3.2f;
        [SerializeField, Min(0f)] private float wallJamExitDuration = 0.45f;
        [SerializeField, Min(0f)] private float stuckDetectSeconds = 0.25f;
        [SerializeField, Min(0f)] private float stuckMinMovePerSecond = 0.32f;
        [SerializeField, Min(0.1f)] private float cornerDangerBoostRadius = 2.6f;
        [SerializeField, Min(0.05f)] private float xpSeekRadius = 9f;
        [SerializeField, Min(0f)] private float xpSeekWeight = 1.2f;
        [SerializeField, Min(0.1f)] private float xpSafeMinDistance = 2.35f;
        [SerializeField, Min(0f)] private float xpDangerDensityLimit = 1.55f;
        [SerializeField, Min(0f)] private float xpAbortBoundsRisk = 0.95f;
        [SerializeField, Min(0f)] private float centerBiasWeight = 0.55f;
        [SerializeField, Min(0f)] private float edgeAvoidWeight = 2.4f;
        [SerializeField, Min(0f)] private float crowdRepulsionWeight = 2.6f;
        [SerializeField, Min(0f)] private float strafeWeight = 1.2f;
        [SerializeField, Min(0f)] private float approachWeight = 0.45f;
        [SerializeField, Min(0.1f)] private float strafeProbeDistance = 1.4f;

        private Transform _player;
        private EnemyRegistry _enemyRegistry;
        private Rect _arenaBounds;
        private float _preferredRange = 6f;
        private float _playerCollisionRadius = 0.35f;

        private float _strafeSign = 1f;
        private float _nextStrafeFlipAt;

        private float _lastThreatDensity;
        private float _lastNearestDistance = float.MaxValue;
        private Vector2 _lastNearestDirection = Vector2.right;
        private Vector2 _lastMoveDirection = Vector2.right;
        private Vector2 _lastPlayerPosition;
        private float _stuckTime;
        private float _forcedEscapeUntil;
        private ExperienceOrb _cachedXpTarget;
        private float _nextXpScanAt;

        public void Initialize(
            Transform player,
            EnemyRegistry enemyRegistry,
            Rect arenaBounds,
            float preferredRange,
            float playerCollisionRadius)
        {
            _player = player;
            _enemyRegistry = enemyRegistry;
            _arenaBounds = arenaBounds;
            _preferredRange = Mathf.Max(1f, preferredRange);
            _playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);

            _strafeSign = Random.value < 0.5f ? -1f : 1f;
            _nextStrafeFlipAt = Time.time + Random.Range(0.8f, 1.8f);
            _lastThreatDensity = 0f;
            _lastNearestDistance = float.MaxValue;
            _lastNearestDirection = Vector2.right;
            _lastMoveDirection = Vector2.right;
            _lastPlayerPosition = _player != null ? (Vector2)_player.position : Vector2.zero;
            _stuckTime = 0f;
            _forcedEscapeUntil = -1f;
            _cachedXpTarget = null;
            _nextXpScanAt = 0f;
        }

        public Vector2 ReadMove()
        {
            if (_player == null)
            {
                return Vector2.zero;
            }

            var playerPosition = (Vector2)_player.position;
            var movementDelta = (playerPosition - _lastPlayerPosition).magnitude;
            UpdateThreatSnapshot();
            var boundsRisk = EvaluateBoundsRisk(playerPosition);
            UpdateStuckState(boundsRisk, movementDelta);

            var forcedEscape = Time.time < _forcedEscapeUntil;
            var emergencyEscape = forcedEscape || ShouldUseEscape(boundsRisk);

            Vector2 move;
            if (emergencyEscape)
            {
                move = ComputeEscapeSteer(boundsRisk);
                if (forcedEscape)
                {
                    move += ComputeWallJamEscape(playerPosition) * wallJamEscapeWeight;
                }
            }
            else
            {
                move = _lastNearestDistance < float.MaxValue
                    ? ComputeThreatSteer()
                    : ComputeIdleSteer();

                if (TryGetXpSeekSteer(playerPosition, boundsRisk, out var xpSteer))
                {
                    move += xpSteer * xpSeekWeight;
                }
            }

            move += ComputeBoundsSteer() * edgeAvoidWeight;
            move += ComputeCenterSteer() * centerBiasWeight;

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }
            else if (move.sqrMagnitude > 0.0001f)
            {
                move = move.normalized;
            }

            if (move.sqrMagnitude > 0.0001f)
            {
                _lastMoveDirection = move;
            }

            _lastPlayerPosition = playerPosition;
            return move;
        }

        public int PickLevelUpOption(LevelUpOption[] options)
        {
            if (options == null || options.Length == 0)
            {
                return 0;
            }

            var bestIndex = 0;
            var bestScore = float.MinValue;
            for (var i = 0; i < options.Length; i++)
            {
                var score = options[i].UpgradeType switch
                {
                    LevelUpUpgradeType.AttackSpeed => 92f,
                    LevelUpUpgradeType.Damage => 80f,
                    LevelUpUpgradeType.MoveSpeed => 84f,
                    _ => 0f,
                };

                score += options[i].Value * 100f;

                if (_lastThreatDensity > 1.8f)
                {
                    score += options[i].UpgradeType switch
                    {
                        LevelUpUpgradeType.MoveSpeed => 42f,
                        LevelUpUpgradeType.AttackSpeed => 10f,
                        _ => 0f,
                    };
                }

                if (_lastNearestDistance < emergencyRadius * 1.35f)
                {
                    score += options[i].UpgradeType switch
                    {
                        LevelUpUpgradeType.MoveSpeed => 60f,
                        LevelUpUpgradeType.AttackSpeed => 12f,
                        _ => 0f,
                    };
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void UpdateThreatSnapshot()
        {
            _lastThreatDensity = 0f;
            _lastNearestDistance = float.MaxValue;
            _lastNearestDirection = Vector2.right;

            if (_enemyRegistry == null)
            {
                return;
            }

            var enemies = _enemyRegistry.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                var toEnemy = (Vector2)(enemy.transform.position - _player.position);
                var centerDistance = toEnemy.magnitude;
                if (centerDistance <= 0.0001f)
                {
                    continue;
                }

                var combinedRadius = enemy.CollisionRadius + _playerCollisionRadius;
                var surfaceDistance = Mathf.Max(0.01f, centerDistance - combinedRadius);
                var direction = toEnemy / centerDistance;

                if (surfaceDistance < _lastNearestDistance)
                {
                    _lastNearestDistance = surfaceDistance;
                    _lastNearestDirection = direction;
                }

                if (surfaceDistance <= dangerRadius)
                {
                    var proximity = 1f - (surfaceDistance / dangerRadius);
                    _lastThreatDensity += proximity * proximity;
                }
            }
        }

        private Vector2 ComputeThreatSteer()
        {
            var desiredRange = Mathf.Clamp(_preferredRange * 0.72f, emergencyRadius * 1.4f, dangerRadius * 1.1f);

            Vector2 radial;
            if (_lastNearestDistance < desiredRange)
            {
                var panic = Mathf.InverseLerp(desiredRange, emergencyRadius * 0.85f, _lastNearestDistance);
                radial = -_lastNearestDirection * Mathf.Lerp(0.8f, 2.4f, panic);
            }
            else
            {
                var far = Mathf.InverseLerp(desiredRange, desiredRange * 2f, _lastNearestDistance);
                radial = _lastNearestDirection * (approachWeight * far);
            }

            var crowdFlee = ComputeCrowdRepulsion();
            var tangent = ChooseStrafeDirection(_lastNearestDirection) * strafeWeight;

            var steer = crowdFlee + radial + tangent;
            if (steer.sqrMagnitude < 0.02f)
            {
                steer = ChooseStrafeDirection(_lastNearestDirection);
            }

            return steer;
        }

        private void UpdateStuckState(float boundsRisk, float movementDelta)
        {
            var nearWall = boundsRisk >= 0.92f;
            var tryingToMove = _lastMoveDirection.sqrMagnitude > 0.35f;
            var minExpectedMove = Mathf.Max(0.0001f, stuckMinMovePerSecond * Time.deltaTime);
            var barelyMoved = movementDelta <= minExpectedMove;

            if (nearWall && tryingToMove && barelyMoved)
            {
                _stuckTime += Time.deltaTime;
                if (_stuckTime >= stuckDetectSeconds)
                {
                    _forcedEscapeUntil = Time.time + Mathf.Max(0.1f, wallJamExitDuration);
                    _stuckTime = 0f;
                }
            }
            else
            {
                _stuckTime = Mathf.Max(0f, _stuckTime - (Time.deltaTime * 2.5f));
            }
        }

        private Vector2 ComputeWallJamEscape(Vector2 playerPosition)
        {
            var wallNormal = ComputeWallNormal(playerPosition);
            var centerDirection = (_arenaBounds.center - playerPosition).normalized;
            var threatAway = _lastNearestDistance < float.MaxValue ? -_lastNearestDirection : Vector2.zero;
            var cornerPressure = ComputeCornerPressure(playerPosition);

            var steer = wallNormal * 2.3f + centerDirection * (1.1f + cornerPressure * 1.4f) + threatAway * 0.7f;
            if (steer.sqrMagnitude <= 0.0001f)
            {
                steer = centerDirection.sqrMagnitude > 0.0001f ? centerDirection : Vector2.right;
            }

            return steer.normalized * Mathf.Lerp(1.2f, 2.6f, cornerPressure);
        }

        private Vector2 ComputeWallNormal(Vector2 position)
        {
            var normal = Vector2.zero;
            var effectiveBuffer = edgeBuffer + (_playerCollisionRadius * 1.8f);

            var leftDistance = position.x - _arenaBounds.xMin;
            var rightDistance = _arenaBounds.xMax - position.x;
            var bottomDistance = position.y - _arenaBounds.yMin;
            var topDistance = _arenaBounds.yMax - position.y;

            if (leftDistance < effectiveBuffer) normal.x += Mathf.Pow(1f - (leftDistance / effectiveBuffer), 2f);
            if (rightDistance < effectiveBuffer) normal.x -= Mathf.Pow(1f - (rightDistance / effectiveBuffer), 2f);
            if (bottomDistance < effectiveBuffer) normal.y += Mathf.Pow(1f - (bottomDistance / effectiveBuffer), 2f);
            if (topDistance < effectiveBuffer) normal.y -= Mathf.Pow(1f - (topDistance / effectiveBuffer), 2f);

            if (normal.sqrMagnitude > 0.0001f)
            {
                return normal.normalized;
            }

            var centerDirection = (_arenaBounds.center - position).normalized;
            return centerDirection.sqrMagnitude > 0.0001f ? centerDirection : Vector2.right;
        }

        private bool TryGetXpSeekSteer(Vector2 playerPosition, float boundsRisk, out Vector2 xpSteer)
        {
            xpSteer = Vector2.zero;
            if (_lastNearestDistance < xpSafeMinDistance)
            {
                return false;
            }

            if (_lastThreatDensity > xpDangerDensityLimit)
            {
                return false;
            }

            if (boundsRisk > xpAbortBoundsRisk)
            {
                return false;
            }

            if (!TryRefreshXpTarget(playerPosition))
            {
                return false;
            }

            if (_cachedXpTarget == null)
            {
                return false;
            }

            var targetPosition = (Vector2)_cachedXpTarget.transform.position;
            var toOrb = targetPosition - playerPosition;
            var distance = toOrb.magnitude;
            if (distance <= 0.0001f || distance > xpSeekRadius)
            {
                return false;
            }

            var dangerHere = EvaluateDangerAt(playerPosition);
            var dangerAtOrb = EvaluateDangerAt(targetPosition);
            if (dangerAtOrb > dangerHere + 0.95f)
            {
                return false;
            }

            var orbBoundsRisk = EvaluateBoundsRisk(targetPosition);
            if (orbBoundsRisk > 1.1f)
            {
                return false;
            }

            var seekStrength = Mathf.InverseLerp(xpSeekRadius, 0.5f, distance);
            seekStrength *= 1f + (_cachedXpTarget.Value * 0.12f);
            xpSteer = toOrb.normalized * Mathf.Clamp(seekStrength, 0.2f, 1.35f);
            return true;
        }

        private bool TryRefreshXpTarget(Vector2 playerPosition)
        {
            if (IsUsableXpTarget(_cachedXpTarget, playerPosition))
            {
                return true;
            }

            if (Time.time < _nextXpScanAt)
            {
                return false;
            }

            _nextXpScanAt = Time.time + 0.2f;
            _cachedXpTarget = null;

            var orbs = ExperienceOrb.ActiveOrbs;
            var bestScore = float.MaxValue;
            for (var i = 0; i < orbs.Count; i++)
            {
                var orb = orbs[i];
                if (!IsUsableXpTarget(orb, playerPosition))
                {
                    continue;
                }

                var orbPosition = (Vector2)orb.transform.position;
                var distance = Vector2.Distance(playerPosition, orbPosition);
                var danger = EvaluateDangerAt(orbPosition);
                var boundsRisk = EvaluateBoundsRisk(orbPosition);
                var score = distance + (danger * 0.9f) + (boundsRisk * 1.4f) - (orb.Value * 0.55f);
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                _cachedXpTarget = orb;
            }

            return _cachedXpTarget != null;
        }

        private bool IsUsableXpTarget(ExperienceOrb orb, Vector2 playerPosition)
        {
            if (orb == null || !orb.isActiveAndEnabled)
            {
                return false;
            }

            var orbPosition = (Vector2)orb.transform.position;
            var distance = Vector2.Distance(playerPosition, orbPosition);
            if (distance > xpSeekRadius * 1.35f)
            {
                return false;
            }

            if (EvaluateBoundsRisk(orbPosition) > 1.2f)
            {
                return false;
            }

            return true;
        }

        private float ComputeCornerPressure(Vector2 position)
        {
            var bottomLeft = new Vector2(_arenaBounds.xMin, _arenaBounds.yMin);
            var bottomRight = new Vector2(_arenaBounds.xMax, _arenaBounds.yMin);
            var topLeft = new Vector2(_arenaBounds.xMin, _arenaBounds.yMax);
            var topRight = new Vector2(_arenaBounds.xMax, _arenaBounds.yMax);

            var nearestCornerDistance = Mathf.Min(
                Vector2.Distance(position, bottomLeft),
                Mathf.Min(
                    Vector2.Distance(position, bottomRight),
                    Mathf.Min(Vector2.Distance(position, topLeft), Vector2.Distance(position, topRight))));

            var radius = Mathf.Max(0.2f, cornerDangerBoostRadius);
            return Mathf.Clamp01(1f - (nearestCornerDistance / radius));
        }

        private bool ShouldUseEscape(float boundsRisk)
        {
            if (_lastNearestDistance < emergencyRadius * 1.18f)
            {
                return true;
            }

            if (boundsRisk >= 1.05f)
            {
                return true;
            }

            if (_lastThreatDensity > 1.95f && boundsRisk > 0.42f)
            {
                return true;
            }

            var cornerPressure = ComputeCornerPressure((Vector2)_player.position);
            if (cornerPressure > 0.28f && (_lastThreatDensity > 1.15f || _lastNearestDistance < dangerRadius * 0.9f))
            {
                return true;
            }

            return false;
        }

        private Vector2 ComputeEscapeSteer(float currentBoundsRisk)
        {
            var centerDirection = (_arenaBounds.center - (Vector2)_player.position).normalized;
            var fallback = centerDirection.sqrMagnitude > 0.0001f
                ? centerDirection
                : -_lastNearestDirection;

            var probeDirections = Mathf.Clamp(escapeProbeDirections, 6, 24);
            var bestDirection = fallback;
            var bestScore = float.MaxValue;
            var playerPosition = (Vector2)_player.position;
            var maxCenterDistance = Mathf.Max(0.1f, Mathf.Min(_arenaBounds.width, _arenaBounds.height) * 0.5f);

            for (var i = 0; i < probeDirections; i++)
            {
                var angle = (Mathf.PI * 2f * i) / probeDirections;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var probe = playerPosition + direction * escapeProbeDistance;
                var danger = EvaluateDangerAt(probe);
                var boundsRisk = EvaluateBoundsRisk(probe);
                var centerDistance = Vector2.Distance(probe, _arenaBounds.center) / maxCenterDistance;
                var cornerPressure = ComputeCornerPressure(probe);
                var momentumDot = Vector2.Dot(direction, _lastMoveDirection);
                var towardThreatDot = Vector2.Dot(direction, _lastNearestDirection);

                var score =
                    danger * escapeDangerWeight +
                    boundsRisk * escapeBoundsWeight +
                    cornerPressure * 1.6f +
                    centerDistance * escapeCenterWeight -
                    Mathf.Max(0f, momentumDot) * escapeMomentumWeight;

                if (towardThreatDot > 0f)
                {
                    score += towardThreatDot * 0.9f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestDirection = direction;
                }
            }

            var panic = Mathf.Clamp01(
                Mathf.InverseLerp(emergencyRadius * 2f, emergencyRadius * 0.75f, _lastNearestDistance) +
                Mathf.InverseLerp(0.25f, 1.5f, currentBoundsRisk) * 0.5f);
            var intensity = Mathf.Lerp(1.2f, 2.4f, panic);
            return bestDirection * intensity;
        }

        private Vector2 ComputeCrowdRepulsion()
        {
            if (_enemyRegistry == null)
            {
                return Vector2.zero;
            }

            var repulsion = Vector2.zero;
            var enemies = _enemyRegistry.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                var toEnemy = (Vector2)(enemy.transform.position - _player.position);
                var centerDistance = toEnemy.magnitude;
                if (centerDistance <= 0.0001f)
                {
                    continue;
                }

                var combinedRadius = enemy.CollisionRadius + _playerCollisionRadius;
                var surfaceDistance = Mathf.Max(0.01f, centerDistance - combinedRadius);
                if (surfaceDistance > dangerRadius * 1.2f)
                {
                    continue;
                }

                var direction = toEnemy / centerDistance;
                var proximity = 1f / Mathf.Pow(surfaceDistance + 0.3f, 1.35f);
                var weight = crowdRepulsionWeight * proximity;

                if (surfaceDistance < emergencyRadius)
                {
                    var boost = 1f + (emergencyRadius - surfaceDistance) / emergencyRadius;
                    weight *= 2.3f * boost;
                }

                repulsion += -direction * weight;
            }

            return repulsion;
        }

        private Vector2 ChooseStrafeDirection(Vector2 nearestDirection)
        {
            if (nearestDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector2.right;
            }

            var left = new Vector2(-nearestDirection.y, nearestDirection.x).normalized;
            var right = -left;
            var currentPosition = (Vector2)_player.position;

            var leftProbe = currentPosition + left * strafeProbeDistance;
            var rightProbe = currentPosition + right * strafeProbeDistance;

            var leftRisk = EvaluateDangerAt(leftProbe) + EvaluateBoundsRisk(leftProbe) * 1.35f;
            var rightRisk = EvaluateDangerAt(rightProbe) + EvaluateBoundsRisk(rightProbe) * 1.35f;

            if (Mathf.Abs(leftRisk - rightRisk) < 0.08f)
            {
                if (Time.time >= _nextStrafeFlipAt)
                {
                    _strafeSign *= -1f;
                    _nextStrafeFlipAt = Time.time + Random.Range(0.8f, 1.8f);
                }

                return _strafeSign >= 0f ? left : right;
            }

            if (leftRisk < rightRisk)
            {
                _strafeSign = 1f;
                return left;
            }

            _strafeSign = -1f;
            return right;
        }

        private float EvaluateDangerAt(Vector2 probePosition)
        {
            if (_enemyRegistry == null)
            {
                return 0f;
            }

            var danger = 0f;
            var enemies = _enemyRegistry.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }

                var centerDistance = Vector2.Distance(probePosition, enemy.transform.position);
                var surfaceDistance = Mathf.Max(0.01f, centerDistance - (enemy.CollisionRadius + _playerCollisionRadius));
                if (surfaceDistance > dangerRadius * 1.4f)
                {
                    continue;
                }

                danger += 1f / (surfaceDistance + 0.25f);
                if (surfaceDistance < emergencyRadius)
                {
                    danger += (emergencyRadius - surfaceDistance) * 2.7f;
                }
            }

            return danger;
        }

        private Vector2 ComputeBoundsSteer()
        {
            var position = (Vector2)_player.position;
            var steer = Vector2.zero;
            var effectiveBuffer = edgeBuffer + (_playerCollisionRadius * 1.4f);

            var leftDistance = position.x - _arenaBounds.xMin;
            var rightDistance = _arenaBounds.xMax - position.x;
            var bottomDistance = position.y - _arenaBounds.yMin;
            var topDistance = _arenaBounds.yMax - position.y;

            if (leftDistance < effectiveBuffer) steer.x += Mathf.Pow(1f - (leftDistance / effectiveBuffer), 2f);
            if (rightDistance < effectiveBuffer) steer.x -= Mathf.Pow(1f - (rightDistance / effectiveBuffer), 2f);
            if (bottomDistance < effectiveBuffer) steer.y += Mathf.Pow(1f - (bottomDistance / effectiveBuffer), 2f);
            if (topDistance < effectiveBuffer) steer.y -= Mathf.Pow(1f - (topDistance / effectiveBuffer), 2f);

            return steer;
        }

        private float EvaluateBoundsRisk(Vector2 position)
        {
            var risk = 0f;
            var effectiveBuffer = edgeBuffer + (_playerCollisionRadius * 1.4f);

            var leftDistance = position.x - _arenaBounds.xMin;
            var rightDistance = _arenaBounds.xMax - position.x;
            var bottomDistance = position.y - _arenaBounds.yMin;
            var topDistance = _arenaBounds.yMax - position.y;

            if (leftDistance < effectiveBuffer) risk += Mathf.Pow(1f - (leftDistance / effectiveBuffer), 2f);
            if (rightDistance < effectiveBuffer) risk += Mathf.Pow(1f - (rightDistance / effectiveBuffer), 2f);
            if (bottomDistance < effectiveBuffer) risk += Mathf.Pow(1f - (bottomDistance / effectiveBuffer), 2f);
            if (topDistance < effectiveBuffer) risk += Mathf.Pow(1f - (topDistance / effectiveBuffer), 2f);

            return risk;
        }

        private Vector2 ComputeCenterSteer()
        {
            var center = _arenaBounds.center;
            var toCenter = center - (Vector2)_player.position;
            var distance = toCenter.magnitude;
            if (distance <= 0.25f)
            {
                return Vector2.zero;
            }

            var maxDistance = Mathf.Min(_arenaBounds.width, _arenaBounds.height) * 0.45f;
            var strength = Mathf.InverseLerp(maxDistance * 0.25f, maxDistance, distance);
            return toCenter.normalized * strength;
        }

        private Vector2 ComputeIdleSteer()
        {
            var t = Time.time * 0.7f;
            var orbit = new Vector2(Mathf.Sin(t * 1.17f), Mathf.Cos(t * 0.91f));
            var center = (_arenaBounds.center - (Vector2)_player.position).normalized;
            var move = orbit.sqrMagnitude > 1f ? orbit.normalized * 0.35f : orbit * 0.35f;
            move += center * 0.42f;
            return move;
        }
    }
}
