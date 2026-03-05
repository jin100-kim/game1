using System.Collections.Generic;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public static class CombatTextSpawner
    {
        public static readonly Color EnemyDamagedColor = new Color(1f, 0.92f, 0.35f, 1f);
        public static readonly Color PlayerDamagedColor = new Color(1f, 0.35f, 0.35f, 1f);

        private const int PopupPoolPrewarmCount = 40;
        private const float PopupLifetime = 0.65f;
        private const float PopupRiseSpeed = 1.35f;

        private static readonly Queue<DamageNumberPopup> PopupPool = new();

        private static Font _font;
        private static bool _fontInitialized;
        private static Transform _poolRoot;
        private static bool _poolPrepared;

        public static void SpawnDamage(Vector3 worldPosition, float damageValue, Color color)
        {
            if (damageValue <= 0f)
            {
                return;
            }

            EnsurePoolPrepared();

            var popup = GetPopup();
            if (popup == null)
            {
                return;
            }

            popup.gameObject.SetActive(true);
            var jitteredPosition = worldPosition + new Vector3(Random.Range(-0.08f, 0.08f), 0f, 0f);
            popup.Show(jitteredPosition, Mathf.CeilToInt(damageValue).ToString(), color, PopupLifetime, PopupRiseSpeed);
        }

        private static void EnsurePoolPrepared()
        {
            if (_poolPrepared && _poolRoot != null)
            {
                return;
            }

            if (_poolRoot == null)
            {
                PopupPool.Clear();
            }

            _poolPrepared = true;
            EnsureFont();
            EnsurePoolRoot();

            for (var i = 0; i < PopupPoolPrewarmCount; i++)
            {
                var popup = CreatePopupInstance();
                ReturnPopupToPool(popup);
            }
        }

        private static void EnsurePoolRoot()
        {
            if (_poolRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("DamageTextPool");
            _poolRoot = rootObject.transform;
        }

        private static DamageNumberPopup GetPopup()
        {
            while (PopupPool.Count > 0)
            {
                var pooled = PopupPool.Dequeue();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            return CreatePopupInstance();
        }

        private static DamageNumberPopup CreatePopupInstance()
        {
            EnsurePoolRoot();

            var popupObject = new GameObject("DamageText");
            popupObject.transform.SetParent(_poolRoot, false);

            var textMesh = popupObject.AddComponent<TextMesh>();
            textMesh.text = string.Empty;
            textMesh.fontSize = 56;
            textMesh.characterSize = 0.055f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;

            EnsureFont();
            if (_font != null)
            {
                textMesh.font = _font;
                var renderer = textMesh.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _font.material;
                renderer.sortingOrder = 50;
            }

            var popup = popupObject.AddComponent<DamageNumberPopup>();
            popup.Initialize(textMesh, ReturnPopupToPool);
            popupObject.SetActive(false);
            return popup;
        }

        private static void ReturnPopupToPool(DamageNumberPopup popup)
        {
            if (popup == null)
            {
                return;
            }

            var popupObject = popup.gameObject;
            popupObject.SetActive(false);
            popupObject.transform.SetParent(_poolRoot, false);
            PopupPool.Enqueue(popup);
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
