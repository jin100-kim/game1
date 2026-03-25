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

        private UIDocument _toolkitDocument;
        private PanelSettings _toolkitPanelSettings;
        private VisualElement _toolkitMainShell;
        private Label _toolkitStatusLabel;
        private Label _toolkitProfileLabel;
        private Label _toolkitRecentRunLabel;
        private Button _toolkitSinglePlayButton;
        private Button _toolkitMultiPlayButton;
        private Button _toolkitMetaButton;
        private Button _toolkitOptionsButton;
        private Button _toolkitQuitButton;

        private bool HasToolkitMainMenu => _toolkitMainShell != null;

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
            _toolkitDocument.panelSettings = _toolkitPanelSettings;

            var root = _toolkitDocument.rootVisualElement;
            root.Clear();
            layout.CloneTree(root);
            if (styles != null && !root.styleSheets.Contains(styles))
            {
                root.styleSheets.Add(styles);
            }

            _toolkitMainShell = root.Q<VisualElement>("screen");
            _toolkitStatusLabel = root.Q<Label>("status-line");
            _toolkitProfileLabel = root.Q<Label>("profile-summary");
            _toolkitRecentRunLabel = root.Q<Label>("recent-run-summary");
            _toolkitSinglePlayButton = root.Q<Button>("single-play-button");
            _toolkitMultiPlayButton = root.Q<Button>("multi-play-button");
            _toolkitMetaButton = root.Q<Button>("meta-button");
            _toolkitOptionsButton = root.Q<Button>("options-button");
            _toolkitQuitButton = root.Q<Button>("quit-button");

            if (_toolkitSinglePlayButton != null) _toolkitSinglePlayButton.clicked += OnSinglePlayClicked;
            if (_toolkitMultiPlayButton != null) _toolkitMultiPlayButton.clicked += OnMultiPlayClicked;
            if (_toolkitMetaButton != null) _toolkitMetaButton.clicked += OnMetaClicked;
            if (_toolkitOptionsButton != null) _toolkitOptionsButton.clicked += OnOptionsClicked;
            if (_toolkitQuitButton != null) _toolkitQuitButton.clicked += OnQuitClicked;

            UpdateToolkitOverviewSummary();
            UpdateToolkitStatus(string.Empty);
            UpdateToolkitInteractivity(!MultiplayerSessionController.EnsureInstance().IsBusy);
            UpdateToolkitMainMenuVisibility(false);
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
            _toolkitMetaButton?.SetEnabled(interactable);
            _toolkitOptionsButton?.SetEnabled(interactable);
            _toolkitQuitButton?.SetEnabled(interactable);
        }

        private void FocusToolkitPrimaryButton()
        {
            if (_toolkitMainShell == null || _toolkitMainShell.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            _toolkitSinglePlayButton?.schedule.Execute(() => _toolkitSinglePlayButton.Focus()).ExecuteLater(0);
        }

        private void OnDestroy()
        {
            if (_toolkitDocument != null)
            {
                if (_toolkitSinglePlayButton != null) _toolkitSinglePlayButton.clicked -= OnSinglePlayClicked;
                if (_toolkitMultiPlayButton != null) _toolkitMultiPlayButton.clicked -= OnMultiPlayClicked;
                if (_toolkitMetaButton != null) _toolkitMetaButton.clicked -= OnMetaClicked;
                if (_toolkitOptionsButton != null) _toolkitOptionsButton.clicked -= OnOptionsClicked;
                if (_toolkitQuitButton != null) _toolkitQuitButton.clicked -= OnQuitClicked;
            }

            if (_toolkitPanelSettings != null)
            {
                Destroy(_toolkitPanelSettings);
                _toolkitPanelSettings = null;
            }
        }
    }
}
