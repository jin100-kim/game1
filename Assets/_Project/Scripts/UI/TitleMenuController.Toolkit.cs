using EJR.Game.Audio;
using UnityEngine;
using UnityEngine.UIElements;
using EJR.Game.Core;
using EJR.Game.Multiplayer;

namespace EJR.Game.UI
{
    public sealed partial class TitleMenuController
    {
        private const string ToolkitLayoutResourcePath = "UI/Title/TitleMenuLayout";
        private const string ToolkitStylesResourcePath = "UI/Title/TitleMenuStyles";
        private const string ToolkitOptionsLayoutResourcePath = "UI/Common/SettingsPanelLayout";
        private const string ToolkitOptionsStylesResourcePath = "UI/Common/SettingsPanelStyles";
        private const string ToolkitRuntimeThemeResourcePath = "UI/Common/UnityDefaultRuntimeTheme";

        private UIDocument _toolkitDocument;
        private PanelSettings _toolkitPanelSettings;
        private VisualElement _toolkitMainShell;
        private VisualElement _toolkitOptionsScreen;
        private Label _toolkitStatusLabel;
        private Label _toolkitProfileLabel;
        private Label _toolkitRecentRunLabel;
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
        private Button _toolkitQuitButton;
        private Button _toolkitOptionsBackButton;

        private bool HasToolkitMainMenu => _toolkitMainShell != null;
        private bool HasToolkitOptionsScreen => _toolkitOptionsScreen != null;

        private bool SupportsToolkitOptionsPanel()
        {
            return Resources.Load<VisualTreeAsset>(ToolkitOptionsLayoutResourcePath) != null;
        }

        private void BuildToolkitMainMenu()
        {
            if (_toolkitDocument != null)
            {
                return;
            }

            var layout = Resources.Load<VisualTreeAsset>(ToolkitLayoutResourcePath);
            if (layout == null)
            {
                Debug.LogWarning($"Title menu layout resource not found at Resources/{ToolkitLayoutResourcePath}.");
                return;
            }

            var styles = Resources.Load<StyleSheet>(ToolkitStylesResourcePath);

            var documentObject = new GameObject("TitleToolkitMenu");
            documentObject.transform.SetParent(transform, false);

            _toolkitDocument = documentObject.AddComponent<UIDocument>();
            _toolkitPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _toolkitPanelSettings.name = "RuntimeTitleMenuPanelSettings";
            _toolkitPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _toolkitPanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _toolkitPanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _toolkitPanelSettings.match = 0.5f;
            _toolkitPanelSettings.sortingOrder = 120;
            _toolkitPanelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ToolkitRuntimeThemeResourcePath);
            _toolkitDocument.panelSettings = _toolkitPanelSettings;

            var root = _toolkitDocument.rootVisualElement;
            root.Clear();
            layout.CloneTree(root);
            if (styles != null && !root.styleSheets.Contains(styles))
            {
                root.styleSheets.Add(styles);
            }

            var optionsLayout = Resources.Load<VisualTreeAsset>(ToolkitOptionsLayoutResourcePath);
            if (optionsLayout != null)
            {
                optionsLayout.CloneTree(root);
                var optionsStyles = Resources.Load<StyleSheet>(ToolkitOptionsStylesResourcePath);
                if (optionsStyles != null && !root.styleSheets.Contains(optionsStyles))
                {
                    root.styleSheets.Add(optionsStyles);
                }
            }

            _toolkitMainShell = root.Q<VisualElement>("screen");
            _toolkitOptionsScreen = root.Q<VisualElement>("settings-screen");
            _toolkitStatusLabel = root.Q<Label>("status-line");
            _toolkitProfileLabel = root.Q<Label>("profile-summary");
            _toolkitRecentRunLabel = root.Q<Label>("recent-run-summary");
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
            _toolkitQuitButton = root.Q<Button>("quit-button");
            _toolkitOptionsBackButton = root.Q<Button>("settings-back-button");

