using System;
using UnityEngine;
using UnityEngine.UIElements;
using UIToolkitButton = UnityEngine.UIElements.Button;
using UIToolkitLabel = UnityEngine.UIElements.Label;
using UIToolkitVisualElement = UnityEngine.UIElements.VisualElement;

namespace EJR.Game.Multiplayer
{
    public sealed partial class MultiplayerGameController
    {
        private const string ToolkitOverlayLayoutResourcePath = "UI/Common/MultiplayerOverlayLayout";
        private const string ToolkitOverlayStylesResourcePath = "UI/Common/MultiplayerOverlayStyles";
        private const string ToolkitOverlayThemeResourcePath = "UI/Common/UnityDefaultRuntimeTheme";
        private const string ToolkitOverlayPanelSettingsResourcePath = "UI/Common/RuntimeMenuPanelSettings";

        private UIDocument _toolkitDocument;
        private PanelSettings _toolkitPanelSettings;
        private UIToolkitVisualElement _toolkitScreen;
        private UIToolkitVisualElement _toolkitStatusCard;
        private UIToolkitLabel _toolkitStatusText;
        private UIToolkitVisualElement _toolkitLobbyPanel;
        private UIToolkitLabel _toolkitLobbyHeaderText;
        private UIToolkitLabel _toolkitPlayerListText;
        private UIToolkitLabel _toolkitStartHintText;
        private UIToolkitButton _toolkitCharacterButton;
        private UIToolkitButton _toolkitMapButton;
        private UIToolkitButton _toolkitDifficultyButton;
        private UIToolkitButton _toolkitReadyButton;
        private UIToolkitButton _toolkitStartButton;
        private UIToolkitVisualElement _toolkitCharacterSelectPanel;
        private UIToolkitLabel _toolkitCharacterSelectTitleText;
        private UIToolkitLabel _toolkitCharacterSelectDetailText;
        private UIToolkitButton _toolkitCharacterSelectActionButton;
        private UIToolkitButton _toolkitCharacterSelectCloseButton;
        private readonly UIToolkitButton[] _toolkitCharacterSelectButtons = new UIToolkitButton[6];
        private UIToolkitVisualElement _toolkitResultPanel;
        private UIToolkitLabel _toolkitResultText;

        private bool HasToolkitOverlay => _toolkitScreen != null;

        private bool SupportsToolkitOverlay()
        {
            return Resources.Load<VisualTreeAsset>(ToolkitOverlayLayoutResourcePath) != null;
        }

        private void BuildToolkitOverlay()
        {
            if (_toolkitDocument != null)
            {
                return;
            }

            var layout = Resources.Load<VisualTreeAsset>(ToolkitOverlayLayoutResourcePath);
            var panelTemplate = Resources.Load<PanelSettings>(ToolkitOverlayPanelSettingsResourcePath);
            if (layout == null || panelTemplate == null)
            {
                return;
            }

            var styles = Resources.Load<StyleSheet>(ToolkitOverlayStylesResourcePath);
            var documentObject = new GameObject("MultiplayerToolkitOverlay");
            documentObject.transform.SetParent(transform, false);

            _toolkitDocument = documentObject.AddComponent<UIDocument>();
            _toolkitPanelSettings = UnityEngine.Object.Instantiate(panelTemplate);
            _toolkitPanelSettings.name = "RuntimeMultiplayerOverlayPanelSettings";
            _toolkitPanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _toolkitPanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _toolkitPanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _toolkitPanelSettings.match = 0.5f;
            _toolkitPanelSettings.sortingOrder = 130;
            _toolkitPanelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ToolkitOverlayThemeResourcePath);
            _toolkitDocument.panelSettings = _toolkitPanelSettings;

            var root = _toolkitDocument.rootVisualElement;
            root.Clear();
            layout.CloneTree(root);
            if (styles != null && !root.styleSheets.Contains(styles))
            {
                root.styleSheets.Add(styles);
            }

