using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerGameController : MonoBehaviour
    {
        [SerializeField] private Rect arenaBounds = new Rect(-12f, -7f, 24f, 14f);

        private Font _font;
        private Text _infoText;
        private float _nextRefreshAt;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Application.runInBackground = true;
            EnsureCamera();
            EnsureEventSystem();
            EnsureArenaVisuals();
            EnsureOverlay();
            EnsureSharedCoopRuntime();
            RefreshInfoText();
        }

        private void Update()
        {
            if (IsLeaveKeyPressed())
            {
                MultiplayerSessionController.EnsureInstance().LeaveSession();
                return;
            }

            if (Time.unscaledTime >= _nextRefreshAt)
            {
                _nextRefreshAt = Time.unscaledTime + 0.25f;
                RefreshInfoText();
            }
        }

        private void EnsureCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 6f;
            mainCamera.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 1f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private void EnsureArenaVisuals()
        {
            if (GameObject.Find("ArenaVisuals") != null)
            {
                return;
            }

            var root = new GameObject("ArenaVisuals");
            CreateQuad(root.transform, "ArenaBackground", Vector2.zero, new Vector2(arenaBounds.width, arenaBounds.height), new Color(0.10f, 0.12f, 0.18f, 1f), -10);
            CreateQuad(root.transform, "BorderTop", new Vector2(0f, arenaBounds.yMax), new Vector2(arenaBounds.width + 0.5f, 0.3f), new Color(0.92f, 0.74f, 0.18f, 1f), -9);
            CreateQuad(root.transform, "BorderBottom", new Vector2(0f, arenaBounds.yMin), new Vector2(arenaBounds.width + 0.5f, 0.3f), new Color(0.92f, 0.74f, 0.18f, 1f), -9);
            CreateQuad(root.transform, "BorderLeft", new Vector2(arenaBounds.xMin, 0f), new Vector2(0.3f, arenaBounds.height + 0.5f), new Color(0.92f, 0.74f, 0.18f, 1f), -9);
            CreateQuad(root.transform, "BorderRight", new Vector2(arenaBounds.xMax, 0f), new Vector2(0.3f, arenaBounds.height + 0.5f), new Color(0.92f, 0.74f, 0.18f, 1f), -9);
        }

        private void EnsureOverlay()
        {
            if (FindFirstObjectByType<Canvas>() != null)
            {
                return;
            }

            var canvasObject = new GameObject("MultiplayerHUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("InfoPanel");
            panel.transform.SetParent(canvas.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(18f, -18f);
            panelRect.sizeDelta = new Vector2(360f, 124f);

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.42f);

            var textObject = new GameObject("InfoText");
            textObject.transform.SetParent(panel.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 10f);
            textRect.offsetMax = new Vector2(-14f, -10f);

            _infoText = textObject.AddComponent<Text>();
            _infoText.font = _font;
            _infoText.fontSize = 18;
            _infoText.alignment = TextAnchor.UpperLeft;
            _infoText.color = Color.white;
            _infoText.raycastTarget = false;
        }

        private void RefreshInfoText()
        {
            if (_infoText == null)
            {
                return;
            }

            var session = MultiplayerSessionController.EnsureInstance();
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening)
            {
                _infoText.text = "NO ACTIVE SESSION\nESC : RETURN";
                return;
            }

            var mode = manager.IsHost ? "HOST" : "CLIENT";
            var players = session.SessionPlayerCount;
            var maxPlayers = session.SessionMaxPlayers;
            var sessionCode = string.IsNullOrWhiteSpace(session.SessionCode) ? "----" : session.SessionCode;
            var playerLabel = ShortenId(session.LocalPlayerId);
            var alivePlayers = MultiplayerPlayerCombatant.CountAlivePlayers();
            var enemyCount = MultiplayerSharedEnemyActor.CountSpawnedEnemies();

            _infoText.text =
                $"MULTI {mode}\n" +
                $"CODE {sessionCode}\n" +
                $"PLAYERS {players}/{maxPlayers}\n" +
                $"ALIVE {alivePlayers}/{players}\n" +
                $"ENEMIES {enemyCount}\n" +
                $"YOU {playerLabel}\n" +
                "ESC : RETURN";
        }

        private void EnsureSharedCoopRuntime()
        {
            if (FindFirstObjectByType<MultiplayerCoopController>() != null)
            {
                return;
            }

            var runtimeRoot = new GameObject("SharedCoopRuntime");
            runtimeRoot.AddComponent<MultiplayerCoopController>();
        }

        private static bool IsLeaveKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.Escape);
        }

        private static void CreateQuad(Transform parent, string name, Vector2 position, Vector2 size, Color color, int sortingOrder)
        {
            var quad = new GameObject(name);
            quad.transform.SetParent(parent, false);
            quad.transform.position = new Vector3(position.x, position.y, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = quad.AddComponent<SpriteRenderer>();
            renderer.sprite = EJR.Game.Core.RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static string ShortenId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "UNKNOWN";
            }

            return value.Length <= 10 ? value.ToUpperInvariant() : value.Substring(0, 10).ToUpperInvariant();
        }
    }
}
