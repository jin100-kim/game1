using System.Collections.Generic;
using System.Text;
using EJR.Game.Core;
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

        private enum MetaTab
        {
            Unlocks,
            Research,
        }

        private Font _font;
        private Canvas _canvas;
        private GameObject _mainMenuPanel;
        private GameObject _multiplayerPanel;
        private GameObject _optionsPanel;
        private GameObject _runSetupPanel;
        private GameObject _runSetupCharacterOptionsRoot;
        private GameObject _runSetupWeaponOptionsRoot;
        private GameObject _metaPanel;
        private GameObject _metaContentRoot;
        private GameObject _summaryModal;
        private GameObject _accentBar;
        private Text _statusText;
        private Text _titleText;
        private Text _subtitleText;
        private Text _runSetupCharacterText;
        private Text _runSetupWeaponText;
        private Text _runSetupBonusText;
        private Text _metaHeaderText;
        private Text _metaRecentText;
        private Text _summaryModalText;
        private Text _metaUnlocksTabText;
        private Text _metaResearchTabText;
        private Button _singlePlayButton;
        private Button _multiPlayButton;
        private Button _metaButton;
        private Button _optionsButton;
        private Button _hostButton;
        private Button _joinButton;
        private Button _backButton;
        private Button _optionsBackButton;
        private Button _runSetupCharacterButton;
        private Button _runSetupWeaponButton;
        private Button _summaryMetaButton;
        private Button _metaUnlocksTabButton;
        private Button _metaResearchTabButton;
        private Button[] _runSetupCharacterOptionButtons = System.Array.Empty<Button>();
        private Button[] _runSetupWeaponOptionButtons = System.Array.Empty<Button>();
        private InputField _joinCodeInput;
        private Toggle _fullscreenToggle;
        private bool _suppressDisplayToggleCallback;
        private int _selectedCharacterId;
        private WeaponUpgradeId _selectedStarterWeaponId;
        private string _recentRunSummaryText = "No recent run yet.";
        private MetaTab _currentMetaTab;

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MetaProgressionService.EnsureLoaded();
            _selectedCharacterId = MetaProgressionService.GetSingleSelectedCharacterId();
            _selectedStarterWeaponId = MetaProgressionService.GetSingleSelectedStarterWeapon();
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
            if (MetaProgressionService.TryPeekPendingRunSummary(out var summary))
            {
                _recentRunSummaryText = summary.BuildDisplayText();
                MetaProgressionService.ClearPendingRunSummary();
            }

            RefreshRunSetupPanel();
            RefreshMetaPanel();
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
            var inputModule = GetOrAddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
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
            GetOrAddComponent<StandaloneInputModule>(eventSystem.gameObject);
