using EJR.Game.Audio;
using UnityEngine;
using UnityEngine.UIElements;
using UIToolkitButton = UnityEngine.UIElements.Button;
using UIToolkitLabel = UnityEngine.UIElements.Label;
using UIToolkitSlider = UnityEngine.UIElements.Slider;
using UIToolkitToggle = UnityEngine.UIElements.Toggle;
using UIToolkitVisualElement = UnityEngine.UIElements.VisualElement;

namespace EJR.Game.UI
{
    public sealed partial class HudController
    {
        private const string PauseToolkitLayoutResourcePath = "UI/Common/PausePanelLayout";
        private const string PauseToolkitStylesResourcePath = "UI/Common/PausePanelStyles";
        private const string SettingsToolkitLayoutResourcePath = "UI/Common/SettingsPanelLayout";
        private const string SettingsToolkitStylesResourcePath = "UI/Common/SettingsPanelStyles";
        private const string SettingsToolkitThemeResourcePath = "UI/Common/UnityDefaultRuntimeTheme";
        private const string SettingsToolkitPanelSettingsResourcePath = "UI/Common/RuntimeMenuPanelSettings";

        private UIDocument _pauseSettingsDocument;
        private PanelSettings _pauseSettingsPanelSettings;
        private UIToolkitVisualElement _pauseToolkitScreen;
        private UIToolkitVisualElement _pauseToolkitMainView;
        private UIToolkitVisualElement _pauseSettingsToolkitScreen;
        private UIToolkitButton _pauseToolkitResumeButton;
        private UIToolkitButton _pauseToolkitSettingsButton;
        private UIToolkitButton _pauseToolkitQuitButton;
        private UIToolkitToggle _pauseSettingsToolkitFullscreenToggle;
        private UIToolkitSlider _pauseSettingsToolkitMasterVolumeSlider;
        private UIToolkitSlider _pauseSettingsToolkitBgmVolumeSlider;
        private UIToolkitSlider _pauseSettingsToolkitSfxVolumeSlider;
        private UIToolkitLabel _pauseSettingsToolkitMasterVolumeValueLabel;
        private UIToolkitLabel _pauseSettingsToolkitBgmVolumeValueLabel;
        private UIToolkitLabel _pauseSettingsToolkitSfxVolumeValueLabel;
        private UIToolkitButton _pauseSettingsToolkitBackButton;
        private System.Action _pauseToolkitResumeAction;
        private System.Action _pauseToolkitQuitAction;

        private bool HasPauseMainToolkit => _pauseToolkitScreen != null;
        private bool HasPauseSettingsToolkit => _pauseSettingsToolkitScreen != null;
        private bool IsPauseMainToolkitVisible => _pauseToolkitScreen != null && _pauseToolkitScreen.resolvedStyle.display != DisplayStyle.None;

