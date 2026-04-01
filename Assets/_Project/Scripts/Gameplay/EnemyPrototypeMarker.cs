using EJR.Game.Core;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    [ExecuteAlways]
    public sealed class EnemyPrototypeMarker : MonoBehaviour
    {
        private const string VisualChildName = "Visual";

        [SerializeField] private string prototypeId = "enemy_prototype";
        [SerializeField] private string displayName = "Enemy Prototype";
        [SerializeField] private RuntimeSpriteFactory.EnemyVisualKind visualKind = RuntimeSpriteFactory.EnemyVisualKind.Slime;
        [SerializeField, Min(0.1f)] private float previewWorldSize = 0.95f;
        [SerializeField] private Color previewTint = Color.white;
        [SerializeField] private int sortingOrder = 0;

        public string PrototypeId => prototypeId;
        public string DisplayName => displayName;
        public RuntimeSpriteFactory.EnemyVisualKind VisualKind => visualKind;

        private void Reset()
        {
            RefreshPreview();
        }

        private void OnEnable()
        {
            RefreshPreview();
        }

        private void OnValidate()
        {
            RefreshPreview();
        }

        [ContextMenu("Refresh Preview")]
        public void RefreshPreview()
        {
            var visualRoot = transform.Find(VisualChildName);
            if (visualRoot == null)
            {
                visualRoot = new GameObject(VisualChildName).transform;
                visualRoot.SetParent(transform, false);
            }

            var renderer = visualRoot.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = RuntimeSpriteFactory.GetEnemySprite(visualKind);
            renderer.color = previewTint;
            renderer.sortingOrder = sortingOrder;
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = CalculateVisualScale(renderer.sprite, previewWorldSize);
        }

        private static Vector3 CalculateVisualScale(Sprite sprite, float desiredWorldSize)
        {
            var clampedSize = Mathf.Max(0.1f, desiredWorldSize);
            if (sprite == null)
            {
                return Vector3.one * clampedSize;
            }

            var spriteBounds = sprite.bounds.size;
            var spriteSize = Mathf.Max(spriteBounds.x, spriteBounds.y);
            if (spriteSize <= 0.0001f)
            {
                return Vector3.one * clampedSize;
            }

            var uniformScale = clampedSize / spriteSize;
            return new Vector3(uniformScale, uniformScale, 1f);
        }
    }
}
