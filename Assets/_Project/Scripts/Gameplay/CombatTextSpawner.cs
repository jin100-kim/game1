using UnityEngine;

namespace EJR.Game.Gameplay
{
    public static class CombatTextSpawner
    {
        public static readonly Color EnemyDamagedColor = new Color(1f, 0.92f, 0.35f, 1f);
        public static readonly Color PlayerDamagedColor = new Color(1f, 0.35f, 0.35f, 1f);

        private static Font _font;
        private static bool _fontInitialized;

        public static void SpawnDamage(Vector3 worldPosition, float damageValue, Color color)
        {
            if (damageValue <= 0f)
            {
                return;
            }

            var popupObject = new GameObject("DamageText");
            popupObject.transform.position = worldPosition + new Vector3(Random.Range(-0.08f, 0.08f), 0f, 0f);

            var textMesh = popupObject.AddComponent<TextMesh>();
            textMesh.text = Mathf.CeilToInt(damageValue).ToString();
            textMesh.fontSize = 56;
            textMesh.characterSize = 0.055f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = color;

            EnsureFont();

            if (_font != null)
            {
                textMesh.font = _font;
                var renderer = textMesh.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _font.material;
                renderer.sortingOrder = 50;
            }

            var popup = popupObject.AddComponent<DamageNumberPopup>();
            popup.Initialize(textMesh, 0.65f, 1.35f);
        }

        private static void EnsureFont()
        {
            if (_fontInitialized)
            {
                return;
            }

            _fontInitialized = true;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
