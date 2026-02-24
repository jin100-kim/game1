using UnityEngine;

namespace EJR.Game.Core
{
    public static class RuntimeSpriteFactory
    {
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
    }
}
