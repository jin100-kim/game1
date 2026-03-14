using System;
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
    public sealed class MultiplayerGameController : MonoBehaviour
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
        private Button _readyButton;
        private Button _startButton;
        private Text _characterButtonText;
        private Text _starterButtonText;
        private Text _readyButtonText;
        private Text _startButtonText;
        private readonly Button[] _choiceButtons = new Button[3];
        private readonly Text[] _choiceButtonTexts = new Text[3];
        private HudController _gameplayHud;
        private string _lastChoiceSignature = string.Empty;
        private AutoPlayAgent _autoPlayAgent;
        private bool _autoPlayEnabled;
        private PlayerMover _boundAutoPlayMover;
        private string _debugRevealBuffer = string.Empty;

        private const string DebugRevealCode = "admin";

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            mainCamera.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 1f);
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
            if (GameObject.Find("ArenaVisuals") != null)
            {
                return;
            }

            var root = new GameObject("ArenaVisuals");
            CreateQuad(root.transform, "ArenaBackground", Vector2.zero, new Vector2(arenaBounds.width, arenaBounds.height), new Color(0.10f, 0.12f, 0.18f, 1f), -10);
            CreateQuad(root.transform, "BorderTop", new Vector2(0f, arenaBounds.yMax), new Vector2(arenaBounds.width + 0.5f, 0.3f), new Color(0.92f, 0.74f, 0.18f, 1f), -9);
            CreateQuad(root.transform, "BorderBottom", new Vector2(0f, arenaBounds.yMin), new Vector2(arenaBounds.width + 0.5f, 0.3f), new Color(0.92f, 0.74f, 0.18f, 1f), -9);
            CreateQuad(root.transform, "BorderLeft", new Vector2(arenaBounds.xMin, 0f), new Vector2(0.3f, arenaBounds.height + 0.5f), new Color(0.92f, 0.74f, 0.18f, 1f), -9);
            CreateQuad(root.transform, "BorderRight", new Vector2(arenaBounds.xMax, 0f), new Vector2(0.3f, arenaBounds.height + 0.5f), new Color(0.92f, 0.74f, 0.18f, 1f), -9);
        }

        private void EnsureOverlay()
        {
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
            _lobbyPanel = CreatePanel(parent, "LobbyPanel", new Vector2(18f, -146f), new Vector2(520f, 620f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0f, 0f, 0f, 0.44f));
            _lobbyHeaderText = CreateText(_lobbyPanel.transform, "LobbyHeader", Vector2.zero, new Vector2(16f, -16f), new Vector2(1f, 1f), 22, TextAnchor.UpperLeft);
            _lobbyHeaderText.rectTransform.sizeDelta = new Vector2(-32f, 92f);

            _playerListText = CreateText(_lobbyPanel.transform, "PlayerList", Vector2.zero, new Vector2(16f, -116f), new Vector2(1f, 1f), 18, TextAnchor.UpperLeft);
            _playerListText.rectTransform.sizeDelta = new Vector2(-32f, 250f);

            _characterButton = CreateActionButton(_lobbyPanel.transform, "CharacterButton", new Vector2(20f, 210f), new Vector2(220f, 54f), out _characterButtonText, HandleCharacterClicked);
            _starterButton = CreateActionButton(_lobbyPanel.transform, "StarterButton", new Vector2(260f, 210f), new Vector2(220f, 54f), out _starterButtonText, HandleStarterClicked);
            _readyButton = CreateActionButton(_lobbyPanel.transform, "ReadyButton", new Vector2(20f, 278f), new Vector2(220f, 58f), out _readyButtonText, HandleReadyClicked);
            _startButton = CreateActionButton(_lobbyPanel.transform, "StartButton", new Vector2(260f, 278f), new Vector2(220f, 58f), out _startButtonText, HandleStartClicked);

            _startHintText = CreateText(_lobbyPanel.transform, "StartHint", Vector2.zero, new Vector2(16f, -368f), new Vector2(1f, 1f), 16, TextAnchor.UpperLeft);
            _startHintText.rectTransform.sizeDelta = new Vector2(-32f, 210f);
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
            _resultPanel = CreatePanel(parent, "ResultPanel", Vector2.zero, new Vector2(560f, 160f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0f, 0f, 0f, 0.72f));
            _resultText = CreateText(_resultPanel.transform, "ResultText", Vector2.zero, Vector2.zero, Vector2.one, 28, TextAnchor.MiddleCenter);
            _resultText.rectTransform.offsetMin = new Vector2(20f, 20f);
            _resultText.rectTransform.offsetMax = new Vector2(-20f, -20f);
        }

        private void RefreshUi()
        {
            var session = MultiplayerSessionController.EnsureInstance();
            var manager = NetworkManager.Singleton;
            var coop = MultiplayerCoopController.Instance;
            var localPlayer = MultiplayerPlayerCombatant.FindOwnedLocalPlayer();
            var allPlayers = FindObjectsByType<MultiplayerPlayerCombatant>(FindObjectsSortMode.None);

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
            if (_statusText == null)
            {
                return;
            }

            var showStatus = coop == null || coop.Phase == MultiplayerRunPhase.Lobby;
            if (_statusPanel != null)
            {
                _statusPanel.SetActive(showStatus);
            }

            if (!showStatus)
            {
                return;
            }

            var mode = manager != null && manager.IsListening ? (manager.IsHost ? "HOST" : "CLIENT") : "OFFLINE";
            var sessionCode = string.IsNullOrWhiteSpace(session.SessionCode) ? "----" : session.SessionCode;
            var phase = coop != null ? coop.Phase.ToString().ToUpperInvariant() : "BOOT";

            var aliveCount = 0;
            for (var i = 0; i < allPlayers.Length; i++)
            {
                if (allPlayers[i] != null && allPlayers[i].IsTargetable)
                {
                    aliveCount++;
                }
            }

            _statusText.text =
                $"MULTI {mode}\n" +
                $"CODE {sessionCode}\n" +
                $"PHASE {phase}\n" +
                $"PLAYERS {allPlayers.Length}/{session.SessionMaxPlayers}\n" +
                $"ALIVE {aliveCount}\n" +
                $"YOU {(localPlayer != null ? localPlayer.DisplayName : "CONNECTING")}\n" +
                $"{(_autoPlayEnabled ? "AUTO PLAY ON\n" : string.Empty)}" +
                $"{session.CurrentStatus}\n" +
                "ESC : LEAVE";
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
                    .Append(MultiplayerCatalog.GetStarterWeaponDisplayName(player.SelectedStarterWeaponIndex));

                if (player.IsDowned)
                {
                    builder.Append(" | DOWN ").Append(Mathf.RoundToInt(player.ReviveProgress * 100f)).Append('%');
                }
                else
                {
                    builder.Append(" | ").Append(player.IsReady ? "READY" : "NOT READY");
                }

                builder.Append('\n');
            }

            _lobbyHeaderText.text = "Lobby\nChoose character and starter weapon, then ready up.";
            _playerListText.text = builder.Length > 0 ? builder.ToString() : "Waiting for players...";

            var canInteract = localPlayer != null && coop != null && coop.Phase == MultiplayerRunPhase.Lobby;
            _characterButton.interactable = canInteract;
            _starterButton.interactable = canInteract;
            _readyButton.interactable = canInteract;

            _characterButtonText.text = localPlayer != null
                ? $"Character\n{MultiplayerCatalog.GetCharacter(localPlayer.SelectedCharacterId).DisplayName}"
                : "Character\n-";
            _starterButtonText.text = localPlayer != null
                ? $"Starter\n{MultiplayerCatalog.GetStarterWeaponDisplayName(localPlayer.SelectedStarterWeaponIndex)}"
                : "Starter\n-";
            _readyButtonText.text = localPlayer != null && localPlayer.IsReady ? "Cancel Ready" : "Ready";

            var isHost = manager != null && manager.IsHost;
            _startButton.gameObject.SetActive(isHost);
            _startButton.interactable = isHost && coop != null && string.IsNullOrWhiteSpace(coop.GetStartBlockReason());
            _startButtonText.text = "Start Game";
            _startHintText.text = isHost
                ? (coop != null && !string.IsNullOrWhiteSpace(coop.GetStartBlockReason())
                    ? coop.GetStartBlockReason()
                    : "All players ready. Start available.")
                : "The host starts the run after everyone is ready.";
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
                _gameplayHud.HideBossBar();
                return;
            }

            if (localPlayer == null)
            {
                return;
            }

            _gameplayHud.SetTopBar(
                localPlayer.CurrentHealth,
                Mathf.Max(1f, localPlayer.MaxHealth),
                coop.TeamLevel,
                coop.TeamExperience,
                coop.TeamRequiredExperience,
                coop.RemainingSeconds);
            _gameplayHud.SetModeHint(_autoPlayEnabled ? "AUTO PLAY ON" : string.Empty);
            _gameplayHud.SetBuildInfo(localPlayer.WeaponSummary, localPlayer.StatSummary);
            if (coop.BossActive)
            {
                _gameplayHud.SetBossBar(coop.BossCurrentHealth, coop.BossMaxHealth, "BOSS");
            }
            else
            {
                _gameplayHud.HideBossBar();
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
                options[i] = new LevelUpOption(
                    UpgradeCategory.Stat,
                    default,
                    default,
                    0,
                    0,
                    false,
                    false,
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
            if (_resultPanel == null)
            {
                return;
            }

            var showResult = coop != null && coop.Phase == MultiplayerRunPhase.Result;
            _resultPanel.SetActive(showResult);
            if (!showResult)
            {
                return;
            }

            _resultText.text = coop.ResultCleared ? "Run Complete\nReturning to lobby..." : "Team Defeated\nReturning to lobby...";
        }

        private void HandleCharacterClicked()
        {
            MultiplayerPlayerCombatant.FindOwnedLocalPlayer()?.RequestNextCharacterSelection();
            RefreshUi();
        }

        private void HandleStarterClicked()
        {
            MultiplayerPlayerCombatant.FindOwnedLocalPlayer()?.RequestNextStarterWeaponSelection();
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
            }

            _boundAutoPlayMover = localMover;
            if (_boundAutoPlayMover != null && _autoPlayEnabled)
            {
                _boundAutoPlayMover.SetMoveInputReader(ReadAutoPlayMoveInput);
            }
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
