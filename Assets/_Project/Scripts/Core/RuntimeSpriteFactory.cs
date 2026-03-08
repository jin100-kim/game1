using System;
using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Core
{
    public static class RuntimeSpriteFactory
    {
        public enum EnemyVisualKind
        {
            Slime,
            Mushroom,
            Skeleton,
            Boss,
        }

        private readonly struct VisualAssetDescriptor
        {
            public readonly string ResourcePath;
            public readonly string AssetPath;

            public VisualAssetDescriptor(string resourcePath, string assetPath)
            {
                ResourcePath = resourcePath;
                AssetPath = assetPath;
            }
        }

        private const string FramePrefix = "Frame_";
        private const byte AlphaCutoff = 18;
        private const int KeyTolerance = 14;
        private const int SexyFireStackStartFrame = 0;
        private const int SexyFireStackEndFrame = 3;
        private const int SexyFireBoomStartFrame = 4;
        private const int SexyFireBoomEndFrame = 7;
        private const int SexySwordAttackStartFrame = 0;
        private const int SexySwordAttackEndFrame = 4;

        private static readonly VisualAssetDescriptor SlimeDescriptor = new(
            "Aseprite/Slime",
            "Assets/_Project/Art/Aseprite/Slime.aseprite");
        private static readonly VisualAssetDescriptor MushroomDescriptor = new(
            "Aseprite/mushroom",
            "Assets/_Project/Art/Aseprite/mushroom.aseprite");
        private static readonly VisualAssetDescriptor SkeletonDescriptor = new(
            "Aseprite/Skeleton",
            "Assets/_Project/Art/Aseprite/Skeleton.aseprite");
        private static readonly VisualAssetDescriptor BossDescriptor = new(
            "Aseprite/boss",
            "Assets/_Project/Art/Aseprite/boss.aseprite");
        private static readonly VisualAssetDescriptor PlayerDescriptor = new(
            "Aseprite/player001",
            "Assets/_Project/Art/Aseprite/player001.aseprite");
        private static readonly VisualAssetDescriptor Fire1Descriptor = new(
            "Aseprite/sexyrifle",
            "Assets/_Project/Art/Aseprite/sexyrifle.ase");
        private static readonly VisualAssetDescriptor SexySwordDescriptor = new(
            "Aseprite/sexysword",
            "Assets/_Project/Art/Aseprite/sexysword.aseprite");
        private static readonly VisualAssetDescriptor SexyFireDescriptor = new(
            "Aseprite/sexyfire",
            "Assets/_Project/Art/Aseprite/sexyfire.aseprite");
        private static readonly VisualAssetDescriptor SexyDroneDescriptor = new(
            "Aseprite/sexydrone",
            "Assets/_Project/Art/Aseprite/sexydrone.aseprite");

        private static readonly Dictionary<EnemyVisualKind, Sprite[]> EnemyFramesByKind = new();
        private static Sprite[] _playerFrames;
        private static Sprite[] _weaponFire1Frames;
        private static Sprite[] _sexySwordFrames;
        private static Sprite[] _sexySwordAttackFrames;
        private static Sprite[] _sexyFireFrames;
        private static Sprite[] _sexyFireStackFrames;
        private static Sprite[] _sexyFireBoomFrames;
        private static Sprite[] _sexyDroneFrames;

        private static Sprite _squareSprite;

        public static Sprite GetSquareSprite()
        {
            if (_squareSprite != null)
            {
                return _squareSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _squareSprite.name = "RuntimeSquare";
            return _squareSprite;
        }

        public static Sprite GetSlimeSprite()
        {
            return GetEnemySprite(EnemyVisualKind.Slime);
        }

        public static Sprite[] GetSlimeAnimationFrames()
        {
            return GetEnemyAnimationFrames(EnemyVisualKind.Slime);
        }

        public static Sprite GetPlayerSprite()
        {
            var frames = GetPlayerAnimationFrames();
            return frames.Length > 0 ? frames[0] : GetSquareSprite();
        }

        public static Sprite[] GetPlayerAnimationFrames()
        {
            if (_playerFrames != null && _playerFrames.Length > 0)
            {
                return _playerFrames;
            }

            var sourceFrames = LoadSourceFrames(PlayerDescriptor.ResourcePath, PlayerDescriptor.AssetPath);
            if (sourceFrames.Length == 0)
            {
                _playerFrames = new[] { GetSquareSprite() };
                return _playerFrames;
            }

            _playerFrames = sourceFrames;
            return _playerFrames;
        }

        public static Sprite GetWeaponFire1Sprite()
        {
            var frames = GetWeaponFire1AnimationFrames();
            return frames.Length > 0 ? frames[0] : GetSquareSprite();
        }

        public static Sprite[] GetWeaponFire1AnimationFrames()
        {
            if (_weaponFire1Frames != null && _weaponFire1Frames.Length > 0)
            {
                return _weaponFire1Frames;
            }

            var sourceFrames = LoadSourceFrames(Fire1Descriptor.ResourcePath, Fire1Descriptor.AssetPath);
            if (sourceFrames.Length == 0)
            {
                _weaponFire1Frames = new[] { GetSquareSprite() };
                return _weaponFire1Frames;
            }

            var centeredFrames = new Sprite[sourceFrames.Length];
            for (var i = 0; i < sourceFrames.Length; i++)
            {
                centeredFrames[i] = CreateCenteredPivotSprite(sourceFrames[i]) ?? sourceFrames[i];
            }

            _weaponFire1Frames = centeredFrames;
            return _weaponFire1Frames;
        }

        public static Sprite[] GetSexySwordAnimationFrames()
        {
            if (_sexySwordFrames != null && _sexySwordFrames.Length > 0)
            {
                return _sexySwordFrames;
            }

            var sourceFrames = LoadSourceFrames(SexySwordDescriptor.ResourcePath, SexySwordDescriptor.AssetPath);
            if (sourceFrames.Length <= 0)
            {
                _sexySwordFrames = Array.Empty<Sprite>();
                return _sexySwordFrames;
            }

            _sexySwordFrames = sourceFrames;
            return _sexySwordFrames;
        }

        public static Sprite[] GetSexySwordAttackAnimationFrames()
        {
            if (_sexySwordAttackFrames != null && _sexySwordAttackFrames.Length > 0)
            {
                return _sexySwordAttackFrames;
            }

            var sourceFrames = GetSexySwordAnimationFrames();
            _sexySwordAttackFrames = SliceFramesInclusive(
                sourceFrames,
                SexySwordAttackStartFrame,
                SexySwordAttackEndFrame);
            return _sexySwordAttackFrames;
        }

        public static Sprite[] GetSexyFireAnimationFrames()
        {
            if (_sexyFireFrames != null && _sexyFireFrames.Length > 0)
            {
                return _sexyFireFrames;
            }

            var sourceFrames = LoadSourceFrames(SexyFireDescriptor.ResourcePath, SexyFireDescriptor.AssetPath);
            _sexyFireFrames = sourceFrames.Length > 0 ? sourceFrames : Array.Empty<Sprite>();
            return _sexyFireFrames;
        }

        public static Sprite[] GetSexyFireStackAnimationFrames()
        {
            if (_sexyFireStackFrames != null && _sexyFireStackFrames.Length > 0)
            {
                return _sexyFireStackFrames;
            }

            var fireFrames = GetSexyFireAnimationFrames();
            _sexyFireStackFrames = SliceFramesInclusive(
                fireFrames,
                SexyFireStackStartFrame,
                SexyFireStackEndFrame);
            return _sexyFireStackFrames;
        }

        public static Sprite[] GetSexyFireBoomAnimationFrames()
        {
            if (_sexyFireBoomFrames != null && _sexyFireBoomFrames.Length > 0)
            {
                return _sexyFireBoomFrames;
            }

            var fireFrames = GetSexyFireAnimationFrames();
            _sexyFireBoomFrames = SliceFramesInclusive(
                fireFrames,
                SexyFireBoomStartFrame,
                SexyFireBoomEndFrame);
            return _sexyFireBoomFrames;
        }

        public static Sprite[] GetSexyDroneAnimationFrames()
        {
            if (_sexyDroneFrames != null && _sexyDroneFrames.Length > 0)
            {
                return _sexyDroneFrames;
            }

            var sourceFrames = LoadSourceFrames(SexyDroneDescriptor.ResourcePath, SexyDroneDescriptor.AssetPath);
            _sexyDroneFrames = sourceFrames.Length > 0 ? sourceFrames : Array.Empty<Sprite>();
            return _sexyDroneFrames;
        }

        public static Sprite GetEnemySprite(EnemyVisualKind kind)
        {
            var frames = GetEnemyAnimationFrames(kind);
            return frames.Length > 0 ? frames[0] : GetSquareSprite();
        }

        public static Sprite[] GetEnemyAnimationFrames(EnemyVisualKind kind)
        {
            if (EnemyFramesByKind.TryGetValue(kind, out var cachedFrames) && cachedFrames != null && cachedFrames.Length > 0)
            {
                return cachedFrames;
            }

            var descriptor = GetDescriptor(kind);
            var sourceFrames = LoadSourceFrames(descriptor.ResourcePath, descriptor.AssetPath);
            if (sourceFrames.Length == 0)
            {
                var fallback = new[] { GetSquareSprite() };
                EnemyFramesByKind[kind] = fallback;
                return fallback;
            }

            var cleanedFrames = new Sprite[sourceFrames.Length];
            for (var i = 0; i < sourceFrames.Length; i++)
            {
                cleanedFrames[i] = CreateCleanedSprite(sourceFrames[i]) ?? sourceFrames[i];
            }

            EnemyFramesByKind[kind] = cleanedFrames;
            return cleanedFrames;
        }

        private static VisualAssetDescriptor GetDescriptor(EnemyVisualKind kind)
        {
            return kind switch
            {
                EnemyVisualKind.Mushroom => MushroomDescriptor,
                EnemyVisualKind.Skeleton => SkeletonDescriptor,
                EnemyVisualKind.Boss => BossDescriptor,
                _ => SlimeDescriptor,
            };
        }

        private static Sprite[] LoadSourceFrames(string resourcePath, string assetPath)
        {
            var resourceSprites = Resources.LoadAll<Sprite>(resourcePath);
            if (resourceSprites != null && resourceSprites.Length > 0)
            {
                SortFrames(resourceSprites);
                return resourceSprites;
            }

#if UNITY_EDITOR
            var editorAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var spriteCount = 0;
            for (var i = 0; i < editorAssets.Length; i++)
            {
                if (editorAssets[i] is Sprite)
                {
                    spriteCount++;
                }
            }

            if (spriteCount <= 0)
            {
                return Array.Empty<Sprite>();
            }

            var frames = new Sprite[spriteCount];
            var index = 0;
            for (var i = 0; i < editorAssets.Length; i++)
            {
                if (editorAssets[i] is not Sprite sprite)
                {
                    continue;
                }

                frames[index++] = sprite;
            }

            SortFrames(frames);
            return frames;
#else
            return Array.Empty<Sprite>();
#endif
        }

        private static void SortFrames(Sprite[] frames)
        {
            Array.Sort(frames, CompareByFrameOrder);
        }

        private static Sprite[] SliceFramesInclusive(Sprite[] sourceFrames, int startFrame, int endFrame)
        {
            if (sourceFrames == null || sourceFrames.Length <= 0)
            {
                return Array.Empty<Sprite>();
            }

            var safeStart = Mathf.Clamp(Mathf.Min(startFrame, endFrame), 0, sourceFrames.Length - 1);
            var safeEnd = Mathf.Clamp(Mathf.Max(startFrame, endFrame), 0, sourceFrames.Length - 1);
            return SliceFrames(sourceFrames, safeStart, (safeEnd - safeStart) + 1);
        }

        private static Sprite[] SliceFrames(Sprite[] sourceFrames, int start, int count)
        {
            if (sourceFrames == null || sourceFrames.Length <= 0 || count <= 0 || start >= sourceFrames.Length)
            {
                return Array.Empty<Sprite>();
            }

            var clampedStart = Mathf.Clamp(start, 0, sourceFrames.Length - 1);
            var maxCount = sourceFrames.Length - clampedStart;
            var clampedCount = Mathf.Clamp(count, 0, maxCount);
            if (clampedCount <= 0)
            {
                return Array.Empty<Sprite>();
            }

            var sliced = new Sprite[clampedCount];
            Array.Copy(sourceFrames, clampedStart, sliced, 0, clampedCount);
            return sliced;
        }

        private static int CompareByFrameOrder(Sprite left, Sprite right)
        {
            var leftIndex = ExtractFrameIndex(left != null ? left.name : string.Empty);
            var rightIndex = ExtractFrameIndex(right != null ? right.name : string.Empty);
            if (leftIndex != rightIndex)
            {
                return leftIndex.CompareTo(rightIndex);
            }

            var leftName = left != null ? left.name : string.Empty;
            var rightName = right != null ? right.name : string.Empty;
            return string.CompareOrdinal(leftName, rightName);
        }

        private static int ExtractFrameIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return int.MaxValue;
            }

            if (name.StartsWith(FramePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var numberPart = name.Substring(FramePrefix.Length);
                if (int.TryParse(numberPart, out var directIndex))
                {
                    return directIndex;
                }
            }

            for (var start = name.Length - 1; start >= 0; start--)
            {
                if (!char.IsDigit(name[start]))
                {
                    continue;
                }

                var end = start;
                while (start >= 0 && char.IsDigit(name[start]))
                {
                    start--;
                }

                var length = end - start;
                var number = name.Substring(start + 1, length);
                if (int.TryParse(number, out var parsed))
                {
                    return parsed;
                }

                break;
            }

            return int.MaxValue - 1;
        }

        private static Sprite CreateCleanedSprite(Sprite sourceSprite)
        {
            var readableTexture = ExtractSpriteTexture(sourceSprite);
            if (readableTexture == null)
            {
                return null;
            }

            var pixels = readableTexture.GetPixels32();
            if (pixels == null || pixels.Length == 0)
            {
                return null;
            }

            var backgroundKey = EstimateBackgroundColor(pixels, readableTexture.width, readableTexture.height);
            var useColorKey = backgroundKey.a > AlphaCutoff;

            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (pixel.a <= AlphaCutoff || (useColorKey && IsNearColor(pixel, backgroundKey, KeyTolerance)))
                {
                    pixel.a = 0;
                    pixels[i] = pixel;
                }
            }

            readableTexture.SetPixels32(pixels);
            readableTexture.Apply();

            if (!TryGetOpaqueBounds(pixels, readableTexture.width, readableTexture.height, out var minX, out var minY, out var maxX, out var maxY))
            {
                return null;
            }

            const int padding = 1;
            minX = Mathf.Max(0, minX - padding);
            minY = Mathf.Max(0, minY - padding);
            maxX = Mathf.Min(readableTexture.width - 1, maxX + padding);
            maxY = Mathf.Min(readableTexture.height - 1, maxY + padding);

            var croppedWidth = maxX - minX + 1;
            var croppedHeight = maxY - minY + 1;
            var croppedPixels = new Color32[croppedWidth * croppedHeight];
            for (var y = 0; y < croppedHeight; y++)
            {
                var sourceY = minY + y;
                var sourceStart = sourceY * readableTexture.width + minX;
                var targetStart = y * croppedWidth;
                for (var x = 0; x < croppedWidth; x++)
                {
                    croppedPixels[targetStart + x] = pixels[sourceStart + x];
                }
            }

            var croppedTexture = new Texture2D(croppedWidth, croppedHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            croppedTexture.SetPixels32(croppedPixels);
            croppedTexture.Apply();

            var cleanedSprite = Sprite.Create(
                croppedTexture,
                new Rect(0f, 0f, croppedWidth, croppedHeight),
                new Vector2(0.5f, 0.5f),
                sourceSprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            cleanedSprite.name = $"{sourceSprite.name}_RuntimeClean";
            return cleanedSprite;
        }

        private static Texture2D ExtractSpriteTexture(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return null;
            }

            var sourceTexture = sprite.texture;
            var sourceRect = sprite.rect;
            var width = Mathf.Max(1, Mathf.RoundToInt(sourceRect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(sourceRect.height));
            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            var previousFilterMode = sourceTexture.filterMode;

            try
            {
                sourceTexture.filterMode = FilterMode.Point;
                var uvScale = new Vector2(sourceRect.width / sourceTexture.width, sourceRect.height / sourceTexture.height);
                var uvOffset = new Vector2(sourceRect.x / sourceTexture.width, sourceRect.y / sourceTexture.height);
                Graphics.Blit(sourceTexture, renderTexture, uvScale, uvOffset);

                RenderTexture.active = renderTexture;
                var readableTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                readableTexture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                readableTexture.Apply();
                return readableTexture;
            }
            finally
            {
                sourceTexture.filterMode = previousFilterMode;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static Sprite CreateCenteredPivotSprite(Sprite sourceSprite)
        {
            if (sourceSprite == null)
            {
                return null;
            }

            var readableTexture = ExtractSpriteTexture(sourceSprite);
            if (readableTexture == null)
            {
                return null;
            }

            var width = readableTexture.width;
            var height = readableTexture.height;
            var centeredSprite = Sprite.Create(
                readableTexture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                sourceSprite.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            centeredSprite.name = $"{sourceSprite.name}_Centered";
            return centeredSprite;
        }

        private static Color32 EstimateBackgroundColor(Color32[] pixels, int width, int height)
        {
            var bottomLeft = pixels[0];
            var bottomRight = pixels[Mathf.Max(0, width - 1)];
            var topLeft = pixels[Mathf.Max(0, (height - 1) * width)];
            var topRight = pixels[Mathf.Max(0, height * width - 1)];

            return new Color32(
                (byte)((bottomLeft.r + bottomRight.r + topLeft.r + topRight.r) / 4),
                (byte)((bottomLeft.g + bottomRight.g + topLeft.g + topRight.g) / 4),
                (byte)((bottomLeft.b + bottomRight.b + topLeft.b + topRight.b) / 4),
                (byte)((bottomLeft.a + bottomRight.a + topLeft.a + topRight.a) / 4));
        }

        private static bool IsNearColor(Color32 left, Color32 right, int tolerance)
        {
            return Mathf.Abs(left.r - right.r) <= tolerance
                   && Mathf.Abs(left.g - right.g) <= tolerance
                   && Mathf.Abs(left.b - right.b) <= tolerance;
        }

        private static bool TryGetOpaqueBounds(
            Color32[] pixels,
            int width,
            int height,
            out int minX,
            out int minY,
            out int maxX,
            out int maxY)
        {
            minX = width;
            minY = height;
            maxX = -1;
            maxY = -1;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a <= 0)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            return maxX >= minX && maxY >= minY;
        }
    }
}
