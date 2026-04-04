using System;
using System.Collections.Generic;
using EJR.Game.Audio;
using EJR.Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace EJR.Game.UI
{
    public sealed partial class HudController
    {
        private const string FullscreenPreferenceKey = "settings.fullscreen";
        private const float LevelPanelWidth = 860f;
        private const float LevelPanelMinHeight = 300f;
        private const float LevelPanelTopPadding = 34f;
        private const float LevelPanelBottomPadding = 34f;
        private const float LevelTitleHeight = 44f;
        private const float LevelButtonsTopGap = 24f;
        private const float LevelButtonWidth = 760f;
        private const float LevelButtonHeight = 68f;
        private const float LevelButtonSpacing = 14f;
        private const float BossBarRootWidth = 430f;
        private const float BossBarRootHeight = 18f;
        private const float BossBarPadding = 3f;
        private const float HudReferenceWidth = 1920f;
        private const float HudReferenceHeight = 1080f;
        private const float BossDirectionViewportMargin = 0.08f;
        private const float DefaultWaveBannerDuration = 1.8f;

        private readonly Font _font;

        private Canvas _canvas;
        private Text _healthText;
        private Text _xpText;
        private Text _timeText;
        private GameObject _modeHintCard;
        private Text _modeHintText;
        private Button _buildToggleButton;
        private Text _buildToggleText;
        private GameObject _buildPanel;
        private Text _weaponBuildText;
        private Text _statBuildText;
        private bool _isBuildDrawerOpen;
        private GameObject _bossBarPanel;
        private Text _bossNameText;
        private Image _bossBarFill;
        private RectTransform _bossBarFillRect;
        private Text _bossBarValueText;
        private float _bossBarFillMaxWidth;
        private GameObject _bossDirectionIndicatorPanel;
        private RectTransform _bossDirectionIndicatorRect;
        private Text _bossDirectionIndicatorText;
        private GameObject _waveTargetDirectionIndicatorPanel;
        private RectTransform _waveTargetDirectionIndicatorRect;
        private Text _waveTargetDirectionIndicatorText;
        private GameObject _rewardDirectionIndicatorPanel;
        private RectTransform _rewardDirectionIndicatorRect;
        private Text _rewardDirectionIndicatorText;
        private readonly List<GameObject> _rewardDirectionIndicatorPanels = new();
        private readonly List<RectTransform> _rewardDirectionIndicatorRects = new();
        private GameObject _waveStatusPanel;
        private Text _waveStatusText;
        private GameObject _waveBannerPanel;
        private Text _waveBannerText;
        private float _waveBannerHideAt = -1f;

        private GameObject _levelUpPanel;
        private Text _levelUpTitle;
        private Button[] _levelButtons;
        private Text[] _levelButtonTexts;

        private GameObject _resultPanel;
        private Text _resultText;
        private Button _restartButton;
        private GameObject _pausePanel;
        private GameObject _pauseMainContentRoot;
        private Button _pauseSettingsButton;
        private Button _pauseResumeButton;
        private Button _pauseQuitButton;
        private GameObject _pauseSettingsPanel;
        private Toggle _pauseFullscreenToggle;
        private Slider _pauseMasterVolumeSlider;
        private Slider _pauseBgmVolumeSlider;
        private Slider _pauseSfxVolumeSlider;
        private Text _pauseMasterVolumeValueText;
        private Text _pauseBgmVolumeValueText;
        private Text _pauseSfxVolumeValueText;
        private bool _suppressPauseSettingsCallbacks;
        private Button _debugAccessButton;
        private GameObject _debugToolsPanel;
        private Button _debugGrantLevelButton;
        private Button _debugAdvanceTimeButton;
        private Button _debugRerollButton;
        private Button _debugWave1Button;
        private Button _debugWave2Button;
        private Button _debugSkipBossButton;
        private Button _debugInvincibleButton;
        private Button _debugAutoPlayButton;
        private Text _debugGrantLevelLabel;
        private Text _debugAdvanceTimeLabel;
        private Text _debugRerollLabel;
        private Text _debugWave1Label;
        private Text _debugWave2Label;
        private Text _debugSkipBossLabel;
        private Text _debugInvincibleLabel;
        private Text _debugAutoPlayLabel;
        private Func<string, bool> _debugUnlockValidator;
        private Action _debugGrantLevelAction;
        private Action _debugAdvanceTimeAction;
        private Action _debugRerollAction;
        private Action _debugWave1Action;
        private Action _debugWave2Action;
        private Action _debugSkipBossAction;
        private Action _debugInvincibleAction;
        private Action _debugAutoPlayAction;
        private Action<bool> _debugMonsterLabSetEnabledAction;
        private Action<int> _debugMonsterLabSelectVariantAction;
        private Action _debugMonsterLabSpawnOneAction;
        private Action _debugMonsterLabSpawnFiveAction;
        private Action _debugMonsterLabClearAction;
        private Action _debugMonsterLabToggleTimePauseAction;
        private bool _debugAccessVisible;
        private bool _debugInvincibleEnabled;
        private bool _debugAutoPlayEnabled;
        private bool _debugMonsterLabEnabled;
        private bool _debugMonsterLabTimePaused;
        private int _debugMonsterLabSelectedIndex;
        private List<string> _debugMonsterLabVariantNames = new();
        private int _lastCurrentHp = int.MinValue;
        private int _lastMaxHp = int.MinValue;
        private int _lastLevel = int.MinValue;
        private int _lastCurrentXp = int.MinValue;
        private int _lastRequiredXp = int.MinValue;
        private int _lastRemainingSeconds = int.MinValue;
        private string _lastWeaponBuildSummary = string.Empty;
        private string _lastStatBuildSummary = string.Empty;
        private int _lastBossCurrentHp = int.MinValue;
        private int _lastBossMaxHp = int.MinValue;
        private string _lastBossLabel = string.Empty;
        private string _lastWaveStatus = string.Empty;

        public HudController()
        {
            _font = RuntimeFontProvider.GetDefaultFont();
        }

        public void Initialize()
        {
            EnsureEventSystem();
            BuildCanvas();
            BuildGameplayToolkitReference();
            BuildPauseSettingsToolkitReference();
            BuildTopBarReference();
            BuildBuildPanelReference();
            BuildBossBarReference();
            BuildLevelUpPanelReference();
            BuildResultPanelReference();
            BuildPausePanelReference();
            BuildDebugPanelsReference();
        }

        public void SetCanvasVisible(bool visible)
        {
            if (_canvas == null)
            {
                return;
            }

            _canvas.gameObject.SetActive(visible);
            UpdateGameplayToolkitVisibility(visible);
            if (!visible)
            {
                HideLevelUpOptions();
                HideBossBar();
                HideWaveTargetDirectionIndicator();
                HideRewardDirectionIndicator();
                HideWaveStatus();
                HideWaveBanner();
                HidePauseMenu();
                HideResult();
                HideDebugPanels();
            }
        }

        public void SetTopBar(float currentHealth, float maxHealth, int level, int currentXp, int requiredXp, float remainingSeconds)
        {
            var currentHpInt = Mathf.CeilToInt(currentHealth);
            var maxHpInt = Mathf.CeilToInt(maxHealth);
            var remainingSecondsInt = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));

            if (HasGameplayToolkit)
            {
                SetGameplayToolkitTopBar(currentHpInt, maxHpInt, level, currentXp, requiredXp, remainingSecondsInt);
            }

            if (_healthText == null)
            {
                RefreshTransientPanels();
                return;
            }

            if (currentHpInt != _lastCurrentHp || maxHpInt != _lastMaxHp)
            {
                _healthText.text = $"체력 {currentHpInt}/{maxHpInt}";
                _lastCurrentHp = currentHpInt;
                _lastMaxHp = maxHpInt;
            }

            if (level != _lastLevel || currentXp != _lastCurrentXp || requiredXp != _lastRequiredXp)
            {
                _xpText.text = $"레벨 {level}  경험치 {currentXp}/{requiredXp}";
                _lastLevel = level;
                _lastCurrentXp = currentXp;
                _lastRequiredXp = requiredXp;
            }

            if (remainingSecondsInt != _lastRemainingSeconds)
            {
                _timeText.text = $"시간 {FormatTime(remainingSecondsInt)}";
                _lastRemainingSeconds = remainingSecondsInt;
            }

            RefreshTransientPanels();
        }

        private static string FormatTime(int totalSeconds)
        {
            var clampedSeconds = Mathf.Max(0, totalSeconds);
            var minutes = clampedSeconds / 60;
            var seconds = clampedSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        public void SetBuildInfo(string weaponsSummary, string statsSummary)
        {
            weaponsSummary ??= "무기";
            statsSummary ??= "능력치";

            if (HasGameplayToolkit)
            {
                SetGameplayToolkitBuildInfo(weaponsSummary, statsSummary);
            }

            if (_weaponBuildText == null || _statBuildText == null)
            {
                return;
            }

            if (!string.Equals(_lastWeaponBuildSummary, weaponsSummary, StringComparison.Ordinal))
            {
                _weaponBuildText.text = weaponsSummary;
                _lastWeaponBuildSummary = weaponsSummary;
            }

            if (!string.Equals(_lastStatBuildSummary, statsSummary, StringComparison.Ordinal))
            {
                _statBuildText.text = statsSummary;
                _lastStatBuildSummary = statsSummary;
            }
        }

        public void ToggleBuildDrawer()
        {
            SetBuildDrawerOpen(!_isBuildDrawerOpen);
        }

        public void SetBuildDrawerOpen(bool open)
        {
            _isBuildDrawerOpen = open;
            if (HasGameplayToolkit)
            {
                SetGameplayToolkitBuildDrawerOpen(open);
            }

            if (_buildPanel != null)
            {
                _buildPanel.SetActive(open);
            }

            if (_buildToggleText != null)
            {
                _buildToggleText.text = open ? "빌드 닫기" : "빌드";
            }
        }

        public void SetModeHint(string modeHint)
        {
            if (HasGameplayToolkit)
            {
                SetGameplayToolkitModeHint(modeHint);
            }

            if (_modeHintCard != null)
            {
                var visible = !string.IsNullOrWhiteSpace(modeHint);
                _modeHintCard.SetActive(visible);
                if (visible && _modeHintText != null)
                {
                    _modeHintText.text = modeHint;
                }
            }
        }

        public void ConfigureDebugTools(
            Action onGrantLevel,
            Action onGrantLevelsFive,
            Action onReroll,
            Action onWave1,
            Action onWave2,
            Action onBoss,
            Action onToggleInvincible,
            Action onToggleAutoPlay)
        {
            _debugGrantLevelAction = onGrantLevel;
            _debugAdvanceTimeAction = onGrantLevelsFive;
            _debugRerollAction = onReroll;
            _debugWave1Action = onWave1;
            _debugWave2Action = onWave2;
            _debugSkipBossAction = onBoss;
            _debugInvincibleAction = onToggleInvincible;
            _debugAutoPlayAction = onToggleAutoPlay;
            RefreshDebugToolButtons();
            ConfigureGameplayToolkitDebugButtons();
        }

        public void ConfigureMonsterLabTools(
            Action<bool> onSetEnabled,
            Action<int> onSelectVariant,
            Action onSpawnOne,
            Action onSpawnFive,
            Action onClear,
            Action onToggleTimePause,
            IReadOnlyList<string> variantNames)
        {
            _debugMonsterLabSetEnabledAction = onSetEnabled;
            _debugMonsterLabSelectVariantAction = onSelectVariant;
            _debugMonsterLabSpawnOneAction = onSpawnOne;
            _debugMonsterLabSpawnFiveAction = onSpawnFive;
            _debugMonsterLabClearAction = onClear;
            _debugMonsterLabToggleTimePauseAction = onToggleTimePause;
            _debugMonsterLabVariantNames = variantNames != null ? new List<string>(variantNames) : new List<string>();
            ConfigureGameplayToolkitMonsterLabOptions();
            SetGameplayToolkitMonsterLabState(_debugMonsterLabEnabled, _debugMonsterLabSelectedIndex, _debugMonsterLabTimePaused);
        }

        public void SetMonsterLabState(bool enabled, int selectedIndex, bool timePaused)
        {
            _debugMonsterLabEnabled = enabled;
            _debugMonsterLabSelectedIndex = Mathf.Max(0, selectedIndex);
            _debugMonsterLabTimePaused = timePaused;
            SetGameplayToolkitMonsterLabState(enabled, _debugMonsterLabSelectedIndex, timePaused);
        }

        public void SetDebugAccessVisible(bool visible)
        {
            _debugAccessVisible = visible;
            if (HasGameplayToolkit)
            {
                SetGameplayToolkitDebugAccessVisible(visible);
            }

            if (_debugAccessButton != null)
            {
                _debugAccessButton.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                HideDebugPanels();
            }
        }

        public void SetDebugAutoPlayState(bool enabled)
        {
            _debugAutoPlayEnabled = enabled;
            if (HasGameplayToolkit)
            {
                SetGameplayToolkitDebugAutoPlayState(enabled);
            }

            if (_debugAutoPlayLabel != null)
            {
                _debugAutoPlayLabel.text = enabled ? "자동 전투: 켜짐" : "자동 전투: 꺼짐";
            }
        }

        public void SetDebugInvincibleState(bool enabled)
        {
            _debugInvincibleEnabled = enabled;
            if (HasGameplayToolkit)
            {
                SetGameplayToolkitDebugInvincibleState(enabled);
            }

            if (_debugInvincibleLabel != null)
            {
                _debugInvincibleLabel.text = enabled ? "무적: 켜짐" : "무적: 꺼짐";
            }
        }

        public void HideDebugPanels()
        {
            if (HasGameplayToolkit)
            {
                HideGameplayToolkitDebugPanels();
            }

            if (_debugToolsPanel != null)
            {
                _debugToolsPanel.SetActive(false);
            }
        }

        public void SetBossBar(float currentHealth, float maxHealth, string bossLabel = "보스")
        {
            if (HasGameplayToolkit)
            {
                SetGameplayToolkitBossBar(currentHealth, maxHealth, bossLabel);
            }

            if (_bossBarPanel == null || _bossBarFill == null || _bossBarValueText == null || _bossNameText == null)
            {
                return;
            }

            if (!_bossBarPanel.activeSelf)
            {
                _bossBarPanel.SetActive(true);
            }

            var safeMax = Mathf.Max(1f, maxHealth);
            var safeCurrent = Mathf.Clamp(currentHealth, 0f, safeMax);
            var ratio = safeCurrent / safeMax;
            if (_bossBarFillRect != null)
            {
                var size = _bossBarFillRect.sizeDelta;
                size.x = _bossBarFillMaxWidth * ratio;
                _bossBarFillRect.sizeDelta = size;
            }

            var currentHpInt = Mathf.CeilToInt(safeCurrent);
            var maxHpInt = Mathf.CeilToInt(safeMax);
            var label = string.IsNullOrWhiteSpace(bossLabel) ? "보스" : bossLabel;

            if (!string.Equals(_lastBossLabel, label, StringComparison.Ordinal))
            {
                _bossNameText.text = label;
                _lastBossLabel = label;
            }

            if (currentHpInt != _lastBossCurrentHp || maxHpInt != _lastBossMaxHp)
            {
                _bossBarValueText.text = $"{currentHpInt}/{maxHpInt}";
                _lastBossCurrentHp = currentHpInt;
                _lastBossMaxHp = maxHpInt;
            }
        }

        public void HideBossBar()
        {
            if (HasGameplayToolkit)
            {
                HideGameplayToolkitBossBar();
            }

            if (_bossBarPanel != null && _bossBarPanel.activeSelf)
            {
                _bossBarPanel.SetActive(false);
            }

            HideBossDirectionIndicator();

            _lastBossCurrentHp = int.MinValue;
            _lastBossMaxHp = int.MinValue;
            _lastBossLabel = string.Empty;
        }

        public void UpdateBossDirectionIndicator(Camera camera, Vector3 worldPosition)
        {
            if (HasGameplayToolkit)
            {
                UpdateGameplayToolkitBossDirectionIndicator(camera, worldPosition);
                return;
            }

            UpdateDirectionIndicator(_bossDirectionIndicatorPanel, _bossDirectionIndicatorRect, camera, worldPosition);
        }

        public void UpdateWaveTargetDirectionIndicator(Camera camera, Vector3 worldPosition)
        {
            if (HasGameplayToolkit)
            {
                UpdateGameplayToolkitWaveDirectionIndicator(camera, worldPosition);
                return;
            }

            UpdateDirectionIndicator(_waveTargetDirectionIndicatorPanel, _waveTargetDirectionIndicatorRect, camera, worldPosition);
        }

        public void UpdateRewardDirectionIndicator(Camera camera, Vector3 worldPosition)
        {
            UpdateDirectionIndicator(_rewardDirectionIndicatorPanel, _rewardDirectionIndicatorRect, camera, worldPosition);
        }

        public void UpdateRewardDirectionIndicators(Camera camera, IReadOnlyList<Vector3> worldPositions)
        {
            if (HasGameplayToolkit)
            {
                UpdateGameplayToolkitRewardDirectionIndicators(camera, worldPositions);
                return;
            }

            if (worldPositions == null || worldPositions.Count <= 0)
            {
                HideRewardDirectionIndicator();
                return;
            }

            var visibleCount = 0;
            for (var i = 0; i < worldPositions.Count; i++)
            {
                if (!TryGetDirectionIndicatorState(camera, worldPositions[i], out var anchoredPosition, out var angleDegrees))
                {
                    continue;
                }

                EnsureRewardDirectionIndicatorCapacity(visibleCount + 1);
                var panel = _rewardDirectionIndicatorPanels[visibleCount];
                var rect = _rewardDirectionIndicatorRects[visibleCount];
                rect.anchoredPosition = anchoredPosition;
                rect.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
                if (!panel.activeSelf)
                {
                    panel.SetActive(true);
                }

                visibleCount++;
            }

            for (var i = visibleCount; i < _rewardDirectionIndicatorPanels.Count; i++)
            {
                HideDirectionIndicator(_rewardDirectionIndicatorPanels[i]);
            }
        }

        public void HideBossDirectionIndicator()
        {
            if (HasGameplayToolkit)
            {
                HideGameplayToolkitBossDirectionIndicator();
            }

            HideDirectionIndicator(_bossDirectionIndicatorPanel);
        }

        public void HideWaveTargetDirectionIndicator()
        {
            if (HasGameplayToolkit)
            {
                HideGameplayToolkitWaveDirectionIndicator();
            }

            HideDirectionIndicator(_waveTargetDirectionIndicatorPanel);
        }

        public void HideRewardDirectionIndicator()
        {
            if (HasGameplayToolkit)
            {
                HideGameplayToolkitRewardDirectionIndicators();
            }

            for (var i = 0; i < _rewardDirectionIndicatorPanels.Count; i++)
            {
                HideDirectionIndicator(_rewardDirectionIndicatorPanels[i]);
            }
        }

        private void UpdateDirectionIndicator(GameObject panel, RectTransform indicatorRect, Camera camera, Vector3 worldPosition)
        {
            if (panel == null || indicatorRect == null)
            {
                return;
            }

            if (!TryGetDirectionIndicatorState(camera, worldPosition, out var anchoredPosition, out var angleDegrees))
            {
                HideDirectionIndicator(panel);
                return;
            }
            indicatorRect.anchoredPosition = anchoredPosition;
            indicatorRect.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);

            if (!panel.activeSelf)
            {
                panel.SetActive(true);
            }
        }

        private bool TryGetDirectionIndicatorState(Camera camera, Vector3 worldPosition, out Vector2 anchoredPosition, out float angleDegrees)
        {
            anchoredPosition = Vector2.zero;
            angleDegrees = 0f;

            if (camera == null)
            {
                return false;
            }

            var viewport = camera.WorldToViewportPoint(worldPosition);
            var direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
            if (viewport.z < 0f)
            {
                direction = -direction;
            }

            if (viewport.z > 0f &&
                viewport.x >= 0f &&
                viewport.x <= 1f &&
                viewport.y >= 0f &&
                viewport.y <= 1f)
            {
                return false;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            var halfWidth = 0.5f - BossDirectionViewportMargin;
            var halfHeight = 0.5f - BossDirectionViewportMargin;
            var scaleX = Mathf.Abs(direction.x) > 0.0001f ? halfWidth / Mathf.Abs(direction.x) : float.PositiveInfinity;
            var scaleY = Mathf.Abs(direction.y) > 0.0001f ? halfHeight / Mathf.Abs(direction.y) : float.PositiveInfinity;
            var edgePoint = new Vector2(0.5f, 0.5f) + (direction * Mathf.Min(scaleX, scaleY));

            anchoredPosition = new Vector2(
                (edgePoint.x - 0.5f) * HudReferenceWidth,
                (edgePoint.y - 0.5f) * HudReferenceHeight);
            angleDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            return true;
        }

        private static void HideDirectionIndicator(GameObject panel)
        {
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
        }

        private void EnsureRewardDirectionIndicatorCapacity(int requiredCount)
        {
            while (_rewardDirectionIndicatorPanels.Count < requiredCount)
            {
                var nextIndex = _rewardDirectionIndicatorPanels.Count;
                var indicatorPanel = CreatePanel(
                    _canvas.transform,
                    $"RewardDirectionIndicatorV2_{nextIndex}",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(66f, 66f),
                    new Color(0.03f, 0.14f, 0.07f, 0.78f));
                var indicatorRect = indicatorPanel.GetComponent<RectTransform>();
                indicatorPanel.SetActive(false);

                var indicatorText = CreateText(indicatorPanel.transform, $"RewardDirectionArrowV2_{nextIndex}", Vector2.zero, "^");
                indicatorText.fontSize = 34;
                indicatorText.fontStyle = FontStyle.Bold;
                indicatorText.color = new Color(0.40f, 1f, 0.48f, 1f);
                indicatorText.rectTransform.sizeDelta = new Vector2(38f, 38f);

                _rewardDirectionIndicatorPanels.Add(indicatorPanel);
                _rewardDirectionIndicatorRects.Add(indicatorRect);
            }
        }

        public void SetWaveStatus(int waveIndex, int remainingCount)
        {
            if (HasGameplayToolkit)
            {
                SetGameplayToolkitWaveStatus(waveIndex, remainingCount);
            }

            if (_waveStatusPanel == null || _waveStatusText == null)
            {
                return;
            }

            if (waveIndex <= 0 || remainingCount <= 0)
            {
                HideWaveStatus();
                return;
            }

            var nextText = $"웨이브 {waveIndex} | 남은 대상 {remainingCount}";
            if (!string.Equals(_lastWaveStatus, nextText, StringComparison.Ordinal))
            {
                _waveStatusText.text = nextText;
                _lastWaveStatus = nextText;
            }

            if (!_waveStatusPanel.activeSelf)
            {
                _waveStatusPanel.SetActive(true);
            }
        }

        public void HideWaveStatus()
        {
            if (HasGameplayToolkit)
            {
                HideGameplayToolkitWaveStatus();
            }

            if (_waveStatusPanel != null && _waveStatusPanel.activeSelf)
            {
                _waveStatusPanel.SetActive(false);
            }

            _lastWaveStatus = string.Empty;
        }

        public void ShowWaveBanner(string message, float duration = DefaultWaveBannerDuration)
        {
            if (HasGameplayToolkit)
            {
                ShowGameplayToolkitWaveBanner(message);
            }

            if (_waveBannerPanel == null || _waveBannerText == null || string.IsNullOrWhiteSpace(message))
            {
                _waveBannerHideAt = Time.unscaledTime + Mathf.Max(0.2f, duration);
                return;
            }

            _waveBannerText.text = message;
            _waveBannerPanel.SetActive(true);
            _waveBannerHideAt = Time.unscaledTime + Mathf.Max(0.2f, duration);
        }

        public void HideWaveBanner()
        {
            if (HasGameplayToolkit)
            {
                HideGameplayToolkitWaveBanner();
            }

            if (_waveBannerPanel != null && _waveBannerPanel.activeSelf)
            {
                _waveBannerPanel.SetActive(false);
            }

            _waveBannerHideAt = -1f;
        }

        public void ShowLevelUpOptions(LevelUpOption[] options, Action<int> onSelected, string title = "레벨 업 - 하나 선택")
        {
            if (HasGameplayToolkit)
            {
                ShowGameplayToolkitLevelUpOptions(options, onSelected, title);
            }

            if (_levelUpPanel == null || options == null || options.Length == 0)
            {
                return;
            }

            _levelUpPanel.SetActive(true);
            _levelUpTitle.text = string.IsNullOrWhiteSpace(title) ? "레벨 업 - 하나 선택" : title;
            var visibleCount = Mathf.Min(options.Length, _levelButtons.Length);
            LayoutLevelUpPanel(visibleCount);

            for (var i = 0; i < _levelButtons.Length; i++)
            {
                var button = _levelButtons[i];
                var text = _levelButtonTexts[i];
                if (i >= visibleCount)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                button.gameObject.SetActive(true);
                button.interactable = true;
                text.text = options[i].Label;
                button.onClick.RemoveAllListeners();
                var captured = i;
                button.onClick.AddListener(() => onSelected?.Invoke(captured));
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null && visibleCount > 0)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_levelButtons[0].gameObject);
            }

            RefreshTransientPanels();
        }

        public void HideLevelUpOptions()
        {
            if (HasGameplayToolkit)
            {
                HideGameplayToolkitLevelUpOptions();
            }

            if (_levelUpPanel != null)
            {
                _levelUpPanel.SetActive(false);
            }
        }

        public void ShowResult(bool cleared, Action onAction, string actionLabel = "재시작")
        {
            if (HasGameplayToolkit)
            {
                ShowGameplayToolkitResult(cleared ? "클리어" : "게임 오버", onAction, string.IsNullOrEmpty(actionLabel) ? "재시작" : actionLabel);
            }

            if (_resultPanel == null)
            {
                return;
            }

            _resultPanel.SetActive(true);
            _resultText.text = cleared ? "클리어" : "게임 오버";
            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() => onAction?.Invoke());
            _restartButton.GetComponentInChildren<Text>().text = string.IsNullOrEmpty(actionLabel) ? "재시작" : actionLabel;
        }

        public void ShowResult(RunRewardSummary summary, Action onAction, string actionLabel)
        {
            if (HasGameplayToolkit && summary != null)
            {
                ShowGameplayToolkitResult(summary.BuildDisplayText(), onAction, string.IsNullOrEmpty(actionLabel) ? "타이틀로" : actionLabel);
            }

            if (_resultPanel == null || summary == null)
            {
                return;
            }

            _resultPanel.SetActive(true);
            _resultText.text = summary.BuildDisplayText();
            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() => onAction?.Invoke());
            _restartButton.GetComponentInChildren<Text>().text = string.IsNullOrEmpty(actionLabel)
                ? "\uD0C0\uC774\uD2C0\uB85C"
                : actionLabel;
        }

        public void HideResult()
        {
            if (HasGameplayToolkit)
            {
                HideGameplayToolkitResult();
            }

            if (_resultPanel != null)
            {
                _resultPanel.SetActive(false);
            }
        }

        public bool IsPauseMenuVisible => (HasPauseMainToolkit && IsPauseMainToolkitVisible) || (_pausePanel != null && _pausePanel.activeSelf);

        public void ShowPauseMenu(Action onResume, Action onQuit)
        {
            if (HasPauseMainToolkit)
            {
                ConfigurePauseToolkitActions(onResume, onQuit);
                _pausePanel?.SetActive(false);
                UpdatePauseSettingsToolkitVisibility(false);
                UpdatePauseMainToolkitVisibility(true);
                SyncPauseSettingsControls();
                return;
            }

            if (_pausePanel == null)
            {
                return;
            }

            _pausePanel.SetActive(true);
            ClosePauseSettings(playCue: false);
            SyncPauseSettingsControls();

            if (_pauseResumeButton != null)
            {
                _pauseResumeButton.onClick.RemoveAllListeners();
                _pauseResumeButton.onClick.AddListener(() => onResume?.Invoke());
            }

            if (_pauseQuitButton != null)
            {
                _pauseQuitButton.onClick.RemoveAllListeners();
                _pauseQuitButton.onClick.AddListener(() => onQuit?.Invoke());
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null && _pauseResumeButton != null)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_pauseResumeButton.gameObject);
            }
        }

        public void HidePauseMenu()
        {
            if (HasPauseMainToolkit)
            {
                UpdatePauseSettingsToolkitVisibility(false);
                UpdatePauseMainToolkitVisibility(false);
                _pausePanel?.SetActive(false);
                return;
            }

            if (_pausePanel != null)
            {
                ClosePauseSettings(playCue: false);
                _pausePanel.SetActive(false);
            }
        }

        private void EnsureEventSystem()
        {
            var allEventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            EventSystem eventSystem;

            if (allEventSystems.Length == 0)
            {
                var eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }
            else
            {
                eventSystem = allEventSystems[0];
                for (var i = 1; i < allEventSystems.Length; i++)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(allEventSystems[i].gameObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(allEventSystems[i].gameObject);
                    }
                }
            }

            eventSystem.sendNavigationEvents = true;