        private void BuildPauseSettingsToolkitReference()
        {
            if (_pauseSettingsDocument != null || _canvas == null)
            {
                return;
            }

            var pauseLayout = Resources.Load<VisualTreeAsset>(PauseToolkitLayoutResourcePath);
            var settingsLayout = Resources.Load<VisualTreeAsset>(SettingsToolkitLayoutResourcePath);
            var panelTemplate = Resources.Load<PanelSettings>(SettingsToolkitPanelSettingsResourcePath);
            if ((pauseLayout == null && settingsLayout == null) || panelTemplate == null)
            {
                return;
            }

            var pauseStyles = Resources.Load<StyleSheet>(PauseToolkitStylesResourcePath);
            var settingsStyles = Resources.Load<StyleSheet>(SettingsToolkitStylesResourcePath);

            var documentObject = new GameObject("PauseSettingsToolkit");
            documentObject.transform.SetParent(_canvas.transform, false);

            _pauseSettingsDocument = documentObject.AddComponent<UIDocument>();
            _pauseSettingsPanelSettings = Object.Instantiate(panelTemplate);
            _pauseSettingsPanelSettings.name = "RuntimePauseSettingsPanelSettings";
            _pauseSettingsPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _pauseSettingsPanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _pauseSettingsPanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _pauseSettingsPanelSettings.match = 0.5f;
            _pauseSettingsPanelSettings.sortingOrder = 180;
            _pauseSettingsPanelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(SettingsToolkitThemeResourcePath);
            _pauseSettingsDocument.panelSettings = _pauseSettingsPanelSettings;

            var root = _pauseSettingsDocument.rootVisualElement;
            root.Clear();

            if (pauseLayout != null)
            {
                pauseLayout.CloneTree(root);
            }

            if (pauseStyles != null && !root.styleSheets.Contains(pauseStyles))
            {
                root.styleSheets.Add(pauseStyles);
            }

            if (settingsLayout != null)
            {
                settingsLayout.CloneTree(root);
            }

            if (settingsStyles != null && !root.styleSheets.Contains(settingsStyles))
            {
                root.styleSheets.Add(settingsStyles);
            }

            _pauseToolkitScreen = root.Q<UIToolkitVisualElement>("pause-screen");
            _pauseToolkitMainView = root.Q<UIToolkitVisualElement>("pause-main-view");
            _pauseSettingsToolkitScreen = root.Q<UIToolkitVisualElement>("settings-screen");
            _pauseToolkitResumeButton = root.Q<UIToolkitButton>("pause-resume-button");
            _pauseToolkitSettingsButton = root.Q<UIToolkitButton>("pause-settings-button");
            _pauseToolkitQuitButton = root.Q<UIToolkitButton>("pause-quit-button");
            _pauseSettingsToolkitFullscreenToggle = root.Q<UIToolkitToggle>("fullscreen-toggle");
            _pauseSettingsToolkitMasterVolumeSlider = root.Q<UIToolkitSlider>("master-volume-slider");
            _pauseSettingsToolkitBgmVolumeSlider = root.Q<UIToolkitSlider>("bgm-volume-slider");
            _pauseSettingsToolkitSfxVolumeSlider = root.Q<UIToolkitSlider>("sfx-volume-slider");
            _pauseSettingsToolkitMasterVolumeValueLabel = root.Q<UIToolkitLabel>("master-volume-value");
            _pauseSettingsToolkitBgmVolumeValueLabel = root.Q<UIToolkitLabel>("bgm-volume-value");
            _pauseSettingsToolkitSfxVolumeValueLabel = root.Q<UIToolkitLabel>("sfx-volume-value");
            _pauseSettingsToolkitBackButton = root.Q<UIToolkitButton>("settings-back-button");

            if (_pauseToolkitResumeButton != null)
            {
                _pauseToolkitResumeButton.clicked += OnPauseToolkitResumeClicked;
            }

            if (_pauseToolkitSettingsButton != null)
            {
                _pauseToolkitSettingsButton.clicked += OpenPauseSettings;
            }

            if (_pauseToolkitQuitButton != null)
            {
                _pauseToolkitQuitButton.clicked += OnPauseToolkitQuitClicked;
            }

            if (_pauseSettingsToolkitBackButton != null)
            {
                _pauseSettingsToolkitBackButton.text = "돌아가기";
                _pauseSettingsToolkitBackButton.clicked += OnPauseToolkitBackClicked;
            }

            _pauseSettingsToolkitFullscreenToggle?.RegisterValueChangedCallback(OnPauseToolkitFullscreenChanged);
            _pauseSettingsToolkitMasterVolumeSlider?.RegisterValueChangedCallback(OnPauseToolkitMasterVolumeChanged);
            _pauseSettingsToolkitBgmVolumeSlider?.RegisterValueChangedCallback(OnPauseToolkitBgmVolumeChanged);
            _pauseSettingsToolkitSfxVolumeSlider?.RegisterValueChangedCallback(OnPauseToolkitSfxVolumeChanged);

            UpdatePauseMainToolkitVisibility(false);
            UpdatePauseSettingsToolkitVisibility(false);
            SyncPauseSettingsToolkitControls();
        }

