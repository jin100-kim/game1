using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace EJR.Game.Gameplay
{
    public static class CombatTextSpawner
    {
        private readonly struct PopupClusterState
        {
            public PopupClusterState(float lastSpawnTime, int nextSlot)
            {
                LastSpawnTime = lastSpawnTime;
                NextSlot = nextSlot;
            }

            public float LastSpawnTime { get; }
            public int NextSlot { get; }
        }

        public static readonly Color EnemyDamagedColor = new Color(0.72f, 0.96f, 1f, 1f);
        public static readonly Color PlayerDamagedColor = new Color(1f, 0.35f, 0.35f, 1f);
        public static readonly Color PlayerHealedColor = new Color(0.35f, 1f, 0.48f, 1f);
        public static readonly Color LightBonusColor = new Color(1f, 0.92f, 0.35f, 1f);

        private const int PopupPoolPrewarmCount = 40;
        private const float PopupLifetime = 0.65f;
        private const float PopupRiseSpeed = 1.35f;
        private const float ClusterReuseWindow = 0.18f;
        private const float ClusterCellSize = 0.55f;

        private static readonly Vector3[] PopupOffsets =
        {
            new(-0.22f, 0.00f, 0f),
            new(0.22f, 0.02f, 0f),
            new(-0.12f, 0.16f, 0f),
            new(0.12f, 0.18f, 0f),
            new(-0.30f, 0.10f, 0f),
            new(0.30f, 0.12f, 0f),
            new(0f, 0.26f, 0f),
            new(-0.18f, 0.28f, 0f),
            new(0.18f, 0.30f, 0f),
        };

        private static readonly Queue<DamageNumberPopup> PopupPool = new();
        private static readonly Dictionary<Vector2Int, PopupClusterState> PopupClusters = new();

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
            var motion = ReservePopupMotion(worldPosition);
            popup.Show(motion.position, motion.drift, damageValue.ToString("0.0", CultureInfo.InvariantCulture), color, PopupLifetime, PopupRiseSpeed);
        }

        public static void SpawnHealing(Vector3 worldPosition, float healValue)
        {
            var displayValue = Mathf.FloorToInt(healValue + 0.0001f);
            if (displayValue <= 0)
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
            var motion = ReservePopupMotion(worldPosition);
            popup.Show(motion.position, motion.drift, $"+{displayValue.ToString(CultureInfo.InvariantCulture)}", PlayerHealedColor, PopupLifetime, PopupRiseSpeed);
        }

        private static (Vector3 position, Vector3 drift) ReservePopupMotion(Vector3 worldPosition)
        {
            var now = Time.unscaledTime;
            CleanupClusterStates(now);

            var key = new Vector2Int(
                Mathf.RoundToInt(worldPosition.x / ClusterCellSize),
                Mathf.RoundToInt(worldPosition.y / ClusterCellSize));

            PopupClusterState state;
            var hasState = PopupClusters.TryGetValue(key, out state);
            var slot = 0;
            if (hasState && now - state.LastSpawnTime <= ClusterReuseWindow)
            {
                slot = state.NextSlot;
            }

            var nextSlot = (slot + 1) % PopupOffsets.Length;
            PopupClusters[key] = new PopupClusterState(now, nextSlot);

            var offset = PopupOffsets[slot];
            offset.x += Random.Range(-0.025f, 0.025f);
            offset.y += Random.Range(-0.02f, 0.02f);

            var horizontalDirection = Mathf.Approximately(offset.x, 0f)
                ? (slot % 2 == 0 ? -1f : 1f)
                : Mathf.Sign(offset.x);

            var drift = new Vector3(horizontalDirection * 0.16f, 0f, 0f);
            return (worldPosition + offset, drift);
        }

        private static void CleanupClusterStates(float now)
        {
            if (PopupClusters.Count <= 32)
            {
                return;
            }

            var staleKeys = ListPool<Vector2Int>.Get();
            foreach (var pair in PopupClusters)
            {
                if (now - pair.Value.LastSpawnTime > ClusterReuseWindow * 2f)
                {
                    staleKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < staleKeys.Count; i++)
            {
                PopupClusters.Remove(staleKeys[i]);
            }

            ListPool<Vector2Int>.Release(staleKeys);
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

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();

            public static List<T> Get()
            {
                if (Pool.Count > 0)
                {
                    return Pool.Pop();
                }

                return new List<T>();
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