#endif
            eventSystem.UpdateModules();
        }

        private void BuildMenu()
        {
            if (_canvas != null)
            {
                return;
            }

            var existingCanvas = FindFirstObjectByType<Canvas>();
            if (existingCanvas != null)
            {
                _canvas = existingCanvas;
            }
            else
            {
                var canvasObject = new GameObject("TitleCanvas");
                _canvas = canvasObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var scaler = GetOrAddComponent<CanvasScaler>(_canvas.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAddComponent<GraphicRaycaster>(_canvas.gameObject);

            var root = new GameObject("TitleUiRoot");
            root.transform.SetParent(_canvas.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            CreatePanel(root.transform, "Backdrop", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.06f, 0.08f, 0.13f, 1f));
            CreatePanel(root.transform, "BackdropBandLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(360f, 0f), new Color(0.09f, 0.12f, 0.18f, 0.72f));
            CreatePanel(root.transform, "BackdropBandBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 132f), new Color(0.03f, 0.05f, 0.08f, 0.86f));
            CreatePanel(root.transform, "BackdropAccentGlow", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(240f, 0f), new Color(0.22f, 0.18f, 0.06f, 0.18f));

            _accentBar = CreatePanel(root.transform, "AccentBar", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -88f), new Vector2(188f, 6f), new Color(0.96f, 0.74f, 0.18f, 1f));

            _titleText = CreateText(root.transform, "TitleText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -108f), new Vector2(640f, 70f), "ELECTRON EXPEDITION", 40, FontStyle.Bold);
            _titleText.alignment = TextAnchor.MiddleLeft;
            _titleText.color = new Color(0.95f, 0.97f, 1f, 1f);

            _subtitleText = CreateText(root.transform, "SubtitleText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -166f), new Vector2(560f, 42f), "Shared meta progression across solo and co-op runs.", 18, FontStyle.Normal);
            _subtitleText.alignment = TextAnchor.UpperLeft;
            _subtitleText.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            BuildMainMenuPanel(root.transform);
            BuildMultiplayerPanel(root.transform);
            BuildOptionsPanel(root.transform);
            BuildRunSetupPanel(root.transform);
            BuildMetaPanel(root.transform);
            BuildSummaryModal(root.transform);

            var statusPanel = CreatePanel(root.transform, "StatusPanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(980f, 48f), new Color(0.04f, 0.06f, 0.10f, 0.72f));
            _statusText = CreateText(statusPanel.transform, "StatusText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 0f), string.Empty, 15, FontStyle.Normal);
            _statusText.color = new Color(0.9f, 0.82f, 0.54f, 1f);
        }

        private void BuildMainMenuPanel(Transform parent)
        {
            _mainMenuPanel = CreatePanel(parent, "MainMenuPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(112f, -24f), new Vector2(1160f, 560f), new Color(0.03f, 0.05f, 0.09f, 0.32f));

            var overviewCard = CreatePanel(_mainMenuPanel.transform, "OverviewCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260f, 0f), new Vector2(496f, 452f), new Color(0.04f, 0.07f, 0.11f, 0.74f));
            var actionCard = CreatePanel(_mainMenuPanel.transform, "ActionCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(268f, 0f), new Vector2(372f, 452f), new Color(0.02f, 0.03f, 0.06f, 0.84f));

            var overviewHeader = CreateText(overviewCard.transform, "OverviewHeader", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -30f), new Vector2(260f, 24f), "OPERATION BRIEF", 16, FontStyle.Bold);
            overviewHeader.alignment = TextAnchor.MiddleLeft;
            overviewHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var overviewTitle = CreateText(overviewCard.transform, "OverviewTitle", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -72f), new Vector2(360f, 84f), "Arcade survivor runs\nwith shared progression.", 30, FontStyle.Bold);
            overviewTitle.alignment = TextAnchor.UpperLeft;

            var overviewBody = CreateText(overviewCard.transform, "OverviewBody", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -176f), new Vector2(420f, 116f), "Choose a mode, lock in a starter loadout, then loop credits back into permanent unlocks and research.", 18, FontStyle.Normal);
            overviewBody.alignment = TextAnchor.UpperLeft;
            overviewBody.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            var soloCard = CreatePanel(overviewCard.transform, "SoloInfoCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -74f), new Vector2(436f, 80f), new Color(0.09f, 0.13f, 0.19f, 0.86f));
            var soloInfo = CreateText(soloCard.transform, "SoloInfo", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Single Play\nPick character and starter weapon before deployment.", 17, FontStyle.Bold);
            soloInfo.alignment = TextAnchor.MiddleLeft;
            soloInfo.rectTransform.offsetMin = new Vector2(18f, 10f);
            soloInfo.rectTransform.offsetMax = new Vector2(-18f, -10f);

            var metaCard = CreatePanel(overviewCard.transform, "MetaInfoCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(436f, 92f), new Color(0.11f, 0.08f, 0.03f, 0.86f));
            var metaInfo = CreateText(metaCard.transform, "MetaInfo", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "Meta Progression\nCredits unlock characters, starter weapons, and persistent stat nodes.", 17, FontStyle.Bold);
            metaInfo.alignment = TextAnchor.MiddleLeft;
            metaInfo.rectTransform.offsetMin = new Vector2(18f, 10f);
            metaInfo.rectTransform.offsetMax = new Vector2(-18f, -10f);

            var header = CreateText(actionCard.transform, "MainMenuHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(280f, 30f), "SELECT MODE", 18, FontStyle.Bold);
            header.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var subhead = CreateText(actionCard.transform, "MainMenuSubhead", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(260f, 42f), "Start a run, open shared meta, or adjust display settings.", 15, FontStyle.Normal);
            subhead.color = new Color(0.75f, 0.81f, 0.91f, 1f);

            var baseY = 118f;
            _singlePlayButton = CreateButton(actionCard.transform, "SinglePlayButton", new Vector2(0f, baseY), "Single Play", OnSinglePlayClicked, new Vector2(296f, 56f));
            _multiPlayButton = CreateButton(actionCard.transform, "MultiPlayButton", new Vector2(0f, baseY - (ButtonHeight + ButtonSpacing)), "Multiplayer", OnMultiPlayClicked, new Vector2(296f, 56f));
            _metaButton = CreateButton(actionCard.transform, "MetaButton", new Vector2(0f, baseY - ((ButtonHeight + ButtonSpacing) * 2f)), "Meta", OnMetaClicked, new Vector2(296f, 56f));
            _optionsButton = CreateButton(actionCard.transform, "OptionsButton", new Vector2(0f, baseY - ((ButtonHeight + ButtonSpacing) * 3f)), "Options", OnOptionsClicked, new Vector2(296f, 56f));
            CreateButton(actionCard.transform, "QuitButton", new Vector2(0f, baseY - ((ButtonHeight + ButtonSpacing) * 4f)), "Quit", OnQuitClicked, new Vector2(296f, 56f));
        }

        private void BuildMultiplayerPanel(Transform parent)
        {
            _multiplayerPanel = CreatePanel(parent, "MultiplayerPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(920f, 560f), new Color(0.02f, 0.03f, 0.06f, 0.86f));
            _multiplayerPanel.SetActive(false);

            var title = CreateText(_multiplayerPanel.transform, "MultiplayerTitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(320f, 30f), "MULTIPLAYER", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var desc = CreateText(_multiplayerPanel.transform, "MultiplayerDescription", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(540f, 42f), "Host creates a Relay room. Join uses a shared room code.", 16, FontStyle.Normal);
            desc.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            desc.alignment = TextAnchor.MiddleCenter;

            var hostCard = CreatePanel(_multiplayerPanel.transform, "HostCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-206f, -8f), new Vector2(332f, 304f), new Color(0.05f, 0.08f, 0.12f, 0.86f));
            var joinCard = CreatePanel(_multiplayerPanel.transform, "JoinCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(206f, -8f), new Vector2(332f, 304f), new Color(0.05f, 0.08f, 0.12f, 0.86f));

            var hostHeader = CreateText(hostCard.transform, "HostHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(200f, 24f), "HOST ROOM", 18, FontStyle.Bold);
            hostHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            var hostDesc = CreateText(hostCard.transform, "HostDescription", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(252f, 86f), "Start a new co-op session and share the generated room code with other players.", 16, FontStyle.Normal);
            hostDesc.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            _hostButton = CreateButton(hostCard.transform, "HostButton", new Vector2(0f, -90f), "HOST", OnHostClicked, new Vector2(232f, 54f));

            var joinHeader = CreateText(joinCard.transform, "JoinHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(200f, 24f), "JOIN ROOM", 18, FontStyle.Bold);
            joinHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            var joinDesc = CreateText(joinCard.transform, "JoinDescription", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(252f, 52f), "Enter the shared room code to connect.", 16, FontStyle.Normal);
            joinDesc.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            var codeLabel = CreateText(joinCard.transform, "JoinCodeLabel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-88f, -132f), new Vector2(176f, 24f), "ROOM CODE", 15, FontStyle.Bold);
            codeLabel.alignment = TextAnchor.MiddleLeft;
            codeLabel.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            _joinCodeInput = CreateInputField(joinCard.transform, "JoinCodeInput", new Vector2(0f, -18f), new Vector2(232f, 46f), string.Empty, "AB12CD");
            _joinButton = CreateButton(joinCard.transform, "JoinButton", new Vector2(0f, -90f), "JOIN", OnJoinClicked, new Vector2(232f, 54f));
            _backButton = CreateButton(_multiplayerPanel.transform, "BackButton", new Vector2(0f, -212f), "BACK", ShowMainMenu, new Vector2(240f, 46f));
        }

        private void BuildOptionsPanel(Transform parent)
        {
            _optionsPanel = CreatePanel(parent, "OptionsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(640f, 360f), new Color(0.02f, 0.03f, 0.06f, 0.86f));
            _optionsPanel.SetActive(false);

            var title = CreateText(_optionsPanel.transform, "OptionsTitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(320f, 30f), "OPTIONS", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var desc = CreateText(_optionsPanel.transform, "OptionsDescription", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(420f, 42f), "Windowed mode is the default. Toggle fullscreen if you want the game to take over the current display.", 15, FontStyle.Normal);
            desc.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            desc.alignment = TextAnchor.MiddleCenter;

            var displayCard = CreatePanel(_optionsPanel.transform, "DisplayCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(420f, 120f), new Color(0.05f, 0.08f, 0.12f, 0.86f));
            var displayHeader = CreateText(displayCard.transform, "DisplayHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(240f, 24f), "DISPLAY", 16, FontStyle.Bold);
            displayHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _fullscreenToggle = CreateToggle(displayCard.transform, "FullscreenToggle", new Vector2(0f, -12f), new Vector2(240f, 36f), "Fullscreen", OnFullscreenToggleChanged);
            _optionsBackButton = CreateButton(_optionsPanel.transform, "OptionsBackButton", new Vector2(0f, -128f), "BACK", ShowMainMenu, new Vector2(240f, 46f));
        }
        private void BuildRunSetupPanel(Transform parent)
        {
            _runSetupPanel = CreatePanel(parent, "RunSetupPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(1140f, 684f), new Color(0.02f, 0.03f, 0.06f, 0.88f));
            _runSetupPanel.SetActive(false);

            var title = CreateText(_runSetupPanel.transform, "RunSetupHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(420f, 34f), "RUN SETUP", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var hint = CreateText(_runSetupPanel.transform, "RunSetupHint", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(640f, 22f), "Lock in a character and starter weapon before deployment.", 14, FontStyle.Normal);
            hint.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            var characterCard = CreatePanel(_runSetupPanel.transform, "CharacterCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-292f, 18f), new Vector2(388f, 440f), new Color(0.05f, 0.08f, 0.12f, 0.88f));
            var weaponCard = CreatePanel(_runSetupPanel.transform, "WeaponCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(236f, 18f), new Vector2(636f, 440f), new Color(0.05f, 0.08f, 0.12f, 0.88f));
            var summaryCard = CreatePanel(_runSetupPanel.transform, "RunSummaryCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), new Vector2(1024f, 100f), new Color(0.08f, 0.11f, 0.15f, 0.92f));

            var characterHeader = CreateText(characterCard.transform, "CharacterHeader", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(220f, 24f), "CHARACTER", 17, FontStyle.Bold);
            characterHeader.alignment = TextAnchor.MiddleLeft;
            characterHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _runSetupCharacterText = CreateText(characterCard.transform, "RunSetupCharacterText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -66f), new Vector2(320f, 78f), string.Empty, 22, FontStyle.Bold);
            _runSetupCharacterText.alignment = TextAnchor.UpperLeft;
            _runSetupCharacterOptionsRoot = CreateAnchoredRoot(characterCard.transform, "RunSetupCharacterOptionsRoot", new Vector2(24f, 164f), new Vector2(340f, 240f));

            var weaponHeader = CreateText(weaponCard.transform, "WeaponHeader", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(260f, 24f), "STARTER WEAPON", 17, FontStyle.Bold);
            weaponHeader.alignment = TextAnchor.MiddleLeft;
            weaponHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _runSetupWeaponText = CreateText(weaponCard.transform, "RunSetupWeaponText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -66f), new Vector2(520f, 60f), string.Empty, 22, FontStyle.Bold);
            _runSetupWeaponText.alignment = TextAnchor.UpperLeft;
            _runSetupWeaponText.color = new Color(0.97f, 0.98f, 1f, 1f);
            _runSetupWeaponOptionsRoot = CreateAnchoredRoot(weaponCard.transform, "RunSetupWeaponOptionsRoot", new Vector2(24f, 164f), new Vector2(588f, 240f));

            _runSetupBonusText = CreateText(summaryCard.transform, "RunSetupBonusText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 18, FontStyle.Normal);
            _runSetupBonusText.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            CreateButton(_runSetupPanel.transform, "RunSetupStartButton", new Vector2(-118f, -294f), "Start", StartSinglePlay, new Vector2(220f, 52f));
            CreateButton(_runSetupPanel.transform, "RunSetupBackButton", new Vector2(118f, -294f), "BACK", ShowMainMenu, new Vector2(220f, 52f));
        }

        private void BuildMetaPanel(Transform parent)
        {
            _metaPanel = CreatePanel(parent, "MetaPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(1120f, 812f), new Color(0.02f, 0.03f, 0.06f, 0.88f));
            _metaPanel.SetActive(false);

            var title = CreateText(_metaPanel.transform, "MetaTitle", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(220f, 32f), "META", 26, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleLeft;
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            _metaUnlocksTabButton = CreateButton(_metaPanel.transform, "MetaUnlocksTab", new Vector2(240f, -34f), "UNLOCKS", () => SetMetaTab(MetaTab.Unlocks), new Vector2(172f, 42f));
            _metaResearchTabButton = CreateButton(_metaPanel.transform, "MetaResearchTab", new Vector2(430f, -34f), "RESEARCH", () => SetMetaTab(MetaTab.Research), new Vector2(172f, 42f));
            _metaUnlocksTabText = _metaUnlocksTabButton.GetComponentInChildren<Text>();
            _metaResearchTabText = _metaResearchTabButton.GetComponentInChildren<Text>();
            SetTopLeftRect(_metaUnlocksTabButton.GetComponent<RectTransform>(), new Vector2(736f, 24f), new Vector2(172f, 42f));
            SetTopLeftRect(_metaResearchTabButton.GetComponent<RectTransform>(), new Vector2(924f, 24f), new Vector2(172f, 42f));

            var statsCard = CreatePanel(_metaPanel.transform, "MetaStatsCard", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -92f), new Vector2(520f, 132f), new Color(0.05f, 0.08f, 0.12f, 0.88f));
            var recentCard = CreatePanel(_metaPanel.transform, "MetaRecentCard", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -92f), new Vector2(520f, 132f), new Color(0.05f, 0.08f, 0.12f, 0.88f));

            _metaHeaderText = CreateText(statsCard.transform, "MetaHeaderText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 16, FontStyle.Bold);
            _metaHeaderText.rectTransform.offsetMin = new Vector2(18f, 16f);
            _metaHeaderText.rectTransform.offsetMax = new Vector2(-18f, -16f);
            _metaHeaderText.alignment = TextAnchor.UpperLeft;
            _metaRecentText = CreateText(recentCard.transform, "MetaRecentText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 15, FontStyle.Normal);
            _metaRecentText.rectTransform.offsetMin = new Vector2(18f, 16f);
            _metaRecentText.rectTransform.offsetMax = new Vector2(-18f, -16f);
            _metaRecentText.alignment = TextAnchor.UpperLeft;
            _metaRecentText.color = new Color(0.82f, 0.87f, 0.95f, 1f);

            _metaContentRoot = new GameObject("MetaContentRoot");
            _metaContentRoot.transform.SetParent(_metaPanel.transform, false);
            var contentRect = _metaContentRoot.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = new Vector2(32f, -246f);
            contentRect.sizeDelta = new Vector2(1056f, 520f);

            var backButton = CreateButton(_metaPanel.transform, "MetaBackButton", new Vector2(0f, -372f), "BACK", ShowMainMenu, new Vector2(240f, 46f));
            SetBottomCenterRect(backButton.GetComponent<RectTransform>(), new Vector2(0f, 24f), new Vector2(240f, 46f));
        }

        private void BuildSummaryModal(Transform parent)
        {
            _summaryModal = CreatePanel(parent, "SummaryModal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(720f, 420f), new Color(0.02f, 0.03f, 0.06f, 0.92f));
            _summaryModal.SetActive(false);

            var title = CreateText(_summaryModal.transform, "SummaryModalTitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(320f, 30f), "RUN SUMMARY", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var bodyCard = CreatePanel(_summaryModal.transform, "SummaryBodyCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(620f, 196f), new Color(0.05f, 0.08f, 0.12f, 0.86f));
            _summaryModalText = CreateText(bodyCard.transform, "SummaryModalText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 18, FontStyle.Normal);
            _summaryModalText.rectTransform.offsetMin = new Vector2(22f, 18f);
            _summaryModalText.rectTransform.offsetMax = new Vector2(-22f, -18f);
            _summaryMetaButton = CreateButton(_summaryModal.transform, "SummaryMetaButton", new Vector2(-116f, -148f), "Open Meta", OpenMetaFromSummary, new Vector2(212f, 46f));
            CreateButton(_summaryModal.transform, "SummaryCloseButton", new Vector2(116f, -148f), "Close", CloseSummaryModal, new Vector2(212f, 46f));
        }

        private void OnSinglePlayClicked()
        {
            _selectedCharacterId = MetaProgressionService.GetSingleSelectedCharacterId();
            _selectedStarterWeaponId = MetaProgressionService.GetSingleSelectedStarterWeapon();
            RefreshRunSetupPanel();
            ShowPanel(_runSetupPanel, _runSetupCharacterButton);
            SetStatus("Select character and starter weapon.");
        }

        private void OnMetaClicked()
        {
            RefreshMetaPanel();
            ShowPanel(_metaPanel, _metaUnlocksTabButton);
            SetStatus("Spend Credits on unlocks and research.");
        }

        private void OnOptionsClicked()
        {
            SyncFullscreenToggle();
            ShowPanel(_optionsPanel, _fullscreenToggle);
            SetStatus("Display settings updated here.");
        }

        private void OnMultiPlayClicked()
        {
            ShowPanel(_multiplayerPanel, _hostButton);
            UpdateMultiplayerInteractivity();
            SetStatus("Create a Relay session or join with a code.");
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

        private void OnCycleSingleCharacter()
        {
            _selectedCharacterId = MetaProgressionService.GetNextUnlockedCharacterId(_selectedCharacterId);
            MetaProgressionService.SetSingleSelectedCharacterId(_selectedCharacterId);
            RefreshRunSetupPanel();
        }

        private void OnCycleSingleStarterWeapon()
        {
            _selectedStarterWeaponId = MetaProgressionService.GetNextUnlockedStarterWeapon(_selectedStarterWeaponId);
            MetaProgressionService.SetSingleSelectedStarterWeapon(_selectedStarterWeaponId);
            RefreshRunSetupPanel();
        }

        private void SelectSingleCharacter(int characterId)
        {
            if (!MetaProgressionService.IsCharacterUnlocked(characterId))
            {
                return;
            }

            _selectedCharacterId = characterId;
            MetaProgressionService.SetSingleSelectedCharacterId(characterId);
            RefreshRunSetupPanel();
        }

        private void SelectSingleStarterWeapon(WeaponUpgradeId weaponId)
        {
            if (!MetaProgressionService.IsWeaponUnlocked(weaponId))
            {
                return;
            }

            _selectedStarterWeaponId = weaponId;
            MetaProgressionService.SetSingleSelectedStarterWeapon(weaponId);
            RefreshRunSetupPanel();
        }

        private void StartSinglePlay()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                SetStatus("Missing gameplay scene name.");
                return;
            }

            MetaProgressionService.SetSingleSelectedCharacterId(_selectedCharacterId);
            MetaProgressionService.SetSingleSelectedStarterWeapon(_selectedStarterWeaponId);
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameplaySceneName);
        }

        private void SetMetaTab(MetaTab tab)
        {
            _currentMetaTab = tab;
            RefreshMetaPanel();
        }

        private void OpenMetaFromSummary()
        {
            CloseSummaryModal();
            OnMetaClicked();
        }

        private void OpenSummaryModal(RunRewardSummary summary)
        {
            if (_summaryModal == null || _summaryModalText == null || summary == null)
            {
                return;
            }

            _summaryModalText.text = summary.BuildDisplayText();
            _summaryModal.SetActive(true);
            SetTitleChromeVisible(false);
            var eventSystem = EventSystem.current;
            if (eventSystem != null && _summaryMetaButton != null)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_summaryMetaButton.gameObject);
            }
        }

        private void CloseSummaryModal()
        {
            if (_summaryModal != null)
            {
                _summaryModal.SetActive(false);
            }

            SetTitleChromeVisible(_mainMenuPanel != null && _mainMenuPanel.activeSelf);
        }

        private void ShowMainMenu()
        {
            ShowPanel(_mainMenuPanel, _singlePlayButton);
            UpdateMultiplayerInteractivity();
        }

        private void ShowPanel(GameObject activePanel, Selectable preferredSelection)
        {
            if (_mainMenuPanel != null) _mainMenuPanel.SetActive(activePanel == _mainMenuPanel);
            if (_multiplayerPanel != null) _multiplayerPanel.SetActive(activePanel == _multiplayerPanel);
            if (_optionsPanel != null) _optionsPanel.SetActive(activePanel == _optionsPanel);
            if (_runSetupPanel != null) _runSetupPanel.SetActive(activePanel == _runSetupPanel);
            if (_metaPanel != null) _metaPanel.SetActive(activePanel == _metaPanel);
            SetTitleChromeVisible(activePanel == _mainMenuPanel && (_summaryModal == null || !_summaryModal.activeSelf));

            var eventSystem = EventSystem.current;
            if (eventSystem != null && preferredSelection != null)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(preferredSelection.gameObject);
            }
        }

        private void SetTitleChromeVisible(bool visible)
        {
            if (_accentBar != null)
            {
                _accentBar.SetActive(visible);
            }

            if (_titleText != null)
            {
                _titleText.gameObject.SetActive(visible);
            }

            if (_subtitleText != null)
            {
                _subtitleText.gameObject.SetActive(visible);
            }
        }

        private void RefreshRunSetupPanel()
        {
            if (_runSetupCharacterText == null || _runSetupCharacterOptionsRoot == null || _runSetupWeaponOptionsRoot == null)
            {
                return;
            }

            if (!MetaProgressionService.IsCharacterUnlocked(_selectedCharacterId))
            {
                _selectedCharacterId = MetaProgressionService.GetSingleSelectedCharacterId();
                if (!MetaProgressionService.IsCharacterUnlocked(_selectedCharacterId))
                {
                    _selectedCharacterId = SharedGameCatalog.GetDefaultUnlockedCharacterId();
                }
            }

            if (!SharedGameCatalog.IsStarterWeaponSelectable(_selectedStarterWeaponId)
                || !MetaProgressionService.IsWeaponUnlocked(_selectedStarterWeaponId))
            {
                _selectedStarterWeaponId = MetaProgressionService.GetSingleSelectedStarterWeapon();
                if (!SharedGameCatalog.IsStarterWeaponSelectable(_selectedStarterWeaponId)
                    || !MetaProgressionService.IsWeaponUnlocked(_selectedStarterWeaponId))
                {
                    _selectedStarterWeaponId = SharedGameCatalog.GetDefaultUnlockedStarterWeapon();
                }
            }

            var character = SharedGameCatalog.GetCharacter(_selectedCharacterId);
            var weapon = SharedGameCatalog.GetStarterWeaponDefinition(SharedGameCatalog.GetStarterWeaponIndex(_selectedStarterWeaponId));
            _runSetupCharacterText.text = $"{character.DisplayName}\n{BuildMetaBonusSummary(character.TraitBonuses)}";
            _runSetupCharacterText.color = character.Color;
            _runSetupWeaponText.text = $"{weapon.DisplayName}\nUnlocked starter weapon";
            _runSetupBonusText.text = $"RUN START BONUS\n{BuildMetaBonusSummary(MetaProgressionService.GetCombinedRunStartBonuses(_selectedCharacterId))}";

            ClearChildren(_runSetupCharacterOptionsRoot.transform);
            ClearChildren(_runSetupWeaponOptionsRoot.transform);
            RebuildRunSetupCharacterOptions();
            RebuildRunSetupWeaponOptions();
            UpdateMultiplayerInteractivity();
        }

        private void RefreshMetaPanel()
        {
            if (_metaHeaderText == null || _metaRecentText == null || _metaContentRoot == null)
            {
                return;
            }

            _metaHeaderText.text =
                "PROFILE\n" +
                $"Credits {MetaProgressionService.CurrentCredits} | Lifetime {MetaProgressionService.TotalCreditsEarned}\n" +
                $"Runs {MetaProgressionService.RunsPlayed} | Clears {MetaProgressionService.RunsCleared}\n" +
                $"Best Lv {MetaProgressionService.BestLevel} | Best Time {MetaProgressionService.BestTimeSeconds:0.0}s | Kills {MetaProgressionService.TotalEnemiesDefeated}";
            _metaRecentText.text = $"RECENT RUN\n{_recentRunSummaryText}";
            RefreshMetaTabVisuals();
            RebuildMetaContent();
        }

        private void RefreshMetaTabVisuals()
        {
            ApplyTabState(_metaUnlocksTabButton, _metaUnlocksTabText, _currentMetaTab == MetaTab.Unlocks);
            ApplyTabState(_metaResearchTabButton, _metaResearchTabText, _currentMetaTab == MetaTab.Research);
        }

        private void ApplyTabState(Button button, Text label, bool selected)
        {
            if (button == null || label == null)
            {
                return;
            }

            if (button.targetGraphic is Image image)
            {
                image.color = selected ? new Color(0.28f, 0.34f, 0.46f, 1f) : new Color(0.16f, 0.20f, 0.29f, 0.96f);
            }

            label.color = selected ? new Color(0.98f, 0.86f, 0.42f, 1f) : new Color(0.97f, 0.98f, 1f, 1f);
        }

        private void RebuildMetaContent()
        {
            ClearChildren(_metaContentRoot.transform);
            if (_currentMetaTab == MetaTab.Unlocks)
            {
                BuildUnlocksTabContent();
            }
            else
            {
                BuildResearchTabContent();
            }
        }

        private void BuildUnlocksTabContent()
        {
            CreateSectionText(_metaContentRoot.transform, "CharactersHeader", new Vector2(0f, 0f), "Characters");
            CreateSectionText(_metaContentRoot.transform, "WeaponsHeader", new Vector2(536f, 0f), "Weapons");

            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                var unlocked = MetaProgressionService.IsCharacterUnlocked(definition.Id);
                var affordable = MetaProgressionService.CurrentCredits >= definition.UnlockCost;
                var label = unlocked
                    ? $"{definition.DisplayName}\nUnlocked | {BuildMetaBonusSummary(definition.TraitBonuses)}"
                    : $"{definition.DisplayName}\nCost {definition.UnlockCost} | {BuildMetaBonusSummary(definition.TraitBonuses)}";
                CreateMetaEntryButton(_metaContentRoot.transform, $"Character{i}", new Vector2(0f, 52f + (i * 46f)), new Vector2(488f, 40f), label, !unlocked && affordable, () => TryPurchaseCharacter(definition.Id));
            }

            var weaponDisplayIndex = 0;
            for (var i = 0; i < SharedGameCatalog.StarterWeaponDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.StarterWeaponDefinitions[i];
                if (!definition.IsSelectable)
                {
                    continue;
                }

                var unlocked = MetaProgressionService.IsWeaponUnlocked(definition.Id);
                var affordable = MetaProgressionService.CurrentCredits >= definition.UnlockCost;
                var label = unlocked ? $"{definition.DisplayName}\nUnlocked" : $"{definition.DisplayName}\nCost {definition.UnlockCost}";
                var column = weaponDisplayIndex % 2;
                var row = weaponDisplayIndex / 2;
                CreateMetaEntryButton(_metaContentRoot.transform, $"Weapon{weaponDisplayIndex}", new Vector2(536f + (column * 252f), 52f + (row * 44f)), new Vector2(236f, 38f), label, !unlocked && affordable, () => TryPurchaseWeapon(definition.Id));
                weaponDisplayIndex++;
            }
        }

        private void BuildResearchTabContent()
        {
            var nodes = MetaProgressionService.Config.NodeDefinitions;
            for (var i = 0; i < nodes.Count; i++)
            {
                var definition = nodes[i];
                var x = i < 6 ? 0f : 536f;
                var y = (i < 6 ? i : i - 6) * 58f;
                var purchased = MetaProgressionService.IsNodePurchased(definition.Id);
                var missingPrereq = definition.HasPrerequisite && !MetaProgressionService.IsNodePurchased(definition.PrerequisiteId);
                var affordable = MetaProgressionService.CurrentCredits >= definition.Cost;
                var interactable = !purchased && !missingPrereq && affordable;
                var state = purchased ? "Researched" : missingPrereq ? $"Needs {GetNodeTitle(definition.PrerequisiteId)}" : $"Cost {definition.Cost}";
                var label = $"{definition.Title}\n{definition.Description}\n{state}";
                CreateMetaEntryButton(_metaContentRoot.transform, $"Node{i}", new Vector2(x, y), new Vector2(488f, 52f), label, interactable, () => TryPurchaseNode(definition.Id));
            }
        }

        private void TryPurchaseCharacter(int characterId)
        {
            if (MetaProgressionService.TryPurchaseCharacter(characterId, out var reason))
            {
                RefreshMetaPanel();
                SetStatus($"{SharedGameCatalog.GetCharacter(characterId).DisplayName} unlocked.");
            }
            else
            {
                SetStatus(reason);
            }
        }

        private void TryPurchaseWeapon(WeaponUpgradeId weaponId)
        {
            if (MetaProgressionService.TryPurchaseWeapon(weaponId, out var reason))
            {
                RefreshMetaPanel();
                SetStatus($"{SharedGameCatalog.GetWeaponDisplayName(weaponId)} unlocked.");
            }
            else
            {
                SetStatus(reason);
            }
        }

        private void TryPurchaseNode(MetaNodeId nodeId)
        {
            if (MetaProgressionService.TryPurchaseNode(nodeId, out var reason))
            {
                RefreshMetaPanel();
                RefreshRunSetupPanel();
                SetStatus($"{GetNodeTitle(nodeId)} researched.");
            }
            else
            {
                SetStatus(reason);
            }
        }

        private string GetNodeTitle(MetaNodeId nodeId)
        {
            return MetaProgressionService.Config.TryGetNodeDefinition(nodeId, out var definition) ? definition.Title : nodeId.ToString();
        }

        private string BuildMetaBonusSummary(MetaBonusValues bonuses)
        {
            var builder = new StringBuilder();
            AppendBonus(builder, bonuses.attackPowerPercent, "ATK +{0:0.#}%");
            AppendBonus(builder, bonuses.attackSpeedPercent, "ASPD +{0:0.#}%");
            AppendBonus(builder, bonuses.maxHealthFlat, "HP +{0:0.#}");
            AppendBonus(builder, bonuses.healthRegenPerSecond, "Regen +{0:0.##}/s");
            AppendBonus(builder, bonuses.moveSpeedPercent, "Move +{0:0.#}%");
            AppendBonus(builder, bonuses.attackRangePercent, "Range +{0:0.#}%");
            AppendBonus(builder, bonuses.luck, "Luck +{0:0.##}");
            return builder.Length > 0 ? builder.ToString() : "No bonus";
        }

        private static void AppendBonus(StringBuilder builder, float value, string format)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.AppendFormat(format, value);
        }
        private void UpdateMultiplayerInteractivity()
        {
            var interactable = !MultiplayerSessionController.EnsureInstance().IsBusy;
            SetInteractable(_singlePlayButton, interactable);
            SetInteractable(_multiPlayButton, interactable);
            SetInteractable(_metaButton, interactable);
            SetInteractable(_optionsButton, interactable);
            SetInteractable(_hostButton, interactable);
            SetInteractable(_joinButton, interactable);
            SetInteractable(_backButton, interactable);
            SetInteractable(_optionsBackButton, interactable);
            SetInteractable(_runSetupCharacterButton, interactable);
            SetInteractable(_runSetupWeaponButton, interactable);
            for (var i = 0; i < _runSetupCharacterOptionButtons.Length; i++)
            {
                SetInteractable(_runSetupCharacterOptionButtons[i], interactable);
            }

            for (var i = 0; i < _runSetupWeaponOptionButtons.Length; i++)
            {
                SetInteractable(_runSetupWeaponOptionButtons[i], interactable);
            }

            if (_joinCodeInput != null) _joinCodeInput.interactable = interactable;
            if (_fullscreenToggle != null) _fullscreenToggle.interactable = interactable;
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
            {
                selectable.interactable = interactable;
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

            ApplyDisplayMode(useFullscreen, false);
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
                Screen.SetResolution(Mathf.Max(1, resolution.width), Mathf.Max(1, resolution.height), FullScreenMode.FullScreenWindow);
            }
            else
            {
                var resolution = Screen.currentResolution;
                var width = Mathf.Clamp(DefaultWindowWidth, 960, Mathf.Max(960, resolution.width));
                var height = Mathf.Clamp(DefaultWindowHeight, 540, Mathf.Max(540, resolution.height));
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
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

            ApplyDisplayMode(useFullscreen, true);
            SetStatus(useFullscreen ? "Display mode: Fullscreen." : "Display mode: Windowed.");
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                component = gameObject.AddComponent<T>();
            }

            return component;
        }

        private static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private GameObject CreateFullscreenPanel(Transform parent, string name, Color color)
        {
            return CreatePanel(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, color);
        }

        private static GameObject CreateAnchoredRoot(Transform parent, string name, Vector2 topLeft, Vector2 size)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rect.sizeDelta = size;
            return root;
        }

        private static void SetTopLeftRect(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rect.sizeDelta = size;
        }

        private static void SetBottomCenterRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
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

        private Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, string content, int fontSize, FontStyle fontStyle)
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void RebuildRunSetupCharacterOptions()
        {
            var buttons = new List<Button>();
            _runSetupCharacterButton = null;
            var displayIndex = 0;
            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                if (!MetaProgressionService.IsCharacterUnlocked(definition.Id))
                {
                    continue;
                }

                var row = displayIndex;
                var button = CreateMetaEntryButton(
                    _runSetupCharacterOptionsRoot.transform,
                    $"RunSetupCharacter{definition.Id}",
                    new Vector2(0f, row * 56f),
                    new Vector2(340f, 50f),
                    $"{definition.DisplayName}\n{BuildMetaBonusSummary(definition.TraitBonuses)}",
                    true,
                    () => SelectSingleCharacter(definition.Id));
                var label = button.GetComponentInChildren<Text>();
                ApplyRunSetupOptionState(button, label, definition.Id == _selectedCharacterId, definition.Color);
                if (definition.Id == _selectedCharacterId)
                {
                    _runSetupCharacterButton = button;
                }

                buttons.Add(button);
                displayIndex++;
            }

            _runSetupCharacterOptionButtons = buttons.ToArray();
            if (_runSetupCharacterButton == null && _runSetupCharacterOptionButtons.Length > 0)
            {
                _runSetupCharacterButton = _runSetupCharacterOptionButtons[0];
            }
        }

        private void RebuildRunSetupWeaponOptions()
        {
            var buttons = new List<Button>();
            _runSetupWeaponButton = null;
            var displayIndex = 0;
            for (var i = 0; i < SharedGameCatalog.StarterWeaponDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.StarterWeaponDefinitions[i];
                if (!definition.IsSelectable || !MetaProgressionService.IsWeaponUnlocked(definition.Id))
                {
                    continue;
                }

                var column = displayIndex % 3;
                var row = displayIndex / 3;
                var button = CreateMetaEntryButton(
                    _runSetupWeaponOptionsRoot.transform,
                    $"RunSetupWeapon{definition.Id}",
                    new Vector2(column * 194f, row * 52f),
                    new Vector2(182f, 46f),
                    definition.DisplayName,
                    true,
                    () => SelectSingleStarterWeapon(definition.Id));
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.alignment = TextAnchor.MiddleCenter;
                }

                ApplyRunSetupOptionState(button, label, definition.Id == _selectedStarterWeaponId, new Color(0.96f, 0.74f, 0.18f, 1f));
                if (definition.Id == _selectedStarterWeaponId)
                {
                    _runSetupWeaponButton = button;
                }

                buttons.Add(button);
                displayIndex++;
            }

            _runSetupWeaponOptionButtons = buttons.ToArray();
            if (_runSetupWeaponButton == null && _runSetupWeaponOptionButtons.Length > 0)
            {
                _runSetupWeaponButton = _runSetupWeaponOptionButtons[0];
            }
        }

        private static void ApplyRunSetupOptionState(Button button, Text label, bool selected, Color accentColor)
        {
            if (button == null || label == null || button.targetGraphic is not Image image)
            {
                return;
            }

            var baseColor = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            var selectedColor = Color.Lerp(baseColor, accentColor, 0.42f);
            image.color = selected ? selectedColor : baseColor;
            label.color = selected ? new Color(0.98f, 0.86f, 0.42f, 1f) : new Color(0.97f, 0.98f, 1f, 1f);

            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = selected ? Color.Lerp(selectedColor, Color.white, 0.08f) : new Color(0.22f, 0.28f, 0.39f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = selected ? Color.Lerp(selectedColor, Color.black, 0.16f) : new Color(0.12f, 0.15f, 0.22f, 1f);
            colors.disabledColor = new Color(0.14f, 0.14f, 0.14f, 0.84f);
            button.colors = colors;
        }

        private Button CreateButton(Transform parent, string name, Vector2 anchoredPosition, string label, UnityEngine.Events.UnityAction onClick, Vector2? sizeOverride = null)
        {
            var size = sizeOverride ?? new Vector2(ButtonWidth, ButtonHeight);
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.20f, 0.29f, 0.96f);
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
            var labelText = CreateText(buttonObject.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, label, 20, FontStyle.Bold);
            labelText.color = new Color(0.97f, 0.98f, 1f, 1f);
            return button;
        }

        private Button CreateMetaEntryButton(Transform parent, string name, Vector2 topLeft, Vector2 size, string label, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rect.sizeDelta = size;

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            button.onClick.AddListener(onClick);
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.22f, 0.28f, 0.39f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.12f, 0.15f, 0.22f, 1f);
            colors.disabledColor = new Color(0.14f, 0.14f, 0.14f, 0.84f);
            button.colors = colors;
            var labelText = CreateText(buttonObject.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, label, 14, FontStyle.Normal);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.rectTransform.offsetMin = new Vector2(12f, 6f);
            labelText.rectTransform.offsetMax = new Vector2(-12f, -6f);
            return button;
        }

        private void CreateSectionText(Transform parent, string name, Vector2 topLeft, string label)
        {
            var text = CreateText(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(topLeft.x, -topLeft.y), new Vector2(320f, 28f), label, 18, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.96f, 0.74f, 0.18f, 1f);
        }

        private InputField CreateInputField(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string initialText, string placeholderText)
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

        private Toggle CreateToggle(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string label, UnityEngine.Events.UnityAction<bool> onValueChanged)
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
            backgroundRect.sizeDelta = new Vector2(28f, 28f);
            var backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

            var checkmarkObject = new GameObject("Checkmark");
            checkmarkObject.transform.SetParent(backgroundObject.transform, false);
            var checkmarkRect = checkmarkObject.AddComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
            checkmarkRect.sizeDelta = new Vector2(16f, 16f);
            var checkmarkImage = checkmarkObject.AddComponent<Image>();
            checkmarkImage.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(toggleObject.transform, false);
            var labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(42f, 0f);
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
            toggle.onValueChanged.AddListener(onValueChanged);
            return toggle;
        }
    }
}
