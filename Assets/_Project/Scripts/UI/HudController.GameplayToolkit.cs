using System;
using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;
using UnityEngine.UIElements;
using UIToolkitButton = UnityEngine.UIElements.Button;
using UIToolkitLabel = UnityEngine.UIElements.Label;
using UIToolkitVisualElement = UnityEngine.UIElements.VisualElement;

namespace EJR.Game.UI
{
    public sealed partial class HudController
    {
        private const string GameplayToolkitLayoutResourcePath = "UI/Common/GameplayHudLayout";
        private const string GameplayToolkitStylesResourcePath = "UI/Common/GameplayHudStyles";
        private const string GameplayToolkitDevOverlayStylesResourcePath = "UI/Common/DevOverlayStyles";
        private UIDocument _gameplayToolkitDocument;
        private PanelSettings _gameplayToolkitPanelSettings;
        private UIToolkitVisualElement _gameplayToolkitScreen;
        private UIToolkitLabel _gameplayToolkitHealthLabel;
        private UIToolkitLabel _gameplayToolkitXpLabel;
        private UIToolkitLabel _gameplayToolkitTimeLabel;
        private UIToolkitVisualElement _gameplayToolkitModeHintCard;
        private UIToolkitLabel _gameplayToolkitModeHintLabel;
        private UIToolkitVisualElement _gameplayToolkitWaveStatusCard;
        private UIToolkitLabel _gameplayToolkitWaveStatusLabel;
        private UIToolkitVisualElement _gameplayToolkitWaveBannerCard;
        private UIToolkitLabel _gameplayToolkitWaveBannerLabel;
        private UIToolkitButton _gameplayToolkitBuildToggleButton;
        private UIToolkitVisualElement _gameplayToolkitBuildPanel;
        private UIToolkitLabel _gameplayToolkitWeaponBuildLabel;
        private UIToolkitLabel _gameplayToolkitStatBuildLabel;
        private UIToolkitVisualElement _gameplayToolkitBossPanel;
        private UIToolkitLabel _gameplayToolkitBossNameLabel;
        private UIToolkitVisualElement _gameplayToolkitBossFill;
        private UIToolkitLabel _gameplayToolkitBossValueLabel;
        private UIToolkitVisualElement _gameplayToolkitDirectionLayer;
        private UIToolkitLabel _gameplayToolkitBossDirectionIndicator;
        private UIToolkitLabel _gameplayToolkitWaveDirectionIndicator;
        private readonly List<UIToolkitLabel> _gameplayToolkitRewardIndicators = new();
        private UIToolkitVisualElement _gameplayToolkitLevelUpPanel;
        private UIToolkitLabel _gameplayToolkitLevelUpTitle;
        private readonly UIToolkitButton[] _gameplayToolkitLevelButtons = new UIToolkitButton[10];
        private readonly Action[] _gameplayToolkitLevelButtonHandlers = new Action[10];
        private UIToolkitVisualElement _gameplayToolkitResultPanel;
        private UIToolkitLabel _gameplayToolkitResultText;
        private UIToolkitButton _gameplayToolkitResultActionButton;
        private Action _gameplayToolkitResultAction;
        private UIToolkitButton _gameplayToolkitDebugAccessButton;
        private UIToolkitVisualElement _gameplayToolkitDebugPanel;
        private UIToolkitButton _gameplayToolkitDebugGrantLevelButton;
        private UIToolkitButton _gameplayToolkitDebugAdvanceTimeButton;
        private UIToolkitButton _gameplayToolkitDebugRerollButton;
        private UIToolkitButton _gameplayToolkitDebugWave1Button;
        private UIToolkitButton _gameplayToolkitDebugWave2Button;
        private UIToolkitButton _gameplayToolkitDebugSkipBossButton;
        private UIToolkitButton _gameplayToolkitDebugSpeedButton;
        private UIToolkitButton _gameplayToolkitDebugInvincibleButton;
        private UIToolkitButton _gameplayToolkitDebugAutoPlayButton;
        private UIToolkitVisualElement _gameplayToolkitMonsterLabSection;
        private Toggle _gameplayToolkitMonsterLabToggle;
        private DropdownField _gameplayToolkitMonsterLabDropdown;
        private UIToolkitButton _gameplayToolkitMonsterLabSpawnOneButton;
        private UIToolkitButton _gameplayToolkitMonsterLabSpawnFiveButton;
        private UIToolkitButton _gameplayToolkitMonsterLabClearButton;
        private UIToolkitButton _gameplayToolkitMonsterLabPauseButton;
        private readonly Dictionary<UIToolkitButton, Action> _gameplayToolkitDebugHandlers = new();
        private bool _suppressGameplayToolkitMonsterLabCallbacks;

