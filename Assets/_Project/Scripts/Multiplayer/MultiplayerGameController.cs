using System;
using System.Collections.Generic;
using System.Text;
using EJR.Game.Core;
using EJR.Game.Gameplay;
using EJR.Game.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed partial class MultiplayerGameController : MonoBehaviour
    {
        [SerializeField] private Rect arenaBounds = new Rect(-12f, -7f, 24f, 14f);
        [SerializeField] private bool enableDebugAutoPlay = true;
        [SerializeField] private bool startWithAutoPlayEnabled;
        [SerializeField, Min(0.05f)] private float autoPlayChoiceDelay = 0.2f;

        private Font _font;
        private float _nextRefreshAt;
        private float _nextAutoPlayChoiceAt;

        private Canvas _canvas;
        private GameObject _statusPanel;
        private GameObject _lobbyPanel;
        private GameObject _runPanel;
        private GameObject _choicePanel;
        private GameObject _resultPanel;
        private Text _statusText;
        private Text _lobbyHeaderText;
        private Text _playerListText;
        private Text _startHintText;
        private Text _runTopText;
        private Text _buildText;
        private Text _bossText;
        private Text _stateText;
        private Text _choiceTitleText;
        private Text _resultText;
        private Button _characterButton;
        private Button _starterButton;
        private Button _difficultyButton;
        private Button _readyButton;
        private Button _startButton;
        private Text _characterButtonText;
        private Text _starterButtonText;
        private Text _difficultyButtonText;
        private Text _readyButtonText;
        private Text _startButtonText;
        private readonly Button[] _choiceButtons = new Button[3];
        private readonly Text[] _choiceButtonTexts = new Text[3];
        private GameObject _characterSelectPanel;
        private Text _characterSelectTitleText;
        private Text _characterSelectDetailText;
        private Button _characterSelectActionButton;
        private Text _characterSelectActionText;
        private Button _characterSelectCloseButton;
        private readonly Button[] _characterSelectButtons = new Button[6];
        private readonly Text[] _characterSelectButtonTexts = new Text[6];
        private int _inspectedLobbyCharacterId = -1;
        private HudController _gameplayHud;
        private string _lastChoiceSignature = string.Empty;
        private AutoPlayAgent _autoPlayAgent;
        private bool _autoPlayEnabled;
        private PlayerMover _boundAutoPlayMover;
        private string _debugRevealBuffer = string.Empty;
        private string _lastArenaPresentationSignature = string.Empty;
        private int _lastWaveBannerSequence = -1;
        private readonly List<Vector3> _rewardChestWorldPositions = new();

        private const string DebugRevealCode = "admin";

        private void Awake()
        {
            _font = RuntimeFontProvider.GetDefaultFont();
            Application.runInBackground = true;
            _autoPlayAgent = new AutoPlayAgent();
            EnsureCamera();
            EnsureEventSystem();
            EnsureArenaVisuals();
            EnsureOverlay();
            _gameplayHud = new HudController();
            _gameplayHud.Initialize();
            _gameplayHud.ConfigureDebugTools(
                null,
                null,
                null,
                null,
                () =>
                {
                    SetAutoPlayEnabled(!_autoPlayEnabled);
                    RefreshUi();
                });
            _gameplayHud.SetCanvasVisible(false);
            _gameplayHud.SetDebugAccessVisible(false);
            SetAutoPlayEnabled(startWithAutoPlayEnabled);
            RefreshUi();
        }

        private void Update()
        {
            CaptureDebugRevealInput();
            EnsureLocalAutoPlayBinding();
            EnsureLocalCameraFollow();

            var localPlayer = MultiplayerPlayerCombatant.FindOwnedLocalPlayer();

            if (IsLeaveKeyPressed())
            {
                MultiplayerSessionController.EnsureInstance().LeaveSession();
                return;
            }

            if (IsBuildDrawerToggleKeyPressed())
            {
                _gameplayHud?.ToggleBuildDrawer();
            }

            TryHandleAutoPlayChoice(localPlayer);
            HandleChoiceShortcutInput();

            if (Time.unscaledTime >= _nextRefreshAt)
            {
                _nextRefreshAt = Time.unscaledTime + 0.1f;
                RefreshUi();
            }
        }

        private void CaptureDebugRevealInput()
        {
            if (_gameplayHud == null)
            {
                return;
            }

            var typed = Input.inputString;
            if (string.IsNullOrEmpty(typed))
            {
                return;
            }

            for (var i = 0; i < typed.Length; i++)
            {
                var character = typed[i];
                if (character == '\b')
                {
                    if (_debugRevealBuffer.Length > 0)
                    {
                        _debugRevealBuffer = _debugRevealBuffer.Substring(0, _debugRevealBuffer.Length - 1);
                    }

                    continue;
                }

                if (!char.IsLetter(character))
                {
                    if (!char.IsWhiteSpace(character))
                    {
                        _debugRevealBuffer = string.Empty;
                    }

                    continue;
                }

                _debugRevealBuffer += char.ToLowerInvariant(character);
                if (_debugRevealBuffer.Length > DebugRevealCode.Length)
                {
                    _debugRevealBuffer = _debugRevealBuffer.Substring(_debugRevealBuffer.Length - DebugRevealCode.Length);
                }

                if (!string.Equals(_debugRevealBuffer, DebugRevealCode, StringComparison.Ordinal))
                {
                    continue;
                }

                _gameplayHud.SetDebugAccessVisible(true);
                _debugRevealBuffer = string.Empty;
                break;
            }
        }

        private void EnsureLocalCameraFollow()
        {
            var localActor = MultiplayerPlayerActor.FindOwnedLocalPlayer();
            if (localActor == null)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            var follow = mainCamera.GetComponent<CameraFollow2D>();
            if (follow == null || follow.Target != localActor.transform)
            {
                localActor.RefreshOwnerCameraBinding();
            }
        }

        private void OnDestroy()
        {
            if (_boundAutoPlayMover != null)
            {
                _boundAutoPlayMover.SetMoveInputReader(null);
                _boundAutoPlayMover.SetExternalVelocityReader(null);
                _boundAutoPlayMover = null;
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
            mainCamera.orthographicSize = 5.8f;
            mainCamera.backgroundColor = SharedRunCatalog.GetMap(SharedRunCatalog.DefaultMapId).CameraBackgroundColor;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private void EnsureArenaVisuals()
        {
            ApplyArenaPresentation(MultiplayerCoopController.Instance);
        }

        private void ApplyArenaPresentation(MultiplayerCoopController coop)
        {
            var mapDefinition = coop != null ? coop.SelectedMapDefinition : SharedRunCatalog.GetMap(SharedRunCatalog.DefaultMapId);
            var bounds = coop != null ? coop.ArenaBounds : mapDefinition.ArenaBounds;
            var signature = $"{mapDefinition.Id}:{bounds.xMin:0.##}:{bounds.yMin:0.##}:{bounds.width:0.##}:{bounds.height:0.##}";
            if (string.Equals(signature, _lastArenaPresentationSignature, StringComparison.Ordinal))
            {
                return;
            }

            arenaBounds = bounds;
            ArenaVisualPresenter.Apply(bounds, mapDefinition.CameraBackgroundColor, mapDefinition.BoundaryColor, Camera.main);
            var actors = FindObjectsByType<MultiplayerPlayerActor>(FindObjectsSortMode.None);
            for (var i = 0; i < actors.Length; i++)
            {
                actors[i]?.ApplyArenaBounds(bounds);
            }

            _lastArenaPresentationSignature = signature;
        }

        private void EnsureOverlay()
        {
            if (SupportsToolkitOverlay())
            {
                BuildToolkitOverlay();
                return;
            }

            if (_canvas != null)
            {
                return;
            }

            var canvasObject = new GameObject("MultiplayerHUD");
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            _statusPanel = CreatePanel(canvasObject.transform, "StatusPanel", new Vector2(18f, -18f), new Vector2(460f, 112f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.44f));
            _statusText = CreateText(_statusPanel.transform, "StatusText", Vector2.zero, Vector2.zero, Vector2.one, 18, TextAnchor.UpperLeft);
            _statusText.rectTransform.offsetMin = new Vector2(14f, 10f);
            _statusText.rectTransform.offsetMax = new Vector2(-14f, -10f);

            BuildLobbyPanel(canvasObject.transform);
            BuildRunPanel(canvasObject.transform);
            BuildChoicePanel(canvasObject.transform);
            BuildResultPanel(canvasObject.transform);
        }

        private void BuildLobbyPanel(Transform parent)
        {
            _lobbyPanel = CreatePanel(parent, "LobbyPanel", new Vector2(18f, -146f), new Vector2(820f, 620f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.44f));
            _lobbyHeaderText = CreateText(_lobbyPanel.transform, "LobbyHeader", Vector2.zero, new Vector2(16f, -16f), new Vector2(1f, 1f), 22, TextAnchor.UpperLeft);
            _lobbyHeaderText.rectTransform.sizeDelta = new Vector2(-32f, 56f);

            _playerListText = CreateText(_lobbyPanel.transform, "PlayerList", Vector2.zero, new Vector2(16f, -88f), new Vector2(1f, 1f), 18, TextAnchor.UpperLeft);
            _playerListText.rectTransform.sizeDelta = new Vector2(330f, 436f);

            _characterButton = CreateActionButton(_lobbyPanel.transform, "CharacterButton", new Vector2(396f, 90f), new Vector2(392f, 92f), out _characterButtonText, HandleCharacterClicked);
            _starterButton = CreateActionButton(_lobbyPanel.transform, "MapButton", new Vector2(396f, 198f), new Vector2(392f, 72f), out _starterButtonText, HandleStarterClicked);
            _difficultyButton = CreateActionButton(_lobbyPanel.transform, "DifficultyButton", new Vector2(396f, 286f), new Vector2(392f, 72f), out _difficultyButtonText, HandleDifficultyClicked);
            _readyButton = CreateActionButton(_lobbyPanel.transform, "ReadyButton", new Vector2(396f, 380f), new Vector2(188f, 56f), out _readyButtonText, HandleReadyClicked);
            _startButton = CreateActionButton(_lobbyPanel.transform, "StartButton", new Vector2(600f, 380f), new Vector2(188f, 56f), out _startButtonText, HandleStartClicked);

            _startHintText = CreateText(_lobbyPanel.transform, "StartHint", Vector2.zero, new Vector2(396f, -462f), new Vector2(1f, 1f), 16, TextAnchor.UpperLeft);
            _startHintText.rectTransform.sizeDelta = new Vector2(392f, 126f);

            _characterSelectPanel = CreatePanel(_lobbyPanel.transform, "CharacterSelectPanel", new Vector2(20f, -74f), new Vector2(780f, 510f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.03f, 0.05f, 0.09f, 0.96f));
            _characterSelectPanel.SetActive(false);

            _characterSelectTitleText = CreateText(_characterSelectPanel.transform, "CharacterSelectTitle", Vector2.zero, new Vector2(18f, -16f), new Vector2(1f, 1f), 22, TextAnchor.UpperLeft);
            _characterSelectTitleText.rectTransform.sizeDelta = new Vector2(-36f, 36f);

            for (var i = 0; i < _characterSelectButtons.Length; i++)
            {
                var index = i;
                var button = CreateActionButton(
                    _characterSelectPanel.transform,
                    $"CharacterSelectButton{i}",
                    new Vector2(18f, 68f + (i * 68f)),
                    new Vector2(286f, 56f),
                    out _characterSelectButtonTexts[i],
                    () => InspectLobbyCharacter(index));
                _characterSelectButtons[i] = button;
            }

            _characterSelectDetailText = CreateText(_characterSelectPanel.transform, "CharacterSelectDetail", Vector2.zero, new Vector2(334f, -74f), new Vector2(1f, 1f), 17, TextAnchor.UpperLeft);
            _characterSelectDetailText.rectTransform.sizeDelta = new Vector2(420f, 272f);

            _characterSelectActionButton = CreateActionButton(_characterSelectPanel.transform, "CharacterSelectAction", new Vector2(334f, 368f), new Vector2(206f, 56f), out _characterSelectActionText, ConfirmLobbyCharacterSelection);
            _characterSelectCloseButton = CreateActionButton(_characterSelectPanel.transform, "CharacterSelectClose", new Vector2(552f, 368f), new Vector2(206f, 56f), out var closeText, CloseLobbyCharacterSelection);
            closeText.text = "닫기";
        }

        private void BuildRunPanel(Transform parent)
        {
            _runPanel = CreatePanel(parent, "RunPanel", new Vector2(-18f, -18f), new Vector2(520f, 330f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Color(0f, 0f, 0f, 0.44f));
            _runTopText = CreateText(_runPanel.transform, "RunTop", Vector2.zero, new Vector2(16f, -16f), new Vector2(1f, 1f), 20, TextAnchor.UpperLeft);
            _runTopText.rectTransform.sizeDelta = new Vector2(-32f, 96f);

            _bossText = CreateText(_runPanel.transform, "BossText", Vector2.zero, new Vector2(16f, -104f), new Vector2(1f, 1f), 18, TextAnchor.UpperLeft);
            _bossText.rectTransform.sizeDelta = new Vector2(-32f, 54f);

            _stateText = CreateText(_runPanel.transform, "StateText", Vector2.zero, new Vector2(16f, -150f), new Vector2(1f, 1f), 18, TextAnchor.UpperLeft);
            _stateText.rectTransform.sizeDelta = new Vector2(-32f, 54f);

            _buildText = CreateText(_runPanel.transform, "BuildText", Vector2.zero, new Vector2(16f, -204f), new Vector2(1f, 1f), 16, TextAnchor.UpperLeft);
            _buildText.rectTransform.sizeDelta = new Vector2(-32f, 210f);
        }

        private void BuildChoicePanel(Transform parent)
        {
            _choicePanel = CreatePanel(parent, "ChoicePanel", Vector2.zero, new Vector2(700f, 360f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0f, 0f, 0f, 0.68f));
            _choiceTitleText = CreateText(_choicePanel.transform, "ChoiceTitle", Vector2.zero, new Vector2(24f, -24f), new Vector2(1f, 1f), 24, TextAnchor.UpperCenter);
            _choiceTitleText.rectTransform.sizeDelta = new Vector2(-48f, 44f);

            for (var i = 0; i < _choiceButtons.Length; i++)
            {
                var y = 94f + (i * 82f);
                var index = i;
                _choiceButtons[i] = CreateActionButton(_choicePanel.transform, $"ChoiceButton{i + 1}", new Vector2(32f, y), new Vector2(636f, 62f), out _choiceButtonTexts[i], () => HandleChoiceClicked(index));
            }
        }

        private void BuildResultPanel(Transform parent)
        {
            _resultPanel = CreatePanel(parent, "ResultPanel", Vector2.zero, new Vector2(720f, 520f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0f, 0f, 0f, 0.78f));
            _resultText = CreateText(_resultPanel.transform, "ResultText", Vector2.zero, Vector2.zero, Vector2.one, 20, TextAnchor.UpperLeft);
            _resultText.rectTransform.offsetMin = new Vector2(28f, 24f);
            _resultText.rectTransform.offsetMax = new Vector2(-28f, -24f);
        }

        private void RefreshUi()
        {
            var session = MultiplayerSessionController.EnsureInstance();
            var manager = NetworkManager.Singleton;
            var coop = MultiplayerCoopController.Instance;
            var localPlayer = MultiplayerPlayerCombatant.FindOwnedLocalPlayer();
            var allPlayers = FindObjectsByType<MultiplayerPlayerCombatant>(FindObjectsSortMode.None);

            ApplyArenaPresentation(coop);
            RefreshStatusText(session, manager, coop, localPlayer, allPlayers);
            RefreshLobbyUi(manager, coop, localPlayer, allPlayers);
            RefreshRunUi(coop, localPlayer, allPlayers);
            RefreshChoiceUi(localPlayer);
            RefreshResultUi(coop);
        }

        private void RefreshStatusText(
            MultiplayerSessionController session,
            NetworkManager manager,
            MultiplayerCoopController coop,
            MultiplayerPlayerCombatant localPlayer,
            MultiplayerPlayerCombatant[] allPlayers)
        {
            var showStatus = coop == null || coop.Phase == MultiplayerRunPhase.Lobby;
            SetToolkitStatusVisible(showStatus);
            if (_statusPanel != null)
            {
                _statusPanel.SetActive(showStatus);
            }

            if (!showStatus)
            {
                return;
            }

            var mode = manager != null && manager.IsListening ? (manager.IsHost ? "호스트" : "클라이언트") : "오프라인";
            var sessionCode = string.IsNullOrWhiteSpace(session.SessionCode) ? "----" : session.SessionCode;
            var phase = coop == null
                ? "준비 중"
                : coop.Phase switch
                {
                    MultiplayerRunPhase.Lobby => "대기실",
                    MultiplayerRunPhase.Running => "진행 중",
                    MultiplayerRunPhase.LevelChoice => "선택 중",
                    MultiplayerRunPhase.Result => "결과",
                    _ => "준비 중",
                };

            var aliveCount = 0;
            for (var i = 0; i < allPlayers.Length; i++)
            {
                if (allPlayers[i] != null && allPlayers[i].IsTargetable)
                {
                    aliveCount++;
                }
            }

            var statusText =
                $"세션 {mode}\n" +
                $"코드 {sessionCode}\n" +
                $"단계 {phase}\n" +
                $"플레이어 {allPlayers.Length}/{session.SessionMaxPlayers}\n" +
                $"생존 {aliveCount}\n" +
                $"내 정보 {(localPlayer != null ? localPlayer.DisplayName : "접속 중")}\n" +
                $"{(_autoPlayEnabled ? "자동 전투\n" : string.Empty)}" +
                $"{session.CurrentStatus}\n" +
                "ESC : 나가기";

            if (_statusText != null)
            {
                _statusText.text = statusText;
            }

            if (_toolkitStatusText != null)
            {
                _toolkitStatusText.text = statusText;
            }
        }

        private void RefreshLobbyUi(
            NetworkManager manager,
            MultiplayerCoopController coop,
            MultiplayerPlayerCombatant localPlayer,
            MultiplayerPlayerCombatant[] allPlayers)
        {
            if (_lobbyPanel == null)
            {
                return;
            }

            var showLobby = coop == null || coop.Phase == MultiplayerRunPhase.Lobby;
            _lobbyPanel.SetActive(showLobby);
            if (!showLobby)
            {
                return;
            }

            if (RefreshLobbyUiMinimal(manager, coop, localPlayer, allPlayers))
            {
                return;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < allPlayers.Length; i++)
            {
                var player = allPlayers[i];
                if (player == null)
                {
                    continue;
                }

                builder.Append(player.DisplayName)
                    .Append(" | ")
                    .Append("시작 ")
                    .Append(MultiplayerCatalog.GetStarterWeaponDisplayName(player.SelectedStarterWeaponIndex));

                if (player.IsDowned)
                {
                    builder.Append(" | 다운 ").Append(Mathf.RoundToInt(player.ReviveProgress * 100f)).Append('%');
                }
                else
                {
                    builder.Append(" | ").Append(player.IsReady ? "준비" : "미준비");
                }

                builder.Append('\n');
            }

            _lobbyHeaderText.text = "대기실\n캐릭터를 고르고 준비하세요. 시작 무기는 캐릭터에 고정됩니다.";
            _playerListText.text = builder.Length > 0 ? builder.ToString() : "플레이어를 기다리는 중...";

            var canInteract = localPlayer != null && coop != null && coop.Phase == MultiplayerRunPhase.Lobby;
            _starterButtonText.text = coop != null
                ? $"맵\n{coop.SelectedMapDefinition.DisplayName}"
                : "맵\n-";
            if (_difficultyButtonText != null)
            {
                _difficultyButtonText.text = coop != null
                    ? $"난이도\n{coop.SelectedDifficultyDefinition.DisplayName}"
                    : "난이도\n-";
            }

            var lobbyHost = manager != null && manager.IsHost;
            _characterButton.interactable = canInteract;
            _starterButton.interactable = canInteract && lobbyHost;
            if (_difficultyButton != null)
            {
                _difficultyButton.interactable = canInteract && lobbyHost;
            }

            _readyButton.interactable = canInteract;

            _characterButtonText.text = localPlayer != null
                ? $"캐릭터\n{MultiplayerCatalog.GetCharacter(localPlayer.SelectedCharacterId).DisplayName}"
                : "캐릭터\n-";
            _starterButtonText.text = localPlayer != null
                ? $"고정 시작\n{MultiplayerCatalog.GetStarterWeaponDisplayName(localPlayer.SelectedStarterWeaponIndex)}"
                : "고정 시작\n-";
            _readyButtonText.text = localPlayer != null && localPlayer.IsReady ? "준비 취소" : "준비";

            var isHost = manager != null && manager.IsHost;
            _starterButtonText.text = coop != null
                ? $"맵\n{coop.SelectedMapDefinition.DisplayName}"
                : "맵\n-";
            if (_difficultyButtonText != null)
            {
                _difficultyButtonText.text = coop != null
                    ? $"난이도\n{coop.SelectedDifficultyDefinition.DisplayName}"
                    : "난이도\n-";
            }

            _starterButtonText.text = coop != null
                ? $"맵\n{coop.SelectedMapDefinition.DisplayName}"
                : "맵\n-";
            if (_difficultyButtonText != null)
            {
                _difficultyButtonText.text = coop != null
                    ? $"난이도\n{coop.SelectedDifficultyDefinition.DisplayName}"
                    : "난이도\n-";
            }

            _startButton.gameObject.SetActive(isHost);
            _startButton.interactable = isHost && coop != null && string.IsNullOrWhiteSpace(coop.GetStartBlockReason());
            _startButtonText.text = "게임 시작";
            _startHintText.text = isHost
                ? (coop != null && !string.IsNullOrWhiteSpace(coop.GetStartBlockReason())
                    ? coop.GetStartBlockReason()
                    : "모든 플레이어 준비 완료. 시작할 수 있습니다.")
                : "모든 플레이어가 준비되면 호스트가 시작합니다.";
        }

        private void RefreshRunUi(MultiplayerCoopController coop, MultiplayerPlayerCombatant localPlayer, MultiplayerPlayerCombatant[] allPlayers)
        {
            if (_runPanel != null)
            {
                _runPanel.SetActive(false);
            }

            if (_gameplayHud == null)
            {
                return;
            }

            var showRun = coop != null && coop.Phase != MultiplayerRunPhase.Lobby;
            _gameplayHud.SetCanvasVisible(showRun);
            if (!showRun)
            {
                _gameplayHud.SetModeHint(string.Empty);
                _gameplayHud.HideWaveStatus();
                _gameplayHud.HideWaveBanner();
                _gameplayHud.HideBossBar();
                _gameplayHud.HideWaveTargetDirectionIndicator();
                _gameplayHud.HideRewardDirectionIndicator();
                _lastWaveBannerSequence = -1;
                return;
            }

            if (localPlayer == null)
            {
                _gameplayHud.HideWaveStatus();
                _gameplayHud.HideWaveBanner();
                _gameplayHud.HideBossBar();
                _gameplayHud.HideWaveTargetDirectionIndicator();
                _gameplayHud.HideRewardDirectionIndicator();
                return;
            }

            _gameplayHud.SetTopBar(
                localPlayer.CurrentHealth,
                Mathf.Max(1f, localPlayer.MaxHealth),
                coop.TeamLevel,
                coop.TeamExperience,
                coop.TeamRequiredExperience,
                coop.RemainingSeconds);
            _gameplayHud.SetModeHint($"{coop.SelectedMapDefinition.DisplayName} | {coop.SelectedDifficultyDefinition.DisplayName}");
            _gameplayHud.SetBuildInfo(localPlayer.WeaponSummary, localPlayer.StatSummary);
            if (coop.HasActiveWave)
            {
                _gameplayHud.SetWaveStatus(coop.ActiveWaveIndex, coop.ActiveWaveRemainingCount);
            }
            else
            {
                _gameplayHud.HideWaveStatus();
            }

            if (coop.WaveBannerSequence > 0 && coop.WaveBannerSequence != _lastWaveBannerSequence)
            {
                _lastWaveBannerSequence = coop.WaveBannerSequence;
                switch (coop.WaveBannerKind)
                {
                    case 1:
                        _gameplayHud.ShowWaveBanner($"웨이브 {Mathf.Max(1, coop.WaveBannerWaveIndex)} 시작\n보상: 증강 선택");
                        break;

                    case 2:
                        _gameplayHud.ShowWaveBanner("웨이브 정리 완료");
                        break;
                }
            }

            RefreshTrackedTargetHud();
        }

        private void RefreshTrackedTargetHud()
        {
            if (_gameplayHud == null)
            {
                return;
            }

            MultiplayerSharedEnemyActor latestHudTarget = null;
            MultiplayerSharedEnemyActor bossTarget = null;
            MultiplayerSharedEnemyActor waveTarget = null;
            var actors = MultiplayerSharedEnemyActor.SpawnedActors;
            for (var i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor == null || !actor.IsSpawned)
                {
                    continue;
                }

                if (actor.IsHudBossTarget && bossTarget == null)
                {
                    bossTarget = actor;
                }

                if (actor.IsHudWaveTarget && waveTarget == null)
                {
                    waveTarget = actor;
                }

                if (!actor.IsHudBossTarget && !actor.IsHudWaveTarget)
                {
                    continue;
                }

                if (latestHudTarget == null || actor.HudSpawnSequence >= latestHudTarget.HudSpawnSequence)
                {
                    latestHudTarget = actor;
                }
            }

            if (bossTarget != null)
            {
                _gameplayHud.UpdateBossDirectionIndicator(Camera.main, bossTarget.transform.position);
            }
            else
            {
                _gameplayHud.HideBossDirectionIndicator();
            }

            if (waveTarget != null)
            {
                _gameplayHud.UpdateWaveTargetDirectionIndicator(Camera.main, waveTarget.transform.position);
            }
            else
            {
                _gameplayHud.HideWaveTargetDirectionIndicator();
            }

            _rewardChestWorldPositions.Clear();
            var pickups = MultiplayerSharedExperienceOrbActor.SpawnedActors;
            for (var i = 0; i < pickups.Count; i++)
            {
                var pickup = pickups[i];
                if (pickup == null || !pickup.IsSpawned || !pickup.IsWaveRewardChest)
                {
                    continue;
                }

                _rewardChestWorldPositions.Add(pickup.transform.position);
            }

            _gameplayHud.UpdateRewardDirectionIndicators(Camera.main, _rewardChestWorldPositions);

            if (latestHudTarget != null)
            {
                _gameplayHud.SetBossBar(latestHudTarget.CurrentHealthValue, latestHudTarget.MaxHealthValue, latestHudTarget.HudLabel);
            }
            else
            {
                _gameplayHud.HideBossBar();
            }
        }

        private bool RefreshLobbyUiMinimal(
            NetworkManager manager,
            MultiplayerCoopController coop,
            MultiplayerPlayerCombatant localPlayer,
            MultiplayerPlayerCombatant[] allPlayers)
        {
            if (_lobbyPanel == null && !HasToolkitOverlay)
            {
                return false;
            }

            var showLobby = coop == null || coop.Phase == MultiplayerRunPhase.Lobby;
            SetToolkitLobbyVisible(showLobby);
            if (!showLobby)
            {
                _lobbyPanel?.SetActive(false);
                SetToolkitCharacterSelectVisible(false);
                return true;
            }

            var sessionCode = MultiplayerSessionController.EnsureInstance().SessionCode;
            var selectionSummary = coop != null
                ? $"맵 {coop.SelectedMapDefinition.DisplayName} | 난이도 {coop.SelectedDifficultyDefinition.DisplayName}"
                : "맵 - | 난이도 -";
            var headerText = string.IsNullOrWhiteSpace(sessionCode)
                ? "멀티플레이 대기실"
                : $"멀티플레이 대기실  |  코드 {sessionCode}";

            headerText = string.IsNullOrWhiteSpace(sessionCode)
                ? $"멀티플레이 대기실\n{selectionSummary}"
                : $"멀티플레이 대기실  |  코드 {sessionCode}\n{selectionSummary}";

            var playerList = BuildLobbyPlayerList(allPlayers);
            var playerListText = playerList.Length > 0 ? playerList.ToString() : "플레이어를 기다리는 중...";

            var canInteract = localPlayer != null && coop != null && coop.Phase == MultiplayerRunPhase.Lobby;
            var isHost = manager != null && manager.IsHost;
            var characterButtonText = localPlayer != null
                ? $"내 캐릭터\n{MultiplayerCatalog.GetCharacter(localPlayer.SelectedCharacterId).DisplayName}"
                : "내 캐릭터\n-";
            var readyButtonText = localPlayer != null && localPlayer.IsReady ? "준비 취소" : "준비";
            var startHintText = isHost
                ? (coop != null && !string.IsNullOrWhiteSpace(coop.GetStartBlockReason())
                    ? coop.GetStartBlockReason()
                    : "모든 플레이어가 준비되면 시작할 수 있습니다.")
                : "호스트가 준비를 확인한 뒤 시작합니다.";

            startHintText = isHost
                ? (coop != null && !string.IsNullOrWhiteSpace(coop.GetStartBlockReason())
                    ? $"{selectionSummary}\n{coop.GetStartBlockReason()}"
                    : $"{selectionSummary}\n모든 플레이어가 준비되면 시작할 수 있습니다.")
                : $"{selectionSummary}\n호스트가 준비를 확인한 뒤 시작합니다.";

            var mapButtonText = coop != null
                ? $"맵\n{coop.SelectedMapDefinition.DisplayName}"
                : "맵\n-";
            var difficultyButtonText = coop != null
                ? $"난이도\n{coop.SelectedDifficultyDefinition.DisplayName}"
                : "난이도\n-";

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(true);
                _lobbyHeaderText.text = headerText;
                _playerListText.text = playerListText;
                _characterButton.interactable = canInteract;
                _starterButton.interactable = canInteract && isHost;
                _readyButton.interactable = canInteract;
                if (_difficultyButton != null)
                {
                    _difficultyButton.interactable = canInteract && isHost;
                }

                _characterButtonText.text = characterButtonText;
                _starterButtonText.text = mapButtonText;
                _readyButtonText.text = readyButtonText;
                _startButton.gameObject.SetActive(isHost);
                _startButton.interactable = isHost && coop != null && string.IsNullOrWhiteSpace(coop.GetStartBlockReason());
                _startButtonText.text = "시작";
                _startHintText.text = startHintText;
                if (_difficultyButtonText != null)
                {
                    _difficultyButtonText.text = difficultyButtonText;
                }
            }

            if (HasToolkitOverlay)
            {
                _toolkitLobbyHeaderText.text = headerText;
                _toolkitPlayerListText.text = playerListText;
                _toolkitCharacterButton.text = characterButtonText;
                _toolkitMapButton.text = mapButtonText;
                _toolkitDifficultyButton.text = difficultyButtonText;
                _toolkitReadyButton.text = readyButtonText;
                _toolkitStartButton.text = "시작";
                _toolkitStartButton.style.display = isHost ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
                _toolkitCharacterButton.SetEnabled(canInteract);
                _toolkitMapButton.SetEnabled(canInteract && isHost);
                _toolkitDifficultyButton.SetEnabled(canInteract && isHost);
                _toolkitReadyButton.SetEnabled(canInteract);
                _toolkitStartButton.SetEnabled(isHost && coop != null && string.IsNullOrWhiteSpace(coop.GetStartBlockReason()));
                _toolkitStartHintText.text = startHintText;
            }

            var showSelector = canInteract && _inspectedLobbyCharacterId >= 0;
            SetToolkitCharacterSelectVisible(showSelector);
            if (_characterSelectPanel != null)
            {
                _characterSelectPanel.SetActive(showSelector);
                if (showSelector)
                {
                    RefreshLobbyCharacterSelection(localPlayer);
                }
            }

            return true;
        }

        private StringBuilder BuildLobbyPlayerList(MultiplayerPlayerCombatant[] allPlayers)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < allPlayers.Length; i++)
            {
                var player = allPlayers[i];
                if (player == null)
                {
                    continue;
                }

                builder.Append(player.DisplayName)
                    .Append(" | 시작 ")
                    .Append(MultiplayerCatalog.GetStarterWeaponDisplayName(player.SelectedStarterWeaponIndex))
                    .Append(" | ")
                    .Append(player.IsDowned
                        ? $"다운 {Mathf.RoundToInt(player.ReviveProgress * 100f)}%"
                        : (player.IsReady ? "준비" : "미준비"))
                    .Append('\n');
            }

            return builder;
        }

        private void RefreshLobbyCharacterSelection(MultiplayerPlayerCombatant localPlayer)
        {
            if ((_characterSelectPanel == null || _characterSelectDetailText == null || _characterSelectActionButton == null || _characterSelectActionText == null) &&
                !HasToolkitOverlay)
            {
                return;
            }

            var inspectedId = _inspectedLobbyCharacterId >= 0
                ? SharedGameCatalog.NormalizeCharacterId(_inspectedLobbyCharacterId)
                : (localPlayer != null ? localPlayer.SelectedCharacterId : MetaProgressionService.GetSingleSelectedCharacterId());
            var inspectedCharacter = SharedGameCatalog.GetCharacter(inspectedId);
            var unlocked = MetaProgressionService.IsCharacterUnlocked(inspectedId);
            var selected = localPlayer != null && inspectedId == localPlayer.SelectedCharacterId;

            var detailText =
                $"{inspectedCharacter.DisplayName}\n\n" +
                $"시작 무기\n{SharedGameCatalog.GetWeaponDisplayName(inspectedCharacter.StarterWeaponId)}\n\n" +
                $"기본 보너스\n{BuildMetaBonusSummary(inspectedCharacter.BaseBonuses)}\n\n" +
                $"고유 패시브\n{inspectedCharacter.PassiveDescription}";

            if (_characterSelectTitleText != null)
            {
                _characterSelectTitleText.text = "캐릭터 선택";
            }

            if (_characterSelectDetailText != null)
            {
                _characterSelectDetailText.text = detailText;
            }

            if (_toolkitCharacterSelectTitleText != null)
            {
                _toolkitCharacterSelectTitleText.text = "캐릭터 선택";
            }

            if (_toolkitCharacterSelectDetailText != null)
            {
                _toolkitCharacterSelectDetailText.text = detailText;
            }

            var actionText = "이 캐릭터 선택";
            var canSelect = true;
            if (!unlocked)
            {
                canSelect = false;
                actionText = $"해금 필요 ({inspectedCharacter.UnlockCost} 코인)";
            }
            else if (selected)
            {
                canSelect = false;
                actionText = "현재 선택됨";
            }

            if (_characterSelectActionButton != null)
            {
                _characterSelectActionButton.interactable = canSelect;
                _characterSelectActionText.text = actionText;
            }

            if (_toolkitCharacterSelectActionButton != null)
            {
                _toolkitCharacterSelectActionButton.SetEnabled(canSelect);
                _toolkitCharacterSelectActionButton.text = actionText;
            }

            for (var i = 0; i < _characterSelectButtons.Length; i++)
            {
                var button = _characterSelectButtons[i];
                var label = _characterSelectButtonTexts[i];
                if (button == null || label == null || i >= SharedGameCatalog.CharacterDefinitions.Count)
                {
                    continue;
                }

                var definition = SharedGameCatalog.CharacterDefinitions[i];
                var available = MetaProgressionService.IsCharacterUnlocked(definition.Id);
                var optionText = available
                    ? $"{definition.DisplayName} | {SharedGameCatalog.GetWeaponDisplayName(definition.StarterWeaponId)}"
                    : $"{definition.DisplayName} | 잠김";
                label.text = optionText;

                if (button.targetGraphic is Image image)
                {
                    image.color = definition.Id == inspectedId
                        ? new Color(0.24f, 0.32f, 0.44f, 1f)
                        : new Color(0.14f, 0.18f, 0.28f, 0.95f);
                }

                if (_toolkitCharacterSelectButtons[i] != null)
                {
                    _toolkitCharacterSelectButtons[i].text = optionText;
                    _toolkitCharacterSelectButtons[i].style.backgroundColor = definition.Id == inspectedId
                        ? new UnityEngine.UIElements.StyleColor(new Color(0.24f, 0.32f, 0.44f, 1f))
                        : new UnityEngine.UIElements.StyleColor(new Color(0.14f, 0.18f, 0.28f, 0.95f));
                }
            }
        }

        private void RefreshChoiceUi(MultiplayerPlayerCombatant localPlayer)
        {
            if (_choicePanel != null)
            {
                _choicePanel.SetActive(false);
            }

            if (_gameplayHud == null)
            {
                return;
            }

            var hasChoice = localPlayer != null && localPlayer.HasLocalPendingChoice;
            if (!hasChoice)
            {
                _lastChoiceSignature = string.Empty;
                _gameplayHud.HideLevelUpOptions();
                return;
            }

            var optionCount = Mathf.Min(localPlayer.LocalPendingChoiceCount, 3);
            var options = new LevelUpOption[optionCount];
            var signatureBuilder = new StringBuilder(localPlayer.LocalPendingTitle);
            for (var i = 0; i < optionCount; i++)
            {
                var label = localPlayer.GetLocalPendingChoiceLabel(i);
                signatureBuilder.Append('|').Append(label);
                options[i] = LevelUpOption.CreateGlobalStatRoll(
                    StatUpgradeId.AttackPower,
                    OptionRarity.Common,
                    0f,
                    string.Empty,
                    string.Empty,
                    label);
            }

            var signature = signatureBuilder.ToString();
            if (string.Equals(signature, _lastChoiceSignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastChoiceSignature = signature;
            _gameplayHud.ShowLevelUpOptions(options, HandleChoiceClicked, localPlayer.LocalPendingTitle);
        }

        private void RefreshResultUi(MultiplayerCoopController coop)
        {
            var showResult = coop != null && coop.Phase == MultiplayerRunPhase.Result;
            SetToolkitResultVisible(showResult);
            _resultPanel?.SetActive(showResult);
            if (!showResult)
            {
                return;
            }

            if (MetaProgressionService.TryPeekPendingRunSummary(out var summary) && summary != null)
            {
                var resultText = $"{summary.BuildDisplayText()}\n\n타이틀로 돌아가는 중...";
                if (_resultText != null)
                {
                    _resultText.text = resultText;
                }

                if (_toolkitResultText != null)
                {
                    _toolkitResultText.text = resultText;
                }
                return;
            }

            var fallbackResultText = coop.ResultCleared ? "클리어\n타이틀로 돌아가는 중..." : "팀 전멸\n타이틀로 돌아가는 중...";
            if (_resultText != null)
            {
                _resultText.text = fallbackResultText;
            }

            if (_toolkitResultText != null)
            {
                _toolkitResultText.text = fallbackResultText;
            }
        }

        private void HandleCharacterClicked()
        {
            var localPlayer = MultiplayerPlayerCombatant.FindOwnedLocalPlayer();
            _inspectedLobbyCharacterId = localPlayer != null
                ? localPlayer.SelectedCharacterId
                : MetaProgressionService.GetSingleSelectedCharacterId();
            RefreshUi();
        }

        private void HandleStarterClicked()
        {
            var manager = NetworkManager.Singleton;
            var coop = MultiplayerCoopController.Instance;
            if (manager == null || !manager.IsHost || coop == null)
            {
                return;
            }

            for (var offset = 1; offset <= SharedRunCatalog.MapDefinitions.Count; offset++)
            {
                var index = (coop.SelectedMapIndex + offset) % SharedRunCatalog.MapDefinitions.Count;
                var mapDefinition = SharedRunCatalog.GetMapByIndex(index);
                if (!SharedRunCatalog.IsMapUnlocked(mapDefinition.Id))
                {
                    continue;
                }

                coop.RequestSelectMap(index);
                RefreshUi();
                return;
            }
        }

        private void HandleDifficultyClicked()
        {
            var manager = NetworkManager.Singleton;
            var coop = MultiplayerCoopController.Instance;
            if (manager == null || !manager.IsHost || coop == null)
            {
                return;
            }

            var nextIndex = (coop.SelectedDifficultyIndex + 1) % SharedRunCatalog.DifficultyDefinitions.Count;
            coop.RequestSelectDifficulty(nextIndex);
            RefreshUi();
        }

        private void HandleReadyClicked()
        {
            MultiplayerPlayerCombatant.FindOwnedLocalPlayer()?.RequestToggleReady();
            RefreshUi();
        }

        private void HandleStartClicked()
        {
            MultiplayerCoopController.Instance?.RequestStartGame();
            RefreshUi();
        }

        private void InspectLobbyCharacter(int characterId)
        {
            _inspectedLobbyCharacterId = SharedGameCatalog.NormalizeCharacterId(characterId);
            RefreshUi();
        }

        private void ConfirmLobbyCharacterSelection()
        {
            var localPlayer = MultiplayerPlayerCombatant.FindOwnedLocalPlayer();
            if (localPlayer == null)
            {
                return;
            }

            localPlayer.RequestCharacterSelection(_inspectedLobbyCharacterId);
            CloseLobbyCharacterSelection();
        }

        private void CloseLobbyCharacterSelection()
        {
            _inspectedLobbyCharacterId = -1;
            RefreshUi();
        }

        private void HandleChoiceClicked(int optionIndex)
        {
            MultiplayerPlayerCombatant.FindOwnedLocalPlayer()?.SubmitLevelChoice(optionIndex);
            RefreshUi();
        }

        private void HandleChoiceShortcutInput()
        {
            var localPlayer = MultiplayerPlayerCombatant.FindOwnedLocalPlayer();
            if (localPlayer == null || !localPlayer.HasLocalPendingChoice)
            {
                return;
            }

            for (var optionIndex = 0; optionIndex < Mathf.Min(3, localPlayer.LocalPendingChoiceCount); optionIndex++)
            {
                if (IsOptionKeyPressed(optionIndex))
                {
                    localPlayer.SubmitLevelChoice(optionIndex);
                    return;
                }
            }
        }

        private void SetAutoPlayEnabled(bool enabled)
        {
            _autoPlayEnabled = enabled && enableDebugAutoPlay;
            if (_boundAutoPlayMover != null)
            {
                _boundAutoPlayMover.SetMoveInputReader(_autoPlayEnabled ? ReadAutoPlayMoveInput : null);
            }

            _nextAutoPlayChoiceAt = Time.unscaledTime + Mathf.Max(0.05f, autoPlayChoiceDelay);
            _gameplayHud?.SetDebugAutoPlayState(_autoPlayEnabled);
        }

        private void EnsureLocalAutoPlayBinding()
        {
            var localPlayer = MultiplayerPlayerCombatant.FindOwnedLocalPlayer();
            var localMover = localPlayer != null ? localPlayer.GetComponent<PlayerMover>() : null;
            if (ReferenceEquals(localMover, _boundAutoPlayMover))
            {
                return;
            }

            if (_boundAutoPlayMover != null)
            {
                _boundAutoPlayMover.SetMoveInputReader(null);
                _boundAutoPlayMover.SetExternalVelocityReader(null);
            }

            _boundAutoPlayMover = localMover;
            if (_boundAutoPlayMover != null)
            {
                _boundAutoPlayMover.SetExternalVelocityReader(ReadLocalBossPullVelocity);
                if (_autoPlayEnabled)
                {
                    _boundAutoPlayMover.SetMoveInputReader(ReadAutoPlayMoveInput);
                }
            }
        }

        private Vector2 ReadLocalBossPullVelocity()
        {
            var coop = MultiplayerCoopController.Instance;
            var localPlayer = MultiplayerPlayerCombatant.FindOwnedLocalPlayer();
            if (coop == null || localPlayer == null || coop.Phase != MultiplayerRunPhase.Running)
            {
                return Vector2.zero;
            }

            if (!MultiplayerSharedEnemyActor.TryGetCurrentBossActor(out var bossActor))
            {
                return Vector2.zero;
            }

            if (!bossActor.TryGetBossPullState(out var center, out var radius, out var speed))
            {
                return Vector2.zero;
            }

            return ComputeBossPullVelocity(localPlayer.transform.position, center, radius, speed);
        }

        private static Vector2 ComputeBossPullVelocity(Vector3 playerPosition, Vector2 center, float radius, float speed)
        {
            if (radius <= 0.0001f || speed <= 0.0001f)
            {
                return Vector2.zero;
            }

            var toCenter = center - (Vector2)playerPosition;
            var distance = toCenter.magnitude;
            if (distance <= 0.0001f || distance > radius)
            {
                return Vector2.zero;
            }

            return toCenter / distance * speed;
        }

        private Vector2 ReadAutoPlayMoveInput()
        {
            if (!_autoPlayEnabled)
            {
                return Vector2.zero;
            }

            var coop = MultiplayerCoopController.Instance;
            var localPlayer = MultiplayerPlayerCombatant.FindOwnedLocalPlayer();
            if (coop == null
                || localPlayer == null
                || coop.Phase != MultiplayerRunPhase.Running
                || localPlayer.IsDowned)
            {
                return Vector2.zero;
            }

            var healthRatio = localPlayer.MaxHealth > 0f
                ? localPlayer.CurrentHealth / localPlayer.MaxHealth
                : 1f;

            return _autoPlayAgent != null
                ? _autoPlayAgent.EvaluateMove(localPlayer.transform.position, coop.ArenaBounds, healthRatio, coop.EnemyRegistry, ResolveNearestSharedOrbPosition)
                : Vector2.zero;
        }

        private Vector3? ResolveNearestSharedOrbPosition(Vector3 fromPosition)
        {
            var bestDistanceSq = 9f * 9f;
            Vector3? bestPosition = null;
            var orbs = FindObjectsByType<MultiplayerSharedExperienceOrbActor>(FindObjectsSortMode.None);
            for (var i = 0; i < orbs.Length; i++)
            {
                var orb = orbs[i];
                if (orb == null || !orb.IsSpawned)
                {
                    continue;
                }

                var distanceSq = (orb.transform.position - fromPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                bestPosition = orb.transform.position;
            }

            return bestPosition;
        }

        private void TryHandleAutoPlayChoice(MultiplayerPlayerCombatant localPlayer)
        {
            if (!_autoPlayEnabled || localPlayer == null || !localPlayer.HasLocalPendingChoice)
            {
                return;
            }

            if (Time.unscaledTime < _nextAutoPlayChoiceAt)
            {
                return;
            }

            _nextAutoPlayChoiceAt = Time.unscaledTime + Mathf.Max(0.05f, autoPlayChoiceDelay);
            localPlayer.SubmitLevelChoice(0);
        }

        private static string BuildMetaBonusSummary(MetaBonusValues bonuses)
        {
            if (Mathf.Approximately(bonuses.attackPowerPercent, 0f)
                && Mathf.Approximately(bonuses.attackSpeedPercent, 0f)
                && Mathf.Approximately(bonuses.maxHealthFlat, 0f)
                && Mathf.Approximately(bonuses.healthRegenPerSecond, 0f)
                && Mathf.Approximately(bonuses.moveSpeedPercent, 0f)
                && Mathf.Approximately(bonuses.attackRangePercent, 0f))
            {
                return "보정 없음";
            }

            var builder = new StringBuilder();
            AppendBonus(builder, bonuses.attackPowerPercent, "피해량");
            AppendBonus(builder, bonuses.attackSpeedPercent, "공속");
            AppendBonus(builder, bonuses.maxHealthFlat, "최대 체력", integer: true);
            AppendBonus(builder, bonuses.healthRegenPerSecond, "체력 재생", suffix: "/초");
            AppendBonus(builder, bonuses.moveSpeedPercent, "이동 속도");
            AppendBonus(builder, bonuses.attackRangePercent, "범위");
            return builder.ToString();
        }

        private static void AppendBonus(StringBuilder builder, float value, string label, bool integer = false, string suffix = "%")
        {
            if (Mathf.Approximately(value, 0f))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(label)
                .Append(' ')
                .Append(value > 0f ? "+" : string.Empty)
                .Append(integer ? Mathf.RoundToInt(value).ToString() : value.ToString("0.#"))
                .Append(suffix);
        }

        private GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = panel.AddComponent<Image>();
            image.color = color;
            return panel;
        }

        private Text CreateText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchoredPosition,
            Vector2 anchorMax,
            int fontSize,
            TextAnchor alignment)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPosition;

            var text = textObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button CreateActionButton(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            out Text label,
            UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(anchoredPosition.x, -anchoredPosition.y);
            rect.sizeDelta = size;

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.14f, 0.18f, 0.28f, 0.95f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            var text = CreateText(buttonObject.transform, "Label", Vector2.zero, Vector2.zero, Vector2.one, 18, TextAnchor.MiddleCenter);
            text.rectTransform.offsetMin = new Vector2(10f, 8f);
            text.rectTransform.offsetMax = new Vector2(-10f, -8f);
            label = text;
            return button;
        }

        private static bool IsLeaveKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.Escape);
        }

        private static bool IsBuildDrawerToggleKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.Tab);
        }

        private static bool IsOptionKeyPressed(int zeroBasedIndex)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return zeroBasedIndex switch
                {
                    0 => keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame,
                    1 => keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame,
                    2 => keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame,
                    _ => false,
                };
            }
#endif
            return zeroBasedIndex switch
            {
                0 => Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1),
                1 => Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2),
                2 => Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3),
                _ => false,
            };
        }

        private static void CreateQuad(Transform parent, string name, Vector2 position, Vector2 size, Color color, int sortingOrder)
        {
            var quad = new GameObject(name);
            quad.transform.SetParent(parent, false);
            quad.transform.position = new Vector3(position.x, position.y, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = quad.AddComponent<SpriteRenderer>();
            renderer.sprite = EJR.Game.Core.RuntimeSpriteFactory.GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static string FormatTime(int totalSeconds)
        {
            var clampedSeconds = Mathf.Max(0, totalSeconds);
            var minutes = clampedSeconds / 60;
            var seconds = clampedSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
