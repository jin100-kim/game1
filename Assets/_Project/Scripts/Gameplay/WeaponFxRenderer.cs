using System.Collections.Generic;
using EJR.Game.Audio;
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
            // 새로운 화염 폭발 프리팹을 시도합니다.
            if (SpawnPrefabFx("VFX/Fireball/VFX_2D_Projectile_Fire_Impact_01_Color_Static", center, Quaternion.identity, Vector3.one * scale, duration, sortingOrder))
            {
                return;
            }

            // 실패 시 기존 방식으로 폴백
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

        public static bool SpawnPrefabFx(
            string resourcePath,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float duration,
            int sortingOrder)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return false;

            var fxObject = Object.Instantiate(prefab, position, rotation);
            VfxAudioRouter.RouteEmbeddedAudio(fxObject);
            fxObject.transform.localScale = scale;

            // 정렬 순서 적용 (SpriteRenderer나 ParticleSystemRenderer가 있는 경우)
            var renderers = fxObject.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sortingOrder = sortingOrder;
            }

            Object.Destroy(fxObject, Mathf.Max(0.1f, duration));
            return true;
        }

        public static void SpawnChainSegmentedFx(
            Transform parent,
            Vector3 from,
            Vector3 to,
            GameObject prefab,
            float segmentLength,
            float duration,
            int sortingOrder = 600)
        {
            if (prefab == null) return;

            var fullSegment = to - from;
            fullSegment.z = 0f;
            var totalLength = fullSegment.magnitude;
            if (totalLength <= 0.05f) return;

            var direction = fullSegment / totalLength;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            // 거리에 맞춰 마디가 몇 개 필요한지 계산합니다.
            var count = Mathf.CeilToInt(totalLength / Mathf.Max(0.1f, segmentLength));
            var step = fullSegment / count;

            for (int i = 0; i < count; i++)
            {
                var pos = from + (step * (i + 0.5f));
                var fx = Object.Instantiate(prefab, pos, Quaternion.Euler(0, 0, angle));
                VfxAudioRouter.RouteEmbeddedAudio(fx);
                if (parent != null) fx.transform.SetParent(parent);

                // 각 마디마다 약간의 무작위성을 주어 "지지직"거리는 느낌을 줍니다.
                var randomScale = Random.Range(0.8f, 1.2f);
                var flip = (Random.value > 0.5f) ? 1 : -1;
                fx.transform.localScale = new Vector3(segmentLength * randomScale, 1.0f * flip, 1.0f);

                // 정렬 순서 적용
                var renderers = fx.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers) r.sortingOrder = sortingOrder;

                Object.Destroy(fx, duration);
            }
        }

        public static void SpawnStretchedPrefabFx(
            Transform parent,
            Vector3 from,
            Vector3 to,
            GameObject prefab,
            float duration,
            int sortingOrder = 500,
            float widthScale = 1.0f)
        {
            if (prefab == null) return;

            var segment = to - from;
            segment.z = 0f;
            var length = segment.magnitude;
            if (length <= 0.0001f) return;

            // 픽셀 아트 에셋들의 기본 방향은 보통 위(Up)를 향하고 있습니다.
            // 하지만 번개 에셋들은 오른쪽(Right)을 향하고 있을 수도 있으므로, 
            // 에셋의 기본 방향이 오른쪽이라고 가정하고 회전을 계산합니다.
            var direction = segment / length;
            var midpoint = from + (segment * 0.5f);
            var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            
            // 번개 프리팹의 길이를 두 점 사이의 거리에 맞춥니다.
            // 기본 길이가 1유닛이라고 가정할 때 거리에 맞춰 X축 스케일을 조정합니다.
            var scale = new Vector3(length, widthScale, 1f);

            var fxObject = Object.Instantiate(prefab, midpoint, rotation);
            VfxAudioRouter.RouteEmbeddedAudio(fxObject);
            if (parent != null) fxObject.transform.SetParent(parent);
            fxObject.transform.localScale = scale;

            // 정렬 순서 적용
            var renderers = fxObject.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sortingOrder = sortingOrder;
            }

            Object.Destroy(fxObject, Mathf.Max(0.1f, duration));
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

        public static void SpawnProceduralLightningFx(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Color color,
            float width,
            float duration,
            int segments = 10,
            float jitterAmount = 0.2f,
            int sortingOrder = 600)
        {
            var fxObject = new GameObject("ProceduralLightningFx");
            if (parent != null) fxObject.transform.SetParent(parent, false);

            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, color, width, false, true, sortingOrder);

            // 지그재그 포인트를 생성합니다.
            lineRenderer.positionCount = segments + 1;
            var direction = to - from;
            var length = direction.magnitude;
            var normalizedDir = direction / length;
            var rightAxis = new Vector3(-normalizedDir.y, normalizedDir.x, 0);

            lineRenderer.SetPosition(0, from);
            for (int i = 1; i < segments; i++)
            {
                float t = i / (float)segments;
                var basePos = from + (direction * t);
                var jitter = rightAxis * Random.Range(-jitterAmount, jitterAmount);
                lineRenderer.SetPosition(i, basePos + jitter + Vector3.back * 0.1f);
            }
            lineRenderer.SetPosition(segments, to);

            // 번쩍이는 애니메이션을 위해 아주 잠깐 뒤에 사라지게 합니다.
            Object.Destroy(fxObject, duration);
        }

        public static void ConfigureLineRenderer(
            LineRenderer lineRenderer,
            Color color,
            float width,
            bool loop,
            bool useWorldSpace,
            int sortingOrder = 500,
            Material customMaterial = null)
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
            lineRenderer.textureMode = LineTextureMode.Tile; // 텍스처를 타일링하여 깨짐 방지
            lineRenderer.startWidth = Mathf.Max(0.001f, width);
            lineRenderer.endWidth = Mathf.Max(0.001f, width);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.sortingOrder = sortingOrder;
            lineRenderer.sharedMaterial = customMaterial != null ? customMaterial : GetOrCreateSharedFxMaterial();
        }

        public static void SpawnTexturedLineFx(
            Transform parent,
            Vector3 from,
            Vector3 to,
            Material material,
            float width,
            float duration,
            int sortingOrder = 600)
        {
            var fxObject = new GameObject("TexturedLineFx");
            if (parent != null) fxObject.transform.SetParent(parent, false);

            var lineRenderer = fxObject.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRenderer, Color.white, width, false, true, sortingOrder, material);
            lineRenderer.SetPosition(0, from);
            lineRenderer.SetPosition(1, to);

            Object.Destroy(fxObject, duration);
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

    internal static class VfxAudioRouter
    {
        public static void RouteEmbeddedAudio(GameObject root, bool playOnce = true, float volumeScale = 1f)
        {
            if (root == null)
            {
                return;
            }

            var audioSources = root.GetComponentsInChildren<AudioSource>(true);
            for (var i = 0; i < audioSources.Length; i++)
            {
                var source = audioSources[i];
                if (source == null)
                {
                    continue;
                }

                var clip = source.clip;
                var sourceVolume = source.volume;
                var sourcePitch = source.pitch;

                source.Stop();
                source.playOnAwake = false;
                source.loop = false;
                source.mute = true;
                source.enabled = false;

                if (playOnce && clip != null)
                {
                    AudioService.Instance.PlaySfxClip(clip, sourceVolume * volumeScale, sourcePitch);
                }
            }
        }
    }
}