        private bool HasGameplayToolkit => _gameplayToolkitScreen != null;

        private bool SupportsGameplayToolkitHud()
        {
            return Resources.Load<VisualTreeAsset>(GameplayToolkitLayoutResourcePath) != null;
        }

        private void BuildGameplayToolkitReference()
        {
            if (_gameplayToolkitDocument != null || _canvas == null)
            {
                return;
            }

            var layout = Resources.Load<VisualTreeAsset>(GameplayToolkitLayoutResourcePath);
            var panelTemplate = Resources.Load<PanelSettings>(SettingsToolkitPanelSettingsResourcePath);
            if (layout == null || panelTemplate == null)
            {
                return;
            }

            var styles = Resources.Load<StyleSheet>(GameplayToolkitStylesResourcePath);
            var devOverlayStyles = Resources.Load<StyleSheet>(GameplayToolkitDevOverlayStylesResourcePath);

            var documentObject = new GameObject("GameplayHudToolkit");
            documentObject.transform.SetParent(_canvas.transform, false);

            _gameplayToolkitDocument = documentObject.AddComponent<UIDocument>();
            _gameplayToolkitPanelSettings = UnityEngine.Object.Instantiate(panelTemplate);
            _gameplayToolkitPanelSettings.name = "RuntimeGameplayHudPanelSettings";
            _gameplayToolkitPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _gameplayToolkitPanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _gameplayToolkitPanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _gameplayToolkitPanelSettings.match = 0.5f;
            _gameplayToolkitPanelSettings.sortingOrder = 140;
            _gameplayToolkitPanelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(SettingsToolkitThemeResourcePath);
            _gameplayToolkitDocument.panelSettings = _gameplayToolkitPanelSettings;

            var root = _gameplayToolkitDocument.rootVisualElement;
            root.Clear();
            layout.CloneTree(root);
            if (styles != null && !root.styleSheets.Contains(styles))
            {
                root.styleSheets.Add(styles);
            }

            if (devOverlayStyles != null && !root.styleSheets.Contains(devOverlayStyles))
            {
                root.styleSheets.Add(devOverlayStyles);
            }

            _gameplayToolkitScreen = root.Q<UIToolkitVisualElement>("gameplay-hud-screen");
            _gameplayToolkitHealthLabel = root.Q<UIToolkitLabel>("health-text");
            _gameplayToolkitXpLabel = root.Q<UIToolkitLabel>("xp-text");
            _gameplayToolkitTimeLabel = root.Q<UIToolkitLabel>("time-text");
            _gameplayToolkitModeHintCard = root.Q<UIToolkitVisualElement>("mode-hint-card");
            _gameplayToolkitModeHintLabel = root.Q<UIToolkitLabel>("mode-hint-text");
            _gameplayToolkitWaveStatusCard = root.Q<UIToolkitVisualElement>("wave-status-card");
            _gameplayToolkitWaveStatusLabel = root.Q<UIToolkitLabel>("wave-status-text");
            _gameplayToolkitWaveBannerCard = root.Q<UIToolkitVisualElement>("wave-banner-card");
            _gameplayToolkitWaveBannerLabel = root.Q<UIToolkitLabel>("wave-banner-text");
            _gameplayToolkitBuildToggleButton = root.Q<UIToolkitButton>("build-toggle-button");
            _gameplayToolkitBuildPanel = root.Q<UIToolkitVisualElement>("build-panel");
            _gameplayToolkitWeaponBuildLabel = root.Q<UIToolkitLabel>("weapon-build-text");
            _gameplayToolkitStatBuildLabel = root.Q<UIToolkitLabel>("stat-build-text");
            _gameplayToolkitBossPanel = root.Q<UIToolkitVisualElement>("boss-bar-panel");
            _gameplayToolkitBossNameLabel = root.Q<UIToolkitLabel>("boss-name-text");
            _gameplayToolkitBossFill = root.Q<UIToolkitVisualElement>("boss-bar-fill");
            _gameplayToolkitBossValueLabel = root.Q<UIToolkitLabel>("boss-bar-value-text");
            _gameplayToolkitDirectionLayer = root.Q<UIToolkitVisualElement>("direction-layer");
            _gameplayToolkitLevelUpPanel = root.Q<UIToolkitVisualElement>("level-up-panel");
            _gameplayToolkitLevelUpTitle = root.Q<UIToolkitLabel>("level-up-title");
            _gameplayToolkitResultPanel = root.Q<UIToolkitVisualElement>("result-panel");
            _gameplayToolkitResultText = root.Q<UIToolkitLabel>("result-text");
            _gameplayToolkitResultActionButton = root.Q<UIToolkitButton>("result-action-button");
            _gameplayToolkitDebugAccessButton = root.Q<UIToolkitButton>("debug-access-button");
            _gameplayToolkitDebugPanel = root.Q<UIToolkitVisualElement>("debug-tools-panel");
            _gameplayToolkitDebugGrantLevelButton = root.Q<UIToolkitButton>("debug-grant-level-button");
            _gameplayToolkitDebugAdvanceTimeButton = root.Q<UIToolkitButton>("debug-advance-time-button");
            _gameplayToolkitDebugRerollButton = root.Q<UIToolkitButton>("debug-reroll-button");
            _gameplayToolkitDebugWave1Button = root.Q<UIToolkitButton>("debug-wave1-button");
            _gameplayToolkitDebugWave2Button = root.Q<UIToolkitButton>("debug-wave2-button");
            _gameplayToolkitDebugSkipBossButton = root.Q<UIToolkitButton>("debug-skip-boss-button");
            _gameplayToolkitDebugSpeedButton = root.Q<UIToolkitButton>("debug-speed-button");
            _gameplayToolkitDebugInvincibleButton = root.Q<UIToolkitButton>("debug-invincible-button");
            _gameplayToolkitDebugAutoPlayButton = root.Q<UIToolkitButton>("debug-autoplay-button");
            _gameplayToolkitMonsterLabSection = root.Q<UIToolkitVisualElement>("monster-lab-section");
            _gameplayToolkitMonsterLabToggle = root.Q<Toggle>("monster-lab-toggle");
            _gameplayToolkitMonsterLabDropdown = root.Q<DropdownField>("monster-lab-dropdown");
            _gameplayToolkitMonsterLabSpawnOneButton = root.Q<UIToolkitButton>("monster-lab-spawn-one-button");
            _gameplayToolkitMonsterLabSpawnFiveButton = root.Q<UIToolkitButton>("monster-lab-spawn-five-button");
            _gameplayToolkitMonsterLabClearButton = root.Q<UIToolkitButton>("monster-lab-clear-button");
            _gameplayToolkitMonsterLabPauseButton = root.Q<UIToolkitButton>("monster-lab-pause-button");

            for (var i = 0; i < _gameplayToolkitLevelButtons.Length; i++)
            {
                _gameplayToolkitLevelButtons[i] = root.Q<UIToolkitButton>($"level-option-button-{i}");
            }

            if (_gameplayToolkitBuildToggleButton != null)
            {
                _gameplayToolkitBuildToggleButton.clicked += ToggleBuildDrawer;
            }

            if (_gameplayToolkitResultActionButton != null)
            {
                _gameplayToolkitResultActionButton.clicked += OnGameplayToolkitResultActionClicked;
            }

            if (_gameplayToolkitDebugAccessButton != null)
            {
                _gameplayToolkitDebugAccessButton.clicked += ToggleDebugEntry;
            }

            if (_gameplayToolkitMonsterLabToggle != null)
            {
                _gameplayToolkitMonsterLabToggle.RegisterValueChangedCallback(evt =>
                {
                    if (_suppressGameplayToolkitMonsterLabCallbacks)
                    {
                        return;
                    }

                    _debugMonsterLabSetEnabledAction?.Invoke(evt.newValue);
                });
            }

            if (_gameplayToolkitMonsterLabDropdown != null)
            {
                _gameplayToolkitMonsterLabDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (_suppressGameplayToolkitMonsterLabCallbacks || _debugMonsterLabSelectVariantAction == null)
                    {
                        return;
                    }

                    var index = _gameplayToolkitMonsterLabDropdown.choices?.IndexOf(evt.newValue) ?? -1;
                    if (index >= 0)
                    {
                        _debugMonsterLabSelectVariantAction.Invoke(index);
                    }
                });
            }

