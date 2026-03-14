using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public static class WeaponFxRenderer
    {
        private static Material _sharedFxMaterial;

        public static void SpawnKatanaSlashFx(
            Transform parent,
            Vector2 origin,
            Vector2 direction,
            float range,
            int slashIndex,
            float forwardOffset,
            Vector2 localOffset,
            float scaleMultiplier,
            float fps,
            int sortingOrderBase)
        {
            if (parent == null)
            {
                return;
            }

            var useFlippedVariant = (Mathf.Max(0, slashIndex) & 1) == 1;
            var frames = useFlippedVariant
                ? RuntimeSpriteFactory.GetSexySwordAttackFlippedAnimationFrames()
                : RuntimeSpriteFactory.GetSexySwordAttackAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                return;
            }

            var normalizedDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
            var fxObject = new GameObject("KatanaSlashFx");
            fxObject.transform.SetParent(parent, false);

            var forward = Mathf.Max(0.05f, forwardOffset);
            var leftAxis = new Vector2(-normalizedDirection.y, normalizedDirection.x);
            var slashTier = Mathf.Max(0, slashIndex);
            var variantSign = (slashTier & 1) == 0 ? 1f : -1f;
            var lateralVariantOffset = slashTier <= 0 ? 0f : 0.08f * variantSign * Mathf.Min(2f, slashTier);
            var worldOffset = (normalizedDirection * localOffset.x) + (leftAxis * (localOffset.y + lateralVariantOffset));
            var fxPosition = origin + (normalizedDirection * forward) + worldOffset;
            fxObject.transform.position = new Vector3(fxPosition.x, fxPosition.y, -0.02f);
            fxObject.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg);
            var scale = Mathf.Max(0.05f, scaleMultiplier) * Mathf.Max(0.8f, range * 0.4f);
            fxObject.transform.localScale = Vector3.one * scale;

            var renderer = fxObject.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrderBase + Mathf.Max(0, slashIndex);

            var animator = fxObject.AddComponent<SpriteFxAnimator>();
            animator.Initialize(renderer, frames, fps, loop: false, destroyOnComplete: true);
        }

        public static void SpawnSatelliteBeamFx(
            Transform parent,
            Vector3 targetCenter,
            float scale,
            float yOffset,
            float fps,
            float fallbackDuration,
            int sortingOrder)
        {
            if (parent == null)
            {
                return;
            }

            var frames = RuntimeSpriteFactory.GetSexySatelliteBeamAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                return;
            }

            var frame = frames[0];
            var ppu = Mathf.Max(0.0001f, frame.pixelsPerUnit);
            var visualScale = Mathf.Max(0.05f, scale);
            var halfHeight = (frame.rect.height / ppu) * 0.5f * visualScale;
            var totalYOffset = halfHeight + yOffset;

            var fxObject = new GameObject("SatelliteBeamFx");
            fxObject.transform.SetParent(parent, false);
            fxObject.transform.position = new Vector3(targetCenter.x, targetCenter.y + totalYOffset, -0.02f);
            fxObject.transform.localScale = Vector3.one * visualScale;

            var renderer = fxObject.AddComponent<SpriteRenderer>();
            renderer.sprite = frame;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;

            if (frames.Length > 1)
            {
                var animator = fxObject.AddComponent<SpriteFxAnimator>();
                animator.Initialize(renderer, frames, fps, loop: false, destroyOnComplete: true);
                return;
            }

            Object.Destroy(fxObject, Mathf.Max(0.02f, fallbackDuration));
        }

        public static void SpawnLineFx(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Color color,
            float width,
            float duration,
            string name,
            int sortingOrder = 500)
        {
            var points = new[] { from, to };
            SpawnPolylineFx(parent, points, color, width, duration, loop: false, name, sortingOrder);
        }

        public static void SpawnRingFx(
            Transform parent,
            Vector3 center,
            float radius,
            int segments,
            Color color,
            float width,
            float duration,
            string name,
            int sortingOrder = 500)
        {
            if (parent == null)
            {
                return;
            }

            var fxObject = new GameObject(name);
            fxObject.transform.SetParent(parent, false);
            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, color, width, loop: true, useWorldSpace: true, sortingOrder);
            SetCircleLinePositions(lineRenderer, center, radius, segments, -0.02f);
            Object.Destroy(fxObject, Mathf.Max(0.02f, duration));
        }

        public static void SpawnPolylineFx(
            Transform parent,
            IReadOnlyList<Vector3> points,
            Color color,
            float width,
            float duration,
            bool loop,
            string name,
            int sortingOrder = 500)
        {
            if (parent == null || points == null || points.Count <= 1)
            {
                return;
            }

            var fxObject = new GameObject(name);
            fxObject.transform.SetParent(parent, false);
            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, color, width, loop, useWorldSpace: true, sortingOrder);
            lineRenderer.positionCount = points.Count;
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                lineRenderer.SetPosition(i, new Vector3(point.x, point.y, -0.02f));
            }

            Object.Destroy(fxObject, Mathf.Max(0.02f, duration));
        }

        public static void ConfigureLineRenderer(
            LineRenderer lineRenderer,
            Color color,
            float width,
            bool loop,
            bool useWorldSpace,
            int sortingOrder = 500)
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.useWorldSpace = useWorldSpace;
            lineRenderer.loop = loop;
            lineRenderer.numCapVertices = 2;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.startWidth = Mathf.Max(0.001f, width);
            lineRenderer.endWidth = Mathf.Max(0.001f, width);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.sortingOrder = sortingOrder;
            lineRenderer.sharedMaterial = GetOrCreateSharedFxMaterial();
        }

        public static void SetCircleLinePositions(LineRenderer lineRenderer, Vector3 center, float radius, int segments, float z)
        {
            if (lineRenderer == null)
            {
                return;
            }

            var clampedRadius = Mathf.Max(0.01f, radius);
            var clampedSegments = Mathf.Clamp(segments, 8, 96);
            lineRenderer.positionCount = clampedSegments;
            for (var i = 0; i < clampedSegments; i++)
            {
                var t = i / (float)clampedSegments;
                var angle = t * Mathf.PI * 2f;
                var point = new Vector3(
                    center.x + (Mathf.Cos(angle) * clampedRadius),
                    center.y + (Mathf.Sin(angle) * clampedRadius),
                    z);
                lineRenderer.SetPosition(i, point);
            }
        }

        private static Material GetOrCreateSharedFxMaterial()
        {
            if (_sharedFxMaterial != null)
            {
                return _sharedFxMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _sharedFxMaterial = new Material(shader)
            {
                name = "WeaponFxMat",
                hideFlags = HideFlags.HideAndDontSave,
            };

            return _sharedFxMaterial;
        }
    }
}
