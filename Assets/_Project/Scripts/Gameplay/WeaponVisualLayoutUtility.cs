using UnityEngine;

namespace EJR.Game.Gameplay
{
    public static class WeaponVisualLayoutUtility
    {
        private const float PivotTolerance = 0.0001f;
        private const float ProjectileEdgeInsetPixels = 1f;

        public static Vector2 CalculateWeaponLocalPosition(
            Vector2 orbitCenterLocal,
            Vector2 aimDirection,
            float aimDistance,
            Vector2 configuredOffset,
            bool flipX,
            float rotationDegrees,
            Sprite sprite)
        {
            var normalizedDirection = aimDirection.sqrMagnitude > 0.000001f ? aimDirection.normalized : Vector2.right;
            var hasCustomPivot = HasAuthoredCustomPivot(sprite);
            var resolvedOffset = hasCustomPivot
                ? Vector2.zero
                : new Vector2(flipX ? -configuredOffset.x : configuredOffset.x, configuredOffset.y);
            var resolvedAimDistance = Mathf.Max(0.05f, aimDistance);
            var resolvedAimDirection = normalizedDirection;
            return orbitCenterLocal + (resolvedAimDirection * resolvedAimDistance) + resolvedOffset;
        }

        public static Vector3 ResolveProjectileSpawnWorld(Transform weaponTransform, SpriteRenderer weaponRenderer, bool flipX)
        {
            if (weaponTransform == null)
            {
                return Vector3.zero;
            }

            if (weaponRenderer == null || weaponRenderer.sprite == null)
            {
                return weaponTransform.position;
            }

            var localAnchor = GetForwardAnchorLocal(weaponRenderer.sprite, flipX);
            return weaponTransform.TransformPoint(localAnchor);
        }

        public static bool HasAuthoredCustomPivot(Sprite sprite)
        {
            if (sprite == null)
            {
                return false;
            }

            var rect = sprite.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            var expectedCenterX = rect.width * 0.5f;
            var expectedCenterY = rect.height * 0.5f;
            return Mathf.Abs(sprite.pivot.x - expectedCenterX) > PivotTolerance
                || Mathf.Abs(sprite.pivot.y - expectedCenterY) > PivotTolerance;
        }

        private static Vector2 GetForwardAnchorLocal(Sprite sprite, bool flipX)
        {
            var rect = sprite.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return Vector2.zero;
            }

            var pixelsPerUnit = Mathf.Max(0.0001f, sprite.pixelsPerUnit);
            var frontDistancePixels = Mathf.Max(0f, rect.width - sprite.pivot.x - ProjectileEdgeInsetPixels);
            var localX = frontDistancePixels / pixelsPerUnit;
            var localY = ((rect.height * 0.5f) - sprite.pivot.y) / pixelsPerUnit;

            return new Vector2(flipX ? -localX : localX, localY);
        }

    }
}
