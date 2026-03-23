using System;
using EJR.Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace EJR.Game.UI
{
    public sealed class HudController
    {
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

        private readonly Font _font;

        private Canvas _canvas;
        private Text _healthText;
        private Text _xpText;
        private Text _timeText;
        private Text _modeHintText;
        private GameObject _buildPanel;
        private Text _weaponBuildText;
        private Text _statBuildText;
        private GameObject _bossBarPanel;
        private Text _bossNameText;
        private Image _bossBarFill;
        private RectTransform _bossBarFillRect;
        private Text _bossBarValueText;
        private float _bossBarFillMaxWidth;

        private GameObject _levelUpPanel;
        private Text _levelUpTitle;
        private Button[] _levelButtons;
        private Text[] _levelButtonTexts;

        private GameObject _resultPanel;
        private Text _resultText;
        private Button _restartButton;
        private GameObject _pausePanel;
        private Button _pauseResumeButton;
        private Button _pauseQuitButton;
        private Button _debugAccessButton;
        private GameObject _debugToolsPanel;
        private Button _debugGrantLevelButton;
        private Button _debugAdvanceTimeButton;
        private Button _debugRerollButton;
        private Button _debugSkipBossButton;
        private Button _debugAutoPlayButton;
        private Text _debugGrantLevelLabel;
        private Text _debugAdvanceTimeLabel;
        private Text _debugRerollLabel;
        private Text _debugSkipBossLabel;
        private Text _debugAutoPlayLabel;
        private Func<string, bool> _debugUnlockValidator;
        private Action _debugGrantLevelAction;
        private Action _debugAdvanceTimeAction;
        private Action _debugRerollAction;
        private Action _debugSkipBossAction;
        private Action _debugAutoPlayAction;
        private bool _debugAccessVisible;
        private bool _debugAutoPlayEnabled;
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

        public HudController()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public void Initialize()
        {
            EnsureEventSystem();
            BuildCanvas();
            BuildTopBar();
            BuildBuildPanel();
            BuildBossBar();
            BuildLevelUpPanel();
            BuildResultPanel();
            BuildPausePanel();
            BuildDebugPanels();
        }

        public void SetCanvasVisible(bool visible)
        {
            if (_canvas == null)
            {
                return;
            }

            _canvas.gameObject.SetActive(visible);
            if (!visible)
            {
                HideLevelUpOptions();
                HideBossBar();
                HidePauseMenu();
                HideResult();
                HideDebugPanels();
            }
        }

        public void SetTopBar(float currentHealth, float maxHealth, int level, int currentXp, int requiredXp, float remainingSeconds)
        {
            if (_healthText == null)
            {
                return;
            }

            var currentHpInt = Mathf.CeilToInt(currentHealth);
            var maxHpInt = Mathf.CeilToInt(maxHealth);
            var remainingSecondsInt = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));

            if (currentHpInt != _lastCurrentHp || maxHpInt != _lastMaxHp)
            {
                _healthText.text = $"HP {currentHpInt}/{maxHpInt}";
                _lastCurrentHp = currentHpInt;
                _lastMaxHp = maxHpInt;
            }

            if (level != _lastLevel || currentXp != _lastCurrentXp || requiredXp != _lastRequiredXp)
            {
                _xpText.text = $"LV {level}  XP {currentXp}/{requiredXp}";
                _lastLevel = level;
                _lastCurrentXp = currentXp;
                _lastRequiredXp = requiredXp;
            }

            if (remainingSecondsInt != _lastRemainingSeconds)
            {
                _timeText.text = $"TIME {FormatTime(remainingSecondsInt)}";
                _lastRemainingSeconds = remainingSecondsInt;
            }
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
            if (_weaponBuildText == null || _statBuildText == null)
            {
                return;
            }

            weaponsSummary ??= "Weapons";
            statsSummary ??= "Stats";

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

        public void SetModeHint(string modeHint)
        {
            if (_modeHintText == null)
            {
                return;
            }

            var hasHint = !string.IsNullOrWhiteSpace(modeHint);
            _modeHintText.gameObject.SetActive(true);
            _modeHintText.text = hasHint ? modeHint : "STANDARD";
            _modeHintText.color = hasHint ? new Color(0.95f, 0.74f, 0.18f, 1f) : new Color(0.76f, 0.82f, 0.90f, 1f);
        }

        public void ConfigureDebugTools(
            Action onGrantLevel,
            Action onAdvanceTime,
            Action onReroll,
            Action onSkipBoss,
            Action onToggleAutoPlay)
        {
            _debugGrantLevelAction = onGrantLevel;
            _debugAdvanceTimeAction = onAdvanceTime;
            _debugRerollAction = onReroll;
            _debugSkipBossAction = onSkipBoss;
            _debugAutoPlayAction = onToggleAutoPlay;
            RefreshDebugToolButtons();
        }

        public void SetDebugAccessVisible(bool visible)
        {
            _debugAccessVisible = visible;
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
            if (_debugAutoPlayLabel != null)
            {
                _debugAutoPlayLabel.text = enabled ? "Auto Play: ON" : "Auto Play: OFF";
            }
        }

        public void HideDebugPanels()
        {
            if (_debugToolsPanel != null)
            {
                _debugToolsPanel.SetActive(false);
            }
        }

        public void SetBossBar(float currentHealth, float maxHealth, string bossLabel = "BOSS")
        {
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
            var label = string.IsNullOrWhiteSpace(bossLabel) ? "BOSS" : bossLabel;

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
            if (_bossBarPanel != null && _bossBarPanel.activeSelf)
            {
                _bossBarPanel.SetActive(false);
            }

            _lastBossCurrentHp = int.MinValue;
            _lastBossMaxHp = int.MinValue;
            _lastBossLabel = string.Empty;
        }

        public void ShowLevelUpOptions(LevelUpOption[] options, Action<int> onSelected, string title = "Level Up - Choose One")
        {
            if (_levelUpPanel == null || options == null || options.Length == 0)
            {
                return;
            }

            _levelUpPanel.SetActive(true);
            _levelUpTitle.text = string.IsNullOrWhiteSpace(title) ? "Level Up - Choose One" : title;
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
        }

        public void HideLevelUpOptions()
        {
            if (_levelUpPanel != null)
            {
                _levelUpPanel.SetActive(false);
            }
        }

        public void ShowResult(bool cleared, Action onAction, string actionLabel = "Restart")
        {
            if (_resultPanel == null)
            {
                return;
            }

            _resultPanel.SetActive(true);
            _resultText.text = cleared ? "Run Complete" : "Game Over";
            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() => onAction?.Invoke());
            _restartButton.GetComponentInChildren<Text>().text = string.IsNullOrEmpty(actionLabel) ? "Restart" : actionLabel;
        }

        public void HideResult()
        {
            if (_resultPanel != null)
            {
                _resultPanel.SetActive(false);
            }
        }

        public bool IsPauseMenuVisible => _pausePanel != null && _pausePanel.activeSelf;

        public void ShowPauseMenu(Action onResume, Action onQuit)
        {
            if (_pausePanel == null)
            {
                return;
            }

            _pausePanel.SetActive(true);

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
            if (_pausePanel != null)
            {
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

            _healthText = CreateText(healthCard.transform, "HealthText", Vector2.zero, "HP");
            _healthText.alignment = TextAnchor.MiddleLeft;
            _healthText.rectTransform.sizeDelta = new Vector2(224f, 44f);
            _healthText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _healthText.fontSize = 22;

            _xpText = CreateText(xpCard.transform, "XPText", Vector2.zero, "XP");
            _xpText.alignment = TextAnchor.MiddleLeft;
            _xpText.rectTransform.sizeDelta = new Vector2(248f, 44f);
            _xpText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _xpText.fontSize = 20;

            _timeText = CreateText(timeCard.transform, "TimeText", Vector2.zero, "TIME");
            _timeText.alignment = TextAnchor.MiddleLeft;
            _timeText.rectTransform.sizeDelta = new Vector2(204f, 44f);
            _timeText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            _timeText.fontSize = 20;

            _modeHintText = CreateText(hintCard.transform, "ModeHintText", Vector2.zero, "STANDARD");
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

            var buildTitle = CreateText(_buildPanel.transform, "BuildTitle", new Vector2(0f, 182f), "BUILD");
            buildTitle.fontSize = 22;
            buildTitle.fontStyle = FontStyle.Bold;
            buildTitle.color = new Color(0.95f, 0.74f, 0.18f, 1f);
            buildTitle.rectTransform.sizeDelta = new Vector2(260f, 28f);

            var weaponsCard = CreatePanel(_buildPanel.transform, "WeaponsCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 54f), new Vector2(336f, 156f), new Color(0.07f, 0.10f, 0.14f, 0.92f));
            var weaponsTitle = CreateText(weaponsCard.transform, "WeaponsTitle", new Vector2(0f, 56f), "WEAPONS");
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
                "Weapons");
            _weaponBuildText.fontSize = 14;

            var statsCard = CreatePanel(_buildPanel.transform, "StatsCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(336f, 184f), new Color(0.07f, 0.10f, 0.14f, 0.92f));
            var statsTitle = CreateText(statsCard.transform, "StatsTitle", new Vector2(0f, 68f), "STATS");
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
                "Stats");
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

            _bossNameText = CreateText(_bossBarPanel.transform, "BossName", new Vector2(-284f, 0f), "BOSS");
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

            _levelUpTitle = CreateText(_levelUpPanel.transform, "Title", Vector2.zero, "Level Up");
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
            _resultPanel = CreatePanel(_canvas.transform, "ResultPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 276f), new Color(0.03f, 0.05f, 0.09f, 0.96f));
            _resultPanel.SetActive(false);
            _resultText = CreateText(_resultPanel.transform, "ResultText", new Vector2(0f, 62f), "Game Over");
            _resultText.fontSize = 32;
            _resultText.fontStyle = FontStyle.Bold;
            _restartButton = CreateButton(_resultPanel.transform, "RestartButton", new Vector2(0f, -58f), new Vector2(264f, 56f));
            _restartButton.GetComponentInChildren<Text>().text = "Restart";
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
                new Vector2(480f, 294f),
                new Color(0.03f, 0.05f, 0.09f, 0.96f));
            _pausePanel.SetActive(false);

            var title = CreateText(_pausePanel.transform, "PauseTitle", new Vector2(0f, 68f), "일시정지");
            title.text = "Paused";
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.95f, 0.74f, 0.18f, 1f);

            var subhead = CreateText(_pausePanel.transform, "PauseSubhead", new Vector2(0f, 42f), "Resume the run or return to the lobby.");
            subhead.fontSize = 15;
            subhead.color = new Color(0.78f, 0.84f, 0.92f, 1f);
            subhead.rectTransform.sizeDelta = new Vector2(320f, 30f);

            _pauseResumeButton = CreateButton(_pausePanel.transform, "ResumeButton", new Vector2(0f, -12f), new Vector2(252f, 56f));
            _pauseResumeButton.GetComponentInChildren<Text>().text = "Resume";
            _pauseResumeButton.GetComponentInChildren<Text>().text = "계속하기";

            _pauseQuitButton = CreateButton(_pausePanel.transform, "QuitButton", new Vector2(0f, -86f), new Vector2(272f, 56f));
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
            _pauseQuitButton.GetComponentInChildren<Text>().text = "Return to Lobby";
            _pauseQuitButton.GetComponentInChildren<Text>().text = "로비로";
            _pauseResumeButton.GetComponentInChildren<Text>().text = "Resume";
            _pauseQuitButton.GetComponentInChildren<Text>().text = "Return to Lobby";
        }

        private void BuildDebugPanels()
        {
            _debugAccessButton = CreateButton(_canvas.transform, "DebugAccessButton", Vector2.zero, new Vector2(72f, 38f));
            var accessRect = _debugAccessButton.GetComponent<RectTransform>();
            accessRect.anchorMin = new Vector2(0f, 0f);
            accessRect.anchorMax = new Vector2(0f, 0f);
            accessRect.pivot = new Vector2(0f, 0f);
            accessRect.anchoredPosition = new Vector2(18f, 18f);
            _debugAccessButton.GetComponentInChildren<Text>().text = "DEV";
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

            var toolsTitle = CreateText(_debugToolsPanel.transform, "DebugToolsTitle", new Vector2(0f, 118f), "DEBUG TOOLS");
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

            var nextVisible = _debugToolsPanel != null && !_debugToolsPanel.activeSelf;
            HideDebugPanels();
            if (_debugToolsPanel != null)
            {
                _debugToolsPanel.SetActive(nextVisible);
            }
        }

        private void RefreshDebugToolButtons()
        {
            ConfigureDebugButton(_debugGrantLevelButton, _debugGrantLevelLabel, "Grant Level", _debugGrantLevelAction);
            ConfigureDebugButton(_debugAdvanceTimeButton, _debugAdvanceTimeLabel, "Advance Time", _debugAdvanceTimeAction);
            ConfigureDebugButton(_debugRerollButton, _debugRerollLabel, "Reroll Choice", _debugRerollAction);
            ConfigureDebugButton(_debugSkipBossButton, _debugSkipBossLabel, "Skip To Boss", _debugSkipBossAction);
            ConfigureDebugButton(_debugAutoPlayButton, _debugAutoPlayLabel, _debugAutoPlayEnabled ? "Auto Play: ON" : "Auto Play: OFF", _debugAutoPlayAction);
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
            outline.effectColor = new Color(0.95f, 0.74f, 0.18f, 0.16f);
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
            labelText.text = "Option";
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.97f, 0.98f, 1f, 1f);
            labelText.fontSize = 17;
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            labelText.raycastTarget = false;

            return button;
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
    }
}