            UpdateGameplayToolkitVisibility(true);
            SetGameplayToolkitBuildDrawerOpen(false);
            HideGameplayToolkitWaveStatus();
            HideGameplayToolkitWaveBanner();
            HideGameplayToolkitBossBar();
            HideGameplayToolkitLevelUpOptions();
            HideGameplayToolkitResult();
            HideGameplayToolkitDebugPanels();
            SetGameplayToolkitDebugAccessVisible(false);
            if (_gameplayToolkitMonsterLabSection != null)
            {
                _gameplayToolkitMonsterLabSection.style.display = DisplayStyle.None;
            }
            ConfigureGameplayToolkitDebugButtons();
        }

        private void UpdateGameplayToolkitVisibility(bool visible)
        {
            if (_gameplayToolkitScreen == null)
            {
                return;
            }

            _gameplayToolkitScreen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetGameplayToolkitTopBar(int currentHp, int maxHp, int level, int currentXp, int requiredXp, int remainingSeconds)
        {
            if (_gameplayToolkitHealthLabel != null)
            {
                _gameplayToolkitHealthLabel.text = $"체력 {currentHp}/{maxHp}";
            }

            if (_gameplayToolkitXpLabel != null)
            {
                _gameplayToolkitXpLabel.text = $"레벨 {level}  경험치 {currentXp}/{requiredXp}";
            }

            if (_gameplayToolkitTimeLabel != null)
            {
                _gameplayToolkitTimeLabel.text = $"시간 {FormatTime(remainingSeconds)}";
            }
        }

        private void SetGameplayToolkitBuildInfo(string weaponsSummary, string statsSummary)
        {
            if (_gameplayToolkitWeaponBuildLabel != null)
            {
                _gameplayToolkitWeaponBuildLabel.text = weaponsSummary ?? "무기";
            }

            if (_gameplayToolkitStatBuildLabel != null)
            {
                _gameplayToolkitStatBuildLabel.text = statsSummary ?? "전투 수치";
            }
        }

        private void SetGameplayToolkitBuildDrawerOpen(bool open)
        {
            if (_gameplayToolkitBuildPanel != null)
            {
                _gameplayToolkitBuildPanel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_gameplayToolkitBuildToggleButton != null)
            {
                _gameplayToolkitBuildToggleButton.text = open ? "빌드 닫기" : "빌드";
            }
        }

        private void SetGameplayToolkitModeHint(string modeHint)
        {
            if (_gameplayToolkitModeHintCard == null || _gameplayToolkitModeHintLabel == null)
            {
                return;
            }

            var visible = !string.IsNullOrWhiteSpace(modeHint);
            _gameplayToolkitModeHintCard.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                _gameplayToolkitModeHintLabel.text = modeHint;
            }
        }

        private void SetGameplayToolkitBossBar(float currentHealth, float maxHealth, string bossLabel)
        {
            if (_gameplayToolkitBossPanel == null || _gameplayToolkitBossFill == null || _gameplayToolkitBossNameLabel == null || _gameplayToolkitBossValueLabel == null)
            {
                return;
            }

            var safeMax = Mathf.Max(1f, maxHealth);
            var safeCurrent = Mathf.Clamp(currentHealth, 0f, safeMax);
            var ratio = safeCurrent / safeMax;

            _gameplayToolkitBossPanel.style.display = DisplayStyle.Flex;
            _gameplayToolkitBossFill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
            _gameplayToolkitBossNameLabel.text = string.IsNullOrWhiteSpace(bossLabel) ? "보스" : bossLabel;
            _gameplayToolkitBossValueLabel.text = $"{Mathf.CeilToInt(safeCurrent)}/{Mathf.CeilToInt(safeMax)}";
        }

        private void HideGameplayToolkitBossBar()
        {
            if (_gameplayToolkitBossPanel != null)
            {
                _gameplayToolkitBossPanel.style.display = DisplayStyle.None;
            }
        }

        private void SetGameplayToolkitWaveStatus(int waveIndex, int remainingCount)
        {
            if (_gameplayToolkitWaveStatusCard == null || _gameplayToolkitWaveStatusLabel == null)
            {
                return;
            }

            _gameplayToolkitWaveStatusCard.style.display = DisplayStyle.Flex;
            _gameplayToolkitWaveStatusLabel.text = $"엘리트 {waveIndex} | 남은 대상 {remainingCount}";
        }

        private void HideGameplayToolkitWaveStatus()
        {
            if (_gameplayToolkitWaveStatusCard != null)
            {
                _gameplayToolkitWaveStatusCard.style.display = DisplayStyle.None;
            }
        }

        private void ShowGameplayToolkitWaveBanner(string message)
        {
            if (_gameplayToolkitWaveBannerCard == null || _gameplayToolkitWaveBannerLabel == null)
            {
                return;
            }

            _gameplayToolkitWaveBannerLabel.text = message;
            _gameplayToolkitWaveBannerCard.style.display = DisplayStyle.Flex;
        }

        private void HideGameplayToolkitWaveBanner()
        {
            if (_gameplayToolkitWaveBannerCard != null)
            {
                _gameplayToolkitWaveBannerCard.style.display = DisplayStyle.None;
            }
        }

        private UIToolkitLabel CreateGameplayToolkitDirectionIndicator(string className)
        {
            if (_gameplayToolkitDirectionLayer == null)
            {
                return null;
            }

            var label = new UIToolkitLabel("▲");
            label.AddToClassList("direction-indicator");
            label.AddToClassList(className);
            label.style.display = DisplayStyle.None;
            _gameplayToolkitDirectionLayer.Add(label);
            return label;
        }

        private void EnsureGameplayToolkitDirectionIndicators()
        {
            _gameplayToolkitBossDirectionIndicator ??= CreateGameplayToolkitDirectionIndicator("direction-indicator-boss");
            _gameplayToolkitWaveDirectionIndicator ??= CreateGameplayToolkitDirectionIndicator("direction-indicator-wave");
        }

        private void EnsureGameplayToolkitRewardIndicatorCapacity(int requiredCount)
        {
            while (_gameplayToolkitRewardIndicators.Count < requiredCount)
            {
                var indicator = CreateGameplayToolkitDirectionIndicator("direction-indicator-reward");
                if (indicator == null)
                {
                    break;
                }

                _gameplayToolkitRewardIndicators.Add(indicator);
            }
        }

        private static void SetGameplayToolkitIndicatorTransform(UIToolkitLabel indicator, Vector2 anchoredPosition, float angleDegrees)
        {
            const float size = 66f;
            indicator.style.left = (HudReferenceWidth * 0.5f) + anchoredPosition.x - (size * 0.5f);
            indicator.style.top = (HudReferenceHeight * 0.5f) - anchoredPosition.y - (size * 0.5f);
            indicator.style.rotate = new Rotate(new Angle(angleDegrees, AngleUnit.Degree));
            indicator.style.display = DisplayStyle.Flex;
        }

        private void UpdateGameplayToolkitDirectionIndicator(UIToolkitLabel indicator, Camera camera, Vector3 worldPosition)
        {
            if (indicator == null)
            {
                return;
            }

            if (!TryGetDirectionIndicatorState(camera, worldPosition, out var anchoredPosition, out var angleDegrees))
            {
                indicator.style.display = DisplayStyle.None;
                return;
            }

            SetGameplayToolkitIndicatorTransform(indicator, anchoredPosition, angleDegrees);
        }

        private void UpdateGameplayToolkitBossDirectionIndicator(Camera camera, Vector3 worldPosition)
        {
            EnsureGameplayToolkitDirectionIndicators();
            UpdateGameplayToolkitDirectionIndicator(_gameplayToolkitBossDirectionIndicator, camera, worldPosition);
        }

        private void UpdateGameplayToolkitWaveDirectionIndicator(Camera camera, Vector3 worldPosition)
        {
            EnsureGameplayToolkitDirectionIndicators();
            UpdateGameplayToolkitDirectionIndicator(_gameplayToolkitWaveDirectionIndicator, camera, worldPosition);
        }

        private void UpdateGameplayToolkitRewardDirectionIndicators(Camera camera, IReadOnlyList<Vector3> worldPositions)
        {
            if (worldPositions == null || worldPositions.Count <= 0)
            {
                HideGameplayToolkitRewardDirectionIndicators();
                return;
            }

            EnsureGameplayToolkitRewardIndicatorCapacity(worldPositions.Count);

            var visibleCount = 0;
            for (var i = 0; i < worldPositions.Count; i++)
            {
                if (!TryGetDirectionIndicatorState(camera, worldPositions[i], out var anchoredPosition, out var angleDegrees))
                {
                    continue;
                }

                if (visibleCount >= _gameplayToolkitRewardIndicators.Count)
                {
                    break;
                }

                SetGameplayToolkitIndicatorTransform(_gameplayToolkitRewardIndicators[visibleCount], anchoredPosition, angleDegrees);
                visibleCount++;
            }

            for (var i = visibleCount; i < _gameplayToolkitRewardIndicators.Count; i++)
            {
                _gameplayToolkitRewardIndicators[i].style.display = DisplayStyle.None;
            }
        }

        private void HideGameplayToolkitBossDirectionIndicator()
        {
            if (_gameplayToolkitBossDirectionIndicator != null)
            {
                _gameplayToolkitBossDirectionIndicator.style.display = DisplayStyle.None;
            }
        }

        private void HideGameplayToolkitWaveDirectionIndicator()
        {
            if (_gameplayToolkitWaveDirectionIndicator != null)
            {
                _gameplayToolkitWaveDirectionIndicator.style.display = DisplayStyle.None;
            }
        }

        private void HideGameplayToolkitRewardDirectionIndicators()
        {
            for (var i = 0; i < _gameplayToolkitRewardIndicators.Count; i++)
            {
                _gameplayToolkitRewardIndicators[i].style.display = DisplayStyle.None;
            }
        }

        private void ShowGameplayToolkitLevelUpOptions(LevelUpOption[] options, Action<int> onSelected, string title)
        {
            if (_gameplayToolkitLevelUpPanel == null || options == null || options.Length == 0)
            {
                return;
            }

            _gameplayToolkitLevelUpPanel.style.display = DisplayStyle.Flex;
            if (_gameplayToolkitLevelUpTitle != null)
            {
                _gameplayToolkitLevelUpTitle.text = string.IsNullOrWhiteSpace(title) ? "레벨 업 - 하나 선택" : title;
            }

            var visibleCount = Mathf.Min(options.Length, _gameplayToolkitLevelButtons.Length);
            for (var i = 0; i < _gameplayToolkitLevelButtons.Length; i++)
            {
                var button = _gameplayToolkitLevelButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (_gameplayToolkitLevelButtonHandlers[i] != null)
                {
                    button.clicked -= _gameplayToolkitLevelButtonHandlers[i];
                    _gameplayToolkitLevelButtonHandlers[i] = null;
                }

                if (i >= visibleCount)
                {
                    button.style.display = DisplayStyle.None;
                    continue;
                }

                var captured = i;
                button.style.display = DisplayStyle.Flex;
                button.text = options[i].Label;
                button.SetEnabled(true);
                _gameplayToolkitLevelButtonHandlers[i] = () => onSelected?.Invoke(captured);
                button.clicked += _gameplayToolkitLevelButtonHandlers[i];
            }

            _gameplayToolkitLevelButtons[0]?.schedule.Execute(() => _gameplayToolkitLevelButtons[0].Focus()).ExecuteLater(0);
        }

        private void HideGameplayToolkitLevelUpOptions()
        {
            if (_gameplayToolkitLevelUpPanel != null)
            {
                _gameplayToolkitLevelUpPanel.style.display = DisplayStyle.None;
            }
        }

        private void ShowGameplayToolkitResult(string bodyText, Action onAction, string actionLabel)
        {
            if (_gameplayToolkitResultPanel == null || _gameplayToolkitResultText == null || _gameplayToolkitResultActionButton == null)
            {
                return;
            }

            _gameplayToolkitResultText.text = bodyText ?? string.Empty;
            _gameplayToolkitResultActionButton.text = string.IsNullOrWhiteSpace(actionLabel) ? "타이틀로" : actionLabel;
            _gameplayToolkitResultAction = onAction;
            _gameplayToolkitResultPanel.style.display = DisplayStyle.Flex;
            _gameplayToolkitResultActionButton.schedule.Execute(() => _gameplayToolkitResultActionButton.Focus()).ExecuteLater(0);
        }

        private void HideGameplayToolkitResult()
        {
            if (_gameplayToolkitResultPanel != null)
            {
                _gameplayToolkitResultPanel.style.display = DisplayStyle.None;
            }
        }

        private void OnGameplayToolkitResultActionClicked()
        {
            _gameplayToolkitResultAction?.Invoke();
        }

        private void SetGameplayToolkitDebugAccessVisible(bool visible)
        {
            if (_gameplayToolkitDebugAccessButton != null)
            {
                _gameplayToolkitDebugAccessButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!visible)
            {
                HideGameplayToolkitDebugPanels();
                return;
            }

            ApplyGameplayToolkitDebugOverlayState();
        }

        private void HideGameplayToolkitDebugPanels()
        {
            if (_gameplayToolkitDebugPanel != null)
            {
                _gameplayToolkitDebugPanel.style.display = DisplayStyle.None;
            }
        }

        private void ToggleGameplayToolkitDebugPanel()
        {
            if (_gameplayToolkitDebugPanel == null)
            {
                return;
            }

            DebugSessionService.ToggleOverlay();
            ApplyGameplayToolkitDebugOverlayState();
        }

        private void ApplyGameplayToolkitDebugOverlayState()
        {
            if (_gameplayToolkitDebugPanel == null)
            {
                return;
            }

            _gameplayToolkitDebugPanel.style.display =
                _debugAccessVisible && DebugSessionService.IsOverlayOpen
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        private void SetGameplayToolkitDebugAutoPlayState(bool enabled)
        {
            if (_gameplayToolkitDebugAutoPlayButton != null)
            {
                _gameplayToolkitDebugAutoPlayButton.text = enabled ? "자동 전투: 켜짐" : "자동 전투: 꺼짐";
            }
        }

        private void SetGameplayToolkitDebugInvincibleState(bool enabled)
        {
            if (_gameplayToolkitDebugInvincibleButton != null)
            {
                _gameplayToolkitDebugInvincibleButton.text = enabled ? "무적: 켜짐" : "무적: 꺼짐";
            }
        }

        private void SetGameplayToolkitDebugPlaySpeedState(float multiplier)
        {
            if (_gameplayToolkitDebugSpeedButton != null)
            {
                _gameplayToolkitDebugSpeedButton.text = $"속도: {Mathf.RoundToInt(Mathf.Max(1f, multiplier))}x";
            }
        }

        private void ConfigureGameplayToolkitDebugButton(UIToolkitButton button, string text, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.text = text;
            button.style.display = action != null ? DisplayStyle.Flex : DisplayStyle.None;

            if (_gameplayToolkitDebugHandlers.TryGetValue(button, out var existingHandler))
            {
                button.clicked -= existingHandler;
                _gameplayToolkitDebugHandlers.Remove(button);
            }

            if (action == null)
            {
                return;
            }

            Action handler = () => action.Invoke();
            _gameplayToolkitDebugHandlers[button] = handler;
            button.clicked += handler;
        }

        private void ConfigureGameplayToolkitDebugButtons()
        {
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitDebugGrantLevelButton, "레벨 +1", _debugGrantLevelAction);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitDebugAdvanceTimeButton, "레벨 +5", _debugAdvanceTimeAction);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitDebugRerollButton, "선택지 다시 굴리기", _debugRerollAction);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitDebugWave1Button, "엘리트1", _debugWave1Action);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitDebugWave2Button, "엘리트2", _debugWave2Action);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitDebugSkipBossButton, "보스", _debugSkipBossAction);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitDebugSpeedButton, $"속도: {Mathf.RoundToInt(Mathf.Max(1f, _debugPlaySpeedMultiplier))}x", _debugSpeedAction);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitDebugInvincibleButton, _debugInvincibleEnabled ? "무적: 켜짐" : "무적: 꺼짐", _debugInvincibleAction);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitDebugAutoPlayButton, _debugAutoPlayEnabled ? "자동 전투: 켜짐" : "자동 전투: 꺼짐", _debugAutoPlayAction);
        }

        private void ConfigureGameplayToolkitMonsterLabOptions()
        {
            if (_gameplayToolkitMonsterLabSection == null || _gameplayToolkitMonsterLabDropdown == null || _gameplayToolkitMonsterLabToggle == null)
            {
                return;
            }

            var hasOptions =
                _debugMonsterLabSetEnabledAction != null &&
                _debugMonsterLabSelectVariantAction != null &&
                _debugMonsterLabVariantNames != null &&
                _debugMonsterLabVariantNames.Count > 0;

            _gameplayToolkitMonsterLabSection.style.display = hasOptions ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasOptions)
            {
                return;
            }

            _suppressGameplayToolkitMonsterLabCallbacks = true;
            _gameplayToolkitMonsterLabDropdown.choices = _debugMonsterLabVariantNames;
            var safeIndex = Mathf.Clamp(_debugMonsterLabSelectedIndex, 0, _debugMonsterLabVariantNames.Count - 1);
            _gameplayToolkitMonsterLabDropdown.SetValueWithoutNotify(_debugMonsterLabVariantNames[safeIndex]);
            _gameplayToolkitMonsterLabToggle.SetValueWithoutNotify(_debugMonsterLabEnabled);
            _gameplayToolkitMonsterLabToggle.label = "실험장 모드";
            _gameplayToolkitMonsterLabDropdown.label = "변주";
            _suppressGameplayToolkitMonsterLabCallbacks = false;

            ConfigureGameplayToolkitDebugButton(_gameplayToolkitMonsterLabSpawnOneButton, "1마리 생성", _debugMonsterLabSpawnOneAction);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitMonsterLabSpawnFiveButton, "5마리 생성", _debugMonsterLabSpawnFiveAction);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitMonsterLabClearButton, "전체 삭제", _debugMonsterLabClearAction);
            ConfigureGameplayToolkitDebugButton(_gameplayToolkitMonsterLabPauseButton, _debugMonsterLabTimePaused ? "시간 재개" : "시간 정지", _debugMonsterLabToggleTimePauseAction);
        }

        private void SetGameplayToolkitMonsterLabState(bool enabled, int selectedIndex, bool timePaused)
        {
            if (_gameplayToolkitMonsterLabSection == null || _gameplayToolkitMonsterLabDropdown == null || _gameplayToolkitMonsterLabToggle == null)
            {
                return;
            }

            if (_debugMonsterLabVariantNames == null || _debugMonsterLabVariantNames.Count == 0)
            {
                _gameplayToolkitMonsterLabSection.style.display = DisplayStyle.None;
                return;
            }

            _gameplayToolkitMonsterLabSection.style.display = DisplayStyle.Flex;
            _suppressGameplayToolkitMonsterLabCallbacks = true;
            if (_gameplayToolkitMonsterLabDropdown.choices == null || _gameplayToolkitMonsterLabDropdown.choices.Count != _debugMonsterLabVariantNames.Count)
            {
                _gameplayToolkitMonsterLabDropdown.choices = _debugMonsterLabVariantNames;
            }

            var safeIndex = Mathf.Clamp(selectedIndex, 0, _debugMonsterLabVariantNames.Count - 1);
            _gameplayToolkitMonsterLabDropdown.SetValueWithoutNotify(_debugMonsterLabVariantNames[safeIndex]);
            _gameplayToolkitMonsterLabToggle.SetValueWithoutNotify(enabled);
            _gameplayToolkitMonsterLabToggle.label = "실험장 모드";
            _gameplayToolkitMonsterLabDropdown.label = "변주";
            _suppressGameplayToolkitMonsterLabCallbacks = false;

            ConfigureGameplayToolkitDebugButton(_gameplayToolkitMonsterLabPauseButton, timePaused ? "시간 재개" : "시간 정지", _debugMonsterLabToggleTimePauseAction);
        }

        private void RefreshGameplayToolkitTransientPanels()
        {
            if (_waveBannerHideAt < 0f)
            {
                return;
            }

            if (Time.unscaledTime >= _waveBannerHideAt)
            {
                HideGameplayToolkitWaveBanner();
            }
        }
    }
}
