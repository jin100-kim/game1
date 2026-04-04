using System.Collections.Generic;
using EJR.Game.Audio;
using EJR.Game.Core;
using EJR.Game.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace EJR.Game.UI
{
    public sealed partial class TitleMenuController
    {
        private const string ToolkitLayoutResourcePath = "UI/Title/TitleMenuLayout";
        private const string ToolkitStylesResourcePath = "UI/Title/TitleMenuStyles";
        private const string ToolkitOptionsLayoutResourcePath = "UI/Common/SettingsPanelLayout";
        private const string ToolkitOptionsStylesResourcePath = "UI/Common/SettingsPanelStyles";
        private const string ToolkitDevOverlayStylesResourcePath = "UI/Common/DevOverlayStyles";
        private const string ToolkitRuntimeThemeResourcePath = "UI/Common/UnityDefaultRuntimeTheme";
        private const string ToolkitPanelSettingsResourcePath = "UI/Common/RuntimeMenuPanelSettings";

        private UIDocument _toolkitDocument;
        private PanelSettings _toolkitPanelSettings;
        private VisualElement _toolkitRoot;
        private VisualElement _toolkitMainScreen;
        private VisualElement _toolkitMultiplayerEntryScreen;
        private VisualElement _toolkitOptionsScreen;
        private VisualElement _toolkitRunSetupScreen;
        private VisualElement _toolkitRunSetupMapStep;
        private VisualElement _toolkitRunSetupCharacterStep;
        private VisualElement _toolkitAchievementScreen;
        private VisualElement _toolkitMetaScreen;
        private VisualElement _toolkitModalLayer;
        private VisualElement _toolkitSummaryModal;
        private VisualElement _toolkitConfirmModal;
        private VisualElement _toolkitDevPanel;
        private VisualElement _toolkitDevCurrencyActions;
        private VisualElement _toolkitDevProgressActions;
        private VisualElement _toolkitDevResetActions;
        private Label _toolkitStatusLabel;
        private Label _toolkitDevPanelStatusLabel;
        private Label _toolkitDevPanelContextLabel;
        private Label _toolkitProfileLabel;
        private Label _toolkitRecentRunLabel;
        private TextField _toolkitJoinCodeField;
        private Label _toolkitRunSetupHeader;
        private Label _toolkitRunSetupHint;
        private Label _toolkitRunSetupMapSelectionLabel;
        private Label _toolkitRunSetupMapLockLabel;
        private Label _toolkitRunSetupSelectionSummaryLabel;
        private Label _toolkitRunSetupCharacterNameLabel;
        private Label _toolkitRunSetupCharacterDetailLabel;
        private ScrollView _toolkitRunSetupCharacterScroll;
        private VisualElement _toolkitRunSetupMapButtonRow;
        private VisualElement _toolkitRunSetupDifficultyButtonRow;
        private ScrollView _toolkitAchievementScroll;
        private Label _toolkitAchievementSummaryLabel;
        private ScrollView _toolkitMetaScroll;
        private Label _toolkitMetaHeaderLabel;
        private Label _toolkitSummaryModalTextLabel;
        private Label _toolkitConfirmModalTextLabel;
        private Toggle _toolkitOptionsFullscreenToggle;
        private Slider _toolkitOptionsMasterVolumeSlider;
        private Slider _toolkitOptionsBgmVolumeSlider;
        private Slider _toolkitOptionsSfxVolumeSlider;
        private Label _toolkitOptionsMasterVolumeValueLabel;
        private Label _toolkitOptionsBgmVolumeValueLabel;
        private Label _toolkitOptionsSfxVolumeValueLabel;
        private Button _toolkitSinglePlayButton;
        private Button _toolkitMultiPlayButton;
        private Button _toolkitAchievementButton;
        private Button _toolkitMetaButton;
        private Button _toolkitOptionsButton;
        private Button _toolkitDevButton;
        private Button _toolkitQuitButton;
        private Button _toolkitMultiplayerHostButton;
        private Button _toolkitMultiplayerJoinButton;
        private Button _toolkitMultiplayerBackButton;
        private Button _toolkitRunSetupMapNextButton;
        private Button _toolkitRunSetupMapBackButton;
        private Button _toolkitRunSetupStartButton;
        private Button _toolkitRunSetupCharacterBackButton;
        private Button _toolkitAchievementBackButton;
        private Button _toolkitMetaBackButton;
        private Button _toolkitMetaUnlocksTabButton;
        private Button _toolkitMetaUpgradesTabButton;
        private Button _toolkitMetaUpgradeResetButton;
        private Button _toolkitSummaryMetaButton;
        private Button _toolkitSummaryCloseButton;
        private Button _toolkitConfirmConfirmButton;
        private Button _toolkitConfirmCancelButton;
        private Button _toolkitOptionsBackButton;
        private readonly List<Button> _toolkitDynamicButtons = new();

        private bool HasToolkitMainMenu => _toolkitDocument != null;
        private bool HasToolkitOptionsScreen => _toolkitOptionsScreen != null;

        private bool SupportsToolkitTitleUi()
        {
            return Resources.Load<VisualTreeAsset>(ToolkitLayoutResourcePath) != null
                && Resources.Load<VisualTreeAsset>(ToolkitOptionsLayoutResourcePath) != null
                && Resources.Load<PanelSettings>(ToolkitPanelSettingsResourcePath) != null;
        }

        private bool SupportsToolkitOptionsPanel()
        {
            return SupportsToolkitTitleUi();
        }

        private void BuildToolkitMainMenu()
        {
            if (_toolkitDocument != null)
            {
                return;
            }

            var layout = Resources.Load<VisualTreeAsset>(ToolkitLayoutResourcePath);
            var settingsLayout = Resources.Load<VisualTreeAsset>(ToolkitOptionsLayoutResourcePath);
            var panelTemplate = Resources.Load<PanelSettings>(ToolkitPanelSettingsResourcePath);
            if (layout == null || settingsLayout == null || panelTemplate == null)
            {
                Debug.LogWarning("Title Toolkit resources are incomplete.");
                return;
            }

            var documentObject = new GameObject("TitleToolkitMenu");
            documentObject.transform.SetParent(transform, false);

            _toolkitDocument = documentObject.AddComponent<UIDocument>();
            _toolkitPanelSettings = Object.Instantiate(panelTemplate);
            _toolkitPanelSettings.name = "RuntimeTitleMenuPanelSettings";
            _toolkitPanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _toolkitPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _toolkitPanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _toolkitPanelSettings.match = 0.5f;
            _toolkitPanelSettings.sortingOrder = 120;
            _toolkitPanelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ToolkitRuntimeThemeResourcePath);
            _toolkitDocument.panelSettings = _toolkitPanelSettings;

            _toolkitRoot = _toolkitDocument.rootVisualElement;
            _toolkitRoot.Clear();
            layout.CloneTree(_toolkitRoot);
            settingsLayout.CloneTree(_toolkitRoot);

            var titleStyles = Resources.Load<StyleSheet>(ToolkitStylesResourcePath);
            if (titleStyles != null && !_toolkitRoot.styleSheets.Contains(titleStyles))
            {
                _toolkitRoot.styleSheets.Add(titleStyles);
            }

            var settingsStyles = Resources.Load<StyleSheet>(ToolkitOptionsStylesResourcePath);
            if (settingsStyles != null && !_toolkitRoot.styleSheets.Contains(settingsStyles))
            {
                _toolkitRoot.styleSheets.Add(settingsStyles);
            }

            var devOverlayStyles = Resources.Load<StyleSheet>(ToolkitDevOverlayStylesResourcePath);
            if (devOverlayStyles != null && !_toolkitRoot.styleSheets.Contains(devOverlayStyles))
            {
                _toolkitRoot.styleSheets.Add(devOverlayStyles);
            }

            QueryToolkitElements(_toolkitRoot);
            BuildToolkitDevPanelActions();
            ConfigureToolkitScroll(_toolkitRunSetupCharacterScroll);
            ConfigureToolkitScroll(_toolkitAchievementScroll);
            ConfigureToolkitScroll(_toolkitMetaScroll);
            WireToolkitCallbacks();
            UpdateToolkitOverviewSummary();
            RefreshAchievementButtonState();
            RefreshToolkitRunSetupPanel();
            RefreshToolkitAchievementPanel();
            RefreshToolkitMetaPanel();
            UpdateToolkitStatus(string.Empty);
            UpdateToolkitInteractivity(!MultiplayerSessionController.EnsureInstance().IsBusy);
            UpdateToolkitScreenVisibility(_mainMenuPanel);
            RefreshToolkitModalLayerVisibility();
        }

        private void QueryToolkitElements(VisualElement root)
        {
            _toolkitMainScreen = root.Q<VisualElement>("main-screen");
            _toolkitMultiplayerEntryScreen = root.Q<VisualElement>("multiplayer-entry-screen");
            _toolkitOptionsScreen = root.Q<VisualElement>("settings-screen");
            _toolkitRunSetupScreen = root.Q<VisualElement>("run-setup-screen");
            _toolkitRunSetupMapStep = root.Q<VisualElement>("run-setup-map-step");
            _toolkitRunSetupCharacterStep = root.Q<VisualElement>("run-setup-character-step");
            _toolkitAchievementScreen = root.Q<VisualElement>("achievement-screen");
            _toolkitMetaScreen = root.Q<VisualElement>("meta-screen");
            _toolkitModalLayer = root.Q<VisualElement>("title-modal-layer");
            _toolkitSummaryModal = root.Q<VisualElement>("summary-modal");
            _toolkitConfirmModal = root.Q<VisualElement>("confirm-modal");
            _toolkitDevPanel = root.Q<VisualElement>("dev-panel");
            _toolkitDevCurrencyActions = root.Q<VisualElement>("dev-currency-actions");
            _toolkitDevProgressActions = root.Q<VisualElement>("dev-progress-actions");
            _toolkitDevResetActions = root.Q<VisualElement>("dev-reset-actions");
            _toolkitStatusLabel = root.Q<Label>("status-line");
            _toolkitDevPanelStatusLabel = root.Q<Label>("dev-panel-status");
            _toolkitDevPanelContextLabel = root.Q<Label>("dev-panel-context");
            _toolkitProfileLabel = root.Q<Label>("profile-summary");
            _toolkitRecentRunLabel = root.Q<Label>("recent-run-summary");
            _toolkitJoinCodeField = root.Q<TextField>("multiplayer-join-code-field");
            _toolkitRunSetupHeader = root.Q<Label>("run-setup-header");
            _toolkitRunSetupHint = root.Q<Label>("run-setup-hint");
            _toolkitRunSetupMapSelectionLabel = root.Q<Label>("run-setup-map-selection");
            _toolkitRunSetupMapLockLabel = root.Q<Label>("run-setup-map-lock-text");
            _toolkitRunSetupSelectionSummaryLabel = root.Q<Label>("run-setup-selection-summary");
            _toolkitRunSetupCharacterNameLabel = root.Q<Label>("run-setup-character-name");
            _toolkitRunSetupCharacterDetailLabel = root.Q<Label>("run-setup-character-detail");
            _toolkitRunSetupCharacterScroll = root.Q<ScrollView>("run-setup-character-scroll");
            _toolkitRunSetupMapButtonRow = root.Q<VisualElement>("run-setup-map-button-row");
            _toolkitRunSetupDifficultyButtonRow = root.Q<VisualElement>("run-setup-difficulty-button-row");
            _toolkitAchievementSummaryLabel = root.Q<Label>("achievement-summary");
            _toolkitAchievementScroll = root.Q<ScrollView>("achievement-scroll");
            _toolkitMetaHeaderLabel = root.Q<Label>("meta-header");
            _toolkitMetaScroll = root.Q<ScrollView>("meta-scroll");
            _toolkitSummaryModalTextLabel = root.Q<Label>("summary-modal-text");
            _toolkitConfirmModalTextLabel = root.Q<Label>("confirm-modal-text");
            _toolkitOptionsFullscreenToggle = root.Q<Toggle>("fullscreen-toggle");
            _toolkitOptionsMasterVolumeSlider = root.Q<Slider>("master-volume-slider");
            _toolkitOptionsBgmVolumeSlider = root.Q<Slider>("bgm-volume-slider");
            _toolkitOptionsSfxVolumeSlider = root.Q<Slider>("sfx-volume-slider");
            _toolkitOptionsMasterVolumeValueLabel = root.Q<Label>("master-volume-value");
            _toolkitOptionsBgmVolumeValueLabel = root.Q<Label>("bgm-volume-value");
            _toolkitOptionsSfxVolumeValueLabel = root.Q<Label>("sfx-volume-value");
            _toolkitSinglePlayButton = root.Q<Button>("single-play-button");
            _toolkitMultiPlayButton = root.Q<Button>("multi-play-button");
            _toolkitAchievementButton = root.Q<Button>("achievement-button");
            _toolkitMetaButton = root.Q<Button>("meta-button");
            _toolkitOptionsButton = root.Q<Button>("options-button");
            _toolkitDevButton = root.Q<Button>("dev-button");
            _toolkitQuitButton = root.Q<Button>("quit-button");
            _toolkitMultiplayerHostButton = root.Q<Button>("multiplayer-host-button");
            _toolkitMultiplayerJoinButton = root.Q<Button>("multiplayer-join-button");
            _toolkitMultiplayerBackButton = root.Q<Button>("multiplayer-back-button");
            _toolkitRunSetupMapNextButton = root.Q<Button>("run-setup-map-next-button");
            _toolkitRunSetupMapBackButton = root.Q<Button>("run-setup-map-back-button");
            _toolkitRunSetupStartButton = root.Q<Button>("run-setup-start-button");
            _toolkitRunSetupCharacterBackButton = root.Q<Button>("run-setup-character-back-button");
            _toolkitAchievementBackButton = root.Q<Button>("achievement-back-button");
            _toolkitMetaBackButton = root.Q<Button>("meta-back-button");
            _toolkitMetaUnlocksTabButton = root.Q<Button>("meta-unlocks-tab");
            _toolkitMetaUpgradesTabButton = root.Q<Button>("meta-upgrades-tab");
            _toolkitMetaUpgradeResetButton = root.Q<Button>("meta-upgrade-reset-button");
            _toolkitSummaryMetaButton = root.Q<Button>("summary-meta-button");
            _toolkitSummaryCloseButton = root.Q<Button>("summary-close-button");
            _toolkitConfirmConfirmButton = root.Q<Button>("confirm-ok-button");
            _toolkitConfirmCancelButton = root.Q<Button>("confirm-cancel-button");
            _toolkitOptionsBackButton = root.Q<Button>("settings-back-button");
        }

        private void WireToolkitCallbacks()
        {
            if (_toolkitSinglePlayButton != null) _toolkitSinglePlayButton.clicked += OnSinglePlayClicked;
            if (_toolkitMultiPlayButton != null) _toolkitMultiPlayButton.clicked += OnMultiPlayClicked;
            if (_toolkitAchievementButton != null) _toolkitAchievementButton.clicked += OnAchievementsClicked;
            if (_toolkitMetaButton != null) _toolkitMetaButton.clicked += OnMetaClicked;
            if (_toolkitOptionsButton != null) _toolkitOptionsButton.clicked += OnOptionsClicked;
            if (_toolkitDevButton != null) _toolkitDevButton.clicked += OnDevClicked;
            if (_toolkitQuitButton != null) _toolkitQuitButton.clicked += OnQuitClicked;
            if (_toolkitMultiplayerHostButton != null) _toolkitMultiplayerHostButton.clicked += OnHostClicked;
            if (_toolkitMultiplayerJoinButton != null) _toolkitMultiplayerJoinButton.clicked += OnJoinClicked;
            if (_toolkitMultiplayerBackButton != null) _toolkitMultiplayerBackButton.clicked += ShowMainMenu;
            if (_toolkitRunSetupMapNextButton != null) _toolkitRunSetupMapNextButton.clicked += GoToRunSetupCharacterStep;
            if (_toolkitRunSetupMapBackButton != null) _toolkitRunSetupMapBackButton.clicked += ShowMainMenu;
            if (_toolkitRunSetupStartButton != null) _toolkitRunSetupStartButton.clicked += StartSinglePlay;
            if (_toolkitRunSetupCharacterBackButton != null) _toolkitRunSetupCharacterBackButton.clicked += GoToRunSetupMapStep;
            if (_toolkitAchievementBackButton != null) _toolkitAchievementBackButton.clicked += ShowMainMenu;
            if (_toolkitMetaBackButton != null) _toolkitMetaBackButton.clicked += ShowMainMenu;
            if (_toolkitMetaUnlocksTabButton != null) _toolkitMetaUnlocksTabButton.clicked += () => SetMetaTab(MetaTab.Unlocks);
            if (_toolkitMetaUpgradesTabButton != null) _toolkitMetaUpgradesTabButton.clicked += () => SetMetaTab(MetaTab.Upgrades);
            if (_toolkitMetaUpgradeResetButton != null) _toolkitMetaUpgradeResetButton.clicked += PromptUpgradeReset;
            if (_toolkitSummaryMetaButton != null) _toolkitSummaryMetaButton.clicked += OpenMetaFromSummary;
            if (_toolkitSummaryCloseButton != null) _toolkitSummaryCloseButton.clicked += CloseSummaryModal;
            if (_toolkitConfirmConfirmButton != null) _toolkitConfirmConfirmButton.clicked += ConfirmPendingAction;
            if (_toolkitConfirmCancelButton != null) _toolkitConfirmCancelButton.clicked += CloseConfirmModal;
            if (_toolkitOptionsBackButton != null) _toolkitOptionsBackButton.clicked += OnToolkitOptionsBackClicked;
            _toolkitOptionsFullscreenToggle?.RegisterValueChangedCallback(OnToolkitOptionsFullscreenChanged);
            _toolkitOptionsMasterVolumeSlider?.RegisterValueChangedCallback(OnToolkitOptionsMasterVolumeChanged);
            _toolkitOptionsBgmVolumeSlider?.RegisterValueChangedCallback(OnToolkitOptionsBgmVolumeChanged);
            _toolkitOptionsSfxVolumeSlider?.RegisterValueChangedCallback(OnToolkitOptionsSfxVolumeChanged);
        }

        private static void ConfigureToolkitScroll(ScrollView scrollView)
        {
            if (scrollView == null)
            {
                return;
            }

            scrollView.contentContainer.style.paddingLeft = 10f;
            scrollView.contentContainer.style.paddingRight = 10f;
            scrollView.contentContainer.style.paddingTop = 10f;
            scrollView.contentContainer.style.paddingBottom = 10f;
        }

        private void UpdateToolkitScreenVisibility(GameObject activePanel)
        {
            if (_toolkitRoot == null)
            {
                return;
            }

            SetDisplay(_toolkitMainScreen, activePanel == _mainMenuPanel);
            SetDisplay(_toolkitMultiplayerEntryScreen, activePanel == _multiplayerPanel);
            SetDisplay(_toolkitOptionsScreen, activePanel == _optionsPanel);
            SetDisplay(_toolkitRunSetupScreen, activePanel == _runSetupPanel);
            SetDisplay(_toolkitAchievementScreen, activePanel == _achievementPanel);
            SetDisplay(_toolkitMetaScreen, activePanel == _metaPanel);

            if (activePanel == _optionsPanel)
            {
                SyncToolkitOptionsControls();
            }

            RefreshToolkitDebugButtonVisibility();

            if (activePanel == _mainMenuPanel)
            {
                FocusToolkitPrimaryButton();
            }

            RefreshToolkitModalLayerVisibility();
        }

        private void RefreshToolkitModalLayerVisibility()
        {
            if (_toolkitModalLayer == null)
            {
                return;
            }

            var showSummary = _summaryModal != null && _summaryModal.activeSelf;
            var showConfirm = _confirmModal != null && _confirmModal.activeSelf;
            SetDisplay(_toolkitModalLayer, showSummary || showConfirm);
            SetDisplay(_toolkitSummaryModal, showSummary);
            SetDisplay(_toolkitConfirmModal, showConfirm);
        }

        private void UpdateToolkitMainMenuVisibility(bool visible)
        {
            SetDisplay(_toolkitMainScreen, visible);
            if (visible)
            {
                FocusToolkitPrimaryButton();
            }
        }

        private void UpdateToolkitOptionsVisibility(bool visible)
        {
            if (_toolkitOptionsScreen == null)
            {
                return;
            }

            SetDisplay(_toolkitOptionsScreen, visible);
            if (visible)
            {
                SyncToolkitOptionsControls();
                _toolkitOptionsFullscreenToggle?.schedule.Execute(() => _toolkitOptionsFullscreenToggle.Focus()).ExecuteLater(0);
            }
        }

        private void UpdateToolkitOverviewSummary()
        {
            if (_toolkitProfileLabel == null || _toolkitRecentRunLabel == null)
            {
                return;
            }

            var unlockedCharacters = 0;
            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                if (MetaProgressionService.IsCharacterUnlocked(SharedGameCatalog.CharacterDefinitions[i].Id))
                {
                    unlockedCharacters++;
                }
            }

            var purchasedUpgradeLevels = 0;
            var upgradeDefinitions = MetaProgressionService.Config.UpgradeDefinitions;
            for (var i = 0; i < upgradeDefinitions.Count; i++)
            {
                purchasedUpgradeLevels += MetaProgressionService.GetUpgradeLevel(upgradeDefinitions[i].Id);
            }

            var selectedCharacter = SharedGameCatalog.GetCharacter(_selectedCharacterId);
            _toolkitProfileLabel.text =
                $"코인 {MetaProgressionService.CurrentCredits}\n" +
                $"선택 {selectedCharacter.DisplayName} | {SharedGameCatalog.GetWeaponDisplayName(selectedCharacter.StarterWeaponId)}\n" +
                $"캐릭터 {unlockedCharacters}/{SharedGameCatalog.CharacterDefinitions.Count} | 강화 {purchasedUpgradeLevels} 단계\n" +
                $"최고 레벨 {MetaProgressionService.BestLevel} | 최고 생존 {MetaProgressionService.BestTimeSeconds:0.0}초";

            _toolkitRecentRunLabel.text = string.IsNullOrWhiteSpace(_recentRunSummaryText)
                ? "최근 결과가 없습니다."
                : _recentRunSummaryText;
        }

        private void UpdateToolkitStatus(string message)
        {
            if (_toolkitStatusLabel != null)
            {
                _toolkitStatusLabel.text = string.IsNullOrWhiteSpace(message) ? " " : message;
            }
        }

        private void UpdateToolkitInteractivity(bool interactable)
        {
            _toolkitSinglePlayButton?.SetEnabled(interactable);
            _toolkitMultiPlayButton?.SetEnabled(interactable);
            _toolkitAchievementButton?.SetEnabled(interactable);
            _toolkitMetaButton?.SetEnabled(interactable);
            _toolkitOptionsButton?.SetEnabled(interactable);
            _toolkitDevButton?.SetEnabled(interactable);
            _toolkitQuitButton?.SetEnabled(interactable);
            _toolkitMultiplayerHostButton?.SetEnabled(interactable);
            _toolkitMultiplayerJoinButton?.SetEnabled(interactable);
            _toolkitMultiplayerBackButton?.SetEnabled(interactable);
            _toolkitAchievementBackButton?.SetEnabled(interactable);
            _toolkitMetaBackButton?.SetEnabled(interactable);
            _toolkitMetaUnlocksTabButton?.SetEnabled(interactable);
            _toolkitMetaUpgradesTabButton?.SetEnabled(interactable);
            _toolkitSummaryMetaButton?.SetEnabled(interactable);
            _toolkitSummaryCloseButton?.SetEnabled(interactable);
            _toolkitConfirmConfirmButton?.SetEnabled(interactable);
            _toolkitConfirmCancelButton?.SetEnabled(interactable);
            _toolkitOptionsScreen?.SetEnabled(interactable);
            _toolkitJoinCodeField?.SetEnabled(interactable);

            RefreshToolkitRunSetupPanel();
            RefreshToolkitMetaPanel();
        }

        private void RefreshToolkitDebugButtonVisibility()
        {
            if (_toolkitDevButton == null)
            {
                return;
            }

            var unlocked = DebugSessionService.IsUnlocked;
            _toolkitDevButton.style.display = unlocked ? DisplayStyle.Flex : DisplayStyle.None;

            if (_toolkitDevPanel != null)
            {
                _toolkitDevPanel.style.display = unlocked && DebugSessionService.IsOverlayOpen
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            if (_toolkitDevPanelStatusLabel != null)
            {
                _toolkitDevPanelStatusLabel.text = unlocked
                    ? "세션 DEV 모드가 활성화되었습니다."
                    : "DEV 모드가 잠겨 있습니다.";
            }

            if (_toolkitDevPanelContextLabel != null)
            {
                _toolkitDevPanelContextLabel.text = "Monster Lab은 싱글 플레이 중 사용할 수 있습니다.";
            }
        }

        private void BuildToolkitDevPanelActions()
        {
            BuildToolkitDevSection(
                _toolkitDevCurrencyActions,
                ("코인 +100", (System.Action)AddCredits100FromDev),
                ("코인 +1000", AddCredits1000FromDev));

            BuildToolkitDevSection(
                _toolkitDevProgressActions,
                ("캐릭터 모두 해금", UnlockAllCharactersFromDev),
                ("맵 모두 해금", UnlockAllMapsFromDev),
                ("도전과제 모두 완료", CompleteAllAchievementsFromDev));

            BuildToolkitDevSection(
                _toolkitDevResetActions,
                ("코인 0으로", PromptResetCreditsFromDev),
                ("캐릭터 해금 초기화", PromptResetCharacterUnlocksFromDev),
                ("맵 클리어 초기화", PromptResetMapClearsFromDev),
                ("도전과제 초기화", PromptResetAchievementsFromDev),
                ("영구 강화 초기화", PromptResetUpgradesFromDev),
                ("메타 전체 초기화", PromptResetAllMetaProgressFromDev));
        }

        private void BuildToolkitDevSection(VisualElement root, params (string Label, System.Action Callback)[] entries)
        {
            if (root == null)
            {
                return;
            }

            root.Clear();
            for (var i = 0; i < entries.Length; i++)
            {
                var button = new Button(entries[i].Callback)
                {
                    text = entries[i].Label,
                };
                button.AddToClassList("dev-panel-button");
                root.Add(button);
                _toolkitDynamicButtons.Add(button);
            }
        }

        private void FocusToolkitPrimaryButton()
        {
            if (_toolkitMainScreen == null || _toolkitMainScreen.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            _toolkitSinglePlayButton?.schedule.Execute(() => _toolkitSinglePlayButton.Focus()).ExecuteLater(0);
        }

        private void SyncToolkitOptionsControls()
        {
            if (_toolkitOptionsScreen == null)
            {
                return;
            }

            _toolkitOptionsFullscreenToggle?.SetValueWithoutNotify(PlayerPrefs.GetInt(FullscreenPreferenceKey, 0) != 0);

            var audio = AudioService.Instance;
            _toolkitOptionsMasterVolumeSlider?.SetValueWithoutNotify(audio.MasterVolume);
            _toolkitOptionsBgmVolumeSlider?.SetValueWithoutNotify(audio.BgmVolume);
            _toolkitOptionsSfxVolumeSlider?.SetValueWithoutNotify(audio.SfxVolume);

            UpdateToolkitSliderValueLabel(_toolkitOptionsMasterVolumeValueLabel, audio.MasterVolume);
            UpdateToolkitSliderValueLabel(_toolkitOptionsBgmVolumeValueLabel, audio.BgmVolume);
            UpdateToolkitSliderValueLabel(_toolkitOptionsSfxVolumeValueLabel, audio.SfxVolume);
        }

        private static void UpdateToolkitSliderValueLabel(Label label, float value)
        {
            if (label != null)
            {
                label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
            }
        }

        private void RefreshToolkitRunSetupPanel()
        {
            if (_toolkitRunSetupScreen == null)
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
            _selectedDifficultyId = SharedRunCatalog.GetDifficulty(_selectedDifficultyId).Id;
            _selectedStarterWeaponId = MetaProgressionService.GetCharacterStarterWeapon(_selectedCharacterId);
            _inspectedCharacterId = SharedGameCatalog.NormalizeCharacterId(_inspectedCharacterId);
            if (_inspectedCharacterId == 0 && _selectedCharacterId != 0)
            {
                _inspectedCharacterId = _selectedCharacterId;
            }

            var selectedMap = SharedRunCatalog.GetMap(_selectedMapId);
            var selectedDifficulty = SharedRunCatalog.GetDifficulty(_selectedDifficultyId);
            var selectedCharacter = SharedGameCatalog.GetCharacter(_selectedCharacterId);
            var inspectedCharacter = SharedGameCatalog.GetCharacter(_inspectedCharacterId);
            var inspectedUnlocked = MetaProgressionService.IsCharacterUnlocked(inspectedCharacter.Id);
            var mapUnlocked = SharedRunCatalog.IsMapUnlocked(_selectedMapId);
            var interactable = !MultiplayerSessionController.EnsureInstance().IsBusy;
            var isMapStep = _currentRunSetupStep == SingleRunSetupStep.MapSelect;

            if (_toolkitRunSetupHeader != null)
            {
                _toolkitRunSetupHeader.text = isMapStep ? "맵 선택" : "캐릭터 선택";
            }

            if (_toolkitRunSetupHint != null)
            {
                _toolkitRunSetupHint.text = isMapStep
                    ? "맵과 난이도를 먼저 정하세요."
                    : "캐릭터를 선택하고 출격을 시작하세요.";
            }

            if (_toolkitRunSetupMapSelectionLabel != null)
            {
                _toolkitRunSetupMapSelectionLabel.text = $"{selectedMap.DisplayName} | {selectedDifficulty.DisplayName}";
            }

            if (_toolkitRunSetupMapLockLabel != null)
            {
                _toolkitRunSetupMapLockLabel.text = mapUnlocked
                    ? "선택한 맵으로 출격할 수 있습니다."
                    : SharedRunCatalog.GetMapUnlockRequirementText(selectedMap.Id);
            }

            if (_toolkitRunSetupSelectionSummaryLabel != null)
            {
                _toolkitRunSetupSelectionSummaryLabel.text =
                    $"{selectedMap.DisplayName} | {selectedDifficulty.DisplayName} | 현재 선택 {selectedCharacter.DisplayName}";
            }

            if (_toolkitRunSetupCharacterNameLabel != null)
            {
                _toolkitRunSetupCharacterNameLabel.text =
                    $"{inspectedCharacter.DisplayName} | {SharedGameCatalog.GetWeaponDisplayName(inspectedCharacter.StarterWeaponId)}";
                _toolkitRunSetupCharacterNameLabel.style.color = inspectedCharacter.Color;
            }

            if (_toolkitRunSetupCharacterDetailLabel != null)
            {
                _toolkitRunSetupCharacterDetailLabel.text =
                    $"기본 보너스 {BuildMetaBonusSummary(selectedCharacter.BaseBonuses)}\n" +
                    $"고유 특성 {selectedCharacter.PassiveDescription}";
            }

            if (_toolkitRunSetupCharacterDetailLabel != null)
            {
                var inspectedStatus = GetToolkitCharacterStatusText(
                    inspectedCharacter,
                    inspectedUnlocked,
                    inspectedCharacter.Id == _selectedCharacterId);
                _toolkitRunSetupCharacterDetailLabel.text =
                    $"상태: {inspectedStatus}\n" +
                    $"기본 보너스 {BuildMetaBonusSummary(inspectedCharacter.BaseBonuses)}\n" +
                    $"고유 특성 {inspectedCharacter.PassiveDescription}";
            }

            SetDisplay(_toolkitRunSetupMapStep, isMapStep);
            SetDisplay(_toolkitRunSetupCharacterStep, !isMapStep);
            RebuildToolkitRunSetupMapButtons();
            RebuildToolkitRunSetupDifficultyButtons();
            RebuildToolkitRunSetupCharacterButtons();

            _toolkitRunSetupMapNextButton?.SetEnabled(interactable && mapUnlocked && isMapStep);
            _toolkitRunSetupMapBackButton?.SetEnabled(interactable);
            _toolkitRunSetupCharacterBackButton?.SetEnabled(interactable);
            _toolkitRunSetupStartButton?.SetEnabled(
                interactable &&
                !isMapStep &&
                MetaProgressionService.IsCharacterUnlocked(_selectedCharacterId) &&
                mapUnlocked);

            UpdateToolkitOverviewSummary();
        }

        private void RebuildToolkitRunSetupMapButtons()
        {
            if (_toolkitRunSetupMapButtonRow == null)
            {
                return;
            }

            _toolkitRunSetupMapButtonRow.Clear();
            _toolkitDynamicButtons.Clear();

            var interactable = !MultiplayerSessionController.EnsureInstance().IsBusy;
            for (var i = 0; i < SharedRunCatalog.MapDefinitions.Count; i++)
            {
                var definition = SharedRunCatalog.MapDefinitions[i];
                var unlocked = SharedRunCatalog.IsMapUnlocked(definition.Id);
                var button = new Button(() => SelectSingleMap(definition.Id))
                {
                    text = unlocked ? definition.DisplayName : $"{definition.DisplayName}\n잠김"
                };
                button.AddToClassList("title-chip-button");
                button.EnableInClassList("is-selected", definition.Id == _selectedMapId);
                button.EnableInClassList("is-locked", !unlocked);
                button.SetEnabled(interactable && unlocked);
                _toolkitRunSetupMapButtonRow.Add(button);
                _toolkitDynamicButtons.Add(button);
            }
        }

        private void RebuildToolkitRunSetupDifficultyButtons()
        {
            if (_toolkitRunSetupDifficultyButtonRow == null)
            {
                return;
            }

            _toolkitRunSetupDifficultyButtonRow.Clear();

            var interactable = !MultiplayerSessionController.EnsureInstance().IsBusy;
            for (var i = 0; i < SharedRunCatalog.DifficultyDefinitions.Count; i++)
            {
                var definition = SharedRunCatalog.DifficultyDefinitions[i];
                var button = new Button(() => SelectSingleDifficulty(definition.Id))
                {
                    text = definition.DisplayName
                };
                button.AddToClassList("title-chip-button");
                button.EnableInClassList("is-selected", definition.Id == _selectedDifficultyId);
                button.SetEnabled(interactable);
                _toolkitRunSetupDifficultyButtonRow.Add(button);
                _toolkitDynamicButtons.Add(button);
            }
        }

        private void RebuildToolkitRunSetupCharacterButtons()
        {
            if (_toolkitRunSetupCharacterScroll == null)
            {
                return;
            }

            _toolkitRunSetupCharacterScroll.contentContainer.Clear();
            _toolkitRunSetupCharacterScroll.mode = ScrollViewMode.Vertical;
            _toolkitRunSetupCharacterScroll.contentContainer.style.width = Length.Percent(100);
            _toolkitRunSetupCharacterScroll.contentContainer.style.flexGrow = 1f;
            _toolkitRunSetupCharacterScroll.contentContainer.style.flexDirection = FlexDirection.Row;
            _toolkitRunSetupCharacterScroll.contentContainer.style.flexWrap = Wrap.Wrap;
            _toolkitRunSetupCharacterScroll.contentContainer.style.alignContent = Align.FlexStart;
            _toolkitRunSetupCharacterScroll.contentContainer.style.alignItems = Align.FlexStart;
            _toolkitRunSetupCharacterScroll.contentContainer.style.justifyContent = Justify.FlexStart;

            var interactable = !MultiplayerSessionController.EnsureInstance().IsBusy;
            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                var unlocked = MetaProgressionService.IsCharacterUnlocked(definition.Id);
                var displayStatus = GetToolkitCharacterStatusText(definition, unlocked, definition.Id == _selectedCharacterId);
                var status = unlocked
                    ? (definition.Id == _selectedCharacterId ? "선택됨" : "선택 가능")
                    : definition.UnlockSource == CharacterUnlockSource.Achievement
                        ? "도전과제 해금"
                        : "상점 해금";

                var button = new Button(() => InspectToolkitCharacter(definition.Id));
                button.AddToClassList("title-list-entry");
                button.AddToClassList("title-character-entry");
                button.EnableInClassList("is-selected", definition.Id == _inspectedCharacterId);
                button.EnableInClassList("is-locked", !unlocked);
                button.SetEnabled(interactable);

                var portraitShell = new VisualElement();
                portraitShell.AddToClassList("title-character-entry-visual-shell");

                var portrait = new Image
                {
                    sprite = RuntimeSpriteFactory.GetPlayerSprite(),
                    tintColor = definition.Color,
                    scaleMode = ScaleMode.ScaleToFit,
                };
                portrait.AddToClassList("title-character-entry-visual");
                portraitShell.Add(portrait);
                button.Add(portraitShell);

                var metaColumn = new VisualElement();
                metaColumn.AddToClassList("title-character-entry-meta");

                var title = new Label(definition.DisplayName);
                title.AddToClassList("title-entry-title");
                title.AddToClassList("title-character-entry-name");
                metaColumn.Add(title);


                var subtitle = new Label($"기본 보너스 {BuildMetaBonusSummary(definition.BaseBonuses)}");
                subtitle.AddToClassList("title-entry-subtitle");
                subtitle.style.display = DisplayStyle.None;
                button.Add(subtitle);

                var detail = new Label(definition.PassiveDescription);
                detail.AddToClassList("title-entry-subtitle");
                detail.style.display = DisplayStyle.None;
                button.Add(detail);

                var statusLabel = new Label(displayStatus);
                statusLabel.AddToClassList("title-entry-status");
                statusLabel.AddToClassList("title-character-entry-status");
                if (unlocked)
                {
                    statusLabel.AddToClassList("is-completed");
                }

                metaColumn.Add(statusLabel);
                button.Add(metaColumn);
                _toolkitRunSetupCharacterScroll.contentContainer.Add(button);
                _toolkitDynamicButtons.Add(button);
            }
        }

        private static string GetToolkitCharacterStatusText(SharedCharacterDefinition definition, bool unlocked, bool selected)
        {
            if (unlocked)
            {
                return selected ? "선택됨" : "선택 가능";
            }

            return definition.UnlockSource == CharacterUnlockSource.Achievement
                ? "도전과제 해금"
                : "상점 해금";
        }

        private void InspectToolkitCharacter(int characterId)
        {
            characterId = SharedGameCatalog.NormalizeCharacterId(characterId);
            _inspectedCharacterId = characterId;

            if (MetaProgressionService.IsCharacterUnlocked(characterId))
            {
                var definition = SharedGameCatalog.GetCharacter(characterId);
                _selectedCharacterId = characterId;
                _selectedStarterWeaponId = definition.StarterWeaponId;
            }

            RefreshToolkitRunSetupPanel();
        }

        private void RefreshToolkitAchievementPanel()
        {
            if (_toolkitAchievementScroll == null || _toolkitAchievementSummaryLabel == null)
            {
                return;
            }

            _toolkitAchievementScroll.contentContainer.Clear();
            _toolkitAchievementScroll.contentContainer.style.flexDirection = FlexDirection.Row;
            _toolkitAchievementScroll.contentContainer.style.flexWrap = Wrap.Wrap;
            _toolkitAchievementScroll.contentContainer.style.alignContent = Align.FlexStart;
            _toolkitAchievementScroll.contentContainer.style.alignItems = Align.FlexStart;
            _toolkitAchievementScroll.contentContainer.style.justifyContent = Justify.FlexStart;
            _toolkitAchievementScroll.contentContainer.style.width = Length.Percent(100);
            var entries = MetaProgressionService.GetAchievementEntries();
            var completedCount = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.IsCompleted)
                {
                    completedCount++;
                }

                var row = new VisualElement();
                row.AddToClassList("title-list-entry");
                row.AddToClassList("title-meta-card");
                row.AddToClassList("title-achievement-card");
                row.EnableInClassList("is-completed", entry.IsCompleted);

                var title = new Label(entry.DisplayName);
                title.AddToClassList("title-entry-title");
                title.AddToClassList("title-meta-card-title");
                row.Add(title);

                var subtitle = new Label(entry.Description);
                subtitle.AddToClassList("title-entry-subtitle");
                subtitle.AddToClassList("title-meta-card-body");
                row.Add(subtitle);

                var progress = new Label(entry.ProgressText);
                progress.AddToClassList("title-entry-status");
                progress.AddToClassList("title-meta-card-status");
                if (entry.IsCompleted)
                {
                    progress.AddToClassList("is-completed");
                }

                row.Add(progress);

                if (!string.IsNullOrWhiteSpace(entry.RewardText))
                {
                    var reward = new Label(entry.RewardText);
                    reward.AddToClassList("title-entry-subtitle");
                    reward.AddToClassList("title-achievement-card-reward");
                    row.Add(reward);
                }

                _toolkitAchievementScroll.contentContainer.Add(row);
            }

            _toolkitAchievementSummaryLabel.text = $"달성 {completedCount}/{entries.Count}";
        }

        private void RefreshToolkitMetaPanel()
        {
            if (_toolkitMetaScroll == null || _toolkitMetaHeaderLabel == null)
            {
                return;
            }

            _toolkitMetaHeaderLabel.text =
                $"코인 {MetaProgressionService.CurrentCredits} | 누적 수익 {MetaProgressionService.TotalCreditsEarned}\n" +
                $"플레이 {MetaProgressionService.RunsPlayed} | 클리어 {MetaProgressionService.RunsCleared}\n" +
                $"최고 레벨 {MetaProgressionService.BestLevel} | 최고 시간 {MetaProgressionService.BestTimeSeconds:0.0}초 | 처치 {MetaProgressionService.TotalEnemiesDefeated}";

            ApplyToolkitMetaTabState(_toolkitMetaUnlocksTabButton, _currentMetaTab == MetaTab.Unlocks);
            ApplyToolkitMetaTabState(_toolkitMetaUpgradesTabButton, _currentMetaTab == MetaTab.Upgrades);

            var refund = MetaProgressionService.GetUpgradeRefundPreview();
            if (_toolkitMetaUpgradeResetButton != null)
            {
                _toolkitMetaUpgradeResetButton.text = refund > 0 ? $"강화 초기화 | {refund} 코인" : "강화 초기화";
                _toolkitMetaUpgradeResetButton.SetEnabled(_currentMetaTab == MetaTab.Upgrades && refund > 0);
                SetDisplay(_toolkitMetaUpgradeResetButton, _currentMetaTab == MetaTab.Upgrades);
            }

            _toolkitMetaScroll.contentContainer.Clear();
            _toolkitMetaScroll.contentContainer.style.flexDirection = FlexDirection.Row;
            _toolkitMetaScroll.contentContainer.style.flexWrap = Wrap.Wrap;
            _toolkitMetaScroll.contentContainer.style.alignContent = Align.FlexStart;
            _toolkitMetaScroll.contentContainer.style.alignItems = Align.FlexStart;
            _toolkitMetaScroll.contentContainer.style.justifyContent = Justify.FlexStart;
            _toolkitMetaScroll.contentContainer.style.width = Length.Percent(100);
            if (_currentMetaTab == MetaTab.Unlocks)
            {
                BuildToolkitCharacterShopContent();
            }
            else
            {
                BuildToolkitUpgradeShopContent();
            }

            UpdateToolkitOverviewSummary();
        }

        private void BuildToolkitCharacterShopContent()
        {
            if (_toolkitMetaScroll == null)
            {
                return;
            }

            var content = _toolkitMetaScroll.contentContainer;
            for (var i = 0; i < SharedGameCatalog.CharacterDefinitions.Count; i++)
            {
                var definition = SharedGameCatalog.CharacterDefinitions[i];
                if (definition.UnlockSource == CharacterUnlockSource.Achievement)
                {
                    continue;
                }

                var unlocked = MetaProgressionService.IsCharacterUnlocked(definition.Id);
                var canBuy = CanPurchaseCharacter(definition.Id);
                var row = CreateToolkitRowWithAction(
                    $"{definition.DisplayName} | {SharedGameCatalog.GetWeaponDisplayName(definition.StarterWeaponId)}",
                    $"기본 보너스 {BuildMetaBonusSummary(definition.BaseBonuses)}\n고유 특성 {definition.PassiveDescription}",
                    unlocked ? "해금 완료" : $"비용 {definition.UnlockCost} 코인",
                    unlocked ? "해금 완료" : $"구매 ({definition.UnlockCost}코인)",
                    unlocked ? null : () => PromptCharacterPurchase(definition.Id),
                    unlocked || canBuy,
                    definition.Color,
                    unlocked);
                content.Add(row);
            }
        }

        private void BuildToolkitUpgradeShopContent()
        {
            if (_toolkitMetaScroll == null)
            {
                return;
            }

            var content = _toolkitMetaScroll.contentContainer;
            var definitions = MetaProgressionService.Config.UpgradeDefinitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                var level = MetaProgressionService.GetUpgradeLevel(definition.Id);
                var maxed = level >= definition.MaxLevel;
                var cost = maxed ? 0 : MetaProgressionService.Config.GetUpgradeCost(definition.Id, level);
                var canBuy = CanPurchaseUpgrade(definition.Id);
                var card = CreateToolkitUpgradeCard(definition, level, cost, canBuy, maxed);
                content.Add(card);
            }
        }

        private VisualElement CreateToolkitUpgradeCard(
            MetaUpgradeDefinition definition,
            int level,
            int cost,
            bool canBuy,
            bool maxed)
        {
            var card = new VisualElement();
            card.AddToClassList("title-list-entry");
            card.AddToClassList("title-meta-card");
            card.AddToClassList("title-upgrade-card");
            card.EnableInClassList("is-completed", maxed);

            var title = new Label(definition.Title);
            title.AddToClassList("title-entry-title");
            title.AddToClassList("title-meta-card-title");
            card.Add(title);

            var subtitle = new Label(definition.Description);
            subtitle.AddToClassList("title-entry-subtitle");
            subtitle.AddToClassList("title-meta-card-body");
            card.Add(subtitle);

            var levelSummary = new Label(BuildUpgradeLevelSummary(definition, level));
            levelSummary.AddToClassList("title-entry-status");
            levelSummary.AddToClassList("title-meta-card-status");
            if (maxed)
            {
                levelSummary.AddToClassList("is-completed");
            }

            card.Add(levelSummary);
            card.Add(CreateToolkitUpgradePipRow(level, definition.MaxLevel));

            var button = new Button
            {
                text = maxed ? "최대" : $"구매 ({cost}코인)"
            };
            button.AddToClassList("title-footer-button");
            button.AddToClassList("title-meta-card-action");
            button.SetEnabled(!maxed && canBuy);
            if (!maxed)
            {
                button.clicked += () => TryPurchaseUpgrade(definition.Id);
            }

            card.Add(button);
            _toolkitDynamicButtons.Add(button);
            return card;
        }

        private static VisualElement CreateToolkitUpgradePipRow(int level, int maxLevel)
        {
            var row = new VisualElement();
            row.AddToClassList("title-upgrade-pip-row");
            var clampedMaxLevel = Mathf.Max(1, maxLevel);
            var clampedLevel = Mathf.Clamp(level, 0, clampedMaxLevel);
            for (var i = 0; i < clampedMaxLevel; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("title-upgrade-pip");
                if (i < clampedLevel)
                {
                    pip.AddToClassList("is-filled");
                }

                row.Add(pip);
            }

            return row;
        }

        private string BuildUpgradeLevelSummary(MetaUpgradeDefinition definition, int level)
        {
            var currentSummary = level > 0
                ? BuildMetaBonusSummary(definition.StepBonuses * level)
                : "없음";
            if (level >= definition.MaxLevel)
            {
                return $"{currentSummary} | 최대 단계";
            }

            var nextSummary = BuildMetaBonusSummary(definition.StepBonuses * (level + 1));
            return $"{currentSummary} | 다음 {nextSummary}";
        }

        private VisualElement CreateToolkitRowWithAction(
            string titleText,
            string subtitleText,
            string statusText,
            string actionText,
            System.Action action,
            bool actionEnabled,
            Color titleColor,
            bool completed)
        {
            var row = new VisualElement();
            row.AddToClassList("title-list-entry");
            row.AddToClassList("title-meta-card");
            row.EnableInClassList("is-completed", completed);

            var title = new Label(titleText);
            title.AddToClassList("title-entry-title");
            title.AddToClassList("title-meta-card-title");
            title.style.color = titleColor;
            row.Add(title);

            var subtitle = new Label(subtitleText);
            subtitle.AddToClassList("title-entry-subtitle");
            subtitle.AddToClassList("title-meta-card-body");
            row.Add(subtitle);

            var status = new Label(statusText);
            status.AddToClassList("title-entry-status");
            status.AddToClassList("title-meta-card-status");
            if (completed)
            {
                status.AddToClassList("is-completed");
            }

            row.Add(status);

            var button = new Button();
            button.text = actionText;
            button.AddToClassList("title-footer-button");
            button.AddToClassList("title-meta-card-action");
            button.SetEnabled(actionEnabled && action != null);
            if (action != null)
            {
                button.clicked += () => action();
            }

            row.Add(button);
            _toolkitDynamicButtons.Add(button);
            return row;
        }

        private static void ApplyToolkitMetaTabState(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = selected
                ? new Color(52f / 255f, 80f / 255f, 114f / 255f, 1f)
                : new Color(20f / 255f, 28f / 255f, 38f / 255f, 0.98f);
            button.style.color = selected
                ? new Color(0.98f, 0.86f, 0.42f, 1f)
                : new Color(0.93f, 0.95f, 0.98f, 1f);
        }

        private static void SetDisplay(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private string GetToolkitJoinCode()
        {
            return _toolkitJoinCodeField?.value ?? string.Empty;
        }

        private void OnToolkitOptionsBackClicked()
        {
            AudioService.Instance.PlayUi(AudioCueId.UiBack);
            ShowMainMenu();
            SetStatus("메뉴를 선택하세요.");
        }

        private void OnToolkitOptionsFullscreenChanged(ChangeEvent<bool> evt)
        {
            if (_suppressDisplayToggleCallback)
            {
                return;
            }

            ApplyDisplayMode(evt.newValue, true);
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
            SetStatus(evt.newValue ? "화면 모드: 전체 화면" : "화면 모드: 창 모드");
        }

        private void OnToolkitOptionsMasterVolumeChanged(ChangeEvent<float> evt)
        {
            UpdateToolkitSliderValueLabel(_toolkitOptionsMasterVolumeValueLabel, evt.newValue);
            if (_suppressAudioSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetMasterVolume(evt.newValue);
            SyncAudioSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnToolkitOptionsBgmVolumeChanged(ChangeEvent<float> evt)
        {
            UpdateToolkitSliderValueLabel(_toolkitOptionsBgmVolumeValueLabel, evt.newValue);
            if (_suppressAudioSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetBgmVolume(evt.newValue);
            SyncAudioSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnToolkitOptionsSfxVolumeChanged(ChangeEvent<float> evt)
        {
            UpdateToolkitSliderValueLabel(_toolkitOptionsSfxVolumeValueLabel, evt.newValue);
            if (_suppressAudioSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetSfxVolume(evt.newValue);
            SyncAudioSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnDestroy()
        {
            if (_toolkitDocument != null)
            {
                if (_toolkitSinglePlayButton != null) _toolkitSinglePlayButton.clicked -= OnSinglePlayClicked;
                if (_toolkitMultiPlayButton != null) _toolkitMultiPlayButton.clicked -= OnMultiPlayClicked;
                if (_toolkitAchievementButton != null) _toolkitAchievementButton.clicked -= OnAchievementsClicked;
                if (_toolkitMetaButton != null) _toolkitMetaButton.clicked -= OnMetaClicked;
                if (_toolkitOptionsButton != null) _toolkitOptionsButton.clicked -= OnOptionsClicked;
                if (_toolkitQuitButton != null) _toolkitQuitButton.clicked -= OnQuitClicked;
                if (_toolkitMultiplayerHostButton != null) _toolkitMultiplayerHostButton.clicked -= OnHostClicked;
                if (_toolkitMultiplayerJoinButton != null) _toolkitMultiplayerJoinButton.clicked -= OnJoinClicked;
                if (_toolkitMultiplayerBackButton != null) _toolkitMultiplayerBackButton.clicked -= ShowMainMenu;
                if (_toolkitRunSetupMapNextButton != null) _toolkitRunSetupMapNextButton.clicked -= GoToRunSetupCharacterStep;
                if (_toolkitRunSetupMapBackButton != null) _toolkitRunSetupMapBackButton.clicked -= ShowMainMenu;
                if (_toolkitRunSetupStartButton != null) _toolkitRunSetupStartButton.clicked -= StartSinglePlay;
                if (_toolkitRunSetupCharacterBackButton != null) _toolkitRunSetupCharacterBackButton.clicked -= GoToRunSetupMapStep;
                if (_toolkitAchievementBackButton != null) _toolkitAchievementBackButton.clicked -= ShowMainMenu;
                if (_toolkitMetaBackButton != null) _toolkitMetaBackButton.clicked -= ShowMainMenu;
                if (_toolkitSummaryMetaButton != null) _toolkitSummaryMetaButton.clicked -= OpenMetaFromSummary;
                if (_toolkitSummaryCloseButton != null) _toolkitSummaryCloseButton.clicked -= CloseSummaryModal;
                if (_toolkitConfirmConfirmButton != null) _toolkitConfirmConfirmButton.clicked -= ConfirmPendingAction;
                if (_toolkitConfirmCancelButton != null) _toolkitConfirmCancelButton.clicked -= CloseConfirmModal;
                if (_toolkitOptionsBackButton != null) _toolkitOptionsBackButton.clicked -= OnToolkitOptionsBackClicked;
                _toolkitOptionsFullscreenToggle?.UnregisterValueChangedCallback(OnToolkitOptionsFullscreenChanged);
                _toolkitOptionsMasterVolumeSlider?.UnregisterValueChangedCallback(OnToolkitOptionsMasterVolumeChanged);
                _toolkitOptionsBgmVolumeSlider?.UnregisterValueChangedCallback(OnToolkitOptionsBgmVolumeChanged);
                _toolkitOptionsSfxVolumeSlider?.UnregisterValueChangedCallback(OnToolkitOptionsSfxVolumeChanged);
            }

            if (_toolkitPanelSettings != null)
            {
                Destroy(_toolkitPanelSettings);
                _toolkitPanelSettings = null;
            }
        }
    }
}
