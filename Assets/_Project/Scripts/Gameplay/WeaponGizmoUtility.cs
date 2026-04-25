using UnityEngine;

namespace EJR.Game.Gameplay
{
    public static class WeaponGizmoUtility
    {
        private const int CollisionGizmoSegments = 32;

        public static void DrawCircleCollisionGizmo(Vector3 center, float radius, Color color)
        {
            Gizmos.color = color;
            var previousPoint = center + new Vector3(radius, 0f, 0f);
            for (var i = 1; i <= CollisionGizmoSegments; i++)
            {
                var t = i / (float)CollisionGizmoSegments;
                var angle = t * Mathf.PI * 2f;
                var nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }

        public static void DrawProjectilePathGizmo(Vector2 start, Vector2 direction, float range, float hitRadius, Color color)
        {
            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            var end = start + (normalizedDirection * Mathf.Max(0.1f, range));
            Gizmos.color = color;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(start, hitRadius);
            Gizmos.DrawWireSphere(end, hitRadius);
        }

        public static void DrawConeCollisionGizmo(Vector2 origin, Vector2 direction, float range, float halfAngle, Color color)
        {
            var clampedRange = Mathf.Max(0.05f, range);
            var clampedHalfAngle = Mathf.Clamp(halfAngle, 1f, 179f);
            var left = RotateDirection(direction, -clampedHalfAngle);
            var right = RotateDirection(direction, clampedHalfAngle);

            Gizmos.color = color;
            Gizmos.DrawLine(origin, origin + (left * clampedRange));
            Gizmos.DrawLine(origin, origin + (right * clampedRange));

            var previousPoint = origin + (left * clampedRange);
            for (var segmentIndex = 1; segmentIndex <= CollisionGizmoSegments; segmentIndex++)
            {
                var t = segmentIndex / (float)CollisionGizmoSegments;
                var angle = Mathf.Lerp(-clampedHalfAngle, clampedHalfAngle, t);
                var nextPoint = origin + (RotateDirection(direction, angle) * clampedRange);
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }

        public static void DrawCapsuleCollisionGizmo(Vector2 start, Vector2 end, float radius, Color color)
        {
            var clampedRadius = Mathf.Max(0.01f, radius);
            var axis = end - start;
            var normalizedAxis = axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector2.right;
            var normal = new Vector2(-normalizedAxis.y, normalizedAxis.x) * clampedRadius;

            Gizmos.color = color;
            Gizmos.DrawLine(start + normal, end + normal);
            Gizmos.DrawLine(start - normal, end - normal);
            Gizmos.DrawWireSphere(start, clampedRadius);
            Gizmos.DrawWireSphere(end, clampedRadius);
        }

        private static Vector2 RotateDirection(Vector2 direction, float angleDegrees)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(
                (direction.x * cos) - (direction.y * sin),
                (direction.x * sin) + (direction.y * cos)
            );
        }
    }
}