            _toolkitScreen = root.Q<UIToolkitVisualElement>("multiplayer-screen");
            _toolkitStatusCard = root.Q<UIToolkitVisualElement>("multiplayer-status-card");
            _toolkitStatusText = root.Q<UIToolkitLabel>("multiplayer-status-text");
            _toolkitLobbyPanel = root.Q<UIToolkitVisualElement>("multiplayer-lobby-panel");
            _toolkitLobbyHeaderText = root.Q<UIToolkitLabel>("multiplayer-lobby-header");
            _toolkitPlayerListText = root.Q<UIToolkitLabel>("multiplayer-player-list");
            _toolkitStartHintText = root.Q<UIToolkitLabel>("multiplayer-start-hint");
            _toolkitCharacterButton = root.Q<UIToolkitButton>("multiplayer-character-button");
            _toolkitMapButton = root.Q<UIToolkitButton>("multiplayer-map-button");
            _toolkitDifficultyButton = root.Q<UIToolkitButton>("multiplayer-difficulty-button");
            _toolkitReadyButton = root.Q<UIToolkitButton>("multiplayer-ready-button");
            _toolkitStartButton = root.Q<UIToolkitButton>("multiplayer-start-button");
            _toolkitCharacterSelectPanel = root.Q<UIToolkitVisualElement>("multiplayer-character-select-panel");
            _toolkitCharacterSelectTitleText = root.Q<UIToolkitLabel>("multiplayer-character-select-title");
            _toolkitCharacterSelectDetailText = root.Q<UIToolkitLabel>("multiplayer-character-detail");
            _toolkitCharacterSelectActionButton = root.Q<UIToolkitButton>("multiplayer-character-select-action");
            _toolkitCharacterSelectCloseButton = root.Q<UIToolkitButton>("multiplayer-character-select-close");
            _toolkitResultPanel = root.Q<UIToolkitVisualElement>("multiplayer-result-panel");
            _toolkitResultText = root.Q<UIToolkitLabel>("multiplayer-result-text");

            for (var i = 0; i < _toolkitCharacterSelectButtons.Length; i++)
            {
                _toolkitCharacterSelectButtons[i] = root.Q<UIToolkitButton>($"multiplayer-character-option-{i}");
                if (_toolkitCharacterSelectButtons[i] == null)
                {
                    continue;
                }

                var captured = i;
                _toolkitCharacterSelectButtons[i].clicked += () => InspectLobbyCharacter(captured);
            }

            if (_toolkitCharacterButton != null) _toolkitCharacterButton.clicked += HandleCharacterClicked;
            if (_toolkitMapButton != null) _toolkitMapButton.clicked += HandleStarterClicked;
            if (_toolkitDifficultyButton != null) _toolkitDifficultyButton.clicked += HandleDifficultyClicked;
            if (_toolkitReadyButton != null) _toolkitReadyButton.clicked += HandleReadyClicked;
            if (_toolkitStartButton != null) _toolkitStartButton.clicked += HandleStartClicked;
            if (_toolkitCharacterSelectActionButton != null) _toolkitCharacterSelectActionButton.clicked += ConfirmLobbyCharacterSelection;
            if (_toolkitCharacterSelectCloseButton != null) _toolkitCharacterSelectCloseButton.clicked += CloseLobbyCharacterSelection;

            SetToolkitStatusVisible(false);
            SetToolkitLobbyVisible(false);
            SetToolkitResultVisible(false);
            SetToolkitCharacterSelectVisible(false);
        }

        private void SetToolkitStatusVisible(bool visible)
        {
            if (_toolkitStatusCard != null)
            {
                _toolkitStatusCard.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void SetToolkitLobbyVisible(bool visible)
        {
            if (_toolkitLobbyPanel != null)
            {
                _toolkitLobbyPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void SetToolkitCharacterSelectVisible(bool visible)
        {
            if (_toolkitCharacterSelectPanel != null)
            {
                _toolkitCharacterSelectPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void SetToolkitResultVisible(bool visible)
        {
            if (_toolkitResultPanel != null)
            {
                _toolkitResultPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