            if (_toolkitSinglePlayButton != null) _toolkitSinglePlayButton.clicked += OnSinglePlayClicked;
            if (_toolkitMultiPlayButton != null) _toolkitMultiPlayButton.clicked += OnMultiPlayClicked;
            if (_toolkitAchievementButton != null) _toolkitAchievementButton.clicked += OnAchievementsClicked;
            if (_toolkitMetaButton != null) _toolkitMetaButton.clicked += OnMetaClicked;
            if (_toolkitOptionsButton != null) _toolkitOptionsButton.clicked += OnOptionsClicked;
            if (_toolkitQuitButton != null) _toolkitQuitButton.clicked += OnQuitClicked;
            if (_toolkitOptionsBackButton != null) _toolkitOptionsBackButton.clicked += OnToolkitOptionsBackClicked;
            _toolkitOptionsFullscreenToggle?.RegisterValueChangedCallback(OnToolkitOptionsFullscreenChanged);
            _toolkitOptionsMasterVolumeSlider?.RegisterValueChangedCallback(OnToolkitOptionsMasterVolumeChanged);
            _toolkitOptionsBgmVolumeSlider?.RegisterValueChangedCallback(OnToolkitOptionsBgmVolumeChanged);
            _toolkitOptionsSfxVolumeSlider?.RegisterValueChangedCallback(OnToolkitOptionsSfxVolumeChanged);

            UpdateToolkitOverviewSummary();
            RefreshAchievementButtonState();
            UpdateToolkitStatus(string.Empty);
            UpdateToolkitInteractivity(!MultiplayerSessionController.EnsureInstance().IsBusy);
            UpdateToolkitMainMenuVisibility(false);
            UpdateToolkitOptionsVisibility(false);
        }

        private void UpdateToolkitMainMenuVisibility(bool visible)
        {
            if (_toolkitMainShell == null)
            {
                return;
            }

            _toolkitMainShell.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
            {
                FocusToolkitPrimaryButton();
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
                $"크레딧 {MetaProgressionService.CurrentCredits}\n" +
                $"선택 {selectedCharacter.DisplayName} · {SharedGameCatalog.GetWeaponDisplayName(selectedCharacter.StarterWeaponId)}\n" +
                $"캐릭터 {unlockedCharacters}/{SharedGameCatalog.CharacterDefinitions.Count} · 강화 {purchasedUpgradeLevels}단계\n" +
                $"최고 레벨 {MetaProgressionService.BestLevel} · 최고 생존 {MetaProgressionService.BestTimeSeconds:0.0}초";

            _toolkitRecentRunLabel.text = string.IsNullOrWhiteSpace(_recentRunSummaryText)
                ? "최근 결과가 없습니다."
                : _recentRunSummaryText;
        }

        private void UpdateToolkitStatus(string message)
        {
            if (_toolkitStatusLabel == null)
            {
                return;
            }

            _toolkitStatusLabel.text = string.IsNullOrWhiteSpace(message) ? " " : message;
        }

        private void UpdateToolkitInteractivity(bool interactable)
        {
            _toolkitSinglePlayButton?.SetEnabled(interactable);
            _toolkitMultiPlayButton?.SetEnabled(interactable);
            _toolkitAchievementButton?.SetEnabled(interactable);
            _toolkitMetaButton?.SetEnabled(interactable);
            _toolkitOptionsButton?.SetEnabled(interactable);
            _toolkitQuitButton?.SetEnabled(interactable);
            _toolkitOptionsScreen?.SetEnabled(interactable);
        }

        private void FocusToolkitPrimaryButton()
        {
            if (_toolkitMainShell == null || _toolkitMainShell.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            _toolkitSinglePlayButton?.schedule.Execute(() => _toolkitSinglePlayButton.Focus()).ExecuteLater(0);
        }

        private void UpdateToolkitOptionsVisibility(bool visible)
        {
            if (_toolkitOptionsScreen == null)
            {
                return;
            }

            _toolkitOptionsScreen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                SyncToolkitOptionsControls();
                _toolkitOptionsFullscreenToggle?.schedule.Execute(() => _toolkitOptionsFullscreenToggle.Focus()).ExecuteLater(0);
            }
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
            if (label == null)
            {
                return;
            }

            label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }

        private void OnToolkitOptionsBackClicked()
        {
            AudioService.Instance.PlayUi(AudioCueId.UiBack);
            ShowMainMenu();
            SetStatus("모드를 선택하세요.");
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