#if ENABLE_INPUT_SYSTEM
            var inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
            {
                inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            inputSystemModule.enabled = true;
            if (inputSystemModule.actionsAsset == null)
            {
                inputSystemModule.AssignDefaultActions();
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

            standalone.enabled = true;
#endif

            eventSystem.UpdateModules();
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("HUD");
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private void BuildTopBar()
        {
            var healthCard = CreatePanel(
                _canvas.transform,
                "HealthCard",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(272f, 78f),
                new Color(0.04f, 0.07f, 0.11f, 0.86f));
            var xpCard = CreatePanel(
                _canvas.transform,
                "XpCard",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(314f, -24f),
                new Vector2(304f, 78f),
                new Color(0.04f, 0.07f, 0.11f, 0.86f));
            var timeCard = CreatePanel(
                _canvas.transform,
                "TimeCard",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-292f, -24f),
                new Vector2(252f, 78f),
                new Color(0.04f, 0.07f, 0.11f, 0.86f));
            var hintCard = CreatePanel(
                _canvas.transform,
                "ModeHintCard",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                new Vector2(252f, 78f),
                new Color(0.04f, 0.07f, 0.11f, 0.86f));

            _healthText = CreateText(healthCard.transform, "HealthText", Vector2.zero, "체력");
            _healthText.alignment = TextAnchor.MiddleLeft;
            _healthText.rectTransform.sizeDelta = new Vector2(224f, 44f);
            _healthText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _healthText.fontSize = 22;

            _xpText = CreateText(xpCard.transform, "XPText", Vector2.zero, "경험치");
            _xpText.alignment = TextAnchor.MiddleLeft;
            _xpText.rectTransform.sizeDelta = new Vector2(248f, 44f);
            _xpText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _xpText.fontSize = 20;

            _timeText = CreateText(timeCard.transform, "TimeText", Vector2.zero, "시간");
            _timeText.alignment = TextAnchor.MiddleLeft;
            _timeText.rectTransform.sizeDelta = new Vector2(204f, 44f);
            _timeText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _timeText.fontSize = 20;

            _modeHintText = CreateText(hintCard.transform, "ModeHintText", Vector2.zero, "표준");
            _modeHintText.fontSize = 16;
            _modeHintText.alignment = TextAnchor.MiddleCenter;
            _modeHintText.rectTransform.sizeDelta = new Vector2(204f, 44f);
            _modeHintText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _modeHintText.color = new Color(0.76f, 0.82f, 0.90f, 1f);
        }

        private void BuildBuildPanel()
        {
            _buildPanel = CreatePanel(
                _canvas.transform,
                "BuildPanel",
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-24f, -16f),
                new Vector2(384f, 436f),
                new Color(0.03f, 0.05f, 0.09f, 0.88f));

            var buildTitle = CreateText(_buildPanel.transform, "BuildTitle", new Vector2(0f, 182f), "빌드");
            buildTitle.fontSize = 22;
            buildTitle.fontStyle = FontStyle.Bold;
            buildTitle.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            buildTitle.rectTransform.sizeDelta = new Vector2(260f, 28f);

            var weaponsCard = CreatePanel(_buildPanel.transform, "WeaponsCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 54f), new Vector2(336f, 156f), new Color(0.07f, 0.10f, 0.14f, 0.92f));
            var weaponsTitle = CreateText(weaponsCard.transform, "WeaponsTitle", new Vector2(0f, 56f), "무기");
            weaponsTitle.fontSize = 15;
            weaponsTitle.fontStyle = FontStyle.Bold;
            weaponsTitle.alignment = TextAnchor.MiddleLeft;
            weaponsTitle.rectTransform.sizeDelta = new Vector2(280f, 24f);
            weaponsTitle.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            _weaponBuildText = CreateMultilineText(
                weaponsCard.transform,
                "WeaponsBuildText",
                new Vector2(0f, 34f),
                new Vector2(296f, 92f),
                "무기");
            _weaponBuildText.fontSize = 14;

            var statsCard = CreatePanel(_buildPanel.transform, "StatsCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(336f, 184f), new Color(0.07f, 0.10f, 0.14f, 0.92f));
            var statsTitle = CreateText(statsCard.transform, "StatsTitle", new Vector2(0f, 68f), "능력치");
            statsTitle.fontSize = 15;
            statsTitle.fontStyle = FontStyle.Bold;
            statsTitle.alignment = TextAnchor.MiddleLeft;
            statsTitle.rectTransform.sizeDelta = new Vector2(280f, 24f);
            statsTitle.color = new Color(0.72f, 0.79f, 0.89f, 1f);

            _statBuildText = CreateMultilineText(
                statsCard.transform,
                "StatsBuildText",
                new Vector2(0f, 50f),
                new Vector2(296f, 128f),
                "능력치");
            _statBuildText.fontSize = 14;
        }

        private void BuildBossBar()
        {
            _bossBarPanel = CreatePanel(
                _canvas.transform,
                "BossBarPanel",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -118f),
                new Vector2(780f, 64f),
                new Color(0.05f, 0.08f, 0.12f, 0.88f));
            _bossBarPanel.SetActive(false);

            _bossNameText = CreateText(_bossBarPanel.transform, "BossName", new Vector2(-284f, 0f), "보스");
            _bossNameText.alignment = TextAnchor.MiddleLeft;
            _bossNameText.fontSize = 19;
            _bossNameText.rectTransform.sizeDelta = new Vector2(180f, 28f);
            _bossNameText.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            var barRoot = new GameObject("BossBarRoot");
            barRoot.transform.SetParent(_bossBarPanel.transform, false);
            var barRootRect = barRoot.AddComponent<RectTransform>();
            barRootRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRootRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRootRect.pivot = new Vector2(0.5f, 0.5f);
            barRootRect.anchoredPosition = new Vector2(24f, 0f);
            barRootRect.sizeDelta = new Vector2(BossBarRootWidth, BossBarRootHeight);

            var barBg = barRoot.AddComponent<Image>();
            barBg.color = new Color(0.14f, 0.15f, 0.18f, 0.95f);

            var barFillObject = new GameObject("BossBarFill");
            barFillObject.transform.SetParent(barRoot.transform, false);
            var barFillRect = barFillObject.AddComponent<RectTransform>();
            barFillRect.anchorMin = new Vector2(0f, 0.5f);
            barFillRect.anchorMax = new Vector2(0f, 0.5f);
            barFillRect.pivot = new Vector2(0f, 0.5f);
            barFillRect.anchoredPosition = new Vector2(BossBarPadding, 0f);
            _bossBarFillMaxWidth = BossBarRootWidth - (BossBarPadding * 2f);
            var fillHeight = BossBarRootHeight - (BossBarPadding * 2f);
            barFillRect.sizeDelta = new Vector2(_bossBarFillMaxWidth, fillHeight);
            _bossBarFillRect = barFillRect;

            _bossBarFill = barFillObject.AddComponent<Image>();
            _bossBarFill.type = Image.Type.Simple;
            _bossBarFill.color = new Color(0.9f, 0.18f, 0.24f, 0.95f);

            _bossBarValueText = CreateText(_bossBarPanel.transform, "BossHpText", new Vector2(304f, 0f), "0/0");
            _bossBarValueText.alignment = TextAnchor.MiddleRight;
            _bossBarValueText.fontSize = 16;
            _bossBarValueText.rectTransform.sizeDelta = new Vector2(120f, 24f);
        }

        private void BuildLevelUpPanel()
        {
            _levelUpPanel = CreatePanel(_canvas.transform, "LevelUpPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(LevelPanelWidth, LevelPanelMinHeight), new Color(0.03f, 0.05f, 0.09f, 0.96f));
            _levelUpPanel.SetActive(false);

            _levelUpTitle = CreateText(_levelUpPanel.transform, "Title", Vector2.zero, "레벨 업");
            _levelUpTitle.fontSize = 30;
            _levelUpTitle.fontStyle = FontStyle.Bold;
            _levelUpTitle.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            _levelUpTitle.rectTransform.sizeDelta = new Vector2(720f, 72f);
            _levelButtons = new Button[10];
            _levelButtonTexts = new Text[10];

            for (var i = 0; i < _levelButtons.Length; i++)
            {
                var button = CreateButton(_levelUpPanel.transform, $"OptionButton{i}", Vector2.zero, new Vector2(LevelButtonWidth, LevelButtonHeight));
                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 18;
                label.rectTransform.offsetMin = new Vector2(22f, 10f);
                label.rectTransform.offsetMax = new Vector2(-22f, -10f);
                _levelButtons[i] = button;
                _levelButtonTexts[i] = label;
            }

            LayoutLevelUpPanel(3);
        }

        private void LayoutLevelUpPanel(int visibleOptionCount)
        {
            if (_levelUpPanel == null || _levelUpTitle == null)
            {
                return;
            }

            var clampedCount = Mathf.Clamp(visibleOptionCount, 1, _levelButtons != null ? _levelButtons.Length : 1);
            var buttonsHeight = (clampedCount * LevelButtonHeight) + (Mathf.Max(0, clampedCount - 1) * LevelButtonSpacing);
            var panelHeight = Mathf.Max(
                LevelPanelMinHeight,
                LevelPanelTopPadding + LevelTitleHeight + LevelButtonsTopGap + buttonsHeight + LevelPanelBottomPadding);

            var panelRect = _levelUpPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.sizeDelta = new Vector2(LevelPanelWidth, panelHeight);
            }

            var topY = (panelHeight * 0.5f) - LevelPanelTopPadding;
            var titleRect = _levelUpTitle.rectTransform;
            titleRect.anchoredPosition = new Vector2(0f, topY - (LevelTitleHeight * 0.5f));

            var firstButtonCenterY = topY - LevelTitleHeight - LevelButtonsTopGap - (LevelButtonHeight * 0.5f);
            for (var i = 0; i < _levelButtons.Length; i++)
            {
                var buttonRect = _levelButtons[i].GetComponent<RectTransform>();
                if (buttonRect == null)
                {
                    continue;
                }

                buttonRect.sizeDelta = new Vector2(LevelButtonWidth, LevelButtonHeight);
                buttonRect.anchoredPosition = new Vector2(0f, firstButtonCenterY - (i * (LevelButtonHeight + LevelButtonSpacing)));
            }
        }

        private void BuildResultPanel()
        {
            _resultPanel = CreatePanel(_canvas.transform, "ResultPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 620f), new Color(0.03f, 0.05f, 0.09f, 0.96f));
            _resultPanel.SetActive(false);
            _resultText = CreateMultilineText(_resultPanel.transform, "ResultText", new Vector2(0f, 246f), new Vector2(660f, 420f), "게임 오버");
            _resultText.fontSize = 18;
            _resultText.fontStyle = FontStyle.Bold;
            _resultText.alignment = TextAnchor.UpperLeft;
            _restartButton = CreateButton(_resultPanel.transform, "RestartButton", new Vector2(0f, -248f), new Vector2(264f, 56f));
            _restartButton.GetComponentInChildren<Text>().text = "\uD0C0\uC774\uD2C0\uB85C";
        }

        private void BuildPausePanel()
        {
            _pausePanel = CreatePanel(
                _canvas.transform,
                "PausePanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(560f, 540f),
                new Color(0.03f, 0.05f, 0.09f, 0.96f));
            _pausePanel.SetActive(false);

            var title = CreateText(_pausePanel.transform, "PauseTitle", new Vector2(0f, 68f), "일시정지");
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            title.text = "일시 정지";
            title.rectTransform.anchoredPosition = new Vector2(0f, 198f);

            var subhead = CreateText(_pausePanel.transform, "PauseSubhead", new Vector2(0f, 42f), "전투를 계속하거나 타이틀로 돌아갑니다.");
            subhead.fontSize = 15;
            subhead.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            subhead.rectTransform.sizeDelta = new Vector2(440f, 38f);
            subhead.text = "전투를 계속하거나 설정을 바꾼 뒤 타이틀로 돌아갈 수 있습니다.";
            subhead.rectTransform.anchoredPosition = new Vector2(0f, 166f);

            _pauseResumeButton = CreateButton(_pausePanel.transform, "ResumeButton", new Vector2(0f, 70f), new Vector2(252f, 56f));
            _pauseSettingsButton = CreateButton(_pausePanel.transform, "PauseSettingsButton", new Vector2(0f, -2f), new Vector2(252f, 56f));
            _pauseSettingsButton.GetComponentInChildren<Text>().text = "설정";
            _pauseSettingsButton.onClick.RemoveAllListeners();
            _pauseSettingsButton.onClick.AddListener(OpenPauseSettings);
            _pauseResumeButton.GetComponentInChildren<Text>().text = "계속하기";
            _pauseResumeButton.GetComponentInChildren<Text>().text = "계속하기";

            _pauseQuitButton = CreateButton(_pausePanel.transform, "QuitButton", new Vector2(0f, -74f), new Vector2(272f, 56f));
            if (_pauseQuitButton.targetGraphic is Image quitImage)
            {
                quitImage.color = new Color(0.31f, 0.15f, 0.18f, 0.98f);
                var quitColors = _pauseQuitButton.colors;
                quitColors.normalColor = quitImage.color;
                quitColors.highlightedColor = new Color(0.40f, 0.20f, 0.24f, 1f);
                quitColors.selectedColor = quitColors.highlightedColor;
                quitColors.pressedColor = new Color(0.23f, 0.11f, 0.14f, 1f);
                _pauseQuitButton.colors = quitColors;
            }
            _pauseQuitButton.GetComponentInChildren<Text>().text = "타이틀로";
            _pauseQuitButton.GetComponentInChildren<Text>().text = "타이틀로";

            _pauseSettingsPanel = CreatePanel(
                _pausePanel.transform,
                "PauseSettingsPanel",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -68f),
                new Vector2(500f, 290f),
                new Color(0.05f, 0.08f, 0.12f, 0.92f));
            _pauseSettingsPanel.SetActive(false);

            var settingsTitle = CreateText(_pauseSettingsPanel.transform, "PauseSettingsTitle", new Vector2(0f, 118f), "설정");
            settingsTitle.fontSize = 22;
            settingsTitle.fontStyle = FontStyle.Bold;
            settingsTitle.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            var displayCard = CreatePanel(
                _pauseSettingsPanel.transform,
                "PauseDisplayCard",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 46f),
                new Vector2(420f, 78f),
                new Color(0.07f, 0.10f, 0.14f, 0.92f));
            var displayLabel = CreateText(displayCard.transform, "PauseDisplayLabel", new Vector2(0f, 20f), "화면");
            displayLabel.fontSize = 16;
            displayLabel.fontStyle = FontStyle.Bold;
            displayLabel.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            displayLabel.rectTransform.sizeDelta = new Vector2(160f, 24f);
            _pauseFullscreenToggle = CreateToggle(displayCard.transform, "PauseFullscreenToggle", new Vector2(0f, -12f), new Vector2(240f, 32f), "전체 화면", OnPauseFullscreenToggleChanged);

            var audioCard = CreatePanel(
                _pauseSettingsPanel.transform,
                "PauseAudioCard",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -42f),
                new Vector2(440f, 136f),
                new Color(0.07f, 0.10f, 0.14f, 0.92f));
            var audioLabel = CreateText(audioCard.transform, "PauseAudioLabel", new Vector2(0f, 46f), "오디오");
            audioLabel.fontSize = 16;
            audioLabel.fontStyle = FontStyle.Bold;
            audioLabel.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            audioLabel.rectTransform.sizeDelta = new Vector2(160f, 24f);

            CreateSliderControl(audioCard.transform, "PauseMasterVolume", new Vector2(0f, 12f), "마스터", OnPauseMasterVolumeChanged, out _pauseMasterVolumeSlider, out _pauseMasterVolumeValueText);
            CreateSliderControl(audioCard.transform, "PauseBgmVolume", new Vector2(0f, -22f), "배경음", OnPauseBgmVolumeChanged, out _pauseBgmVolumeSlider, out _pauseBgmVolumeValueText);
            CreateSliderControl(audioCard.transform, "PauseSfxVolume", new Vector2(0f, -56f), "효과음", OnPauseSfxVolumeChanged, out _pauseSfxVolumeSlider, out _pauseSfxVolumeValueText);

            var settingsBackButton = CreateButton(_pauseSettingsPanel.transform, "PauseSettingsBackButton", new Vector2(0f, -116f), new Vector2(220f, 44f));
            settingsBackButton.GetComponentInChildren<Text>().text = "돌아가기";
            settingsBackButton.onClick.RemoveAllListeners();
            settingsBackButton.onClick.AddListener(() => ClosePauseSettings());
        }

        private void BuildDebugPanels()
        {
            _debugAccessButton = CreateButton(_canvas.transform, "DebugAccessButton", Vector2.zero, new Vector2(72f, 38f));
            var accessRect = _debugAccessButton.GetComponent<RectTransform>();
            accessRect.anchorMin = new Vector2(0f, 0f);
            accessRect.anchorMax = new Vector2(0f, 0f);
            accessRect.pivot = new Vector2(0f, 0f);
            accessRect.anchoredPosition = new Vector2(18f, 18f);
            _debugAccessButton.GetComponentInChildren<Text>().text = "개발";
            _debugAccessButton.onClick.RemoveAllListeners();
            _debugAccessButton.onClick.AddListener(ToggleDebugEntry);
            _debugAccessButton.gameObject.SetActive(false);

            _debugToolsPanel = CreatePanel(
                _canvas.transform,
                "DebugToolsPanel",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(18f, 68f),
                new Vector2(320f, 300f),
                new Color(0.03f, 0.05f, 0.09f, 0.94f));
            _debugToolsPanel.SetActive(false);

            var toolsTitle = CreateText(_debugToolsPanel.transform, "DebugToolsTitle", new Vector2(0f, 118f), "디버그 도구");
            toolsTitle.fontSize = 18;
            toolsTitle.fontStyle = FontStyle.Bold;
            toolsTitle.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            _debugGrantLevelButton = CreateButton(_debugToolsPanel.transform, "DebugGrantLevelButton", new Vector2(0f, 66f), new Vector2(252f, 40f));
            _debugGrantLevelLabel = _debugGrantLevelButton.GetComponentInChildren<Text>();

            _debugAdvanceTimeButton = CreateButton(_debugToolsPanel.transform, "DebugAdvanceTimeButton", new Vector2(0f, 20f), new Vector2(252f, 40f));
            _debugAdvanceTimeLabel = _debugAdvanceTimeButton.GetComponentInChildren<Text>();

            _debugRerollButton = CreateButton(_debugToolsPanel.transform, "DebugRerollButton", new Vector2(0f, -26f), new Vector2(252f, 40f));
            _debugRerollLabel = _debugRerollButton.GetComponentInChildren<Text>();

            _debugSkipBossButton = CreateButton(_debugToolsPanel.transform, "DebugSkipBossButton", new Vector2(0f, -72f), new Vector2(252f, 40f));
            _debugSkipBossLabel = _debugSkipBossButton.GetComponentInChildren<Text>();

            _debugAutoPlayButton = CreateButton(_debugToolsPanel.transform, "DebugAutoPlayButton", new Vector2(0f, -118f), new Vector2(252f, 40f));
            _debugAutoPlayLabel = _debugAutoPlayButton.GetComponentInChildren<Text>();

            RefreshDebugToolButtons();
        }

        private void ToggleDebugEntry()
        {
            if (!_debugAccessVisible)
            {
                return;
            }

            if (HasGameplayToolkit)
            {
                ToggleGameplayToolkitDebugPanel();
                return;
            }

            var nextToolkitVisible = HasGameplayToolkit &&
                                     (_gameplayToolkitDebugPanel == null || _gameplayToolkitDebugPanel.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.None);
            var nextVisible = _debugToolsPanel != null && !_debugToolsPanel.activeSelf;
            HideDebugPanels();
            if (HasGameplayToolkit && nextToolkitVisible)
            {
                ToggleGameplayToolkitDebugPanel();
            }

            if (_debugToolsPanel != null)
            {
                _debugToolsPanel.SetActive(nextVisible);
            }
        }

        private void RefreshDebugToolButtons()
        {
            ConfigureDebugButton(_debugGrantLevelButton, _debugGrantLevelLabel, "레벨 +1", _debugGrantLevelAction);
            ConfigureDebugButton(_debugAdvanceTimeButton, _debugAdvanceTimeLabel, "레벨 +5", _debugAdvanceTimeAction);
            ConfigureDebugButton(_debugRerollButton, _debugRerollLabel, "선택지 다시 굴리기", _debugRerollAction);
            ConfigureDebugButton(_debugWave1Button, _debugWave1Label, "1웨이브", _debugWave1Action);
            ConfigureDebugButton(_debugWave2Button, _debugWave2Label, "2웨이브", _debugWave2Action);
            ConfigureDebugButton(_debugSkipBossButton, _debugSkipBossLabel, "보스", _debugSkipBossAction);
            ConfigureDebugButton(_debugInvincibleButton, _debugInvincibleLabel, _debugInvincibleEnabled ? "무적: 켜짐" : "무적: 꺼짐", _debugInvincibleAction);
            ConfigureDebugButton(_debugAutoPlayButton, _debugAutoPlayLabel, _debugAutoPlayEnabled ? "자동 전투: 켜짐" : "자동 전투: 꺼짐", _debugAutoPlayAction);
            ConfigureGameplayToolkitDebugButtons();
        }

        private void BuildTopBarReference()
        {
            if (HasGameplayToolkit)
            {
                return;
            }

            var healthCard = CreatePanel(_canvas.transform, "HealthCardV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(250f, 72f), new Color(0.04f, 0.07f, 0.11f, 0.9f));
            var xpCard = CreatePanel(_canvas.transform, "XpCardV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(290f, -24f), new Vector2(334f, 72f), new Color(0.04f, 0.07f, 0.11f, 0.9f));
            var timeCard = CreatePanel(_canvas.transform, "TimeCardV2", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(218f, 72f), new Color(0.04f, 0.07f, 0.11f, 0.9f));
            _modeHintCard = CreatePanel(_canvas.transform, "ModeHintCardV2", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-256f, -24f), new Vector2(212f, 72f), new Color(0.04f, 0.07f, 0.11f, 0.9f));
            _modeHintCard.SetActive(false);
            _waveStatusPanel = CreatePanel(_canvas.transform, "WaveStatusPanelV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(300f, 60f), new Color(0.04f, 0.07f, 0.11f, 0.92f));
            _waveStatusPanel.SetActive(false);
            _waveBannerPanel = CreatePanel(_canvas.transform, "WaveBannerPanelV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -94f), new Vector2(360f, 54f), new Color(0.08f, 0.11f, 0.16f, 0.95f));
            _waveBannerPanel.SetActive(false);

            _healthText = CreateText(healthCard.transform, "HealthTextV2", Vector2.zero, "\uCCB4\uB825");
            _healthText.alignment = TextAnchor.MiddleLeft;
            _healthText.rectTransform.sizeDelta = new Vector2(210f, 42f);
            _healthText.fontSize = 21;

            _xpText = CreateText(xpCard.transform, "XPTextV2", Vector2.zero, "\uACBD\uD5D8\uCE58");
            _xpText.alignment = TextAnchor.MiddleLeft;
            _xpText.rectTransform.sizeDelta = new Vector2(280f, 42f);
            _xpText.fontSize = 19;

            _timeText = CreateText(timeCard.transform, "TimeTextV2", Vector2.zero, "\uC2DC\uAC04");
            _timeText.alignment = TextAnchor.MiddleLeft;
            _timeText.rectTransform.sizeDelta = new Vector2(172f, 42f);
            _timeText.fontSize = 19;

            _modeHintText = CreateText(_modeHintCard.transform, "ModeHintTextV2", Vector2.zero, string.Empty);
            _modeHintText.fontSize = 15;
            _modeHintText.alignment = TextAnchor.MiddleCenter;
            _modeHintText.rectTransform.sizeDelta = new Vector2(172f, 42f);
            _modeHintText.color = new Color(0.76f, 0.82f, 0.90f, 1f);
            _waveStatusText = CreateText(_waveStatusPanel.transform, "WaveStatusTextV2", Vector2.zero, string.Empty);
            _waveStatusText.fontSize = 17;
            _waveStatusText.alignment = TextAnchor.MiddleCenter;
            _waveStatusText.rectTransform.sizeDelta = new Vector2(260f, 36f);
            _waveStatusText.color = new Color(0.95f, 0.96f, 1f, 1f);
            _waveBannerText = CreateText(_waveBannerPanel.transform, "WaveBannerTextV2", Vector2.zero, string.Empty);
            _waveBannerText.fontSize = 18;
            _waveBannerText.fontStyle = FontStyle.Bold;
            _waveBannerText.alignment = TextAnchor.MiddleCenter;
            _waveBannerText.rectTransform.sizeDelta = new Vector2(320f, 34f);
            _waveBannerText.color = new Color(1f, 0.92f, 0.42f, 1f);
        }

        private void BuildBuildPanelReference()
        {
            if (HasGameplayToolkit)
            {
                return;
            }

            _buildToggleButton = CreateButton(_canvas.transform, "BuildToggleButtonV2", new Vector2(-1f, -1f), new Vector2(132f, 46f));
            var toggleRect = _buildToggleButton.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 1f);
            toggleRect.anchorMax = new Vector2(0f, 1f);
            toggleRect.pivot = new Vector2(0f, 1f);
            toggleRect.anchoredPosition = new Vector2(24f, -112f);
            _buildToggleText = _buildToggleButton.GetComponentInChildren<Text>();
            _buildToggleText.text = "빌드";
            _buildToggleButton.onClick.RemoveAllListeners();
            _buildToggleButton.onClick.AddListener(ToggleBuildDrawer);

            _buildPanel = CreatePanel(_canvas.transform, "BuildPanelV2", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -168f), new Vector2(342f, 402f), new Color(0.03f, 0.05f, 0.09f, 0.9f));
            _buildPanel.SetActive(false);

            var buildTitle = CreateText(_buildPanel.transform, "BuildTitleV2", new Vector2(0f, 166f), "\uBE4C\uB4DC");
            buildTitle.fontSize = 22;
            buildTitle.fontStyle = FontStyle.Bold;
            buildTitle.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            buildTitle.rectTransform.sizeDelta = new Vector2(220f, 28f);

            var weaponsCard = CreatePanel(_buildPanel.transform, "WeaponsCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 56f), new Vector2(304f, 148f), new Color(0.07f, 0.10f, 0.14f, 0.94f));
            var weaponsTitle = CreateText(weaponsCard.transform, "WeaponsTitleV2", new Vector2(0f, 52f), "\uBB34\uAE30");
            weaponsTitle.fontSize = 15;
            weaponsTitle.fontStyle = FontStyle.Bold;
            weaponsTitle.alignment = TextAnchor.MiddleLeft;
            weaponsTitle.rectTransform.sizeDelta = new Vector2(260f, 24f);
            weaponsTitle.color = new Color(0.72f, 0.79f, 0.89f, 1f);
            _weaponBuildText = CreateMultilineText(weaponsCard.transform, "WeaponsBuildTextV2", new Vector2(0f, 30f), new Vector2(264f, 92f), "\uBB34\uAE30");
            _weaponBuildText.fontSize = 14;

            var statsCard = CreatePanel(_buildPanel.transform, "StatsCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -114f), new Vector2(304f, 170f), new Color(0.07f, 0.10f, 0.14f, 0.94f));
            var statsTitle = CreateText(statsCard.transform, "StatsTitleV2", new Vector2(0f, 62f), "\uC804\uD22C \uC218\uCE58");
            statsTitle.fontSize = 15;
            statsTitle.fontStyle = FontStyle.Bold;
            statsTitle.alignment = TextAnchor.MiddleLeft;
            statsTitle.rectTransform.sizeDelta = new Vector2(260f, 24f);
            statsTitle.color = new Color(0.72f, 0.79f, 0.89f, 1f);
            _statBuildText = CreateMultilineText(statsCard.transform, "StatsBuildTextV2", new Vector2(0f, 44f), new Vector2(264f, 118f), "\uC804\uD22C \uC218\uCE58");
            _statBuildText.fontSize = 14;
        }

        private void BuildBossBarReference()
        {
            if (HasGameplayToolkit)
            {
                return;
            }

            _bossBarPanel = CreatePanel(_canvas.transform, "BossBarPanelV2", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(840f, 68f), new Color(0.05f, 0.08f, 0.12f, 0.9f));
            _bossBarPanel.SetActive(false);

            _bossNameText = CreateText(_bossBarPanel.transform, "BossNameV2", new Vector2(-300f, 0f), "\uBCF4\uC2A4");
            _bossNameText.alignment = TextAnchor.MiddleLeft;
            _bossNameText.fontSize = 19;
            _bossNameText.rectTransform.sizeDelta = new Vector2(200f, 28f);
            _bossNameText.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            var barRoot = new GameObject("BossBarRootV2");
            barRoot.transform.SetParent(_bossBarPanel.transform, false);
            var barRootRect = barRoot.AddComponent<RectTransform>();
            barRootRect.anchorMin = new Vector2(0.5f, 0.5f);
            barRootRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRootRect.pivot = new Vector2(0.5f, 0.5f);
            barRootRect.anchoredPosition = new Vector2(28f, 0f);
            barRootRect.sizeDelta = new Vector2(BossBarRootWidth + 40f, BossBarRootHeight + 2f);

            var barBg = barRoot.AddComponent<Image>();
            barBg.color = new Color(0.14f, 0.15f, 0.18f, 0.95f);

            var barFillObject = new GameObject("BossBarFillV2");
            barFillObject.transform.SetParent(barRoot.transform, false);
            var barFillRect = barFillObject.AddComponent<RectTransform>();
            barFillRect.anchorMin = new Vector2(0f, 0.5f);
            barFillRect.anchorMax = new Vector2(0f, 0.5f);
            barFillRect.pivot = new Vector2(0f, 0.5f);
            barFillRect.anchoredPosition = new Vector2(BossBarPadding, 0f);
            _bossBarFillMaxWidth = (BossBarRootWidth + 40f) - (BossBarPadding * 2f);
            var fillHeight = (BossBarRootHeight + 2f) - (BossBarPadding * 2f);
            barFillRect.sizeDelta = new Vector2(_bossBarFillMaxWidth, fillHeight);
            _bossBarFillRect = barFillRect;

            _bossBarFill = barFillObject.AddComponent<Image>();
            _bossBarFill.color = new Color(0.9f, 0.18f, 0.24f, 0.95f);

            _bossBarValueText = CreateText(_bossBarPanel.transform, "BossHpTextV2", new Vector2(332f, 0f), "0/0");
            _bossBarValueText.alignment = TextAnchor.MiddleRight;
            _bossBarValueText.fontSize = 16;
            _bossBarValueText.rectTransform.sizeDelta = new Vector2(128f, 24f);

            _bossDirectionIndicatorPanel = CreatePanel(
                _canvas.transform,
                "BossDirectionIndicatorV2",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(66f, 66f),
                new Color(0.12f, 0.02f, 0.04f, 0.78f));
            _bossDirectionIndicatorRect = _bossDirectionIndicatorPanel.GetComponent<RectTransform>();
            _bossDirectionIndicatorPanel.SetActive(false);

            _bossDirectionIndicatorText = CreateText(_bossDirectionIndicatorPanel.transform, "BossDirectionArrowV2", Vector2.zero, "^");
            _bossDirectionIndicatorText.fontSize = 34;
            _bossDirectionIndicatorText.fontStyle = FontStyle.Bold;
            _bossDirectionIndicatorText.color = new Color(1f, 0.26f, 0.30f, 1f);
            _bossDirectionIndicatorText.rectTransform.sizeDelta = new Vector2(38f, 38f);

            _waveTargetDirectionIndicatorPanel = CreatePanel(
                _canvas.transform,
                "WaveTargetDirectionIndicatorV2",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(66f, 66f),
                new Color(0.14f, 0.04f, 0.04f, 0.74f));
            _waveTargetDirectionIndicatorRect = _waveTargetDirectionIndicatorPanel.GetComponent<RectTransform>();
            _waveTargetDirectionIndicatorPanel.SetActive(false);

            _waveTargetDirectionIndicatorText = CreateText(_waveTargetDirectionIndicatorPanel.transform, "WaveTargetDirectionArrowV2", Vector2.zero, "^");
            _waveTargetDirectionIndicatorText.fontSize = 34;
            _waveTargetDirectionIndicatorText.fontStyle = FontStyle.Bold;
            _waveTargetDirectionIndicatorText.color = new Color(1f, 0.86f, 0.86f, 1f);
            _waveTargetDirectionIndicatorText.rectTransform.sizeDelta = new Vector2(38f, 38f);

            _rewardDirectionIndicatorPanel = CreatePanel(
                _canvas.transform,
                "RewardDirectionIndicatorV2",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(66f, 66f),
                new Color(0.03f, 0.14f, 0.07f, 0.78f));
            _rewardDirectionIndicatorRect = _rewardDirectionIndicatorPanel.GetComponent<RectTransform>();
            _rewardDirectionIndicatorPanel.SetActive(false);

            _rewardDirectionIndicatorText = CreateText(_rewardDirectionIndicatorPanel.transform, "RewardDirectionArrowV2", Vector2.zero, "^");
            _rewardDirectionIndicatorText.fontSize = 34;
            _rewardDirectionIndicatorText.fontStyle = FontStyle.Bold;
            _rewardDirectionIndicatorText.color = new Color(0.40f, 1f, 0.48f, 1f);
            _rewardDirectionIndicatorText.rectTransform.sizeDelta = new Vector2(38f, 38f);
            _rewardDirectionIndicatorPanels.Clear();
            _rewardDirectionIndicatorRects.Clear();
            _rewardDirectionIndicatorPanels.Add(_rewardDirectionIndicatorPanel);
            _rewardDirectionIndicatorRects.Add(_rewardDirectionIndicatorRect);
        }

        private void BuildLevelUpPanelReference()
        {
            if (HasGameplayToolkit)
            {
                return;
            }

            _levelUpPanel = CreatePanel(_canvas.transform, "LevelUpPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(LevelPanelWidth, LevelPanelMinHeight), new Color(0.03f, 0.05f, 0.09f, 0.96f));
            _levelUpPanel.SetActive(false);

            _levelUpTitle = CreateText(_levelUpPanel.transform, "TitleV2", Vector2.zero, "\uB808\uBCA8 \uC5C5");
            _levelUpTitle.fontSize = 30;
            _levelUpTitle.fontStyle = FontStyle.Bold;
            _levelUpTitle.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            _levelUpTitle.rectTransform.sizeDelta = new Vector2(760f, 72f);

            _levelButtons = new Button[10];
            _levelButtonTexts = new Text[10];
            for (var i = 0; i < _levelButtons.Length; i++)
            {
                var button = CreateButton(_levelUpPanel.transform, $"OptionButtonV2_{i}", Vector2.zero, new Vector2(LevelButtonWidth, LevelButtonHeight));
                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 18;
                label.rectTransform.offsetMin = new Vector2(22f, 10f);
                label.rectTransform.offsetMax = new Vector2(-22f, -10f);
                _levelButtons[i] = button;
                _levelButtonTexts[i] = label;
            }

            LayoutLevelUpPanel(3);
        }

        private void BuildResultPanelReference()
        {
            if (HasGameplayToolkit)
            {
                return;
            }

            _resultPanel = CreatePanel(_canvas.transform, "ResultPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 680f), new Color(0.03f, 0.05f, 0.09f, 0.96f));
            _resultPanel.SetActive(false);

            var title = CreateText(_resultPanel.transform, "ResultTitleV2", new Vector2(0f, 292f), "\uB7F0 \uACB0\uACFC");
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.95f, 0.97f, 1f, 1f);

            var bodyCard = CreatePanel(_resultPanel.transform, "ResultBodyCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(792f, 486f), new Color(0.07f, 0.10f, 0.15f, 0.96f));
            _resultText = CreateMultilineText(bodyCard.transform, "ResultTextV2", new Vector2(0f, -24f), new Vector2(724f, 430f), "\uB7F0 \uACB0\uACFC");
            _resultText.fontSize = 17;
            _resultText.fontStyle = FontStyle.Normal;
            _resultText.alignment = TextAnchor.UpperLeft;
            _restartButton = CreateButton(_resultPanel.transform, "RestartButtonV2", new Vector2(0f, -286f), new Vector2(292f, 56f));
            _restartButton.GetComponentInChildren<Text>().text = "\uD0C0\uC774\uD2C0\uB85C";
        }

        private void BuildPausePanelReference()
        {
            if (HasPauseMainToolkit)
            {
                return;
            }

            _pausePanel = CreatePanel(_canvas.transform, "PausePanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 560f), new Color(0.03f, 0.05f, 0.09f, 0.96f));
            _pausePanel.SetActive(false);

            _pauseMainContentRoot = new GameObject("PauseMainContentRootV2");
            _pauseMainContentRoot.transform.SetParent(_pausePanel.transform, false);
            var mainContentRect = _pauseMainContentRoot.AddComponent<RectTransform>();
            mainContentRect.anchorMin = Vector2.zero;
            mainContentRect.anchorMax = Vector2.one;
            mainContentRect.pivot = new Vector2(0.5f, 0.5f);
            mainContentRect.offsetMin = Vector2.zero;
            mainContentRect.offsetMax = Vector2.zero;

            var title = CreateText(_pauseMainContentRoot.transform, "PauseTitleV2", new Vector2(0f, 206f), "\uC77C\uC2DC \uC815\uC9C0");
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            var subhead = CreateText(_pauseMainContentRoot.transform, "PauseSubheadV2", new Vector2(0f, 168f), "\uD50C\uB808\uC774 \uACC4\uC18D, \uC124\uC815, \uD0C0\uC774\uD2C0 \uC774\uB3D9\uC744 \uC5EC\uAE30\uC11C \uACE0\uB985\uB2C8\uB2E4.");
            subhead.fontSize = 15;
            subhead.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            subhead.rectTransform.sizeDelta = new Vector2(520f, 38f);

            _pauseResumeButton = CreateButton(_pauseMainContentRoot.transform, "ResumeButtonV2", new Vector2(-150f, 74f), new Vector2(248f, 56f));
            _pauseResumeButton.GetComponentInChildren<Text>().text = "\uACC4\uC18D\uD558\uAE30";

            _pauseSettingsButton = CreateButton(_pauseMainContentRoot.transform, "PauseSettingsButtonV2", new Vector2(150f, 74f), new Vector2(248f, 56f));
            _pauseSettingsButton.GetComponentInChildren<Text>().text = "\uC124\uC815";
            _pauseSettingsButton.onClick.RemoveAllListeners();
            _pauseSettingsButton.onClick.AddListener(OpenPauseSettings);

            _pauseQuitButton = CreateButton(_pauseMainContentRoot.transform, "QuitButtonV2", new Vector2(0f, 2f), new Vector2(292f, 56f));
            _pauseQuitButton.GetComponentInChildren<Text>().text = "\uD0C0\uC774\uD2C0\uB85C";
            if (_pauseQuitButton.targetGraphic is Image quitImage)
            {
                quitImage.color = new Color(0.31f, 0.15f, 0.18f, 0.98f);
                var quitColors = _pauseQuitButton.colors;
                quitColors.normalColor = quitImage.color;
                quitColors.highlightedColor = new Color(0.40f, 0.20f, 0.24f, 1f);
                quitColors.selectedColor = quitColors.highlightedColor;
                quitColors.pressedColor = new Color(0.23f, 0.11f, 0.14f, 1f);
                _pauseQuitButton.colors = quitColors;
            }

            _pauseSettingsPanel = CreatePanel(_pausePanel.transform, "PauseSettingsPanelV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(580f, 340f), new Color(0.05f, 0.08f, 0.12f, 0.96f));
            _pauseSettingsPanel.SetActive(false);

            var settingsTitle = CreateText(_pauseSettingsPanel.transform, "PauseSettingsTitleV2", new Vector2(0f, 140f), "\uC124\uC815");
            settingsTitle.fontSize = 22;
            settingsTitle.fontStyle = FontStyle.Bold;
            settingsTitle.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            var displayCard = CreatePanel(_pauseSettingsPanel.transform, "PauseDisplayCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 76f), new Vector2(500f, 92f), new Color(0.07f, 0.10f, 0.14f, 0.94f));
            var displayLabel = CreateText(displayCard.transform, "PauseDisplayLabelV2", new Vector2(0f, 20f), "\uD654\uBA74");
            displayLabel.fontSize = 16;
            displayLabel.fontStyle = FontStyle.Bold;
            displayLabel.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            displayLabel.rectTransform.sizeDelta = new Vector2(160f, 24f);
            _pauseFullscreenToggle = CreateToggle(displayCard.transform, "PauseFullscreenToggleV2", new Vector2(0f, -8f), new Vector2(248f, 32f), "\uC804\uCCB4 \uD654\uBA74", OnPauseFullscreenToggleChanged);

            var audioCard = CreatePanel(_pauseSettingsPanel.transform, "PauseAudioCardV2", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(500f, 156f), new Color(0.07f, 0.10f, 0.14f, 0.94f));
            var audioLabel = CreateText(audioCard.transform, "PauseAudioLabelV2", new Vector2(0f, 52f), "\uC624\uB514\uC624");
            audioLabel.fontSize = 16;
            audioLabel.fontStyle = FontStyle.Bold;
            audioLabel.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            audioLabel.rectTransform.sizeDelta = new Vector2(160f, 24f);

            CreateSliderControl(audioCard.transform, "PauseMasterVolumeV2", new Vector2(0f, 18f), "\uB9C8\uC2A4\uD130", OnPauseMasterVolumeChanged, out _pauseMasterVolumeSlider, out _pauseMasterVolumeValueText);
            CreateSliderControl(audioCard.transform, "PauseBgmVolumeV2", new Vector2(0f, -18f), "\uBC30\uACBD\uC74C", OnPauseBgmVolumeChanged, out _pauseBgmVolumeSlider, out _pauseBgmVolumeValueText);
            CreateSliderControl(audioCard.transform, "PauseSfxVolumeV2", new Vector2(0f, -54f), "\uD6A8\uACFC\uC74C", OnPauseSfxVolumeChanged, out _pauseSfxVolumeSlider, out _pauseSfxVolumeValueText);

            var settingsBackButton = CreateButton(_pauseSettingsPanel.transform, "PauseSettingsBackButtonV2", new Vector2(0f, -138f), new Vector2(228f, 44f));
            settingsBackButton.GetComponentInChildren<Text>().text = "\uB3CC\uC544\uAC00\uAE30";
            settingsBackButton.onClick.RemoveAllListeners();
            settingsBackButton.onClick.AddListener(() => ClosePauseSettings());
        }

        private void BuildDebugPanelsReference()
        {
            if (HasGameplayToolkit)
            {
                return;
            }

            _debugAccessButton = CreateButton(_canvas.transform, "DebugAccessButtonV2", Vector2.zero, new Vector2(72f, 38f));
            var accessRect = _debugAccessButton.GetComponent<RectTransform>();
            accessRect.anchorMin = new Vector2(0f, 0f);
            accessRect.anchorMax = new Vector2(0f, 0f);
            accessRect.pivot = new Vector2(0f, 0f);
            accessRect.anchoredPosition = new Vector2(18f, 18f);
            _debugAccessButton.GetComponentInChildren<Text>().text = "DEV";
            _debugAccessButton.onClick.RemoveAllListeners();
            _debugAccessButton.onClick.AddListener(ToggleDebugEntry);
            _debugAccessButton.gameObject.SetActive(false);

            _debugToolsPanel = CreatePanel(_canvas.transform, "DebugToolsPanelV2", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 68f), new Vector2(328f, 438f), new Color(0.03f, 0.05f, 0.09f, 0.94f));
            _debugToolsPanel.SetActive(false);

            var toolsTitle = CreateText(_debugToolsPanel.transform, "DebugToolsTitleV2", new Vector2(0f, 188f), "\uB514\uBC84\uADF8 \uB3C4\uAD6C");
            toolsTitle.fontSize = 18;
            toolsTitle.fontStyle = FontStyle.Bold;
            toolsTitle.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            _debugGrantLevelButton = CreateButton(_debugToolsPanel.transform, "DebugGrantLevelButtonV2", new Vector2(0f, 136f), new Vector2(260f, 40f));
            _debugGrantLevelLabel = _debugGrantLevelButton.GetComponentInChildren<Text>();
            _debugAdvanceTimeButton = CreateButton(_debugToolsPanel.transform, "DebugAdvanceTimeButtonV2", new Vector2(0f, 90f), new Vector2(260f, 40f));
            _debugAdvanceTimeLabel = _debugAdvanceTimeButton.GetComponentInChildren<Text>();
            _debugRerollButton = CreateButton(_debugToolsPanel.transform, "DebugRerollButtonV2", new Vector2(0f, 44f), new Vector2(260f, 40f));
            _debugRerollLabel = _debugRerollButton.GetComponentInChildren<Text>();
            _debugWave1Button = CreateButton(_debugToolsPanel.transform, "DebugWave1ButtonV2", new Vector2(0f, -2f), new Vector2(260f, 40f));
            _debugWave1Label = _debugWave1Button.GetComponentInChildren<Text>();
            _debugWave2Button = CreateButton(_debugToolsPanel.transform, "DebugWave2ButtonV2", new Vector2(0f, -48f), new Vector2(260f, 40f));
            _debugWave2Label = _debugWave2Button.GetComponentInChildren<Text>();
            _debugSkipBossButton = CreateButton(_debugToolsPanel.transform, "DebugSkipBossButtonV2", new Vector2(0f, -94f), new Vector2(260f, 40f));
            _debugSkipBossLabel = _debugSkipBossButton.GetComponentInChildren<Text>();
            _debugInvincibleButton = CreateButton(_debugToolsPanel.transform, "DebugInvincibleButtonV2", new Vector2(0f, -140f), new Vector2(260f, 40f));
            _debugInvincibleLabel = _debugInvincibleButton.GetComponentInChildren<Text>();
            _debugAutoPlayButton = CreateButton(_debugToolsPanel.transform, "DebugAutoPlayButtonV2", new Vector2(0f, -186f), new Vector2(260f, 40f));
            _debugAutoPlayLabel = _debugAutoPlayButton.GetComponentInChildren<Text>();

            RefreshDebugToolButtons();
        }

        private static void ConfigureDebugButton(Button button, Text label, string text, Action action)
        {
            if (button == null || label == null)
            {
                return;
            }

            var isActive = action != null;
            button.gameObject.SetActive(isActive);
            if (!isActive)
            {
                return;
            }

            label.text = text;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action.Invoke());
        }

        private void RefreshTransientPanels()
        {
            if (HasGameplayToolkit)
            {
                RefreshGameplayToolkitTransientPanels();
            }

            if (_waveBannerPanel == null || !_waveBannerPanel.activeSelf || _waveBannerHideAt < 0f)
            {
                return;
            }

            if (Time.unscaledTime >= _waveBannerHideAt)
            {
                HideWaveBanner();
            }
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

        private Text CreateText(Transform parent, string name, Vector2 anchoredPosition, string content)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(220f, 35f);

            var text = textObject.AddComponent<Text>();
            text.font = _font;
            text.text = content;
            text.color = new Color(0.96f, 0.98f, 1f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 19;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
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
            image.color = new Color(0.15f, 0.20f, 0.28f, 0.98f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.selectedColor = new Color(0.23f, 0.30f, 0.40f, 1f);
            colors.highlightedColor = colors.selectedColor;
            colors.pressedColor = new Color(0.11f, 0.15f, 0.21f, 1f);
            colors.disabledColor = new Color(0.18f, 0.18f, 0.20f, 0.74f);
            button.colors = colors;

            var shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.26f);
            shadow.effectDistance = new Vector2(0f, -6f);
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.50f, 0.61f, 0.78f, 0.16f);
            outline.effectDistance = new Vector2(1f, -1f);

            var label = new GameObject("Label");
            label.transform.SetParent(buttonObject.transform, false);
            var labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var labelText = label.AddComponent<Text>();
            labelText.font = _font;
            labelText.text = "선택";
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.97f, 0.98f, 1f, 1f);
            labelText.fontSize = 17;
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            labelText.raycastTarget = false;

            return button;
        }

        private void ApplyPanelChrome(GameObject panel, string name)
        {
            if (panel == null)
            {
                return;
            }

            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -8f);

            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.36f, 0.47f, 0.62f, 0.16f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private Text CreateMultilineText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, string content)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = textObject.AddComponent<Text>();
            text.font = _font;
            text.text = content;
            text.color = new Color(0.94f, 0.96f, 1f, 1f);
            text.alignment = TextAnchor.UpperLeft;
            text.fontSize = 15;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void OpenPauseSettings()
        {
            if (HasPauseSettingsToolkit)
            {
                AudioService.Instance.PlayUi(AudioCueId.UiConfirm);
                UpdatePauseMainToolkitVisibility(false);
                _pauseMainContentRoot?.SetActive(false);
                _pauseSettingsPanel?.SetActive(false);
                SyncPauseSettingsControls();
                UpdatePauseSettingsToolkitVisibility(true);
                return;
            }

            if (_pauseSettingsPanel == null)
            {
                return;
            }

            AudioService.Instance.PlayUi(AudioCueId.UiConfirm);
            _pauseMainContentRoot?.SetActive(false);
            _pauseSettingsPanel.SetActive(true);
            SyncPauseSettingsControls();

            var eventSystem = EventSystem.current;
            if (eventSystem != null && _pauseFullscreenToggle != null)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_pauseFullscreenToggle.gameObject);
            }
        }

        private void ClosePauseSettings(bool playCue = true)
        {
            if (!HasPauseSettingsToolkit && _pauseSettingsPanel == null)
            {
                return;
            }

            if (playCue)
            {
                AudioService.Instance.PlayUi(AudioCueId.UiBack);
            }

            UpdatePauseSettingsToolkitVisibility(false);
            if (HasPauseMainToolkit)
            {
                UpdatePauseMainToolkitVisibility(true);
            }

            _pauseSettingsPanel?.SetActive(false);
            _pauseMainContentRoot?.SetActive(true);

            var eventSystem = EventSystem.current;
            if (eventSystem != null && _pauseSettingsButton != null && _pauseSettingsButton.gameObject.activeInHierarchy)
            {
                eventSystem.SetSelectedGameObject(null);
                eventSystem.SetSelectedGameObject(_pauseSettingsButton.gameObject);
            }
        }

        private void SyncPauseSettingsControls()
        {
            _suppressPauseSettingsCallbacks = true;
            var audio = AudioService.Instance;
            _pauseFullscreenToggle?.SetIsOnWithoutNotify(PlayerPrefs.GetInt(FullscreenPreferenceKey, 0) != 0);
            _pauseMasterVolumeSlider?.SetValueWithoutNotify(audio.MasterVolume);
            _pauseBgmVolumeSlider?.SetValueWithoutNotify(audio.BgmVolume);
            _pauseSfxVolumeSlider?.SetValueWithoutNotify(audio.SfxVolume);
            _suppressPauseSettingsCallbacks = false;

            UpdateSliderValueLabel(_pauseMasterVolumeValueText, audio.MasterVolume);
            UpdateSliderValueLabel(_pauseBgmVolumeValueText, audio.BgmVolume);
            UpdateSliderValueLabel(_pauseSfxVolumeValueText, audio.SfxVolume);
            SyncPauseSettingsToolkitControls();
        }

        private void OnPauseFullscreenToggleChanged(bool useFullscreen)
        {
            if (_suppressPauseSettingsCallbacks)
            {
                return;
            }

            ApplyDisplayMode(useFullscreen);
            SyncPauseSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnPauseMasterVolumeChanged(float value)
        {
            UpdateSliderValueLabel(_pauseMasterVolumeValueText, value);
            if (_suppressPauseSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetMasterVolume(value);
            SyncPauseSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnPauseBgmVolumeChanged(float value)
        {
            UpdateSliderValueLabel(_pauseBgmVolumeValueText, value);
            if (_suppressPauseSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetBgmVolume(value);
            SyncPauseSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private void OnPauseSfxVolumeChanged(float value)
        {
            UpdateSliderValueLabel(_pauseSfxVolumeValueText, value);
            if (_suppressPauseSettingsCallbacks)
            {
                return;
            }

            AudioService.Instance.SetSfxVolume(value);
            SyncPauseSettingsControls();
            AudioService.Instance.PlayUi(AudioCueId.UiAdjust);
        }

        private static void UpdateSliderValueLabel(Text label, float value)
        {
            if (label == null)
            {
                return;
            }

            label.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }

        private static void ApplyDisplayMode(bool useFullscreen)
        {
            PlayerPrefs.SetInt(FullscreenPreferenceKey, useFullscreen ? 1 : 0);
            PlayerPrefs.Save();

            if (useFullscreen)
            {
                var resolution = Screen.currentResolution;
                Screen.SetResolution(Mathf.Max(1, resolution.width), Mathf.Max(1, resolution.height), FullScreenMode.FullScreenWindow);
                return;
            }

            var width = Mathf.Max(1280, Mathf.RoundToInt(Screen.currentResolution.width * 0.8f));
            var height = Mathf.Max(720, Mathf.RoundToInt(Screen.currentResolution.height * 0.8f));
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
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
            rootRect.sizeDelta = new Vector2(400f, 26f);

            var labelText = CreateText(root.transform, "Label", new Vector2(-142f, 0f), label);
            labelText.fontSize = 15;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.rectTransform.sizeDelta = new Vector2(92f, 24f);

            var sliderObject = new GameObject("Slider");
            sliderObject.transform.SetParent(root.transform, false);
            var sliderRect = sliderObject.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(0f, 0.5f);
            sliderRect.pivot = new Vector2(0f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(70f, 0f);
            sliderRect.sizeDelta = new Vector2(220f, 18f);

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
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(0f, 5f);
            fillAreaRect.offsetMax = new Vector2(0f, -5f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.96f, 0.74f, 0.18f, 0.96f);

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
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

            valueText = CreateText(root.transform, "Value", new Vector2(166f, 0f), "0%");
            valueText.fontSize = 14;
            valueText.fontStyle = FontStyle.Bold;
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.rectTransform.sizeDelta = new Vector2(56f, 24f);
            valueText.color = new Color(0.78f, 0.84f, 0.92f, 1f);
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
            backgroundRect.sizeDelta = new Vector2(26f, 26f);
            var backgroundImage = backgroundObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

            var checkmarkObject = new GameObject("Checkmark");
            checkmarkObject.transform.SetParent(backgroundObject.transform, false);
            var checkmarkRect = checkmarkObject.AddComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
            checkmarkRect.sizeDelta = new Vector2(14f, 14f);
            var checkmarkImage = checkmarkObject.AddComponent<Image>();
            checkmarkImage.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            var labelText = CreateText(toggleObject.transform, "Label", new Vector2(80f, 0f), label);
            labelText.fontSize = 17;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.rectTransform.sizeDelta = new Vector2(220f, 26f);

            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.onValueChanged.AddListener(onValueChanged);
            return toggle;
        }
    }
}
