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

        public static void SpawnFireBurstFx(
            Transform parent,
            Vector3 center,
            float scale,
            float duration,
            int sortingOrder,
            string name = "FireBurstFx")
        {
            var frames = RuntimeSpriteFactory.GetSexyFireBoomAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                return;
            }

            SpawnAnimatedSpriteFx(
                parent,
                center,
                Quaternion.identity,
                Vector3.one * Mathf.Max(0.05f, scale),
                frames,
                duration,
                sortingOrder,
                name,
                centerBySpriteBounds: true);
        }

        public static void SpawnStretchBeamFx(
            Transform parent,
            Vector3 from,
            Vector3 to,
            float widthScale,
            float duration,
            Color fallbackLineColor,
            float fallbackLineWidth,
            string name,
            int sortingOrder = 500,
            float minimumLength = 0.4f)
        {
            var segment = to - from;
            segment.z = 0f;
            var length = segment.magnitude;
            if (length <= 0.0001f)
            {
                return;
            }

            var clampedMinimumLength = Mathf.Max(0.05f, minimumLength);
            if (length < clampedMinimumLength)
            {
                SpawnLineFx(parent, from, to, fallbackLineColor, fallbackLineWidth, duration, name, sortingOrder);
                return;
            }

            var frames = RuntimeSpriteFactory.GetSexySatelliteBeamAnimationFrames();
            if (frames == null || frames.Length <= 0)
            {
                SpawnLineFx(parent, from, to, fallbackLineColor, fallbackLineWidth, duration, name, sortingOrder);
                return;
            }

            var referenceFrame = GetTallestFrame(frames);
            if (referenceFrame == null)
            {
                SpawnLineFx(parent, from, to, fallbackLineColor, fallbackLineWidth, duration, name, sortingOrder);
                return;
            }

            var pixelsPerUnit = Mathf.Max(0.0001f, referenceFrame.pixelsPerUnit);
            var baseHeight = Mathf.Max(0.0001f, referenceFrame.rect.height / pixelsPerUnit);
            var direction = -segment / length;
            var midpoint = from + (segment * 0.5f);
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            var scale = new Vector3(Mathf.Max(0.05f, widthScale), length / baseHeight, 1f);

            SpawnAnimatedSpriteFx(
                parent,
                midpoint,
                rotation,
                scale,
                frames,
                duration,
                sortingOrder,
                name,
                centerBySpriteBounds: false);
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
            var fxObject = new GameObject(name);
            if (parent != null)
            {
                fxObject.transform.SetParent(parent, false);
            }

            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, color, width, loop: true, useWorldSpace: true, sortingOrder);
            SetCircleLinePositions(lineRenderer, center, radius, segments, -0.02f);
            Object.Destroy(fxObject, Mathf.Max(0.02f, duration));
        }

        public static void SpawnBurstFx(
            Transform parent,
            Vector3 center,
            Color color,
            int spokeCount,
            float innerRadius,
            float outerRadius,
            float width,
            float duration,
            string name,
            int sortingOrder = 500)
        {
            if (parent == null)
            {
                return;
            }

            var clampedSpokes = Mathf.Clamp(spokeCount, 1, 16);
            var clampedInnerRadius = Mathf.Max(0f, innerRadius);
            var clampedOuterRadius = Mathf.Max(clampedInnerRadius + 0.01f, outerRadius);
            var angleOffset = Random.Range(0f, 360f);
            for (var i = 0; i < clampedSpokes; i++)
            {
                var angle = angleOffset + ((360f / clampedSpokes) * i);
                var radians = angle * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
                var from = center + (direction * clampedInnerRadius);
                var to = center + (direction * clampedOuterRadius);
                SpawnLineFx(parent, from, to, color, width, duration, $"{name}_{i}", sortingOrder);
            }
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
            if (points == null || points.Count <= 1)
            {
                return;
            }

            var fxObject = new GameObject(name);
            if (parent != null)
            {
                fxObject.transform.SetParent(parent, false);
            }

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

        private static void SpawnAnimatedSpriteFx(
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            IReadOnlyList<Sprite> frames,
            float duration,
            int sortingOrder,
            string name,
            bool centerBySpriteBounds)
        {
            if (frames == null || frames.Count <= 0)
            {
                return;
            }

            var firstFrame = frames[0];
            if (firstFrame == null)
            {
                return;
            }

            var fxObject = new GameObject(name);
            if (parent != null)
            {
                fxObject.transform.SetParent(parent, false);
            }

            var clampedScale = new Vector3(
                Mathf.Max(0.05f, scale.x),
                Mathf.Max(0.05f, scale.y),
                Mathf.Max(0.05f, scale.z));
            var placementOffset = ResolveSpritePlacementOffset(firstFrame, clampedScale, centerBySpriteBounds);
            fxObject.transform.position = position + (rotation * placementOffset);
            fxObject.transform.rotation = rotation;
            fxObject.transform.localScale = clampedScale;

            var renderer = fxObject.AddComponent<SpriteRenderer>();
            renderer.sprite = firstFrame;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;

            var clampedDuration = Mathf.Max(0.02f, duration);
            if (frames.Count > 1)
            {
                var animator = fxObject.AddComponent<SpriteFxAnimator>();
                var framesPerSecond = Mathf.Max(1f, frames.Count / clampedDuration);
                animator.Initialize(renderer, ToSpriteArray(frames), framesPerSecond, loop: false, destroyOnComplete: true);
                return;
            }

            Object.Destroy(fxObject, clampedDuration);
        }

        private static Vector3 ResolveSpritePlacementOffset(Sprite sprite, Vector3 scale, bool centerBySpriteBounds)
        {
            if (!centerBySpriteBounds || sprite == null)
            {
                return new Vector3(0f, 0f, -0.02f);
            }

            var centerFromPivot = sprite.bounds.center;
            return new Vector3(
                -centerFromPivot.x * scale.x,
                -centerFromPivot.y * scale.y,
                -0.02f);
        }

        private static Sprite GetTallestFrame(IReadOnlyList<Sprite> frames)
        {
            if (frames == null || frames.Count <= 0)
            {
                return null;
            }

            Sprite tallestFrame = null;
            var tallestHeight = -1f;
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                if (frame == null || frame.rect.height <= tallestHeight)
                {
                    continue;
                }

                tallestFrame = frame;
                tallestHeight = frame.rect.height;
            }

            return tallestFrame;
        }

        private static Sprite[] ToSpriteArray(IReadOnlyList<Sprite> frames)
        {
            if (frames is Sprite[] spriteArray)
            {
                return spriteArray;
            }

            var copiedFrames = new Sprite[frames.Count];
            for (var i = 0; i < frames.Count; i++)
            {
                copiedFrames[i] = frames[i];
            }

            return copiedFrames;
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
