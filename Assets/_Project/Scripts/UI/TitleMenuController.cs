using System;
using System.Collections.Generic;
using System.Text;
using EJR.Game.Audio;
using EJR.Game.Core;
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
    public sealed partial class TitleMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "ExpandedMapScene";

        private const string FullscreenPreferenceKey = "settings.fullscreen";
        private const float OptionsSliderWidth = 268f;
        private const int DefaultWindowWidth = 1600;
        private const int DefaultWindowHeight = 900;
        private const float ButtonWidth = 320f;
        private const float ButtonHeight = 58f;
        private const float ButtonSpacing = 18f;

        private enum MetaTab
        {
            Unlocks,
            Upgrades,
            Research = Upgrades,
        }

        private enum SingleRunSetupStep
        {
            MapSelect,
            CharacterSelect,
        }

        private Font _font;
        private Canvas _canvas;
        private GameObject _mainMenuPanel;
        private GameObject _multiplayerPanel;
        private GameObject _optionsPanel;
        private GameObject _runSetupPanel;
        private GameObject _runSetupMapStepRoot;
        private GameObject _runSetupCharacterStepRoot;
        private GameObject _runSetupCharacterOptionsRoot;
        private GameObject _runSetupWeaponOptionsRoot;
        private RectTransform _runSetupCharacterOptionsContentRect;
        private RectTransform _runSetupDetailContentRect;
        private GameObject _achievementPanel;
        private RectTransform _achievementContentRect;
        private GameObject _metaPanel;
        private GameObject _metaContentRoot;
        private GameObject _summaryModal;
        private GameObject _accentBar;
        private Text _titleText;
        private Text _subtitleText;
        private Text _runSetupHeaderText;
        private Text _runSetupHintText;
        private Text _runSetupCharacterText;
        private Text _runSetupWeaponText;
        private Text _runSetupBonusText;
        private Text _runSetupSelectionSummaryText;
        private Text _runSetupMapStepSelectionText;
        private Text _runSetupMapLockText;
        private Text _runSetupPrimaryActionText;
        private Text _runSetupMapNextText;
        private Text _runSetupStartText;
        private Text _achievementSummaryText;
        private Text _metaHeaderText;
        private Text _metaRecentText;
        private Text _summaryModalText;
        private Text _metaUnlocksTabText;
        private Text _metaResearchTabText;
        private Text _achievementButtonText;
        private Button _singlePlayButton;
        private Button _multiPlayButton;
        private Button _achievementButton;
        private Button _metaButton;
        private Button _optionsButton;
        private Button _hostButton;
        private Button _joinButton;
        private Button _backButton;
        private Button _optionsBackButton;
        private Button _runSetupCharacterButton;
        private Button _runSetupWeaponButton;
        private Button _runSetupPrimaryActionButton;
        private Button _runSetupMapNextButton;
        private Button _runSetupMapBackButton;
        private Button _runSetupStartButton;
        private Button _runSetupCharacterBackButton;
        private Button _summaryMetaButton;
        private Button _metaResetButton;
        private Button _achievementBackButton;
        private Button _metaUnlocksTabButton;
        private Button _metaResearchTabButton;
        private Button _confirmConfirmButton;
        private Button _confirmCancelButton;
        private Button[] _runSetupCharacterOptionButtons = System.Array.Empty<Button>();
        private Button[] _runSetupWeaponOptionButtons = System.Array.Empty<Button>();
        private Button[] _runSetupMapButtons = System.Array.Empty<Button>();
        private Button[] _runSetupDifficultyButtons = System.Array.Empty<Button>();
        private Text[] _runSetupMapButtonTexts = System.Array.Empty<Text>();
        private Text[] _runSetupDifficultyButtonTexts = System.Array.Empty<Text>();
        private Button[] _metaCharacterButtons = System.Array.Empty<Button>();
        private Button[] _metaUpgradeButtons = System.Array.Empty<Button>();
        private InputField _joinCodeInput;
        private Toggle _fullscreenToggle;
        private bool _suppressDisplayToggleCallback;
        private Slider _masterVolumeSlider;
        private Slider _bgmVolumeSlider;
        private Slider _sfxVolumeSlider;
        private Text _masterVolumeValueText;
        private Text _bgmVolumeValueText;
        private Text _sfxVolumeValueText;
        private bool _suppressAudioSettingsCallbacks;
        private int _selectedCharacterId;
        private int _inspectedCharacterId;
        private WeaponUpgradeId _selectedStarterWeaponId;
        private string _selectedMapId = SharedRunCatalog.DefaultMapId;
        private string _selectedDifficultyId = SharedRunCatalog.DefaultDifficultyId;
        private string _recentRunSummaryText = "최근 전적이 없습니다.";
        private MetaTab _currentMetaTab;
        private SingleRunSetupStep _currentRunSetupStep;
        private GameObject _confirmModal;
        private Text _confirmModalText;
        private Action _pendingConfirmAction;

        private void Awake()
        {
            Debug.Log("[EJR] 1: Awake Start");
            GameplaySpeedService.ApplyMenuTimeState();
            Debug.Log("[EJR] 2: Speed Service Applied");
            _font = RuntimeFontProvider.GetDefaultFont();
            Debug.Log("[EJR] 3: Font Loaded: " + (_font != null ? _font.name : "NULL"));
            MetaProgressionService.EnsureLoaded();
            Debug.Log("[EJR] 4: MetaProgression Loaded");
            _selectedCharacterId = MetaProgressionService.GetSingleSelectedCharacterId();
            _inspectedCharacterId = _selectedCharacterId;
            _selectedStarterWeaponId = MetaProgressionService.GetSingleSelectedStarterWeapon();
            _selectedMapId = RunSelectionService.SingleMapId;
            _selectedDifficultyId = SharedRunCatalog.DefaultDifficultyId;
            _currentRunSetupStep = SingleRunSetupStep.CharacterSelect;
            Debug.Log("[EJR] 5: Selections Initialized");
            InitializeDisplaySettings();
            Debug.Log("[EJR] 6: Display Settings Initialized");
            EnsureCamera();
            Debug.Log("[EJR] 7: Camera Ensured");
            EnsureEventSystem();
            Debug.Log("[EJR] 8: Event System Ensured");
            
            // UI Toolkit을 강제로 끄고 기존 uGUI 방식으로 빌드합니다.
            BuildMenu(); 
            Debug.Log("[EJR] 9: Menu Built (uGUI Forced) - Awake End");
        }

        private void OnEnable()
        {
            DebugSessionService.Changed += HandleDebugSessionChanged;
        }

        private void OnDisable()
        {
            DebugSessionService.Changed -= HandleDebugSessionChanged;
        }

        private void Start()
        {
            AudioService.Instance.PlayMusic(AudioCueId.MainTheme);
            SyncFullscreenToggle();
            SyncAudioSettingsControls();
            if (MetaProgressionService.TryPeekPendingRunSummary(out var summary))
            {
                _recentRunSummaryText = summary.BuildDisplayText();
                MetaProgressionService.ClearPendingRunSummary();
            }

            RefreshRunSetupPanelV2();
            RefreshAchievementButtonState();
            RefreshMetaPanel();
            ShowMainMenu();

            SetStatus("모드를 선택하세요.");
        }

        private void Update()
        {
            if (DebugSessionService.CaptureTypedInput(Input.inputString))
            {
                RefreshToolkitDebugButtonVisibility();
                SetStatus("DEV 기능이 열렸습니다.");
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

            _accentBar = CreatePanel(root.transform, "AccentBar", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -88f), new Vector2(188f, 6f), new Color(0.36f, 0.47f, 0.62f, 0.18f));

            _titleText = CreateText(root.transform, "TitleText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -108f), new Vector2(640f, 70f), "전자오락 원정대", 40, FontStyle.Bold);
            _titleText.alignment = TextAnchor.MiddleLeft;
            _titleText.color = new Color(0.95f, 0.97f, 1f, 1f);

            _subtitleText = CreateText(root.transform, "SubtitleText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(104f, -166f), new Vector2(560f, 42f), "모드를 고르고 바로 시작하세요.", 18, FontStyle.Normal);
            _subtitleText.alignment = TextAnchor.UpperLeft;
            _subtitleText.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            BuildToolkitStateReferences(root.transform);
            BuildToolkitMainMenu();
        }

        private void BuildToolkitStateReferences(Transform parent)
        {
            _mainMenuPanel = CreateToolkitStatePanel(parent, "MainMenuPanelState");
            _multiplayerPanel = CreateToolkitStatePanel(parent, "MultiplayerPanelState");
            _optionsPanel = CreateToolkitStatePanel(parent, "OptionsPanelState");
            _runSetupPanel = CreateToolkitStatePanel(parent, "RunSetupPanelState");
            _achievementPanel = CreateToolkitStatePanel(parent, "AchievementPanelState");
            _metaPanel = CreateToolkitStatePanel(parent, "MetaPanelState");
            _summaryModal = CreateToolkitStatePanel(parent, "SummaryModalState");
            _confirmModal = CreateToolkitStatePanel(parent, "ConfirmModalState");
        }

        private static GameObject CreateToolkitStatePanel(Transform parent, string name)
        {
            var state = new GameObject(name, typeof(RectTransform));
            state.transform.SetParent(parent, false);
            state.SetActive(false);
            return state;
        }

        private void BuildMainMenuPanel(Transform parent)
        {
            _mainMenuPanel = CreatePanel(parent, "MainMenuPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(112f, -24f), new Vector2(1160f, 560f), new Color(0.03f, 0.05f, 0.09f, 0.32f));

            var overviewCard = CreatePanel(_mainMenuPanel.transform, "OverviewCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260f, 0f), new Vector2(496f, 452f), new Color(0.04f, 0.07f, 0.11f, 0.74f));
            var actionCard = CreatePanel(_mainMenuPanel.transform, "ActionCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(268f, 0f), new Vector2(372f, 452f), new Color(0.02f, 0.03f, 0.06f, 0.84f));

            var overviewHeader = CreateText(overviewCard.transform, "OverviewHeader", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -30f), new Vector2(260f, 24f), "작전 브리프", 16, FontStyle.Bold);
            overviewHeader.alignment = TextAnchor.MiddleLeft;
            overviewHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var overviewTitle = CreateText(overviewCard.transform, "OverviewTitle", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -72f), new Vector2(360f, 84f), "아케이드 생존 전투\n공유 성장 진행도", 30, FontStyle.Bold);
            overviewTitle.alignment = TextAnchor.UpperLeft;

            var overviewBody = CreateText(overviewCard.transform, "OverviewBody", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -176f), new Vector2(420f, 116f), "모드를 고르고 시작 장비를 정한 뒤,\n획득한 크레딧을 영구 해금과 연구에 투자합니다.", 18, FontStyle.Normal);
            overviewBody.alignment = TextAnchor.UpperLeft;
            overviewBody.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            var soloCard = CreatePanel(overviewCard.transform, "SoloInfoCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -74f), new Vector2(436f, 80f), new Color(0.09f, 0.13f, 0.19f, 0.86f));
            var soloInfo = CreateText(soloCard.transform, "SoloInfo", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "싱글 플레이\n출격 전에 캐릭터를 고르고,\n고정 시작 무기로 출발합니다.", 17, FontStyle.Bold);
            soloInfo.alignment = TextAnchor.MiddleLeft;
            soloInfo.rectTransform.offsetMin = new Vector2(18f, 10f);
            soloInfo.rectTransform.offsetMax = new Vector2(-18f, -10f);

            var metaCard = CreatePanel(overviewCard.transform, "MetaInfoCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(436f, 92f), new Color(0.11f, 0.08f, 0.03f, 0.86f));
            var metaInfo = CreateText(metaCard.transform, "MetaInfo", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "메타 성장\n크레딧으로 캐릭터를 해금하고,\n영구 강화를 투자합니다.", 17, FontStyle.Bold);
            metaInfo.alignment = TextAnchor.MiddleLeft;
            metaInfo.rectTransform.offsetMin = new Vector2(18f, 10f);
            metaInfo.rectTransform.offsetMax = new Vector2(-18f, -10f);

            var header = CreateText(actionCard.transform, "MainMenuHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(280f, 30f), "모드 선택", 18, FontStyle.Bold);
            header.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var subhead = CreateText(actionCard.transform, "MainMenuSubhead", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(260f, 42f), "런을 시작하거나 메타를 열고,\n화면 설정을 조정할 수 있습니다.", 15, FontStyle.Normal);
            subhead.color = new Color(0.75f, 0.81f, 0.91f, 1f);

            var baseY = 118f;
            _singlePlayButton = CreateButton(actionCard.transform, "SinglePlayButton", new Vector2(0f, baseY), "싱글 플레이", OnSinglePlayClicked, new Vector2(296f, 56f));
            _multiPlayButton = CreateButton(actionCard.transform, "MultiPlayButton", new Vector2(0f, baseY - (ButtonHeight + ButtonSpacing)), "멀티플레이", OnMultiPlayClicked, new Vector2(296f, 56f));
            _metaButton = CreateButton(actionCard.transform, "MetaButton", new Vector2(0f, baseY - ((ButtonHeight + ButtonSpacing) * 2f)), "메타", OnMetaClicked, new Vector2(296f, 56f));
            _optionsButton = CreateButton(actionCard.transform, "OptionsButton", new Vector2(0f, baseY - ((ButtonHeight + ButtonSpacing) * 3f)), "설정", OnOptionsClicked, new Vector2(296f, 56f));
            CreateButton(actionCard.transform, "QuitButton", new Vector2(0f, baseY - ((ButtonHeight + ButtonSpacing) * 4f)), "종료", OnQuitClicked, new Vector2(296f, 56f));
        }

        private void BuildMultiplayerPanel(Transform parent)
        {
            _multiplayerPanel = CreatePanel(parent, "MultiplayerPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(920f, 560f), new Color(0.02f, 0.03f, 0.06f, 0.86f));
            _multiplayerPanel.SetActive(false);

            var title = CreateText(_multiplayerPanel.transform, "MultiplayerTitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(320f, 30f), "멀티플레이", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var desc = CreateText(_multiplayerPanel.transform, "MultiplayerDescription", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(540f, 42f), "호스트는 릴레이 방을 만들고,\n참가는 공유된 방 코드로 접속합니다.", 16, FontStyle.Normal);
            desc.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            desc.alignment = TextAnchor.MiddleCenter;

            var hostCard = CreatePanel(_multiplayerPanel.transform, "HostCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-206f, -8f), new Vector2(332f, 304f), new Color(0.05f, 0.08f, 0.12f, 0.86f));
            var joinCard = CreatePanel(_multiplayerPanel.transform, "JoinCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(206f, -8f), new Vector2(332f, 304f), new Color(0.05f, 0.08f, 0.12f, 0.86f));

            var hostHeader = CreateText(hostCard.transform, "HostHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(200f, 24f), "방 만들기", 18, FontStyle.Bold);
            hostHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            var hostDesc = CreateText(hostCard.transform, "HostDescription", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(252f, 86f), "새 협동 세션을 만들고 생성된 코드를\n다른 플레이어에게 공유합니다.", 16, FontStyle.Normal);
            hostDesc.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            _hostButton = CreateButton(hostCard.transform, "HostButton", new Vector2(0f, -90f), "호스트", OnHostClicked, new Vector2(232f, 54f));

            var joinHeader = CreateText(joinCard.transform, "JoinHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(200f, 24f), "방 참가", 18, FontStyle.Bold);
            joinHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            var joinDesc = CreateText(joinCard.transform, "JoinDescription", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(252f, 52f), "공유된 방 코드를 입력해 접속합니다.", 16, FontStyle.Normal);
            joinDesc.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            var codeLabel = CreateText(joinCard.transform, "JoinCodeLabel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-88f, -132f), new Vector2(176f, 24f), "방 코드", 15, FontStyle.Bold);
            codeLabel.alignment = TextAnchor.MiddleLeft;
            codeLabel.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            _joinCodeInput = CreateInputField(joinCard.transform, "JoinCodeInput", new Vector2(0f, -18f), new Vector2(232f, 46f), string.Empty, "AB12CD");
            _joinButton = CreateButton(joinCard.transform, "JoinButton", new Vector2(0f, -90f), "참가", OnJoinClicked, new Vector2(232f, 54f));
            _backButton = CreateButton(_multiplayerPanel.transform, "BackButton", new Vector2(0f, -212f), "뒤로", ShowMainMenu, new Vector2(240f, 46f));
        }

        private void BuildOptionsPanel(Transform parent)
        {
            BuildOptionsPanelLayout(parent);
#if false
            _optionsPanel = CreatePanel(parent, "OptionsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(640f, 520f), new Color(0.02f, 0.03f, 0.06f, 0.86f));
            _optionsPanel.SetActive(false);

            var title = CreateText(_optionsPanel.transform, "OptionsTitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(320f, 30f), "설정", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var desc = CreateText(_optionsPanel.transform, "OptionsDescription", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(420f, 42f), "기본은 창 모드입니다. 전체 화면으로 전환하면\n현재 디스플레이를 꽉 채웁니다.", 15, FontStyle.Normal);
            desc.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            desc.alignment = TextAnchor.MiddleCenter;

            var displayCard = CreatePanel(_optionsPanel.transform, "DisplayCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(420f, 120f), new Color(0.05f, 0.08f, 0.12f, 0.86f));
            var displayHeader = CreateText(displayCard.transform, "DisplayHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(240f, 24f), "화면", 16, FontStyle.Bold);
            displayHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _fullscreenToggle = CreateToggle(displayCard.transform, "FullscreenToggle", new Vector2(0f, -12f), new Vector2(240f, 36f), "전체 화면", OnFullscreenToggleChanged);
            _optionsBackButton = CreateButton(_optionsPanel.transform, "OptionsBackButton", new Vector2(0f, -128f), "뒤로", ShowMainMenu, new Vector2(240f, 46f));
            var audioCard = CreatePanel(_optionsPanel.transform, "AudioCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(480f, 148f), new Color(0.05f, 0.08f, 0.12f, 0.86f));
            var audioHeader = CreateText(audioCard.transform, "AudioHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(240f, 24f), "오디오", 16, FontStyle.Bold);
            audioHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            CreateSliderControl(audioCard.transform, "MasterVolume", new Vector2(0f, 28f), "마스터", OnMasterVolumeChanged, out _masterVolumeSlider, out _masterVolumeValueText);
            CreateSliderControl(audioCard.transform, "BgmVolume", new Vector2(0f, -8f), "배경음", OnBgmVolumeChanged, out _bgmVolumeSlider, out _bgmVolumeValueText);
            CreateSliderControl(audioCard.transform, "SfxVolume", new Vector2(0f, -44f), "효과음", OnSfxVolumeChanged, out _sfxVolumeSlider, out _sfxVolumeValueText);
        #endif
        }

        private void BuildOptionsPanelLayout(Transform parent)
        {
            _optionsPanel = CreatePanel(parent, "OptionsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(700f, 580f), new Color(0.02f, 0.03f, 0.06f, 0.86f));
            _optionsPanel.SetActive(false);

            var title = CreateText(_optionsPanel.transform, "OptionsTitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(320f, 30f), "\uC124\uC815", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var desc = CreateText(_optionsPanel.transform, "OptionsDescription", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(460f, 42f), "\uAE30\uBCF8\uC740 \uCC3D \uBAA8\uB4DC\uC785\uB2C8\uB2E4. \uC804\uCCB4 \uD654\uBA74\uC73C\uB85C \uC804\uD658\uD558\uBA74\n\uD604\uC7AC \uB514\uC2A4\uD50C\uB808\uC774\uB97C \uAF49 \uCC44\uC6C1\uB2C8\uB2E4.", 15, FontStyle.Normal);
            desc.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            desc.alignment = TextAnchor.MiddleCenter;

            var displayCard = CreatePanel(_optionsPanel.transform, "DisplayCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 92f), new Vector2(460f, 136f), new Color(0.05f, 0.08f, 0.12f, 0.86f));
            var displayHeader = CreateText(displayCard.transform, "DisplayHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(240f, 24f), "\uD654\uBA74", 16, FontStyle.Bold);
            displayHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            _fullscreenToggle = CreateToggle(displayCard.transform, "FullscreenToggle", new Vector2(0f, -8f), new Vector2(270f, 36f), "\uC804\uCCB4 \uD654\uBA74", OnFullscreenToggleChanged);

            var displayHint = CreateText(displayCard.transform, "DisplayHint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(320f, 20f), "\uBCC0\uACBD \uC0AC\uD56D\uC740 \uBC14\uB85C \uC801\uC6A9\uB429\uB2C8\uB2E4.", 13, FontStyle.Normal);
            displayHint.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            var audioCard = CreatePanel(_optionsPanel.transform, "AudioCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(520f, 196f), new Color(0.05f, 0.08f, 0.12f, 0.86f));
            var audioHeader = CreateText(audioCard.transform, "AudioHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(240f, 24f), "\uC624\uB514\uC624", 16, FontStyle.Bold);
            audioHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            CreateSliderControl(audioCard.transform, "MasterVolume", new Vector2(0f, 36f), "\uB9C8\uC2A4\uD130", OnMasterVolumeChanged, out _masterVolumeSlider, out _masterVolumeValueText);
            CreateSliderControl(audioCard.transform, "BgmVolume", new Vector2(0f, -6f), "\uBC30\uACBD\uC74C", OnBgmVolumeChanged, out _bgmVolumeSlider, out _bgmVolumeValueText);
            CreateSliderControl(audioCard.transform, "SfxVolume", new Vector2(0f, -48f), "\uD6A8\uACFC\uC74C", OnSfxVolumeChanged, out _sfxVolumeSlider, out _sfxVolumeValueText);

            _optionsBackButton = CreateButton(_optionsPanel.transform, "OptionsBackButton", new Vector2(0f, -232f), "\uB4A4\uB85C", ShowMainMenu, new Vector2(240f, 46f));
        }

        private void BuildRunSetupPanel(Transform parent)
        {
            _runSetupPanel = CreatePanel(parent, "RunSetupPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(1140f, 684f), new Color(0.02f, 0.03f, 0.06f, 0.88f));
            _runSetupPanel.SetActive(false);

            var title = CreateText(_runSetupPanel.transform, "RunSetupHeader", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(420f, 34f), "출격 준비", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var hint = CreateText(_runSetupPanel.transform, "RunSetupHint", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(640f, 22f), "출격 전에 캐릭터를 선택하세요. 시작 무기는 캐릭터에 고정됩니다.", 14, FontStyle.Normal);
            hint.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            var characterCard = CreatePanel(_runSetupPanel.transform, "CharacterCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-206f, 18f), new Vector2(640f, 440f), new Color(0.05f, 0.08f, 0.12f, 0.88f));
            var weaponCard = CreatePanel(_runSetupPanel.transform, "WeaponCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(332f, 18f), new Vector2(384f, 440f), new Color(0.05f, 0.08f, 0.12f, 0.88f));
            var summaryCard = CreatePanel(_runSetupPanel.transform, "RunSummaryCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), new Vector2(1024f, 100f), new Color(0.08f, 0.11f, 0.15f, 0.92f));

            var characterHeader = CreateText(characterCard.transform, "CharacterHeader", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(220f, 24f), "캐릭터", 17, FontStyle.Bold);
            characterHeader.alignment = TextAnchor.MiddleLeft;
            characterHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _runSetupCharacterText = CreateText(characterCard.transform, "RunSetupCharacterText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -66f), new Vector2(592f, 78f), string.Empty, 22, FontStyle.Bold);
            _runSetupCharacterText.alignment = TextAnchor.UpperLeft;
            _runSetupCharacterOptionsRoot = CreateAnchoredRoot(characterCard.transform, "RunSetupCharacterOptionsRoot", new Vector2(24f, 164f), new Vector2(592f, 520f));

            var weaponHeader = CreateText(weaponCard.transform, "WeaponHeader", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(260f, 24f), "시작 무기", 17, FontStyle.Bold);
            weaponHeader.alignment = TextAnchor.MiddleLeft;
            weaponHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _runSetupWeaponText = CreateText(weaponCard.transform, "RunSetupWeaponText", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -66f), new Vector2(336f, 60f), string.Empty, 22, FontStyle.Bold);
            _runSetupWeaponText.alignment = TextAnchor.UpperLeft;
            _runSetupWeaponText.color = new Color(0.97f, 0.98f, 1f, 1f);
            _runSetupWeaponOptionsRoot = CreateAnchoredRoot(weaponCard.transform, "RunSetupWeaponOptionsRoot", new Vector2(24f, 164f), new Vector2(588f, 240f));

            _runSetupBonusText = CreateText(summaryCard.transform, "RunSetupBonusText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 18, FontStyle.Normal);
            _runSetupBonusText.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            CreateButton(_runSetupPanel.transform, "RunSetupStartButton", new Vector2(-118f, -294f), "시작", StartSinglePlay, new Vector2(220f, 52f));
            CreateButton(_runSetupPanel.transform, "RunSetupBackButton", new Vector2(118f, -294f), "뒤로", ShowMainMenu, new Vector2(220f, 52f));
        }

        private void BuildMetaPanel(Transform parent)
        {
            _metaPanel = CreatePanel(parent, "MetaPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(1120f, 812f), new Color(0.02f, 0.03f, 0.06f, 0.88f));
            _metaPanel.SetActive(false);

            var title = CreateText(_metaPanel.transform, "MetaTitle", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(220f, 32f), "메타", 26, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleLeft;
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            _metaUnlocksTabButton = CreateButton(_metaPanel.transform, "MetaUnlocksTab", new Vector2(240f, -34f), "해금", () => SetMetaTab(MetaTab.Unlocks), new Vector2(172f, 42f));
            _metaResearchTabButton = CreateButton(_metaPanel.transform, "MetaResearchTab", new Vector2(430f, -34f), "연구", () => SetMetaTab(MetaTab.Research), new Vector2(172f, 42f));
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

            var backButton = CreateButton(_metaPanel.transform, "MetaBackButton", new Vector2(0f, -372f), "뒤로", ShowMainMenu, new Vector2(240f, 46f));
            SetBottomCenterRect(backButton.GetComponent<RectTransform>(), new Vector2(0f, 24f), new Vector2(240f, 46f));
        }

        private void BuildSummaryModal(Transform parent)
        {
            _summaryModal = CreatePanel(parent, "SummaryModal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(720f, 420f), new Color(0.02f, 0.03f, 0.06f, 0.92f));
            _summaryModal.SetActive(false);

            var title = CreateText(_summaryModal.transform, "SummaryModalTitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(320f, 30f), "런 결과", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var bodyCard = CreatePanel(_summaryModal.transform, "SummaryBodyCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(620f, 196f), new Color(0.05f, 0.08f, 0.12f, 0.86f));
            _summaryModalText = CreateText(bodyCard.transform, "SummaryModalText", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 18, FontStyle.Normal);
            _summaryModalText.rectTransform.offsetMin = new Vector2(22f, 18f);
            _summaryModalText.rectTransform.offsetMax = new Vector2(-22f, -18f);
            _summaryMetaButton = CreateButton(_summaryModal.transform, "SummaryMetaButton", new Vector2(-116f, -148f), "메타 열기", OpenMetaFromSummary, new Vector2(212f, 46f));
            CreateButton(_summaryModal.transform, "SummaryCloseButton", new Vector2(116f, -148f), "닫기", CloseSummaryModal, new Vector2(212f, 46f));
        }

        private void BuildConfirmModal(Transform parent)
        {
            _confirmModal = CreatePanel(parent, "ConfirmModal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(560f, 260f), new Color(0.02f, 0.03f, 0.06f, 0.95f));
            _confirmModal.SetActive(false);

            var title = CreateText(_confirmModal.transform, "ConfirmTitle", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(240f, 28f), "\uD655\uC778", 22, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            _confirmModalText = CreateText(_confirmModal.transform, "ConfirmBody", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 18, FontStyle.Normal);
            _confirmModalText.rectTransform.offsetMin = new Vector2(28f, 84f);
            _confirmModalText.rectTransform.offsetMax = new Vector2(-28f, -92f);
            _confirmModalText.alignment = TextAnchor.MiddleCenter;

            _confirmConfirmButton = CreateButton(_confirmModal.transform, "ConfirmOkButton", new Vector2(-110f, -84f), "\uD655\uC778", ConfirmPendingAction, new Vector2(180f, 46f));
            _confirmCancelButton = CreateButton(_confirmModal.transform, "ConfirmCancelButton", new Vector2(110f, -84f), "\uCDE8\uC18C", CloseConfirmModal, new Vector2(180f, 46f));
        }

        private void BuildMainMenuPanelReference(Transform parent)
        {
            _mainMenuPanel = CreatePanel(parent, "MainMenuPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(432f, 612f), new Color(0.04f, 0.07f, 0.11f, 0.92f));

            var fallbackTitle = CreateText(_mainMenuPanel.transform, "MainMenuTitleV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(320f, 44f), "\uC804\uC790\uC624\uB77D \uC6D0\uC815\uB300", 34, FontStyle.Bold);
            fallbackTitle.color = new Color(0.95f, 0.97f, 1f, 1f);

            var fallbackBaseY = 118f;
            _singlePlayButton = CreateButton(_mainMenuPanel.transform, "SinglePlayButtonV2", new Vector2(0f, fallbackBaseY), "\uC2F1\uAE00 \uD50C\uB808\uC774", OnSinglePlayClicked, new Vector2(296f, 58f));
            _multiPlayButton = CreateButton(_mainMenuPanel.transform, "MultiPlayButtonV2", new Vector2(0f, fallbackBaseY - (ButtonHeight + ButtonSpacing)), "\uBA40\uD2F0\uD50C\uB808\uC774", OnMultiPlayClicked, new Vector2(296f, 58f));
            _achievementButton = CreateButton(_mainMenuPanel.transform, "AchievementButtonV2", new Vector2(0f, fallbackBaseY - ((ButtonHeight + ButtonSpacing) * 2f)), "\uB3C4\uC804\uACFC\uC81C", OnAchievementsClicked, new Vector2(296f, 58f));
            _achievementButtonText = _achievementButton.GetComponentInChildren<Text>();
            _metaButton = CreateButton(_mainMenuPanel.transform, "MetaButtonV2", new Vector2(0f, fallbackBaseY - ((ButtonHeight + ButtonSpacing) * 3f)), "\uCF54\uC778 \uC0C1\uC810", OnMetaClicked, new Vector2(296f, 58f));
            _optionsButton = CreateButton(_mainMenuPanel.transform, "OptionsButtonV2", new Vector2(0f, fallbackBaseY - ((ButtonHeight + ButtonSpacing) * 4f)), "\uC124\uC815", OnOptionsClicked, new Vector2(296f, 58f));
            CreateButton(_mainMenuPanel.transform, "QuitButtonV2", new Vector2(0f, fallbackBaseY - ((ButtonHeight + ButtonSpacing) * 5f)), "\uC885\uB8CC", OnQuitClicked, new Vector2(296f, 58f));
            return;
        }

#if false

            _mainMenuPanel = CreatePanel(parent, "MainMenuPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(92f, -18f), new Vector2(1240f, 620f), new Color(0.03f, 0.05f, 0.09f, 0.22f));

            var overviewCard = CreatePanel(_mainMenuPanel.transform, "OverviewCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-246f, 0f), new Vector2(696f, 524f), new Color(0.04f, 0.07f, 0.11f, 0.82f));
            var actionCard = CreatePanel(_mainMenuPanel.transform, "ActionCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(348f, 0f), new Vector2(376f, 524f), new Color(0.02f, 0.03f, 0.06f, 0.9f));

            var overviewHeader = CreateText(overviewCard.transform, "OverviewHeaderV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -30f), new Vector2(220f, 24f), "메인 메뉴", 16, FontStyle.Bold);
            overviewHeader.alignment = TextAnchor.MiddleLeft;
            overviewHeader.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            var overviewTitle = CreateText(overviewCard.transform, "OverviewTitleV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -72f), new Vector2(540f, 96f), "바로 출격", 36, FontStyle.Bold);
            overviewTitle.alignment = TextAnchor.UpperLeft;
            overviewTitle.color = new Color(0.98f, 0.98f, 1f, 1f);

            var overviewBody = CreateText(overviewCard.transform, "OverviewBodyV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -180f), new Vector2(584f, 74f), "캐릭터를 고르고 바로 시작하거나,\n강화와 멀티플레이를 관리하세요.", 18, FontStyle.Normal);
            overviewBody.alignment = TextAnchor.UpperLeft;
            overviewBody.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            var soloCard = CreatePanel(overviewCard.transform, "SoloInfoCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(620f, 92f), new Color(0.08f, 0.11f, 0.17f, 0.92f));
            var soloInfo = CreateText(soloCard.transform, "SoloInfoV2", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "싱글 플레이\n선택한 캐릭터로 바로 출격합니다.", 17, FontStyle.Bold);
            soloInfo.alignment = TextAnchor.MiddleLeft;
            soloInfo.rectTransform.offsetMin = new Vector2(20f, 14f);
            soloInfo.rectTransform.offsetMax = new Vector2(-20f, -14f);

            var metaCard = CreatePanel(overviewCard.transform, "MetaInfoCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -158f), new Vector2(620f, 92f), new Color(0.09f, 0.10f, 0.14f, 0.92f));
            var metaInfo = CreateText(metaCard.transform, "MetaInfoV2", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "메타 강화\n캐릭터 해금과 영구 강화를 관리합니다.", 17, FontStyle.Bold);
            metaInfo.alignment = TextAnchor.MiddleLeft;
            metaInfo.rectTransform.offsetMin = new Vector2(20f, 14f);
            metaInfo.rectTransform.offsetMax = new Vector2(-20f, -14f);

            var coopCard = CreatePanel(overviewCard.transform, "CoopInfoCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -270f), new Vector2(620f, 92f), new Color(0.05f, 0.09f, 0.12f, 0.92f));
            var coopInfo = CreateText(coopCard.transform, "CoopInfoV2", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "멀티플레이\n방을 만들거나 코드로 바로 참가합니다.", 17, FontStyle.Bold);
            coopInfo.alignment = TextAnchor.MiddleLeft;
            coopInfo.rectTransform.offsetMin = new Vector2(20f, 14f);
            coopInfo.rectTransform.offsetMax = new Vector2(-20f, -14f);

            var header = CreateText(actionCard.transform, "MainMenuHeaderV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(260f, 30f), "메뉴", 18, FontStyle.Bold);
            header.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            var subhead = CreateText(actionCard.transform, "MainMenuSubheadV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(292f, 52f), "원하는 메뉴를 고르세요.", 15, FontStyle.Normal);
            subhead.color = new Color(0.75f, 0.81f, 0.91f, 1f);

            var baseY = 150f;
            _singlePlayButton = CreateButton(actionCard.transform, "SinglePlayButtonV2", new Vector2(0f, baseY), "싱글 플레이", OnSinglePlayClicked, new Vector2(304f, 58f));
            _multiPlayButton = CreateButton(actionCard.transform, "MultiPlayButtonV2", new Vector2(0f, baseY - (ButtonHeight + ButtonSpacing)), "멀티플레이", OnMultiPlayClicked, new Vector2(304f, 58f));
            _metaButton = CreateButton(actionCard.transform, "MetaButtonV2", new Vector2(0f, baseY - ((ButtonHeight + ButtonSpacing) * 2f)), "코인 상점", OnMetaClicked, new Vector2(304f, 58f));
            _optionsButton = CreateButton(actionCard.transform, "OptionsButtonV2", new Vector2(0f, baseY - ((ButtonHeight + ButtonSpacing) * 3f)), "설정", OnOptionsClicked, new Vector2(304f, 58f));
            CreateButton(actionCard.transform, "QuitButtonV2", new Vector2(0f, baseY - ((ButtonHeight + ButtonSpacing) * 4f)), "종료", OnQuitClicked, new Vector2(304f, 58f));
        }

#endif

        private void BuildMultiplayerPanelReference(Transform parent)
        {
            _multiplayerPanel = CreatePanel(parent, "MultiplayerPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(1000f, 590f), new Color(0.02f, 0.03f, 0.06f, 0.9f));
            _multiplayerPanel.SetActive(false);

            var title = CreateText(_multiplayerPanel.transform, "MultiplayerTitleV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(320f, 30f), "멀티플레이", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var hostCard = CreatePanel(_multiplayerPanel.transform, "HostCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-226f, -12f), new Vector2(372f, 332f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            var joinCard = CreatePanel(_multiplayerPanel.transform, "JoinCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(226f, -12f), new Vector2(372f, 332f), new Color(0.05f, 0.08f, 0.12f, 0.9f));

            var hostHeader = CreateText(hostCard.transform, "HostHeaderV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(240f, 24f), "방 만들기", 18, FontStyle.Bold);
            hostHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _hostButton = CreateButton(hostCard.transform, "HostButtonV2", new Vector2(0f, -70f), "호스트 시작", OnHostClicked, new Vector2(248f, 54f));

            var joinHeader = CreateText(joinCard.transform, "JoinHeaderV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(240f, 24f), "코드 참가", 18, FontStyle.Bold);
            joinHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            var codeLabel = CreateText(joinCard.transform, "JoinCodeLabelV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-90f, -108f), new Vector2(180f, 24f), "방 코드", 15, FontStyle.Bold);
            codeLabel.alignment = TextAnchor.MiddleLeft;
            codeLabel.color = new Color(0.72f, 0.79f, 0.89f, 1f);
            _joinCodeInput = CreateInputField(joinCard.transform, "JoinCodeInputV2", new Vector2(0f, -4f), new Vector2(248f, 48f), string.Empty, "AB12CD");
            _joinButton = CreateButton(joinCard.transform, "JoinButtonV2", new Vector2(0f, -120f), "참가", OnJoinClicked, new Vector2(248f, 54f));
            _backButton = CreateButton(_multiplayerPanel.transform, "BackButtonV2", new Vector2(0f, -242f), "뒤로", ShowMainMenu, new Vector2(248f, 46f));
        }

        private void BuildOptionsPanelReference(Transform parent)
        {
            if (SupportsToolkitOptionsPanel())
            {
                _optionsPanel = new GameObject("OptionsPanelStateV2", typeof(RectTransform));
                _optionsPanel.transform.SetParent(parent, false);
                _optionsPanel.SetActive(false);
                return;
            }

            _optionsPanel = CreatePanel(parent, "OptionsPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(760f, 520f), new Color(0.02f, 0.03f, 0.06f, 0.9f));
            _optionsPanel.SetActive(false);

            var title = CreateText(_optionsPanel.transform, "OptionsTitleV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(320f, 32f), "\uC124\uC815", 26, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var displayCard = CreatePanel(_optionsPanel.transform, "DisplayCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(560f, 112f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            var displayHeader = CreateText(displayCard.transform, "DisplayHeaderV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(220f, 24f), "\uD654\uBA74", 16, FontStyle.Bold);
            displayHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _fullscreenToggle = CreateToggle(displayCard.transform, "FullscreenToggleV2", new Vector2(0f, 2f), new Vector2(248f, 36f), "\uC804\uCCB4 \uD654\uBA74", OnFullscreenToggleChanged);

            var audioCard = CreatePanel(_optionsPanel.transform, "AudioCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(560f, 204f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            var audioHeader = CreateText(audioCard.transform, "AudioHeaderV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(240f, 24f), "\uC624\uB514\uC624", 16, FontStyle.Bold);
            audioHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            CreateSliderControl(audioCard.transform, "MasterVolumeV2", new Vector2(0f, 44f), "\uB9C8\uC2A4\uD130", OnMasterVolumeChanged, out _masterVolumeSlider, out _masterVolumeValueText);
            CreateSliderControl(audioCard.transform, "BgmVolumeV2", new Vector2(0f, 0f), "\uBC30\uACBD\uC74C", OnBgmVolumeChanged, out _bgmVolumeSlider, out _bgmVolumeValueText);
            CreateSliderControl(audioCard.transform, "SfxVolumeV2", new Vector2(0f, -44f), "\uD6A8\uACFC\uC74C", OnSfxVolumeChanged, out _sfxVolumeSlider, out _sfxVolumeValueText);

            _optionsBackButton = CreateButton(_optionsPanel.transform, "OptionsBackButtonV2", new Vector2(0f, -210f), "\uB4A4\uB85C", ShowMainMenu, new Vector2(248f, 46f));
        }

        #if false
        private void BuildRunSetupPanelReference(Transform parent)
        {
            _runSetupPanel = CreatePanel(parent, "RunSetupPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(1240f, 778f), new Color(0.02f, 0.03f, 0.06f, 0.9f));
            _runSetupPanel.SetActive(false);

            var title = CreateText(_runSetupPanel.transform, "RunSetupHeaderV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(420f, 34f), "싱글 플레이", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var rosterCard = CreatePanel(_runSetupPanel.transform, "CharacterRosterCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-244f, 6f), new Vector2(664f, 616f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            var rosterHeader = CreateText(rosterCard.transform, "RosterHeaderV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(240f, 24f), "캐릭터 선택", 17, FontStyle.Bold);
            rosterHeader.alignment = TextAnchor.MiddleLeft;
            rosterHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            CreateScrollViewport(rosterCard.transform, "RunSetupCharacterScrollV2", new Vector2(24f, 68f), new Vector2(608f, 524f), out _runSetupCharacterOptionsContentRect);
            _runSetupCharacterOptionsRoot = _runSetupCharacterOptionsContentRect.gameObject;

            var detailCard = CreatePanel(_runSetupPanel.transform, "RunDetailCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(338f, 6f), new Vector2(436f, 616f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            var detailHeader = CreateText(detailCard.transform, "DetailHeaderV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(220f, 24f), "캐릭터 정보", 17, FontStyle.Bold);
            detailHeader.alignment = TextAnchor.MiddleLeft;
            detailHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            _runSetupCharacterText = CreateText(detailCard.transform, "RunSetupCharacterTextV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -74f), new Vector2(388f, 42f), string.Empty, 24, FontStyle.Bold);
            _runSetupCharacterText.alignment = TextAnchor.UpperLeft;

            var detailViewport = CreateScrollViewport(detailCard.transform, "RunDetailScrollV2", new Vector2(24f, 128f), new Vector2(388f, 232f), out _runSetupDetailContentRect);
            if (detailViewport.TryGetComponent<Image>(out var detailViewportImage))
            {
                detailViewportImage.color = new Color(0.07f, 0.10f, 0.14f, 0.94f);
            }

            _runSetupBonusText = CreateText(_runSetupDetailContentRect.transform, "RunSetupBonusTextV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(352f, 0f), string.Empty, 16, FontStyle.Normal);
            _runSetupBonusText.alignment = TextAnchor.UpperLeft;
            _runSetupBonusText.color = new Color(0.82f, 0.87f, 0.95f, 1f);
            _runSetupWeaponText = null;
            _runSetupSelectionSummaryText = CreateText(detailCard.transform, "RunSetupSelectionSummaryV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -384f), new Vector2(388f, 24f), string.Empty, 15, FontStyle.Bold);
            _runSetupSelectionSummaryText.alignment = TextAnchor.MiddleLeft;
            _runSetupSelectionSummaryText.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var mapHeader = CreateText(detailCard.transform, "RunSetupMapHeaderV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -416f), new Vector2(180f, 20f), "맵", 15, FontStyle.Bold);
            mapHeader.alignment = TextAnchor.MiddleLeft;
            mapHeader.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            var mapDefinitions = SharedRunCatalog.MapDefinitions;
            _runSetupMapButtons = new Button[mapDefinitions.Count];
            _runSetupMapButtonTexts = new Text[mapDefinitions.Count];
            for (var i = 0; i < mapDefinitions.Count; i++)
            {
                var mapId = mapDefinitions[i].Id;
                var button = CreateButton(detailCard.transform, $"RunSetupMapButton{i}", Vector2.zero, mapDefinitions[i].DisplayName, () => SelectSingleMap(mapId), new Vector2(120f, 42f));
                SetTopLeftRect(button.GetComponent<RectTransform>(), new Vector2(24f + (i * 132f), 446f), new Vector2(120f, 42f));
                _runSetupMapButtons[i] = button;
                _runSetupMapButtonTexts[i] = button.GetComponentInChildren<Text>();
                if (_runSetupMapButtonTexts[i] != null)
                {
                    _runSetupMapButtonTexts[i].fontSize = 15;
                }
            }

            _runSetupMapLockText = CreateText(detailCard.transform, "RunSetupMapLockTextV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -498f), new Vector2(388f, 22f), string.Empty, 13, FontStyle.Normal);
            _runSetupMapLockText.alignment = TextAnchor.MiddleLeft;
            _runSetupMapLockText.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            _runSetupDifficultyButtons = System.Array.Empty<Button>();
            _runSetupDifficultyButtonTexts = System.Array.Empty<Text>();

            _runSetupPrimaryActionButton = CreateButton(detailCard.transform, "RunSetupPrimaryActionButtonV2", Vector2.zero, "선택", () => TrySelectOrPurchaseCharacter(_inspectedCharacterId), new Vector2(388f, 48f));
            SetBottomCenterRect(_runSetupPrimaryActionButton.GetComponent<RectTransform>(), new Vector2(0f, 92f), new Vector2(388f, 48f));
            _runSetupPrimaryActionText = _runSetupPrimaryActionButton.GetComponentInChildren<Text>();
            _runSetupPrimaryActionButton.gameObject.SetActive(false);

            _runSetupWeaponOptionsRoot = null;
            _runSetupStartButton = CreateButton(_runSetupPanel.transform, "RunSetupStartButtonV2", new Vector2(-126f, -338f), "출격 시작", StartSinglePlay, new Vector2(240f, 52f));
            _runSetupStartText = _runSetupStartButton.GetComponentInChildren<Text>();
            CreateButton(_runSetupPanel.transform, "RunSetupBackButtonV2", new Vector2(126f, -338f), "뒤로", ShowMainMenu, new Vector2(240f, 52f));
        }

        #endif

        private void BuildRunSetupPanelReference(Transform parent)
        {
            _runSetupPanel = CreatePanel(parent, "RunSetupPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(1240f, 778f), new Color(0.02f, 0.03f, 0.06f, 0.9f));
            _runSetupPanel.SetActive(false);

            _runSetupHeaderText = CreateText(_runSetupPanel.transform, "RunSetupHeaderV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(420f, 34f), "맵 선택", 24, FontStyle.Bold);
            _runSetupHeaderText.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _runSetupHintText = CreateText(_runSetupPanel.transform, "RunSetupHintV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(640f, 22f), "출격할 맵을 먼저 고르세요.", 14, FontStyle.Normal);
            _runSetupHintText.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            _runSetupMapStepRoot = CreateStretchRoot(_runSetupPanel.transform, "RunSetupMapStepRootV2");
            var mapCard = CreatePanel(_runSetupMapStepRoot.transform, "RunSetupMapCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(976f, 560f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            var mapCardTitle = CreateText(mapCard.transform, "RunSetupMapCardTitleV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -34f), new Vector2(320f, 28f), "출격 지역", 18, FontStyle.Bold);
            mapCardTitle.alignment = TextAnchor.MiddleLeft;
            mapCardTitle.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            _runSetupMapStepSelectionText = CreateText(mapCard.transform, "RunSetupMapStepSelectionV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -82f), new Vector2(912f, 72f), string.Empty, 18, FontStyle.Bold);
            _runSetupMapStepSelectionText.alignment = TextAnchor.UpperLeft;
            _runSetupMapStepSelectionText.color = new Color(0.97f, 0.98f, 1f, 1f);

            var mapHeader = CreateText(mapCard.transform, "RunSetupMapHeaderV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -184f), new Vector2(180f, 20f), "맵", 15, FontStyle.Bold);
            mapHeader.alignment = TextAnchor.MiddleLeft;
            mapHeader.color = new Color(0.78f, 0.84f, 0.92f, 1f);

            var mapDefinitions = SharedRunCatalog.MapDefinitions;
            _runSetupMapButtons = new Button[mapDefinitions.Count];
            _runSetupMapButtonTexts = new Text[mapDefinitions.Count];
            for (var i = 0; i < mapDefinitions.Count; i++)
            {
                var mapId = mapDefinitions[i].Id;
                var button = CreateButton(mapCard.transform, $"RunSetupMapButton{i}", Vector2.zero, mapDefinitions[i].DisplayName, () => SelectSingleMap(mapId), new Vector2(280f, 52f));
                SetTopLeftRect(button.GetComponent<RectTransform>(), new Vector2(32f + (i * 300f), 214f), new Vector2(280f, 52f));
                _runSetupMapButtons[i] = button;
                _runSetupMapButtonTexts[i] = button.GetComponentInChildren<Text>();
                if (_runSetupMapButtonTexts[i] != null)
                {
                    _runSetupMapButtonTexts[i].fontSize = 17;
                }
            }

            _runSetupMapLockText = CreateText(mapCard.transform, "RunSetupMapLockTextV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -286f), new Vector2(912f, 22f), string.Empty, 13, FontStyle.Normal);
            _runSetupMapLockText.alignment = TextAnchor.MiddleLeft;
            _runSetupMapLockText.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            _runSetupDifficultyButtons = System.Array.Empty<Button>();
            _runSetupDifficultyButtonTexts = System.Array.Empty<Text>();

            _runSetupMapNextButton = CreateButton(_runSetupMapStepRoot.transform, "RunSetupNextButtonV2", new Vector2(-126f, -338f), "다음", GoToRunSetupCharacterStep, new Vector2(240f, 52f));
            _runSetupMapNextText = _runSetupMapNextButton.GetComponentInChildren<Text>();
            _runSetupMapBackButton = CreateButton(_runSetupMapStepRoot.transform, "RunSetupMapBackButtonV2", new Vector2(126f, -338f), "뒤로", ShowMainMenu, new Vector2(240f, 52f));

            _runSetupCharacterStepRoot = CreateStretchRoot(_runSetupPanel.transform, "RunSetupCharacterStepRootV2");
            var rosterCard = CreatePanel(_runSetupCharacterStepRoot.transform, "CharacterRosterCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-244f, 6f), new Vector2(664f, 616f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            var rosterHeader = CreateText(rosterCard.transform, "RosterHeaderV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(240f, 24f), "캐릭터 선택", 17, FontStyle.Bold);
            rosterHeader.alignment = TextAnchor.MiddleLeft;
            rosterHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);
            CreateScrollViewport(rosterCard.transform, "RunSetupCharacterScrollV2", new Vector2(24f, 68f), new Vector2(608f, 524f), out _runSetupCharacterOptionsContentRect);
            _runSetupCharacterOptionsRoot = _runSetupCharacterOptionsContentRect.gameObject;

            var detailCard = CreatePanel(_runSetupCharacterStepRoot.transform, "RunDetailCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(338f, 6f), new Vector2(436f, 616f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            var detailHeader = CreateText(detailCard.transform, "DetailHeaderV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(220f, 24f), "출격 정보", 17, FontStyle.Bold);
            detailHeader.alignment = TextAnchor.MiddleLeft;
            detailHeader.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            _runSetupSelectionSummaryText = CreateText(detailCard.transform, "RunSetupSelectionSummaryV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -74f), new Vector2(388f, 24f), string.Empty, 15, FontStyle.Bold);
            _runSetupSelectionSummaryText.alignment = TextAnchor.MiddleLeft;
            _runSetupSelectionSummaryText.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            _runSetupCharacterText = CreateText(detailCard.transform, "RunSetupCharacterTextV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -108f), new Vector2(388f, 42f), string.Empty, 24, FontStyle.Bold);
            _runSetupCharacterText.alignment = TextAnchor.UpperLeft;

            var detailViewport = CreateScrollViewport(detailCard.transform, "RunDetailScrollV2", new Vector2(24f, 164f), new Vector2(388f, 316f), out _runSetupDetailContentRect);
            if (detailViewport.TryGetComponent<Image>(out var detailViewportImage))
            {
                detailViewportImage.color = new Color(0.07f, 0.10f, 0.14f, 0.94f);
            }

            _runSetupBonusText = CreateText(_runSetupDetailContentRect.transform, "RunSetupBonusTextV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(352f, 0f), string.Empty, 16, FontStyle.Normal);
            _runSetupBonusText.alignment = TextAnchor.UpperLeft;
            _runSetupBonusText.color = new Color(0.82f, 0.87f, 0.95f, 1f);
            _runSetupWeaponText = null;

            _runSetupPrimaryActionButton = CreateButton(detailCard.transform, "RunSetupPrimaryActionButtonV2", Vector2.zero, "선택", () => TrySelectOrPurchaseCharacter(_inspectedCharacterId), new Vector2(388f, 48f));
            SetBottomCenterRect(_runSetupPrimaryActionButton.GetComponent<RectTransform>(), new Vector2(0f, 92f), new Vector2(388f, 48f));
            _runSetupPrimaryActionText = _runSetupPrimaryActionButton.GetComponentInChildren<Text>();
            _runSetupPrimaryActionButton.gameObject.SetActive(false);

            _runSetupWeaponOptionsRoot = null;
            _runSetupStartButton = CreateButton(_runSetupCharacterStepRoot.transform, "RunSetupStartButtonV2", new Vector2(-126f, -338f), "출격 시작", StartSinglePlay, new Vector2(240f, 52f));
            _runSetupStartText = _runSetupStartButton.GetComponentInChildren<Text>();
            _runSetupCharacterBackButton = CreateButton(_runSetupCharacterStepRoot.transform, "RunSetupBackButtonV2", new Vector2(126f, -338f), "뒤로", ShowMainMenu, new Vector2(240f, 52f));
        }

        private void BuildMetaPanelReference(Transform parent)
        {
            _metaPanel = CreatePanel(parent, "MetaPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(1180f, 820f), new Color(0.02f, 0.03f, 0.06f, 0.9f));
            _metaPanel.SetActive(false);

            var title = CreateText(_metaPanel.transform, "MetaTitleV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(220f, 32f), "코인 상점", 26, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleLeft;
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            _metaUnlocksTabButton = CreateButton(_metaPanel.transform, "MetaUnlocksTabV2", Vector2.zero, "해금", () => SetMetaTab(MetaTab.Unlocks), new Vector2(172f, 44f));
            SetTopLeftRect(_metaUnlocksTabButton.GetComponent<RectTransform>(), new Vector2(780f, 24f), new Vector2(172f, 44f));
            _metaUnlocksTabText = _metaUnlocksTabButton.GetComponentInChildren<Text>();

            _metaResearchTabButton = CreateButton(_metaPanel.transform, "MetaUpgradesTabV2", Vector2.zero, "강화", () => SetMetaTab(MetaTab.Upgrades), new Vector2(172f, 44f));
            SetTopLeftRect(_metaResearchTabButton.GetComponent<RectTransform>(), new Vector2(972f, 24f), new Vector2(172f, 44f));
            _metaResearchTabText = _metaResearchTabButton.GetComponentInChildren<Text>();

            var statsCard = CreatePanel(_metaPanel.transform, "MetaStatsCardV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -92f), new Vector2(1124f, 88f), new Color(0.05f, 0.08f, 0.12f, 0.9f));

            _metaHeaderText = CreateText(statsCard.transform, "MetaHeaderTextV2", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 16, FontStyle.Bold);
            _metaHeaderText.rectTransform.offsetMin = new Vector2(24f, 14f);
            _metaHeaderText.rectTransform.offsetMax = new Vector2(-24f, -14f);
            _metaHeaderText.alignment = TextAnchor.MiddleLeft;
            _metaRecentText = null;

            _metaContentRoot = new GameObject("MetaContentRootV2");
            _metaContentRoot.transform.SetParent(_metaPanel.transform, false);
            var contentRect = _metaContentRoot.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = new Vector2(32f, -160f);
            contentRect.sizeDelta = new Vector2(1116f, 580f);

            var backButton = CreateButton(_metaPanel.transform, "MetaBackButtonV2", new Vector2(0f, -384f), "뒤로", ShowMainMenu, new Vector2(248f, 46f));
            SetBottomCenterRect(backButton.GetComponent<RectTransform>(), new Vector2(0f, 24f), new Vector2(248f, 46f));
        }

        private void BuildAchievementPanelReference(Transform parent)
        {
            _achievementPanel = CreatePanel(parent, "AchievementPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(1180f, 820f), new Color(0.02f, 0.03f, 0.06f, 0.9f));
            _achievementPanel.SetActive(false);

            var title = CreateText(_achievementPanel.transform, "AchievementTitleV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(280f, 32f), "도전과제", 26, FontStyle.Bold);
            title.alignment = TextAnchor.MiddleLeft;
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var summaryCard = CreatePanel(_achievementPanel.transform, "AchievementSummaryCardV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -92f), new Vector2(1124f, 88f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            _achievementSummaryText = CreateText(summaryCard.transform, "AchievementSummaryTextV2", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 16, FontStyle.Bold);
            _achievementSummaryText.rectTransform.offsetMin = new Vector2(24f, 14f);
            _achievementSummaryText.rectTransform.offsetMax = new Vector2(-24f, -14f);
            _achievementSummaryText.alignment = TextAnchor.MiddleLeft;

            var viewport = CreateScrollViewport(_achievementPanel.transform, "AchievementScrollV2", new Vector2(32f, 160f), new Vector2(1116f, 580f), out _achievementContentRect);
            if (viewport.TryGetComponent<Image>(out var viewportImage))
            {
                viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            }

            _achievementBackButton = CreateButton(_achievementPanel.transform, "AchievementBackButtonV2", new Vector2(0f, -384f), "뒤로", ShowMainMenu, new Vector2(248f, 46f));
            SetBottomCenterRect(_achievementBackButton.GetComponent<RectTransform>(), new Vector2(0f, 24f), new Vector2(248f, 46f));
        }

        private void BuildSummaryModalReference(Transform parent)
        {
            _summaryModal = CreatePanel(parent, "SummaryModalV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(820f, 472f), new Color(0.02f, 0.03f, 0.06f, 0.94f));
            _summaryModal.SetActive(false);

            var title = CreateText(_summaryModal.transform, "SummaryModalTitleV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(320f, 30f), "런 결과", 24, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            var bodyCard = CreatePanel(_summaryModal.transform, "SummaryBodyCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(704f, 236f), new Color(0.05f, 0.08f, 0.12f, 0.88f));
            _summaryModalText = CreateText(bodyCard.transform, "SummaryModalTextV2", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 18, FontStyle.Normal);
            _summaryModalText.rectTransform.offsetMin = new Vector2(22f, 20f);
            _summaryModalText.rectTransform.offsetMax = new Vector2(-22f, -20f);
            _summaryModalText.alignment = TextAnchor.UpperLeft;
            _summaryMetaButton = CreateButton(_summaryModal.transform, "SummaryMetaButtonV2", new Vector2(-124f, -168f), "코인 상점", OpenMetaFromSummary, new Vector2(224f, 46f));
            CreateButton(_summaryModal.transform, "SummaryCloseButtonV2", new Vector2(124f, -168f), "닫기", CloseSummaryModal, new Vector2(224f, 46f));
        }

        private void BuildConfirmModalReference(Transform parent)
        {
            _confirmModal = CreatePanel(parent, "ConfirmModalV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(620f, 292f), new Color(0.02f, 0.03f, 0.06f, 0.96f));
            _confirmModal.SetActive(false);

            var title = CreateText(_confirmModal.transform, "ConfirmTitleV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(240f, 28f), "\uD655\uC778", 22, FontStyle.Bold);
            title.color = new Color(0.96f, 0.74f, 0.18f, 1f);

            _confirmModalText = CreateText(_confirmModal.transform, "ConfirmBodyV2", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, string.Empty, 18, FontStyle.Normal);
            _confirmModalText.rectTransform.offsetMin = new Vector2(28f, 84f);
            _confirmModalText.rectTransform.offsetMax = new Vector2(-28f, -92f);
            _confirmModalText.alignment = TextAnchor.MiddleCenter;

            _confirmConfirmButton = CreateButton(_confirmModal.transform, "ConfirmOkButtonV2", new Vector2(-118f, -96f), "\uD655\uC778", ConfirmPendingAction, new Vector2(188f, 46f));
            _confirmCancelButton = CreateButton(_confirmModal.transform, "ConfirmCancelButtonV2", new Vector2(118f, -96f), "\uCDE8\uC18C", CloseConfirmModal, new Vector2(188f, 46f));
        }

        private void OnSinglePlayClicked()
        {
            _selectedCharacterId = MetaProgressionService.GetSingleSelectedCharacterId();
            _inspectedCharacterId = _selectedCharacterId;
            _selectedStarterWeaponId = MetaProgressionService.GetSingleSelectedStarterWeapon();
            _selectedMapId = SharedRunCatalog.DefaultMapId; // 항상 "forest" (숲)으로 고정
            _selectedDifficultyId = SharedRunCatalog.DefaultDifficultyId;
            _currentRunSetupStep = SingleRunSetupStep.CharacterSelect; // 캐릭터 선택으로 바로 진입
            RefreshRunSetupPanelV2();
            ShowPanel(_runSetupPanel, GetRunSetupPreferredSelection());
            SetStatus("캐릭터를 선택하고 출격하세요.");
            return;
        }

        private void OnAchievementsClicked()
        {
            RefreshAchievementPanel();
            ShowPanel(_achievementPanel, _achievementBackButton);
            MetaProgressionService.MarkAchievementsSeen();
            RefreshAchievementButtonState();
            SetStatus("도전과제 목록을 확인하세요.");
        }

        private void OnMetaClicked()
        {
            _currentMetaTab = MetaTab.Unlocks;
            RefreshMetaPanel();
            ShowPanel(_metaPanel, _metaUnlocksTabButton);
            SetStatus("코인 상점에서 캐릭터 해금과 영구 강화를 관리하세요.");
        }

        private void OnOptionsClicked()
        {
            SyncFullscreenToggle();
            SyncAudioSettingsControls();
            ShowPanel(_optionsPanel, HasToolkitOptionsScreen ? null : _fullscreenToggle);
            SetStatus("화면 설정을 여기서 변경합니다.");
        }

        private void OnMultiPlayClicked()
        {
            ShowPanel(_multiplayerPanel, _hostButton);
            UpdateMultiplayerInteractivity();
            SetStatus("릴레이 세션을 만들거나 코드로 참가하세요.");
        }

        private void OnHostClicked() { }
        private void OnJoinClicked() { }



#if false
        private void OnCycleSingleCharacter()
        {
            _selectedCharacterId = MetaProgressionService.GetNextUnlockedCharacterId(_selectedCharacterId);
            MetaProgressionService.SetSingleSelectedCharacterId(_selectedCharacterId);
            RefreshRunSetupPanelV2();
        }

        private void OnCycleSingleStarterWeapon()
        {
            _selectedStarterWeaponId = MetaProgressionService.GetNextUnlockedStarterWeapon(_selectedStarterWeaponId);
            MetaProgressionService.SetSingleSelectedStarterWeapon(_selectedStarterWeaponId);
            RefreshRunSetupPanelV2();
        }

        private void SelectSingleCharacter(int characterId)
        {
            if (!MetaProgressionService.IsCharacterUnlocked(characterId))
            {
                return;
            }

            _selectedCharacterId = characterId;
            MetaProgressionService.SetSingleSelectedCharacterId(characterId);
            RefreshRunSetupPanelV2();
        }

        private void SelectSingleStarterWeapon(WeaponUpgradeId weaponId)
        {
            if (!MetaProgressionService.IsWeaponUnlocked(weaponId))
            {
                return;
            }

            _selectedStarterWeaponId = weaponId;
            MetaProgressionService.SetSingleSelectedStarterWeapon(weaponId);
            RefreshRunSetupPanelV2();
        }

        private void StartSinglePlay()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                SetStatus("게임플레이 씬 이름이 없습니다.");
                return;
            }

            MetaProgressionService.SetSingleSelectedCharacterId(_selectedCharacterId);
            MetaProgressionService.SetSingleSelectedStarterWeapon(_selectedStarterWeaponId);
            GameplaySpeedService.ApplyMenuTimeState();
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
            if (summary == null)
            {
                return;
            }

            if (HasToolkitMainMenu)
            {
                if (_summaryModal != null)
                {
                    _summaryModal.SetActive(true);
                }

                if (_toolkitSummaryModalTextLabel != null)
                {
                    _toolkitSummaryModalTextLabel.text = summary.BuildDisplayText();
                }

                RefreshToolkitModalLayerVisibility();
                return;
            }

            if (_summaryModal == null || _summaryModalText == null)
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
            if (HasToolkitMainMenu)
            {
                if (_summaryModal != null)
                {
                    _summaryModal.SetActive(false);
                }

                RefreshToolkitModalLayerVisibility();
                return;
            }

            if (_summaryModal != null)
            {
                _summaryModal.SetActive(false);
            }

            SetTitleChromeVisible(_mainMenuPanel != null && _mainMenuPanel.activeSelf);
            UpdateToolkitMainMenuVisibility(_mainMenuPanel != null && _mainMenuPanel.activeSelf);
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
            if (HasToolkitMainMenu && activePanel == _mainMenuPanel && _mainMenuPanel != null)
            {
                _mainMenuPanel.SetActive(false);
            }
            SetTitleChromeVisible(activePanel == _mainMenuPanel && (_summaryModal == null || !_summaryModal.activeSelf));
            UpdateToolkitMainMenuVisibility(activePanel == _mainMenuPanel && (_summaryModal == null || !_summaryModal.activeSelf));

            var eventSystem = EventSystem.current;
            if (eventSystem != null && preferredSelection != null && preferredSelection.gameObject.activeInHierarchy)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(preferredSelection.gameObject);
            }

            if (activePanel == _mainMenuPanel)
            {
                FocusToolkitPrimaryButton();
            }
        }

        private void SetTitleChromeVisible(bool visible)
        {
            visible = false;

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
            _runSetupWeaponText.text = $"{weapon.DisplayName}\n해금된 시작 무기";
            _runSetupBonusText.text = $"출격 보너스\n{BuildMetaBonusSummary(MetaProgressionService.GetCombinedRunStartBonuses(_selectedCharacterId))}";

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
                "프로필\n" +
                $"크레딧 {MetaProgressionService.CurrentCredits} | 누적 획득 {MetaProgressionService.TotalCreditsEarned}\n" +
                $"플레이 {MetaProgressionService.RunsPlayed} | 클리어 {MetaProgressionService.RunsCleared}\n" +
                $"최고 레벨 {MetaProgressionService.BestLevel} | 최고 시간 {MetaProgressionService.BestTimeSeconds:0.0}초 | 처치 {MetaProgressionService.TotalEnemiesDefeated}";
            _metaRecentText.text = $"최근 전적\n{_recentRunSummaryText}";
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
            CreateSectionText(_metaContentRoot.transform, "CharactersHeader", new Vector2(0f, 0f), "캐릭터");
            CreateSectionText(_metaContentRoot.transform, "WeaponsHeader", new Vector2(536f, 0f), "무기");

            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                var unlocked = MetaProgressionService.IsCharacterUnlocked(definition.Id);
                var affordable = MetaProgressionService.CurrentCredits >= definition.UnlockCost;
                var label = unlocked
                    ? $"{definition.DisplayName}\n해금됨 | {BuildMetaBonusSummary(definition.TraitBonuses)}"
                    : $"{definition.DisplayName}\n비용 {definition.UnlockCost} | {BuildMetaBonusSummary(definition.TraitBonuses)}";
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
                var label = unlocked ? $"{definition.DisplayName}\n해금됨" : $"{definition.DisplayName}\n비용 {definition.UnlockCost}";
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
                var state = purchased ? "연구 완료" : missingPrereq ? $"선행: {GetNodeTitle(definition.PrerequisiteId)}" : $"비용 {definition.Cost}";
                var label = $"{definition.Title}\n{definition.Description}\n{state}";
                CreateMetaEntryButton(_metaContentRoot.transform, $"Node{i}", new Vector2(x, y), new Vector2(488f, 52f), label, interactable, () => TryPurchaseNode(definition.Id));
            }
        }

        private void TryPurchaseCharacter(int characterId)
        {
            if (MetaProgressionService.TryPurchaseCharacter(characterId, out var reason))
            {
                RefreshMetaPanel();
                SetStatus($"{SharedGameCatalog.GetCharacter(characterId).DisplayName} 해금 완료.");
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
                SetStatus($"{SharedGameCatalog.GetWeaponDisplayName(weaponId)} 해금 완료.");
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
                RefreshRunSetupPanelV2();
                SetStatus($"{GetNodeTitle(nodeId)} 연구 완료.");
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

#endif

        private void TrySelectOrPurchaseCharacter(int characterId)
        {
            SelectSingleCharacter(characterId);
        }

        private void SelectSingleCharacter(int characterId)
        {
            characterId = SharedGameCatalog.NormalizeCharacterId(characterId);
            if (!MetaProgressionService.IsCharacterUnlocked(characterId))
            {
                return;
            }

            var definition = SharedGameCatalog.GetCharacter(characterId);
            _selectedCharacterId = characterId;
            _inspectedCharacterId = characterId;
            _selectedStarterWeaponId = definition.StarterWeaponId;
            MetaProgressionService.SetSingleSelectedCharacterId(characterId);
            RefreshRunSetupPanelV2();
        }

        private void SelectSingleMap(string mapId)
        {
            var definition = SharedRunCatalog.GetMap(mapId);
            if (!SharedRunCatalog.IsMapUnlocked(definition.Id))
            {
                return;
            }

            _selectedMapId = definition.Id;
            RefreshRunSetupPanelV2();
        }

        private void SelectSingleDifficulty(string difficultyId)
        {
            _selectedDifficultyId = SharedRunCatalog.DefaultDifficultyId;
            RefreshRunSetupPanelV2();
        }

        private void GoToRunSetupCharacterStep()
        {
            if (!SharedRunCatalog.IsMapUnlocked(_selectedMapId))
            {
                SetStatus("선택한 맵이 아직 잠겨 있습니다.");
                return;
            }

            _currentRunSetupStep = SingleRunSetupStep.CharacterSelect;
            RefreshRunSetupPanelV2();
            if (_runSetupPanel != null && _runSetupPanel.activeSelf)
            {
                ShowPanel(_runSetupPanel, GetRunSetupPreferredSelection());
            }

            SetStatus("캐릭터를 선택하고 출격하세요.");
        }

        private void GoToRunSetupMapStep()
        {
            _currentRunSetupStep = SingleRunSetupStep.MapSelect;
            RefreshRunSetupPanelV2();
            if (_runSetupPanel != null && _runSetupPanel.activeSelf)
            {
                ShowPanel(_runSetupPanel, GetRunSetupPreferredSelection());
            }

            SetStatus("맵을 먼저 선택하세요.");
        }

        private void SelectSingleStarterWeapon(WeaponUpgradeId weaponId)
        {
            _selectedStarterWeaponId = weaponId;
            RefreshRunSetupPanelV2();
        }

        private void StartSinglePlay()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                SetStatus("\uAC8C\uC784 \uC2DC\uC791 \uC2EC \uC774\uB984\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.");
                return;
            }

            if (!MetaProgressionService.IsCharacterUnlocked(_selectedCharacterId))
            {
                SetStatus("\uBA3C\uC800 \uD574\uAE08\uB41C \uCE90\uB9AD\uD130\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            if (!SharedRunCatalog.IsMapUnlocked(_selectedMapId))
            {
                SetStatus("선택한 맵이 아직 잠겨 있습니다.");
                return;
            }

            MetaProgressionService.SetSingleSelectedCharacterId(_selectedCharacterId);
            RunSelectionService.SetSingleSelection(_selectedMapId);
            GameplaySpeedService.ApplyMenuTimeState();
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

        private void OpenConfirmModal(string message, Action confirmAction)
        {
            if (HasToolkitMainMenu)
            {
                _pendingConfirmAction = confirmAction;
                if (_confirmModal != null)
                {
                    _confirmModal.SetActive(true);
                }

                if (_toolkitConfirmModalTextLabel != null)
                {
                    _toolkitConfirmModalTextLabel.text = message ?? string.Empty;
                }

                RefreshToolkitModalLayerVisibility();
                return;
            }

            if (_confirmModal == null || _confirmModalText == null)
            {
                confirmAction?.Invoke();
                return;
            }

            _pendingConfirmAction = confirmAction;
            _confirmModalText.text = message ?? string.Empty;
            _confirmModal.SetActive(true);
            var eventSystem = EventSystem.current;
            if (eventSystem != null && _confirmConfirmButton != null)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_confirmConfirmButton.gameObject);
            }
        }

        private void CloseConfirmModal()
        {
            _pendingConfirmAction = null;
            if (HasToolkitMainMenu)
            {
                if (_confirmModal != null)
                {
                    _confirmModal.SetActive(false);
                }

                RefreshToolkitModalLayerVisibility();
                return;
            }

            if (_confirmModal != null)
            {
                _confirmModal.SetActive(false);
            }
        }

        private void ConfirmPendingAction()
        {
            var action = _pendingConfirmAction;
            CloseConfirmModal();
            action?.Invoke();
        }

        private void ShowMainMenu()
        {
            RefreshAchievementButtonState();
            RefreshToolkitDebugButtonVisibility();
            ShowPanel(_mainMenuPanel, _singlePlayButton);
            UpdateMultiplayerInteractivity();
        }

        private void OnDevClicked()
        {
            if (DebugSessionService.IsUnlocked)
            {
                DebugSessionService.ToggleOverlay();
                RefreshToolkitDebugButtonVisibility();
                return;
            }
            SetStatus("DEV 기능은 싱글 플레이 중 사용할 수 있습니다.");
        }

        private void ShowPanel(GameObject activePanel, Selectable preferredSelection)
        {
            if (_mainMenuPanel != null) _mainMenuPanel.SetActive(activePanel == _mainMenuPanel);
            if (_multiplayerPanel != null) _multiplayerPanel.SetActive(activePanel == _multiplayerPanel);
            if (_optionsPanel != null) _optionsPanel.SetActive(activePanel == _optionsPanel);
            if (_runSetupPanel != null) _runSetupPanel.SetActive(activePanel == _runSetupPanel);
            if (_achievementPanel != null) _achievementPanel.SetActive(activePanel == _achievementPanel);
            if (_metaPanel != null) _metaPanel.SetActive(activePanel == _metaPanel);
            if (HasToolkitMainMenu)
            {
                UpdateToolkitScreenVisibility(activePanel);
            }

            var showToolkitMainMenu = activePanel == _mainMenuPanel;
            SetTitleChromeVisible(showToolkitMainMenu);

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
            if (_runSetupCharacterText == null || _runSetupCharacterOptionsRoot == null)
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

            _selectedMapId = SharedRunCatalog.IsMapUnlocked(_selectedMapId)
                ? SharedRunCatalog.GetMap(_selectedMapId).Id
                : GetFirstUnlockedMapId();
            _selectedStarterWeaponId = MetaProgressionService.GetCharacterStarterWeapon(_selectedCharacterId);
            _inspectedCharacterId = _selectedCharacterId;
            var character = SharedGameCatalog.GetCharacter(_selectedCharacterId);
            var selectedMap = SharedRunCatalog.GetMap(_selectedMapId);
            var selectedUnlocked = MetaProgressionService.IsCharacterUnlocked(_selectedCharacterId);
            var selectedMapUnlocked = SharedRunCatalog.IsMapUnlocked(selectedMap.Id);

            _runSetupCharacterText.text = character.DisplayName;
            _runSetupCharacterText.color = character.Color;

            if (_runSetupSelectionSummaryText != null)
            {
                _runSetupSelectionSummaryText.text = $"현재 출격: {selectedMap.DisplayName}";
            }

            if (_runSetupMapLockText != null)
            {
                _runSetupMapLockText.text = BuildRunSetupMapLockText();
            }

            if (_runSetupBonusText != null)
            {
                _runSetupBonusText.text =
                    "잠긴 캐릭터는 상점 해금\n\n" +
                    $"시작 무기\n{SharedGameCatalog.GetWeaponDisplayName(character.StarterWeaponId)}\n\n" +
                    $"기본 보너스\n{BuildMetaBonusSummary(character.BaseBonuses)}\n\n" +
                    $"고유 특성\n{character.PassiveDescription}";
            }

            if (_runSetupBonusText != null)
            {
                _runSetupBonusText.text =
                    "잠긴 캐릭터는 상점 또는 도전과제로 해금\n\n" +
                    $"시작 무기\n{SharedGameCatalog.GetWeaponDisplayName(character.StarterWeaponId)}\n\n" +
                    $"기본 보너스\n{BuildMetaBonusSummary(character.BaseBonuses)}\n\n" +
                    $"고유 특성\n{character.PassiveDescription}";
            }

            if (_runSetupPrimaryActionButton != null)
            {
                _runSetupPrimaryActionButton.gameObject.SetActive(false);
            }

            if (_runSetupStartButton != null)
            {
                _runSetupStartButton.interactable = selectedUnlocked && selectedMapUnlocked;
                if (_runSetupStartText != null)
                {
                    _runSetupStartText.text = selectedUnlocked && selectedMapUnlocked
                        ? "\uCD9C\uACA9 \uC2DC\uC791"
                        : (!selectedUnlocked ? "\uD574\uAE08 \uD6C4 \uCD9C\uACA9" : "맵 해금 후 출격");
                }
            }

            ClearChildren(_runSetupCharacterOptionsRoot.transform);
            if (_runSetupWeaponOptionsRoot != null)
            {
                ClearChildren(_runSetupWeaponOptionsRoot.transform);
            }

            var buttons = new List<Button>();
            _runSetupCharacterButton = null;
            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                var unlocked = MetaProgressionService.IsCharacterUnlocked(definition.Id);
                var selected = definition.Id == _selectedCharacterId;
                var state = unlocked
                    ? (selected ? "\uC120\uD0DD\uB428" : "사용 가능")
                    : "잠김 · 상점 해금";
                state = BuildRunSetupCharacterState(definition, unlocked, selected);
                var label =
                    $"{definition.DisplayName}  |  {SharedGameCatalog.GetWeaponDisplayName(definition.StarterWeaponId)}\n" +
                    state;
                var button = CreateMetaEntryButton(
                    _runSetupCharacterOptionsRoot.transform,
                    $"RunSetupCharacter{definition.Id}",
                    new Vector2(0f, i * 82f),
                    new Vector2(608f, 74f),
                    label,
                    unlocked,
                    () => SelectSingleCharacter(definition.Id));
                var labelText = button.GetComponentInChildren<Text>();
                if (labelText != null)
                {
                    labelText.fontStyle = FontStyle.Bold;
                }

                ApplyRunSetupOptionState(button, labelText, selected, definition.Color);
                if (!unlocked && labelText != null)
                {
                    labelText.color = new Color(0.72f, 0.75f, 0.82f, 1f);
                }

                if (selected)
                {
                    _runSetupCharacterButton = button;
                }

                buttons.Add(button);
            }

            for (var i = 0; i < _runSetupMapButtons.Length && i < SharedRunCatalog.MapDefinitions.Count; i++)
            {
                var definition = SharedRunCatalog.MapDefinitions[i];
                var button = _runSetupMapButtons[i];
                var label = _runSetupMapButtonTexts[i];
                var unlocked = SharedRunCatalog.IsMapUnlocked(definition.Id);
                var selected = string.Equals(definition.Id, _selectedMapId, StringComparison.Ordinal);
                if (label != null)
                {
                    label.text = unlocked ? definition.DisplayName : $"{definition.DisplayName}\n잠김";
                }

                ApplyRunSetupOptionState(button, label, selected && unlocked, definition.BoundaryColor);
                if (button != null)
                {
                    button.interactable = unlocked;
                }

                if (!unlocked && label != null)
                {
                    label.color = new Color(0.72f, 0.75f, 0.82f, 1f);
                }
            }

            for (var i = 0; i < _runSetupDifficultyButtons.Length && i < SharedRunCatalog.DifficultyDefinitions.Count; i++)
            {
                var definition = SharedRunCatalog.DifficultyDefinitions[i];
                var button = _runSetupDifficultyButtons[i];
                var label = _runSetupDifficultyButtonTexts[i];
                var selected = string.Equals(definition.Id, _selectedDifficultyId, StringComparison.Ordinal);
                if (label != null)
                {
                    label.text = definition.DisplayName;
                }

                ApplyRunSetupOptionState(button, label, selected, new Color(0.96f, 0.74f, 0.18f, 1f));
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                }
            }

            _runSetupCharacterOptionButtons = buttons.ToArray();
            _runSetupWeaponOptionButtons = System.Array.Empty<Button>();
            RefreshRunSetupScrollContent();
            UpdateMultiplayerInteractivity();
        }
        private Selectable GetRunSetupPreferredSelection()
        {
            if (_currentRunSetupStep == SingleRunSetupStep.MapSelect)
            {
                var selectedMapIndex = SharedRunCatalog.GetMapIndex(_selectedMapId);
                if (selectedMapIndex >= 0 && selectedMapIndex < _runSetupMapButtons.Length)
                {
                    var selectedMapButton = _runSetupMapButtons[selectedMapIndex];
                    if (selectedMapButton != null && selectedMapButton.IsActive() && selectedMapButton.interactable)
                    {
                        return selectedMapButton;
                    }
                }

                if (_runSetupMapNextButton != null && _runSetupMapNextButton.IsActive() && _runSetupMapNextButton.interactable)
                {
                    return _runSetupMapNextButton;
                }

                return _runSetupMapBackButton;
            }

            if (_runSetupCharacterButton != null && _runSetupCharacterButton.IsActive() && _runSetupCharacterButton.interactable)
            {
                return _runSetupCharacterButton;
            }

            if (_runSetupStartButton != null && _runSetupStartButton.IsActive() && _runSetupStartButton.interactable)
            {
                return _runSetupStartButton;
            }

            return _runSetupCharacterBackButton;
        }

        private void RefreshRunSetupPanelV2()
        {
            if (HasToolkitMainMenu)
            {
                RefreshToolkitRunSetupPanel();
            }

            if (_runSetupCharacterText == null || _runSetupCharacterOptionsRoot == null || _runSetupMapStepRoot == null || _runSetupCharacterStepRoot == null)
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

            _selectedMapId = SharedRunCatalog.IsMapUnlocked(_selectedMapId)
                ? SharedRunCatalog.GetMap(_selectedMapId).Id
                : GetFirstUnlockedMapId();
            _selectedDifficultyId = SharedRunCatalog.DefaultDifficultyId;

            _selectedStarterWeaponId = MetaProgressionService.GetCharacterStarterWeapon(_selectedCharacterId);
            _inspectedCharacterId = _selectedCharacterId;

            var character = SharedGameCatalog.GetCharacter(_selectedCharacterId);
            var selectedMap = SharedRunCatalog.GetMap(_selectedMapId);
            var selectedUnlocked = MetaProgressionService.IsCharacterUnlocked(_selectedCharacterId);
            var selectedMapUnlocked = SharedRunCatalog.IsMapUnlocked(selectedMap.Id);
            var isMapStep = _currentRunSetupStep == SingleRunSetupStep.MapSelect;

            _runSetupMapStepRoot.SetActive(isMapStep);
            _runSetupCharacterStepRoot.SetActive(!isMapStep);

            if (_runSetupHeaderText != null)
            {
                _runSetupHeaderText.text = isMapStep ? "맵 선택" : "캐릭터 선택";
            }

            if (_runSetupHintText != null)
            {
                _runSetupHintText.text = isMapStep
                    ? "출격할 맵을 먼저 고르세요."
                    : "출격할 캐릭터를 선택하세요.";
            }

            if (_runSetupMapStepSelectionText != null)
            {
                _runSetupMapStepSelectionText.text =
                    $"{selectedMap.DisplayName}\n" +
                    $"전장 {selectedMap.ArenaBounds.width:0} x {selectedMap.ArenaBounds.height:0}";
            }

            _runSetupCharacterText.text = character.DisplayName;
            _runSetupCharacterText.color = character.Color;

            if (_runSetupSelectionSummaryText != null)
            {
                _runSetupSelectionSummaryText.text = $"현재 출격: {selectedMap.DisplayName}";
            }

            if (_runSetupMapLockText != null)
            {
                _runSetupMapLockText.text = BuildRunSetupMapLockText();
            }

            if (_runSetupBonusText != null)
            {
                _runSetupBonusText.text =
                    "잠긴 캐릭터는 상점에서 해금\n\n" +
                    $"시작 무기\n{SharedGameCatalog.GetWeaponDisplayName(character.StarterWeaponId)}\n\n" +
                    $"기본 보너스\n{BuildMetaBonusSummary(character.BaseBonuses)}\n\n" +
                    $"고유 특성\n{character.PassiveDescription}";
            }

            if (_runSetupPrimaryActionButton != null)
            {
                _runSetupPrimaryActionButton.gameObject.SetActive(false);
            }

            if (_runSetupMapNextButton != null)
            {
                _runSetupMapNextButton.interactable = selectedMapUnlocked;
            }

            if (_runSetupMapNextText != null)
            {
                _runSetupMapNextText.text = selectedMapUnlocked ? "다음" : "맵 해금 필요";
            }

            if (_runSetupStartButton != null)
            {
                _runSetupStartButton.interactable = selectedUnlocked && selectedMapUnlocked;
                if (_runSetupStartText != null)
                {
                    _runSetupStartText.text = selectedUnlocked && selectedMapUnlocked
                        ? "출격 시작"
                        : (!selectedUnlocked ? "해금 후 출격" : "맵 해금 후 출격");
                }
            }

            ClearChildren(_runSetupCharacterOptionsRoot.transform);
            if (_runSetupWeaponOptionsRoot != null)
            {
                ClearChildren(_runSetupWeaponOptionsRoot.transform);
            }

            var buttons = new List<Button>();
            _runSetupCharacterButton = null;
            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                var unlocked = MetaProgressionService.IsCharacterUnlocked(definition.Id);
                var selected = definition.Id == _selectedCharacterId;
                var state = unlocked
                    ? (selected ? "선택됨" : "사용 가능")
                    : "상점 해금";
                var label =
                    $"{definition.DisplayName}  |  {SharedGameCatalog.GetWeaponDisplayName(definition.StarterWeaponId)}\n" +
                    state;
                var button = CreateMetaEntryButton(
                    _runSetupCharacterOptionsRoot.transform,
                    $"RunSetupCharacter{definition.Id}",
                    new Vector2(0f, i * 82f),
                    new Vector2(608f, 74f),
                    label,
                    unlocked,
                    () => SelectSingleCharacter(definition.Id));
                var labelText = button.GetComponentInChildren<Text>();
                if (labelText != null)
                {
                    labelText.fontStyle = FontStyle.Bold;
                }

                ApplyRunSetupOptionState(button, labelText, selected, definition.Color);
                if (!unlocked && labelText != null)
                {
                    labelText.color = new Color(0.72f, 0.75f, 0.82f, 1f);
                }

                if (selected)
                {
                    _runSetupCharacterButton = button;
                }

                buttons.Add(button);
            }

            for (var i = 0; i < _runSetupMapButtons.Length && i < SharedRunCatalog.MapDefinitions.Count; i++)
            {
                var definition = SharedRunCatalog.MapDefinitions[i];
                var button = _runSetupMapButtons[i];
                var label = _runSetupMapButtonTexts[i];
                var unlocked = SharedRunCatalog.IsMapUnlocked(definition.Id);
                var selected = string.Equals(definition.Id, _selectedMapId, StringComparison.Ordinal);
                if (label != null)
                {
                    label.text = unlocked ? definition.DisplayName : $"{definition.DisplayName}\n잠김";
                }

                ApplyRunSetupOptionState(button, label, selected && unlocked, definition.BoundaryColor);
                if (button != null)
                {
                    button.interactable = unlocked;
                }

                if (!unlocked && label != null)
                {
                    label.color = new Color(0.72f, 0.75f, 0.82f, 1f);
                }
            }

            for (var i = 0; i < _runSetupDifficultyButtons.Length && i < SharedRunCatalog.DifficultyDefinitions.Count; i++)
            {
                var definition = SharedRunCatalog.DifficultyDefinitions[i];
                var button = _runSetupDifficultyButtons[i];
                var label = _runSetupDifficultyButtonTexts[i];
                var selected = string.Equals(definition.Id, _selectedDifficultyId, StringComparison.Ordinal);
                if (label != null)
                {
                    label.text = definition.DisplayName;
                }

                ApplyRunSetupOptionState(button, label, selected, new Color(0.96f, 0.74f, 0.18f, 1f));
                if (button != null)
                {
                    button.gameObject.SetActive(false);
                }
            }

            _runSetupCharacterOptionButtons = buttons.ToArray();
            _runSetupWeaponOptionButtons = System.Array.Empty<Button>();
            RefreshRunSetupScrollContent();
            UpdateMultiplayerInteractivity();
        }

        private void RefreshRunSetupScrollContent()
        {
            if (_runSetupCharacterOptionsContentRect != null)
            {
                var contentHeight = Mathf.Max(524f, SharedGameCatalog.CharacterDefinitions.Count * 82f);
                _runSetupCharacterOptionsContentRect.sizeDelta = new Vector2(608f, contentHeight);
            }

            if (_runSetupDetailContentRect == null || _runSetupBonusText == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            var preferredHeight = Mathf.CeilToInt(_runSetupBonusText.preferredHeight) + 36f;
            _runSetupBonusText.rectTransform.sizeDelta = new Vector2(352f, preferredHeight);
            _runSetupDetailContentRect.sizeDelta = new Vector2(388f, Mathf.Max(316f, preferredHeight + 36f));
        }

        private static string GetFirstUnlockedMapId()
        {
            for (var i = 0; i < SharedRunCatalog.MapDefinitions.Count; i++)
            {
                var definition = SharedRunCatalog.MapDefinitions[i];
                if (SharedRunCatalog.IsMapUnlocked(definition.Id))
                {
                    return definition.Id;
                }
            }

            return SharedRunCatalog.DefaultMapId;
        }

        private static string BuildRunSetupMapLockText()
        {
            for (var i = 0; i < SharedRunCatalog.MapDefinitions.Count; i++)
            {
                var definition = SharedRunCatalog.MapDefinitions[i];
                if (SharedRunCatalog.IsMapUnlocked(definition.Id))
                {
                    continue;
                }

                return $"{definition.DisplayName}: {SharedRunCatalog.GetMapUnlockRequirementText(definition.Id)}";
            }

            return "모든 맵 해금 완료";
        }

        private static string BuildRunSetupCharacterState(SharedCharacterDefinition definition, bool unlocked, bool selected)
        {
            if (unlocked)
            {
                return selected ? "선택됨" : "사용 가능";
            }

            return GetLockedCharacterStatus(definition);
        }

        private static string GetLockedCharacterStatus(SharedCharacterDefinition definition)
        {
            return definition.UnlockSource == CharacterUnlockSource.Achievement
                ? "도전과제 해금"
                : "상점 해금";
        }

        private void RefreshAchievementButtonState()
        {
            var label = MetaProgressionService.HasUnseenAchievements ? "도전과제 NEW" : "도전과제";
            if (_achievementButtonText != null)
            {
                _achievementButtonText.text = label;
            }

            if (_toolkitAchievementButton != null)
            {
                _toolkitAchievementButton.text = label;
            }
        }

        private void RefreshAchievementPanel()
        {
            if (HasToolkitMainMenu)
            {
                RefreshToolkitAchievementPanel();
            }

            if (_achievementSummaryText == null || _achievementContentRect == null)
            {
                return;
            }

            ClearChildren(_achievementContentRect.transform);

            var entries = MetaProgressionService.GetAchievementEntries();
            var completedCount = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.IsCompleted)
                {
                    completedCount++;
                }

                var rowTop = i * 104f;
                var row = CreatePanel(
                    _achievementContentRect.transform,
                    $"AchievementRow{entry.Id}",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, -rowTop),
                    new Vector2(1116f, 92f),
                    entry.IsCompleted
                        ? new Color(0.07f, 0.11f, 0.16f, 0.96f)
                        : new Color(0.08f, 0.10f, 0.14f, 0.96f));

                CreatePanel(
                    row.transform,
                    $"AchievementAccent{entry.Id}",
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0.5f),
                    Vector2.zero,
                    new Vector2(6f, 0f),
                    entry.IsCompleted ? new Color(0.32f, 0.72f, 0.48f, 1f) : new Color(0.42f, 0.50f, 0.62f, 1f));

                var titleText = CreateText(
                    row.transform,
                    $"AchievementTitle{entry.Id}",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(20f, -12f),
                    new Vector2(520f, 22f),
                    entry.DisplayName,
                    16,
                    FontStyle.Bold);
                titleText.alignment = TextAnchor.MiddleLeft;
                titleText.color = entry.IsCompleted ? new Color(0.94f, 0.98f, 0.94f, 1f) : new Color(0.97f, 0.98f, 1f, 1f);

                var statusText = CreateText(
                    row.transform,
                    $"AchievementStatus{entry.Id}",
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-20f, -12f),
                    new Vector2(220f, 22f),
                    entry.IsCompleted ? "달성 완료" : "진행 중",
                    14,
                    FontStyle.Bold);
                statusText.alignment = TextAnchor.MiddleRight;
                statusText.color = entry.IsCompleted ? new Color(0.46f, 0.88f, 0.58f, 1f) : new Color(0.96f, 0.74f, 0.18f, 1f);

                if (entry.IsNew)
                {
                    var newText = CreateText(
                        row.transform,
                        $"AchievementNew{entry.Id}",
                        new Vector2(1f, 1f),
                        new Vector2(1f, 1f),
                        new Vector2(1f, 1f),
                        new Vector2(-248f, -12f),
                        new Vector2(80f, 20f),
                        "NEW",
                        12,
                        FontStyle.Bold);
                    newText.alignment = TextAnchor.MiddleRight;
                    newText.color = new Color(1f, 0.84f, 0.36f, 1f);
                }

                var descriptionText = CreateText(
                    row.transform,
                    $"AchievementDescription{entry.Id}",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(20f, -38f),
                    new Vector2(760f, 20f),
                    entry.Description,
                    14,
                    FontStyle.Normal);
                descriptionText.alignment = TextAnchor.MiddleLeft;
                descriptionText.color = new Color(0.82f, 0.87f, 0.95f, 1f);

                var progressText = CreateText(
                    row.transform,
                    $"AchievementProgress{entry.Id}",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(20f, -64f),
                    new Vector2(320f, 18f),
                    $"진행도 {entry.ProgressText}",
                    13,
                    FontStyle.Normal);
                progressText.alignment = TextAnchor.MiddleLeft;
                progressText.color = new Color(0.72f, 0.79f, 0.89f, 1f);

                var rewardText = CreateText(
                    row.transform,
                    $"AchievementReward{entry.Id}",
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-20f, -64f),
                    new Vector2(460f, 18f),
                    $"보상 {entry.RewardText}",
                    13,
                    FontStyle.Normal);
                rewardText.alignment = TextAnchor.MiddleRight;
                rewardText.color = new Color(0.72f, 0.79f, 0.89f, 1f);
            }

            _achievementContentRect.sizeDelta = new Vector2(1116f, Mathf.Max(580f, entries.Count * 104f));
            _achievementSummaryText.text = $"달성 {completedCount} / {entries.Count}";
            RefreshAchievementButtonState();
        }

        private void RefreshMetaPanel()
        {
            if (HasToolkitMainMenu)
            {
                RefreshToolkitMetaPanel();
            }

            if (_metaHeaderText == null || _metaContentRoot == null)
            {
                return;
            }

            _metaHeaderText.text = $"보유 코인  {MetaProgressionService.CurrentCredits}";
            RefreshMetaTabVisuals();
            RebuildMetaContent();
            RefreshAchievementButtonState();
            UpdateToolkitOverviewSummary();
            UpdateMultiplayerInteractivity();
        }

        private void RefreshMetaTabVisuals()
        {
            if (_metaUnlocksTabText != null)
            {
                _metaUnlocksTabText.text = "해금";
            }

            if (_metaResearchTabText != null)
            {
                _metaResearchTabText.text = "강화";
            }

            ApplyMetaTabState(_metaUnlocksTabButton, _metaUnlocksTabText, _currentMetaTab == MetaTab.Unlocks);
            ApplyMetaTabState(_metaResearchTabButton, _metaResearchTabText, _currentMetaTab == MetaTab.Upgrades);
        }

        private void RebuildMetaContent()
        {
            ClearChildren(_metaContentRoot.transform);
            if (_currentMetaTab == MetaTab.Unlocks)
            {
                BuildCharacterShopContent();
            }
            else
            {
                BuildUpgradeShopContent();
            }
        }

        private void BuildCharacterShopContent()
        {
            CreateSectionText(_metaContentRoot.transform, "CharacterUnlockHeader", new Vector2(0f, 0f), "캐릭터 해금");

            var buttons = new List<Button>();
            var displayIndex = 0;
            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                if (definition.UnlockSource != CharacterUnlockSource.Shop)
                {
                    continue;
                }

                var unlocked = MetaProgressionService.IsCharacterUnlocked(definition.Id);
                var canBuy = CanPurchaseCharacter(definition.Id);
                var rowTop = 44f + (displayIndex * 88f);
                var row = CreatePanel(
                    _metaContentRoot.transform,
                    $"MetaCharacterRow{definition.Id}",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, -rowTop),
                    new Vector2(1116f, 80f),
                    unlocked
                        ? new Color(0.07f, 0.11f, 0.16f, 0.96f)
                        : new Color(0.08f, 0.10f, 0.14f, 0.96f));

                CreatePanel(
                    row.transform,
                    $"MetaCharacterAccent{definition.Id}",
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 0.5f),
                    Vector2.zero,
                    new Vector2(6f, 0f),
                    definition.Color);

                var nameText = CreateText(
                    row.transform,
                    $"MetaCharacterName{definition.Id}",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(20f, -14f),
                    new Vector2(700f, 22f),
                    $"{definition.DisplayName}  |  {SharedGameCatalog.GetWeaponDisplayName(definition.StarterWeaponId)}",
                    16,
                    FontStyle.Bold);
                nameText.alignment = TextAnchor.MiddleLeft;
                nameText.color = definition.Color;

                var detailText = CreateText(
                    row.transform,
                    $"MetaCharacterDetail{definition.Id}",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(20f, -40f),
                    new Vector2(760f, 34f),
                    $"기본 보너스 {BuildMetaBonusSummary(definition.BaseBonuses)}\n고유 특성 {definition.PassiveDescription}",
                    13,
                    FontStyle.Normal);
                detailText.alignment = TextAnchor.UpperLeft;
                detailText.color = new Color(0.82f, 0.87f, 0.95f, 1f);

                var buttonLabelText = unlocked
                    ? "해금 완료"
                    : canBuy
                        ? $"구매 가능 - {definition.UnlockCost} 코인"
                        : $"코인 부족 - {definition.UnlockCost} 코인";
                var button = CreateButton(
                    row.transform,
                    $"MetaCharacter{definition.Id}",
                    Vector2.zero,
                    buttonLabelText,
                    () => PromptCharacterPurchase(definition.Id),
                    new Vector2(226f, 46f));
                var buttonRect = button.GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    buttonRect.anchorMin = new Vector2(1f, 0.5f);
                    buttonRect.anchorMax = new Vector2(1f, 0.5f);
                    buttonRect.pivot = new Vector2(1f, 0.5f);
                    buttonRect.anchoredPosition = new Vector2(-18f, 0f);
                    buttonRect.sizeDelta = new Vector2(226f, 46f);
                }

                button.interactable = !unlocked && canBuy;
                var buttonLabel = button.GetComponentInChildren<Text>();
                if (buttonLabel != null)
                {
                    buttonLabel.fontSize = 14;
                    buttonLabel.fontStyle = FontStyle.Bold;
                    buttonLabel.alignment = TextAnchor.MiddleCenter;
                }

                buttons.Add(button);
                displayIndex++;
            }

            _metaCharacterButtons = buttons.ToArray();
            _metaUpgradeButtons = System.Array.Empty<Button>();
            _metaResetButton = null;
        }

        private void PromptCharacterPurchase(int characterId)
        {
            var definition = SharedGameCatalog.GetCharacter(characterId);
            if (definition.UnlockSource == CharacterUnlockSource.Achievement)
            {
                SetStatus("도전과제로 해금할 수 있는 캐릭터입니다.");
                return;
            }
            if (MetaProgressionService.IsCharacterUnlocked(characterId))
            {
                SetStatus("이미 해금된 캐릭터입니다.");
                return;
            }

            if (MetaProgressionService.CurrentCredits < definition.UnlockCost)
            {
                SetStatus("코인이 부족합니다.");
                return;
            }

            OpenConfirmModal(
                $"{definition.DisplayName} 해금하시겠습니까?\n{definition.UnlockCost} 코인을 사용합니다.",
                () => TryPurchaseCharacter(characterId));
        }

        private void BuildUpgradeShopContent()
        {
            CreateSectionText(_metaContentRoot.transform, "UpgradeHeader", new Vector2(0f, 0f), "\uC601\uAD6C \uAC15\uD654");

            var refund = MetaProgressionService.GetUpgradeRefundPreview();
            _metaResetButton = CreateMetaEntryButton(
                _metaContentRoot.transform,
                "MetaUpgradeReset",
                new Vector2(0f, 44f),
                new Vector2(1116f, 76f),
                $"재분배\n구매한 영구 강화를 모두 초기화하고 {refund} 코인을 돌려받습니다.",
                refund > 0,
                PromptUpgradeReset);
            var resetLabel = _metaResetButton.GetComponentInChildren<Text>();
            if (resetLabel != null)
            {
                resetLabel.fontSize = 15;
                resetLabel.fontStyle = FontStyle.Bold;
                resetLabel.rectTransform.offsetMin = new Vector2(16f, 12f);
                resetLabel.rectTransform.offsetMax = new Vector2(-16f, -12f);
            }

            var buttons = new List<Button>();
            var definitions = MetaProgressionService.Config.UpgradeDefinitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                var level = MetaProgressionService.GetUpgradeLevel(definition.Id);
                var cost = level < definition.MaxLevel ? MetaProgressionService.Config.GetUpgradeCost(definition.Id, level) : 0;
                var canBuy = level < definition.MaxLevel && MetaProgressionService.CurrentCredits >= cost;
                var state = level >= definition.MaxLevel ? "최대 단계" : $"다음 비용 {cost} 코인";
                var label =
                    $"{definition.Title}  Lv.{level}/{definition.MaxLevel}\n" +
                    $"{definition.Description}\n{state}";
                var column = i % 3;
                var row = i / 3;
                var button = CreateMetaEntryButton(
                    _metaContentRoot.transform,
                    $"Upgrade{i}",
                    new Vector2(column * 356f, 136f + (row * 116f)),
                    new Vector2(340f, 102f),
                    label,
                    canBuy,
                    () => TryPurchaseUpgrade(definition.Id));
                var buttonLabel = button.GetComponentInChildren<Text>();
                if (buttonLabel != null)
                {
                    buttonLabel.fontSize = 15;
                    buttonLabel.fontStyle = FontStyle.Bold;
                    buttonLabel.rectTransform.offsetMin = new Vector2(14f, 12f);
                    buttonLabel.rectTransform.offsetMax = new Vector2(-14f, -12f);
                }
                buttons.Add(button);
            }

            _metaCharacterButtons = System.Array.Empty<Button>();
            _metaUpgradeButtons = buttons.ToArray();
        }

        private void TryPurchaseCharacter(int characterId)
        {
            if (MetaProgressionService.TryPurchaseCharacter(characterId, out var reason))
            {
                RefreshMetaPanel();
                RefreshRunSetupPanelV2();
                SetStatus($"{SharedGameCatalog.GetCharacter(characterId).DisplayName} 해금 완료. 런 준비 화면에서 선택하세요.");
            }
            else
            {
                SetStatus(reason);
            }
        }

        private void TryPurchaseUpgrade(MetaUpgradeId upgradeId)
        {
            if (MetaProgressionService.TryPurchaseUpgrade(upgradeId, out var reason))
            {
                RefreshMetaPanel();
                SetStatus($"{SharedGameCatalog.GetMetaUpgradeDisplayName(upgradeId)} \uAC15\uD654 \uC644\uB8CC");
            }
            else
            {
                SetStatus(reason);
            }
        }

        private void PromptUpgradeReset()
        {
            var refund = MetaProgressionService.GetUpgradeRefundPreview();
            if (refund <= 0)
            {
                SetStatus("환불할 강화가 없습니다.");
                return;
            }

            OpenConfirmModal($"영구 강화를 모두 초기화하고 {refund} 코인을 돌려받습니까?", ConfirmUpgradeReset);
        }

        private void ConfirmUpgradeReset()
        {
            if (MetaProgressionService.TryRefundAllUpgrades(out var refundedCredits, out var reason))
            {
                RefreshMetaPanel();
                RefreshRunSetupPanelV2();
                SetStatus($"{refundedCredits} 코인 환불 완료");
            }
            else
            {
                SetStatus(reason);
            }
        }

        private void AddCredits100FromDev()
        {
            MetaProgressionService.AddCreditsForDebug(100);
            RefreshTitleStateAfterDevMutation("코인 100 추가");
        }

        private void AddCredits1000FromDev()
        {
            MetaProgressionService.AddCreditsForDebug(1000);
            RefreshTitleStateAfterDevMutation("코인 1000 추가");
        }

        private void UnlockAllCharactersFromDev()
        {
            MetaProgressionService.UnlockAllCharactersForDebug();
            RefreshTitleStateAfterDevMutation("캐릭터 모두 해금");
        }

        private void UnlockAllMapsFromDev()
        {
            MetaProgressionService.UnlockAllMapsForDebug();
            RefreshTitleStateAfterDevMutation("모든 맵 해금");
        }

        private void CompleteAllAchievementsFromDev()
        {
            MetaProgressionService.CompleteAllAchievementsForDebug();
            RefreshTitleStateAfterDevMutation("도전과제 전체 완료");
        }

        private void PromptResetCreditsFromDev()
        {
            OpenConfirmModal("코인을 0으로 초기화합니까?", ConfirmResetCreditsFromDev);
        }

        private void PromptResetCharacterUnlocksFromDev()
        {
            OpenConfirmModal("캐릭터 해금 상태를 기본값으로 초기화합니까?", ConfirmResetCharacterUnlocksFromDev);
        }

        private void PromptResetMapClearsFromDev()
        {
            OpenConfirmModal("맵 클리어 상태를 모두 초기화합니까?", ConfirmResetMapClearsFromDev);
        }

        private void PromptResetAchievementsFromDev()
        {
            OpenConfirmModal("도전과제 완료 상태를 모두 초기화합니까?", ConfirmResetAchievementsFromDev);
        }

        private void PromptResetUpgradesFromDev()
        {
            PromptUpgradeReset();
        }

        private void PromptResetAllMetaProgressFromDev()
        {
            OpenConfirmModal("코인, 캐릭터, 맵, 도전과제, 영구 강화까지 메타 진행도를 모두 초기화합니까?", ConfirmResetAllMetaProgressFromDev);
        }

        private void ConfirmResetCreditsFromDev()
        {
            MetaProgressionService.SetCreditsForDebug(0);
            RefreshTitleStateAfterDevMutation("코인 초기화 완료");
        }

        private void ConfirmResetCharacterUnlocksFromDev()
        {
            MetaProgressionService.ResetCharacterUnlocksForDebug();
            RefreshTitleStateAfterDevMutation("캐릭터 해금 초기화 완료");
        }

        private void ConfirmResetMapClearsFromDev()
        {
            MetaProgressionService.ResetMapClearsForDebug();
            RefreshTitleStateAfterDevMutation("맵 클리어 초기화 완료");
        }

        private void ConfirmResetAchievementsFromDev()
        {
            MetaProgressionService.ResetAchievementsForDebug();
            RefreshTitleStateAfterDevMutation("도전과제 초기화 완료");
        }

        private void ConfirmResetAllMetaProgressFromDev()
        {
            MetaProgressionService.ResetAllProgressForDebug();
            _selectedMapId = SharedRunCatalog.DefaultMapId;
            _selectedDifficultyId = SharedRunCatalog.DefaultDifficultyId;
            RefreshTitleStateAfterDevMutation("메타 전체 초기화 완료");
        }

        private void RefreshTitleStateAfterDevMutation(string statusMessage)
        {
            _selectedCharacterId = MetaProgressionService.GetSingleSelectedCharacterId();
            _inspectedCharacterId = _selectedCharacterId;
            _selectedStarterWeaponId = MetaProgressionService.GetSingleSelectedStarterWeapon();
            _selectedMapId = SharedRunCatalog.IsMapUnlocked(_selectedMapId)
                ? SharedRunCatalog.GetMap(_selectedMapId).Id
                : GetFirstUnlockedMapId();
            _selectedDifficultyId = SharedRunCatalog.DefaultDifficultyId;

            RefreshRunSetupPanelV2();
            RefreshAchievementPanel();
            RefreshMetaPanel();
            SetStatus(statusMessage);
        }

#if false
        private string BuildMetaBonusSummary(MetaBonusValues bonuses)
        {
            var builder = new StringBuilder();
            AppendBonus(builder, bonuses.attackPowerPercent, "피해량 +{0:0.#}%");
            AppendBonus(builder, bonuses.attackSpeedPercent, "공속 +{0:0.#}%");
            AppendBonus(builder, bonuses.maxHealthFlat, "체력 +{0:0.#}");
            AppendBonus(builder, bonuses.healthRegenPerSecond, "재생 +{0:0.##}/초");
            AppendBonus(builder, bonuses.moveSpeedPercent, "이속 +{0:0.#}%");
            AppendBonus(builder, bonuses.attackRangePercent, "사거리 +{0:0.#}%");
            AppendBonus(builder, bonuses.luck, "행운 +{0:0}");
            return builder.Length > 0 ? builder.ToString() : "보너스 없음";
        }

#endif

        private string BuildMetaBonusSummary(MetaBonusValues bonuses)
        {
            var builder = new StringBuilder();
            AppendBonus(builder, bonuses.attackPowerPercent, "\uD53C\uD574\uB7C9 +{0:0.#}%");
            AppendBonus(builder, bonuses.attackSpeedPercent, "\uACF5\uC18D +{0:0.#}%");
            AppendBonus(builder, bonuses.maxHealthFlat, "\uCCB4\uB825 +{0:0.#}");
            AppendBonus(builder, bonuses.healthRegenPerSecond, "\uC7AC\uC0DD +{0:0.##}/\uCD08");
            AppendBonus(builder, bonuses.moveSpeedPercent, "\uC774\uC18D +{0:0.#}%");
            AppendBonus(builder, bonuses.attackRangePercent, "\uBC94\uC704 +{0:0.#}%");
            AppendBonus(builder, bonuses.luck, "\uD589\uC6B4 +{0:0}");
            AppendBonus(builder, bonuses.experienceGainPercent, "XP +{0:0.#}%");
            AppendBonus(builder, bonuses.creditGainPercent, "\uD06C\uB808\uB527 +{0:0.#}%");
            return builder.Length > 0 ? builder.ToString() : "\uBCF4\uB108\uC2A4 \uC5C6\uC74C";
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

        private static void ApplyMetaTabState(Button button, Text label, bool selected)
        {
            if (button == null || label == null || button.targetGraphic is not Image image)
            {
                return;
            }

            image.color = selected ? new Color(0.28f, 0.34f, 0.46f, 1f) : new Color(0.16f, 0.20f, 0.29f, 0.96f);
            label.color = selected ? new Color(0.98f, 0.86f, 0.42f, 1f) : new Color(0.86f, 0.89f, 0.95f, 1f);
        }

        private static bool TryParseTrailingInt(string value, string prefix, out int parsedValue)
        {
            parsedValue = 0;
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(value.Substring(prefix.Length), out parsedValue);
        }

        private static bool CanPurchaseCharacter(int characterId)
        {
            var definition = SharedGameCatalog.GetCharacter(characterId);
            return definition.UnlockSource == CharacterUnlockSource.Shop
                && !MetaProgressionService.IsCharacterUnlocked(characterId)
                && MetaProgressionService.CurrentCredits >= definition.UnlockCost;
        }

        private static bool CanPurchaseUpgrade(MetaUpgradeId upgradeId)
        {
            if (!MetaProgressionService.Config.TryGetUpgradeDefinition(upgradeId, out var definition))
            {
                return false;
            }

            var level = MetaProgressionService.GetUpgradeLevel(upgradeId);
            if (level >= definition.MaxLevel)
            {
                return false;
            }

            var cost = MetaProgressionService.Config.GetUpgradeCost(upgradeId, level);
            return MetaProgressionService.CurrentCredits >= cost;
        }

        private void UpdateMultiplayerInteractivity()
        {
            var interactable = true;

            try
            {
                SetInteractable(_singlePlayButton, interactable);
            SetInteractable(_multiPlayButton, interactable);
            SetInteractable(_achievementButton, interactable);
            SetInteractable(_metaButton, interactable);
            SetInteractable(_optionsButton, interactable);
            SetInteractable(_hostButton, interactable);
            SetInteractable(_joinButton, interactable);
            SetInteractable(_backButton, interactable);
            SetInteractable(_optionsBackButton, interactable);
            SetInteractable(_achievementBackButton, interactable);
            SetInteractable(_runSetupCharacterButton, interactable);
            SetInteractable(_runSetupMapNextButton, interactable);
            SetInteractable(_runSetupMapBackButton, interactable);
            SetInteractable(_runSetupWeaponButton, interactable);
            SetInteractable(_runSetupCharacterBackButton, interactable);
            if (_runSetupPrimaryActionButton != null && _inspectedCharacterId > 0)
            {
                _runSetupPrimaryActionButton.gameObject.SetActive(false);
                _runSetupPrimaryActionButton.interactable = false;
            }
            if (_runSetupStartButton != null && _selectedCharacterId >= 0)
            {
                _runSetupStartButton.interactable =
                    interactable &&
                    _currentRunSetupStep == SingleRunSetupStep.CharacterSelect &&
                    MetaProgressionService.IsCharacterUnlocked(_selectedCharacterId) &&
                    SharedRunCatalog.IsMapUnlocked(_selectedMapId);
            }
            if (_runSetupMapNextButton != null)
            {
                _runSetupMapNextButton.interactable =
                    interactable &&
                    _currentRunSetupStep == SingleRunSetupStep.MapSelect &&
                    SharedRunCatalog.IsMapUnlocked(_selectedMapId);
            }
            SetInteractable(_metaUnlocksTabButton, interactable);
            SetInteractable(_metaResearchTabButton, interactable);
            if (_metaResetButton != null)
            {
                _metaResetButton.interactable =
                    interactable &&
                    _currentMetaTab == MetaTab.Upgrades &&
                    MetaProgressionService.GetUpgradeRefundPreview() > 0;
            }
            SetInteractable(_confirmConfirmButton, interactable);
            SetInteractable(_confirmCancelButton, interactable);
            for (var i = 0; i < _runSetupCharacterOptionButtons.Length; i++)
            {
                var button = _runSetupCharacterOptionButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.interactable =
                    interactable &&
                    TryParseTrailingInt(button.name, "RunSetupCharacter", out var characterId) &&
                    MetaProgressionService.IsCharacterUnlocked(characterId);
            }

            for (var i = 0; i < _runSetupWeaponOptionButtons.Length; i++)
            {
                SetInteractable(_runSetupWeaponOptionButtons[i], interactable);
            }

            for (var i = 0; i < _runSetupMapButtons.Length && i < SharedRunCatalog.MapDefinitions.Count; i++)
            {
                var button = _runSetupMapButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.interactable = interactable && SharedRunCatalog.IsMapUnlocked(SharedRunCatalog.MapDefinitions[i].Id);
            }

            for (var i = 0; i < _runSetupDifficultyButtons.Length; i++)
            {
                SetInteractable(_runSetupDifficultyButtons[i], interactable);
            }

            for (var i = 0; i < _metaCharacterButtons.Length; i++)
            {
                var button = _metaCharacterButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.interactable =
                    interactable &&
                    TryParseTrailingInt(button.name, "MetaCharacter", out var characterId) &&
                    CanPurchaseCharacter(characterId);
            }

            for (var i = 0; i < _metaUpgradeButtons.Length; i++)
            {
                var button = _metaUpgradeButtons[i];
                if (button == null)
                {
                    continue;
                }

                button.interactable =
                    interactable &&
                    TryParseTrailingInt(button.name, "Upgrade", out var upgradeIndex) &&
                    upgradeIndex >= 0 &&
                    upgradeIndex < MetaProgressionService.Config.UpgradeDefinitions.Count &&
                    CanPurchaseUpgrade(MetaProgressionService.Config.UpgradeDefinitions[upgradeIndex].Id);
            }

            if (_joinCodeInput != null) _joinCodeInput.interactable = interactable;
            if (_fullscreenToggle != null) _fullscreenToggle.interactable = interactable;
            if (_masterVolumeSlider != null) _masterVolumeSlider.interactable = interactable;
            if (_bgmVolumeSlider != null) _bgmVolumeSlider.interactable = interactable;
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.interactable = interactable;
                UpdateToolkitInteractivity(interactable);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
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
            GameplaySpeedService.ApplyMenuTimeState();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }


        private void HandleDebugSessionChanged(bool unlocked)
        {
            RefreshToolkitDebugButtonVisibility();
        }

        private void SetStatus(string message)
        {
            UpdateToolkitStatus(message);
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
            _suppressDisplayToggleCallback = true;
            _fullscreenToggle?.SetIsOnWithoutNotify(PlayerPrefs.GetInt(FullscreenPreferenceKey, 0) != 0);
            _suppressDisplayToggleCallback = false;
            SyncToolkitOptionsControls();
        }

        private void OnFullscreenToggleChanged(bool useFullscreen)
        {
            if (_suppressDisplayToggleCallback)
            {
                return;
            }

            ApplyDisplayMode(useFullscreen, true);
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
            SetStatus(useFullscreen ? "화면 모드: 전체 화면" : "화면 모드: 창 모드");
        }

        private void SyncAudioSettingsControls()
        {
            var audio = AudioService.Instance;
            _suppressAudioSettingsCallbacks = true;
            _masterVolumeSlider?.SetValueWithoutNotify(audio.MasterVolume);
            _bgmVolumeSlider?.SetValueWithoutNotify(audio.BgmVolume);
            _sfxVolumeSlider?.SetValueWithoutNotify(audio.SfxVolume);
            _suppressAudioSettingsCallbacks = false;

            UpdateSliderValueLabel(_masterVolumeValueText, audio.MasterVolume);
            UpdateSliderValueLabel(_bgmVolumeValueText, audio.BgmVolume);
            UpdateSliderValueLabel(_sfxVolumeValueText, audio.SfxVolume);
            SyncToolkitOptionsControls();
        }

        private void OnMasterVolumeChanged(float value)
        {
            UpdateSliderValueLabel(_masterVolumeValueText, value);
            if (_suppressAudioSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetMasterVolume(value);
            SyncAudioSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnBgmVolumeChanged(float value)
        {
            UpdateSliderValueLabel(_bgmVolumeValueText, value);
            if (_suppressAudioSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetBgmVolume(value);
            SyncAudioSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnSfxVolumeChanged(float value)
        {
            UpdateSliderValueLabel(_sfxVolumeValueText, value);
            if (_suppressAudioSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetSfxVolume(value);
            SyncAudioSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private static void UpdateSliderValueLabel(Text label, float value)
        {
            if (label != null)
            {
                label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
            }
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

        private static GameObject CreateStretchRoot(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return root;
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

        private static GameObject CreateScrollViewport(Transform parent, string name, Vector2 topLeft, Vector2 size, out RectTransform contentRect)
        {
            var viewport = new GameObject(name);
            viewport.transform.SetParent(parent, false);

            var viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 1f);
            viewportRect.anchorMax = new Vector2(0f, 1f);
            viewportRect.pivot = new Vector2(0f, 1f);
            viewportRect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            viewportRect.sizeDelta = size;

            var image = viewport.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.001f);
            viewport.AddComponent<RectMask2D>();

            var scrollRect = viewport.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;
            scrollRect.viewport = viewportRect;

            var content = new GameObject($"{name}Content");
            content.transform.SetParent(viewport.transform, false);
            contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = size;
            scrollRect.content = contentRect;

            return viewport;
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
            ApplyPanelChrome(panel, name);
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

        private static AudioCueId ResolveButtonCue(string name, string label)
        {
            var normalizedName = name ?? string.Empty;
            var normalizedLabel = label ?? string.Empty;
            if (normalizedName.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedName.IndexOf("Close", System.StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedLabel.Contains("뒤로")
                || normalizedLabel.Contains("닫기"))
            {
                return AudioCueId.UiBack;
            }

            return AudioCueId.UiConfirm;
        }

        private static void PlayButtonCue(string name, string label)
        {
            AudioService.Instance.PlayUi(ResolveButtonCue(name, label));
        }

        private void CreateSliderControl(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            string label,
            UnityEngine.Events.UnityAction<float> onValueChanged,
            out Slider slider,
            out Text valueText)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = anchoredPosition;
            rootRect.sizeDelta = new Vector2(420f, 28f);

            var labelText = CreateText(root.transform, "Label", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(96f, 24f), label, 16, FontStyle.Bold);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = new Color(0.97f, 0.98f, 1f, 1f);

            var sliderObject = new GameObject("Slider");
            sliderObject.transform.SetParent(root.transform, false);
            var sliderRect = sliderObject.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(0f, 0.5f);
            sliderRect.pivot = new Vector2(0f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(112f, 0f);
            sliderRect.sizeDelta = new Vector2(228f, 18f);

            var background = new GameObject("Background");
            background.transform.SetParent(sliderObject.transform, false);
            var backgroundRect = background.AddComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.sizeDelta = new Vector2(0f, 8f);
            var backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.11f, 0.15f, 0.21f, 0.96f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObject.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(0f, 5f);
            fillAreaRect.offsetMax = new Vector2(0f, -5f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.96f, 0.74f, 0.18f, 0.96f);

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = Vector2.zero;
            handleAreaRect.offsetMax = Vector2.zero;

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(14f, 18f);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.98f, 0.98f, 1f, 1f);

            slider = sliderObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.onValueChanged.AddListener(onValueChanged);

            valueText = CreateText(root.transform, "Value", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(56f, 24f), "0%", 15, FontStyle.Bold);
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            valueText.rectTransform.anchoredPosition = new Vector2(-2f, 0f);
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
            button.onClick.AddListener(() =>
            {
                PlayButtonCue(name, label);
                onClick?.Invoke();
            });
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.22f, 0.28f, 0.39f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.12f, 0.15f, 0.22f, 1f);
            colors.disabledColor = new Color(0.18f, 0.18f, 0.18f, 0.7f);
            button.colors = colors;
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.50f, 0.61f, 0.78f, 0.20f);
            outline.effectDistance = new Vector2(1f, -1f);
            var shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -6f);
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
            button.onClick.AddListener(() =>
            {
                PlayButtonCue(name, label);
                onClick?.Invoke();
            });
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.22f, 0.28f, 0.39f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.12f, 0.15f, 0.22f, 1f);
            colors.disabledColor = new Color(0.14f, 0.14f, 0.14f, 0.84f);
            button.colors = colors;
            var shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.14f);
            shadow.effectDistance = new Vector2(0f, -4f);
            var labelText = CreateText(buttonObject.transform, "Label", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, label, 14, FontStyle.Normal);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.rectTransform.offsetMin = new Vector2(12f, 6f);
            labelText.rectTransform.offsetMax = new Vector2(-12f, -6f);
            return button;
        }

        private void ApplyPanelChrome(GameObject panel, string name)
        {
            if (panel == null || string.IsNullOrEmpty(name) || name.StartsWith("Backdrop", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -8f);

            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.36f, 0.47f, 0.62f, 0.18f);
            outline.effectDistance = new Vector2(1f, -1f);
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
            labelRect.offsetMax = new Vector2(-8f, 0f);
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
