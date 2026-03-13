using EJR.Game.Multiplayer;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EJR.Game.UI
{
    [DisallowMultipleComponent]
    public sealed class TitleMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "SampleScene";

        private const string FullscreenPreferenceKey = "settings.fullscreen";
        private const int DefaultWindowWidth = 1600;
        private const int DefaultWindowHeight = 900;
        private const float ButtonWidth = 320f;
        private const float ButtonHeight = 58f;
        private const float ButtonSpacing = 18f;

        private Font _font;
        private GameObject _mainMenuPanel;
        private GameObject _multiplayerPanel;
        private GameObject _optionsPanel;
        private Text _statusText;
        private Button _singlePlayButton;
        private Button _multiPlayButton;
        private Button _optionsButton;
        private Button _hostButton;
        private Button _joinButton;
        private Button _backButton;
        private Button _optionsBackButton;
        private InputField _joinCodeInput;
        private Toggle _fullscreenToggle;
        private bool _suppressDisplayToggleCallback;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            InitializeDisplaySettings();
            EnsureCamera();
            EnsureEventSystem();
            BuildMenu();
        }

        private void OnEnable()
        {
            MultiplayerSessionController.StatusChanged += HandleStatusChanged;
        }

        private void OnDisable()
        {
            MultiplayerSessionController.StatusChanged -= HandleStatusChanged;
        }

        private void Start()
        {
            SyncFullscreenToggle();
            ShowMainMenu();
            UpdateMultiplayerInteractivity();

            if (MultiplayerSessionController.TryConsumePendingStatus(out var pendingStatus))
            {
                SetStatus(pendingStatus);
            }
            else
            {
                SetStatus("Select a mode.");
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
            mainCamera.backgroundColor = new Color(0.05f, 0.07f, 0.11f, 1f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            eventSystem.sendNavigationEvents = true;

#if ENABLE_INPUT_SYSTEM
            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                standalone.enabled = false;
            }
#else
            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone == null)
            {
                standalone = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif

            eventSystem.UpdateModules();
        }

        private void BuildMenu()
        {
            if (FindFirstObjectByType<Canvas>() != null)
            {
                return;
            }

            var canvasObject = new GameObject("TitleCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            CreateFullscreenPanel(canvas.transform, "Backdrop", new Color(0.06f, 0.08f, 0.13f, 1f));

            var accent = CreatePanel(
                canvas.transform,
                "AccentBar",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 96f),
                new Vector2(420f, 6f),
                new Color(0.96f, 0.74f, 0.18f, 1f));
            accent.GetComponent<Image>().raycastTarget = false;

            var titleText = CreateText(
                canvas.transform,
                "TitleText",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 170f),
                new Vector2(760f, 70f),
                "\uC804\uC790\uC624\uB77D\uC6D0\uC815\uB300",
                34,
                FontStyle.Bold);
            titleText.color = new Color(0.95f, 0.97f, 1f, 1f);

            var subtitleText = CreateText(
                canvas.transform,
                "SubtitleText",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 132f),
                new Vector2(640f, 28f),
                "\uD0C0\uC774\uD2C0 \uD654\uBA74",
                18,
                FontStyle.Normal);
            subtitleText.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            _mainMenuPanel = new GameObject("MainMenuPanel");
            _mainMenuPanel.transform.SetParent(canvas.transform, false);

            _singlePlayButton = CreateButton(
                _mainMenuPanel.transform,
                "SinglePlayButton",
                new Vector2(0f, 32f),
                "\uC2F1\uAE00 \uD50C\uB808\uC774",
                OnSinglePlayClicked);

            _multiPlayButton = CreateButton(
                _mainMenuPanel.transform,
                "MultiPlayButton",
                new Vector2(0f, 32f - (ButtonHeight + ButtonSpacing)),
                "\uBA40\uD2F0 \uD50C\uB808\uC774",
                OnMultiPlayClicked);

            _optionsButton = CreateButton(
                _mainMenuPanel.transform,
                "OptionsButton",
                new Vector2(0f, 32f - ((ButtonHeight + ButtonSpacing) * 2f)),
                "\uC635\uC158",
                OnOptionsClicked);

            CreateButton(
                _mainMenuPanel.transform,
                "QuitButton",
                new Vector2(0f, 32f - ((ButtonHeight + ButtonSpacing) * 3f)),
                "\uAC8C\uC784 \uC885\uB8CC",
                OnQuitClicked);

            _multiplayerPanel = CreatePanel(
                canvas.transform,
                "MultiplayerPanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -12f),
                new Vector2(460f, 340f),
                new Color(0f, 0f, 0f, 0.55f));
            _multiplayerPanel.SetActive(false);

            var multiplayerTitle = CreateText(
                _multiplayerPanel.transform,
                "MultiplayerTitle",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -30f),
                new Vector2(320f, 30f),
                "MULTIPLAYER",
                22,
                FontStyle.Bold);
            multiplayerTitle.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var descriptionText = CreateText(
                _multiplayerPanel.transform,
                "MultiplayerDescription",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -70f),
                new Vector2(360f, 44f),
                "HOST creates a Relay room.\nJOIN uses the shared code.",
                16,
                FontStyle.Normal);
            descriptionText.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            descriptionText.alignment = TextAnchor.MiddleCenter;

            var codeLabel = CreateText(
                _multiplayerPanel.transform,
                "JoinCodeLabel",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-108f, -142f),
                new Vector2(200f, 24f),
                "JOIN CODE",
                16,
                FontStyle.Bold);
            codeLabel.alignment = TextAnchor.MiddleLeft;

            _joinCodeInput = CreateInputField(
                _multiplayerPanel.transform,
                "JoinCodeInput",
                new Vector2(0f, -176f),
                new Vector2(320f, 44f),
                string.Empty,
                "AB12CD");

            var helpText = CreateText(
                _multiplayerPanel.transform,
                "HelpText",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -224f),
                new Vector2(340f, 32f),
                "The host join code appears in the multiplayer HUD.",
                14,
                FontStyle.Normal);
            helpText.color = new Color(0.70f, 0.77f, 0.86f, 1f);

            _hostButton = CreateButton(
                _multiplayerPanel.transform,
                "HostButton",
                new Vector2(-88f, -278f),
                "HOST",
                OnHostClicked,
                new Vector2(144f, 50f));

            _joinButton = CreateButton(
                _multiplayerPanel.transform,
                "JoinButton",
                new Vector2(88f, -278f),
                "JOIN",
                OnJoinClicked,
                new Vector2(144f, 50f));

            _backButton = CreateButton(
                _multiplayerPanel.transform,
                "BackButton",
                new Vector2(0f, -342f),
                "BACK",
                ShowMainMenu,
                new Vector2(220f, 44f));

            _optionsPanel = CreatePanel(
                canvas.transform,
                "OptionsPanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -8f),
                new Vector2(460f, 250f),
                new Color(0f, 0f, 0f, 0.55f));
            _optionsPanel.SetActive(false);

            var optionsTitle = CreateText(
                _optionsPanel.transform,
                "OptionsTitle",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -30f),
                new Vector2(320f, 30f),
                "OPTIONS",
                22,
                FontStyle.Bold);
            optionsTitle.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var optionsDescription = CreateText(
                _optionsPanel.transform,
                "OptionsDescription",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -72f),
                new Vector2(340f, 42f),
                "\uAE30\uBCF8 \uAC12\uC740 \uCC3D\uBAA8\uB4DC\uC785\uB2C8\uB2E4.\n\uCCB4\uD06C\uD558\uBA74 \uC804\uCCB4\uD654\uBA74\uC73C\uB85C \uBCC0\uACBD\uB429\uB2C8\uB2E4.",
                15,
                FontStyle.Normal);
            optionsDescription.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            optionsDescription.alignment = TextAnchor.MiddleCenter;

            _fullscreenToggle = CreateToggle(
                _optionsPanel.transform,
                "FullscreenToggle",
                new Vector2(0f, -140f),
                new Vector2(280f, 36f),
                "\uC804\uCCB4\uD654\uBA74",
                OnFullscreenToggleChanged);

            _optionsBackButton = CreateButton(
                _optionsPanel.transform,
                "OptionsBackButton",
                new Vector2(0f, -192f),
                "BACK",
                ShowMainMenu,
                new Vector2(220f, 44f));

            _statusText = CreateText(
                canvas.transform,
                "StatusText",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -244f),
                new Vector2(760f, 54f),
                string.Empty,
                15,
                FontStyle.Normal);
            _statusText.color = new Color(0.9f, 0.82f, 0.54f, 1f);
        }

        private void OnSinglePlayClicked()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                SetStatus("Missing gameplay scene name.");
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void OnMultiPlayClicked()
        {
            _mainMenuPanel.SetActive(false);
            if (_optionsPanel != null)
            {
                _optionsPanel.SetActive(false);
            }

            _multiplayerPanel.SetActive(true);
            UpdateMultiplayerInteractivity();
            SetStatus("Create a Relay session or join with a code.");

            var eventSystem = EventSystem.current;
            if (eventSystem != null && _hostButton != null)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_hostButton.gameObject);
            }
        }

        private void OnOptionsClicked()
        {
            _mainMenuPanel.SetActive(false);
            if (_multiplayerPanel != null)
            {
                _multiplayerPanel.SetActive(false);
            }

            if (_optionsPanel != null)
            {
                _optionsPanel.SetActive(true);
            }

            SyncFullscreenToggle();
            SetStatus("Display option updated here.");

            var eventSystem = EventSystem.current;
            if (eventSystem != null && _fullscreenToggle != null)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_fullscreenToggle.gameObject);
            }
        }

        private async void OnHostClicked()
        {
            var session = MultiplayerSessionController.EnsureInstance();
            if (session.IsBusy)
            {
                return;
            }

            UpdateMultiplayerInteractivity();
            await session.StartHostAsync();

            if (!this)
            {
                return;
            }

            UpdateMultiplayerInteractivity();
            SetStatus(session.CurrentStatus);
        }

        private async void OnJoinClicked()
        {
            var joinCode = _joinCodeInput != null ? _joinCodeInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                SetStatus("Enter a join code.");
                return;
            }

            var session = MultiplayerSessionController.EnsureInstance();
            if (session.IsBusy)
            {
                return;
            }

            UpdateMultiplayerInteractivity();
            await session.JoinByCodeAsync(joinCode);

            if (!this)
            {
                return;
            }

            UpdateMultiplayerInteractivity();
            SetStatus(session.CurrentStatus);
        }

        private void ShowMainMenu()
        {
            if (_mainMenuPanel != null)
            {
                _mainMenuPanel.SetActive(true);
            }

            if (_multiplayerPanel != null)
            {
                _multiplayerPanel.SetActive(false);
            }

            if (_optionsPanel != null)
            {
                _optionsPanel.SetActive(false);
            }

            UpdateMultiplayerInteractivity();

            var eventSystem = EventSystem.current;
            if (eventSystem != null && _singlePlayButton != null)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_singlePlayButton.gameObject);
            }
        }

        private void UpdateMultiplayerInteractivity()
        {
            var session = MultiplayerSessionController.EnsureInstance();
            var interactable = !session.IsBusy;

            if (_singlePlayButton != null)
            {
                _singlePlayButton.interactable = interactable;
            }

            if (_multiPlayButton != null)
            {
                _multiPlayButton.interactable = interactable;
            }

            if (_optionsButton != null)
            {
                _optionsButton.interactable = interactable;
            }

            if (_hostButton != null)
            {
                _hostButton.interactable = interactable;
            }

            if (_joinButton != null)
            {
                _joinButton.interactable = interactable;
            }

            if (_backButton != null)
            {
                _backButton.interactable = interactable;
            }

            if (_optionsBackButton != null)
            {
                _optionsBackButton.interactable = interactable;
            }

            if (_joinCodeInput != null)
            {
                _joinCodeInput.interactable = interactable;
            }

            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.interactable = interactable;
            }
        }

        private static void OnQuitClicked()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleStatusChanged(string message)
        {
            SetStatus(message);
            UpdateMultiplayerInteractivity();
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message ?? string.Empty;
            }
        }

        private void InitializeDisplaySettings()
        {
            var hasStoredValue = PlayerPrefs.HasKey(FullscreenPreferenceKey);
            var useFullscreen = hasStoredValue && PlayerPrefs.GetInt(FullscreenPreferenceKey, 0) != 0;
            if (!hasStoredValue)
            {
                PlayerPrefs.SetInt(FullscreenPreferenceKey, 0);
                PlayerPrefs.Save();
            }

            ApplyDisplayMode(useFullscreen, persist: false);
        }

        private void ApplyDisplayMode(bool useFullscreen, bool persist)
        {
            if (persist)
            {
                PlayerPrefs.SetInt(FullscreenPreferenceKey, useFullscreen ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (useFullscreen)
            {
                var resolution = Screen.currentResolution;
                var fullscreenWidth = Mathf.Max(1, resolution.width);
                var fullscreenHeight = Mathf.Max(1, resolution.height);
                Screen.SetResolution(fullscreenWidth, fullscreenHeight, FullScreenMode.FullScreenWindow);
            }
            else
            {
                var resolution = Screen.currentResolution;
                var windowWidth = Mathf.Clamp(DefaultWindowWidth, 960, Mathf.Max(960, resolution.width));
                var windowHeight = Mathf.Clamp(DefaultWindowHeight, 540, Mathf.Max(540, resolution.height));
                Screen.SetResolution(windowWidth, windowHeight, FullScreenMode.Windowed);
            }

            SyncFullscreenToggle();
        }

        private void SyncFullscreenToggle()
        {
            if (_fullscreenToggle == null)
            {
                return;
            }

            _suppressDisplayToggleCallback = true;
            _fullscreenToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(FullscreenPreferenceKey, 0) != 0);
            _suppressDisplayToggleCallback = false;
        }

        private void OnFullscreenToggleChanged(bool useFullscreen)
        {
            if (_suppressDisplayToggleCallback)
            {
                return;
            }

            ApplyDisplayMode(useFullscreen, persist: true);
            SetStatus(useFullscreen ? "Display mode: Fullscreen." : "Display mode: Windowed.");
        }

        private GameObject CreateFullscreenPanel(Transform parent, string name, Color color)
        {
            return CreatePanel(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, color);
        }

        private GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = panel.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        private Text CreateText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            string content,
            int fontSize,
            FontStyle fontStyle)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = textObject.AddComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(Transform parent, string name, Vector2 anchoredPosition, string label, UnityEngine.Events.UnityAction onClick)
        {
            return CreateButton(parent, name, anchoredPosition, label, onClick, new Vector2(ButtonWidth, ButtonHeight));
        }

        private Button CreateButton(Transform parent, string name, Vector2 anchoredPosition, string label, UnityEngine.Events.UnityAction onClick, Vector2 size)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.2f, 0.29f, 0.96f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.22f, 0.28f, 0.39f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.12f, 0.15f, 0.22f, 1f);
            colors.disabledColor = new Color(0.18f, 0.18f, 0.18f, 0.7f);
            button.colors = colors;

            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.74f, 0.18f, 0.28f);
            outline.effectDistance = new Vector2(1f, -1f);

            var labelText = CreateText(
                buttonObject.transform,
                "Label",
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                label,
                20,
                FontStyle.Bold);
            labelText.color = new Color(0.97f, 0.98f, 1f, 1f);

            return button;
        }

        private InputField CreateInputField(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            string initialText,
            string placeholderText)
        {
            var inputObject = new GameObject(name);
            inputObject.transform.SetParent(parent, false);

            var rect = inputObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = inputObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

            var outline = inputObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.95f, 0.74f, 0.18f, 0.18f);
            outline.effectDistance = new Vector2(1f, -1f);

            var inputField = inputObject.AddComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.contentType = InputField.ContentType.Standard;
            inputField.lineType = InputField.LineType.SingleLine;

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(inputObject.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 6f);
            textRect.offsetMax = new Vector2(-14f, -6f);

            var text = textObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.supportRichText = false;

            var placeholderObject = new GameObject("Placeholder");
            placeholderObject.transform.SetParent(inputObject.transform, false);
            var placeholderRect = placeholderObject.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(14f, 6f);
            placeholderRect.offsetMax = new Vector2(-14f, -6f);

            var placeholder = placeholderObject.AddComponent<Text>();
            placeholder.font = _font;
            placeholder.fontSize = 18;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(1f, 1f, 1f, 0.34f);
            placeholder.text = placeholderText;
            placeholder.raycastTarget = false;

            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.text = initialText;
            return inputField;
        }

        private Toggle CreateToggle(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            string label,
            UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            var toggleObject = new GameObject(name);
            toggleObject.transform.SetParent(parent, false);

            var rect = toggleObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var toggle = toggleObject.AddComponent<Toggle>();

            var backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(toggleObject.transform, false);
            var backgroundRect = backgroundObject.AddComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0f, 0.5f);
            backgroundRect.pivot = new Vector2(0f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(0f, 0f);
            backgroundRect.sizeDelta = new Vector2(28f, 28f);

            var backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            backgroundObject.AddComponent<Outline>().effectColor = new Color(0.95f, 0.74f, 0.18f, 0.22f);

            var checkmarkObject = new GameObject("Checkmark");
            checkmarkObject.transform.SetParent(backgroundObject.transform, false);
            var checkmarkRect = checkmarkObject.AddComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchoredPosition = Vector2.zero;
            checkmarkRect.sizeDelta = new Vector2(16f, 16f);

            var checkmarkImage = checkmarkObject.AddComponent<Image>();
            checkmarkImage.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(toggleObject.transform, false);
            var labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.offsetMin = new Vector2(42f, 0f);
            labelRect.offsetMax = new Vector2(0f, 0f);

            var labelText = labelObject.AddComponent<Text>();
            labelText.font = _font;
            labelText.text = label;
            labelText.fontSize = 18;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = new Color(0.97f, 0.98f, 1f, 1f);
            labelText.raycastTarget = false;

            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.isOn = false;

            var colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 0.88f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            toggle.colors = colors;

            if (onValueChanged != null)
            {
                toggle.onValueChanged.AddListener(onValueChanged);
            }

            return toggle;
        }
    }
}
