using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public static class ArenaVisualPresenter
    {
        private const float BorderThickness = 0.3f;

        public static void Apply(Rect bounds, Color backgroundColor, Color boundaryColor, Camera targetCamera = null)
        {
            var root = GameObject.Find("ArenaVisuals");
            if (root == null)
            {
                root = new GameObject("ArenaVisuals");
            }

            var squareSprite = RuntimeSpriteFactory.GetSquareSprite();
            if (squareSprite == null)
            {
                return;
            }

            var center = bounds.center;
            UpdateQuad(root.transform, squareSprite, "ArenaBackground", center, new Vector2(bounds.width, bounds.height), backgroundColor, -10);
            UpdateQuad(root.transform, squareSprite, "BorderTop", new Vector2(center.x, bounds.yMax), new Vector2(bounds.width + 0.5f, BorderThickness), boundaryColor, -9);
            UpdateQuad(root.transform, squareSprite, "BorderBottom", new Vector2(center.x, bounds.yMin), new Vector2(bounds.width + 0.5f, BorderThickness), boundaryColor, -9);
            UpdateQuad(root.transform, squareSprite, "BorderLeft", new Vector2(bounds.xMin, center.y), new Vector2(BorderThickness, bounds.height + 0.5f), boundaryColor, -9);
            UpdateQuad(root.transform, squareSprite, "BorderRight", new Vector2(bounds.xMax, center.y), new Vector2(BorderThickness, bounds.height + 0.5f), boundaryColor, -9);

            if (targetCamera != null)
            {
                targetCamera.backgroundColor = backgroundColor;
                targetCamera.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        private static void UpdateQuad(
            Transform parent,
            Sprite sprite,
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            int sortingOrder)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            child.position = new Vector3(position.x, position.y, 0f);
            child.localScale = CalculateScale(sprite, size);
        }

        private static Vector3 CalculateScale(Sprite sprite, Vector2 size)
        {
            var spriteSize = sprite != null ? sprite.bounds.size : Vector3.one;
            var width = spriteSize.x > 0.0001f ? size.x / spriteSize.x : size.x;
            var height = spriteSize.y > 0.0001f ? size.y / spriteSize.y : size.y;
            return new Vector3(Mathf.Max(0.01f, width), Mathf.Max(0.01f, height), 1f);
        }
    }
}