        private void ConfigurePauseToolkitActions(System.Action onResume, System.Action onQuit)
        {
            _pauseToolkitResumeAction = onResume;
            _pauseToolkitQuitAction = onQuit;
        }

        private void UpdatePauseMainToolkitVisibility(bool visible)
        {
            if (_pauseToolkitScreen == null)
            {
                return;
            }

            _pauseToolkitScreen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                return;
            }

            if (_pauseToolkitMainView != null)
            {
                _pauseToolkitMainView.style.display = DisplayStyle.Flex;
            }

            _pauseToolkitResumeButton?.schedule.Execute(() => _pauseToolkitResumeButton.Focus()).ExecuteLater(0);
        }

        private void UpdatePauseSettingsToolkitVisibility(bool visible)
        {
            if (_pauseSettingsToolkitScreen == null)
            {
                return;
            }

            _pauseSettingsToolkitScreen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                if (_pauseToolkitMainView != null)
                {
                    _pauseToolkitMainView.style.display = DisplayStyle.None;
                }

                SyncPauseSettingsToolkitControls();
                _pauseSettingsToolkitFullscreenToggle?.schedule.Execute(() => _pauseSettingsToolkitFullscreenToggle.Focus()).ExecuteLater(0);
            }
        }

        private void SyncPauseSettingsToolkitControls()
        {
            if (_pauseSettingsToolkitScreen == null)
            {
                return;
            }

            var audio = AudioService.Instance;
            _pauseSettingsToolkitFullscreenToggle?.SetValueWithoutNotify(PlayerPrefs.GetInt(FullscreenPreferenceKey, 0) != 0);
            _pauseSettingsToolkitMasterVolumeSlider?.SetValueWithoutNotify(audio.MasterVolume);
            _pauseSettingsToolkitBgmVolumeSlider?.SetValueWithoutNotify(audio.BgmVolume);
            _pauseSettingsToolkitSfxVolumeSlider?.SetValueWithoutNotify(audio.SfxVolume);

            UpdatePauseToolkitValueLabel(_pauseSettingsToolkitMasterVolumeValueLabel, audio.MasterVolume);
            UpdatePauseToolkitValueLabel(_pauseSettingsToolkitBgmVolumeValueLabel, audio.BgmVolume);
            UpdatePauseToolkitValueLabel(_pauseSettingsToolkitSfxVolumeValueLabel, audio.SfxVolume);
        }

        private static void UpdatePauseToolkitValueLabel(UIToolkitLabel label, float value)
        {
            if (label == null)
            {
                return;
            }

            label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }

        private void OnPauseToolkitBackClicked()
        {
            ClosePauseSettings();
        }

        private void OnPauseToolkitResumeClicked()
        {
            _pauseToolkitResumeAction?.Invoke();
        }

        private void OnPauseToolkitQuitClicked()
        {
            _pauseToolkitQuitAction?.Invoke();
        }

        private void OnPauseToolkitFullscreenChanged(ChangeEvent<bool> evt)
        {
            if (_suppressPauseSettingsCallbacks)
            {
                return;
            }

            ApplyDisplayMode(evt.newValue);
            SyncPauseSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnPauseToolkitMasterVolumeChanged(ChangeEvent<float> evt)
        {
            UpdatePauseToolkitValueLabel(_pauseSettingsToolkitMasterVolumeValueLabel, evt.newValue);
            if (_suppressPauseSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetMasterVolume(evt.newValue);
            SyncPauseSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnPauseToolkitBgmVolumeChanged(ChangeEvent<float> evt)
        {
            UpdatePauseToolkitValueLabel(_pauseSettingsToolkitBgmVolumeValueLabel, evt.newValue);
            if (_suppressPauseSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetBgmVolume(evt.newValue);
            SyncPauseSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnPauseToolkitSfxVolumeChanged(ChangeEvent<float> evt)
        {
            UpdatePauseToolkitValueLabel(_pauseSettingsToolkitSfxVolumeValueLabel, evt.newValue);
            if (_suppressPauseSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetSfxVolume(evt.newValue);
            SyncPauseSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }
    }
}
