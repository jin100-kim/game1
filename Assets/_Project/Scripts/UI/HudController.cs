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
        private readonly Font _font;

        private Canvas _canvas;
        private Text _healthText;
        private Text _xpText;
        private Text _timeText;
        private Button _autoPlayButton;
        private Text _autoPlayButtonText;
        private Image _autoPlayButtonImage;

        private GameObject _levelUpPanel;
        private Text _levelUpTitle;
        private Button[] _levelButtons;
        private Text[] _levelButtonTexts;

        private GameObject _resultPanel;
        private Text _resultText;
        private Button _restartButton;
        private int _lastCurrentHp = int.MinValue;
        private int _lastMaxHp = int.MinValue;
        private int _lastLevel = int.MinValue;
        private int _lastCurrentXp = int.MinValue;
        private int _lastRequiredXp = int.MinValue;
        private int _lastRemainingSeconds = int.MinValue;

        public HudController()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public void Initialize()
        {
            EnsureEventSystem();
            BuildCanvas();
            BuildTopBar();
            BuildLevelUpPanel();
            BuildResultPanel();
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
                _timeText.text = $"TIME {remainingSecondsInt}";
                _lastRemainingSeconds = remainingSecondsInt;
            }
        }

        public void BindAutoPlayToggle(bool enabled, Action onToggle)
        {
            if (_autoPlayButton == null)
            {
                return;
            }

            _autoPlayButton.onClick.RemoveAllListeners();
            if (onToggle != null)
            {
                _autoPlayButton.onClick.AddListener(() => onToggle.Invoke());
            }

            SetAutoPlayState(enabled);
        }

        public void SetAutoPlayState(bool enabled)
        {
            if (_autoPlayButtonText != null)
            {
                _autoPlayButtonText.text = enabled ? "AUTO ON" : "AUTO OFF";
            }

            if (_autoPlayButtonImage != null)
            {
                _autoPlayButtonImage.color = enabled
                    ? new Color(0.2f, 0.55f, 0.27f, 1f)
                    : new Color(0.2f, 0.2f, 0.2f, 1f);
            }
        }

        public void ShowLevelUpOptions(LevelUpOption[] options, Action<int> onSelected)
        {
            if (_levelUpPanel == null || options == null || options.Length == 0)
            {
                return;
            }

            _levelUpPanel.SetActive(true);
            _levelUpTitle.text = "Level Up - Choose One";

            for (var i = 0; i < _levelButtons.Length; i++)
            {
                var button = _levelButtons[i];
                var text = _levelButtonTexts[i];
                if (i >= options.Length)
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
            if (eventSystem != null)
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

        public void ShowResult(bool cleared, Action onRestart)
        {
            if (_resultPanel == null)
            {
                return;
            }

            _resultPanel.SetActive(true);
            _resultText.text = cleared ? "Run Complete" : "Game Over";
            _restartButton.onClick.RemoveAllListeners();
            _restartButton.onClick.AddListener(() => onRestart?.Invoke());
        }

        public void HideResult()
        {
            if (_resultPanel != null)
            {
                _resultPanel.SetActive(false);
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
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private void BuildTopBar()
        {
            var top = CreatePanel(_canvas.transform, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(760f, 60f), new Color(0f, 0f, 0f, 0.35f));
            _healthText = CreateText(top.transform, "HealthText", new Vector2(-250f, 0f), "HP");
            _xpText = CreateText(top.transform, "XPText", new Vector2(-40f, 0f), "XP");
            _timeText = CreateText(top.transform, "TimeText", new Vector2(160f, 0f), "TIME");

            _autoPlayButton = CreateButton(top.transform, "AutoPlayButton", new Vector2(300f, 0f), new Vector2(130f, 42f));
            _autoPlayButtonText = _autoPlayButton.GetComponentInChildren<Text>();
            _autoPlayButtonImage = _autoPlayButton.GetComponent<Image>();
            _autoPlayButtonText.text = "AUTO OFF";
        }

        private void BuildLevelUpPanel()
        {
            _levelUpPanel = CreatePanel(_canvas.transform, "LevelUpPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 300f), new Color(0f, 0f, 0f, 0.85f));
            _levelUpPanel.SetActive(false);

            _levelUpTitle = CreateText(_levelUpPanel.transform, "Title", new Vector2(0f, 120f), "Level Up");
            _levelButtons = new Button[3];
            _levelButtonTexts = new Text[3];

            for (var i = 0; i < 3; i++)
            {
                var y = 50f - (i * 75f);
                var button = CreateButton(_levelUpPanel.transform, $"OptionButton{i}", new Vector2(0f, y), new Vector2(360f, 55f));
                _levelButtons[i] = button;
                _levelButtonTexts[i] = button.GetComponentInChildren<Text>();
            }
        }

        private void BuildResultPanel()
        {
            _resultPanel = CreatePanel(_canvas.transform, "ResultPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 200f), new Color(0f, 0f, 0f, 0.85f));
            _resultPanel.SetActive(false);
            _resultText = CreateText(_resultPanel.transform, "ResultText", new Vector2(0f, 40f), "Game Over");
            _restartButton = CreateButton(_resultPanel.transform, "RestartButton", new Vector2(0f, -45f), new Vector2(220f, 55f));
            _restartButton.GetComponentInChildren<Text>().text = "Restart";
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
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 20;
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
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            button.colors = colors;

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
            labelText.color = Color.white;
            labelText.fontSize = 18;
            labelText.raycastTarget = false;

            return button;
        }
    }
}
