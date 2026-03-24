using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EJR.Game.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerSessionController : MonoBehaviour
    {
        public const string TitleSceneName = "TitleScene";
        public const string MultiplayerSceneName = "MultiplayerScene";
        public const string PlayerPrefabResourcePath = "Prefabs/MultiplayerPlayer";
        public const string SharedEnemyPrefabResourcePath = "Prefabs/MultiplayerSharedEnemy";
        public const string SharedProjectilePrefabResourcePath = "Prefabs/MultiplayerSharedProjectile";
        public const string SharedExperienceOrbPrefabResourcePath = "Prefabs/MultiplayerSharedExperienceOrb";

        private const int MaxPlayers = 4;
        private const float SpawnRadius = 3.5f;
        private const string SessionType = "ejr-coop";
        private const string SessionNamePrefix = "EJR";
        private const string AuthProfilePrefix = "ejr";
        private const string RolePropertyKey = "role";

        private enum SessionKind
        {
            None,
            Host,
            Client,
        }

        private static MultiplayerSessionController s_Instance;
        private static string s_PendingStatusMessage;

        private NetworkManager _networkManager;
        private UnityTransport _transport;
        private ISession _session;
        private SessionKind _sessionKind;
        private bool _isShuttingDown;
        private bool _isBusy;
        private string _currentStatus = "멀티플레이 준비 완료.";
        private string _authProfile;
        private string _playerId;

        public static event Action<string> StatusChanged;

        public static MultiplayerSessionController EnsureInstance()
        {
            if (s_Instance != null)
            {
                return s_Instance;
            }

            s_Instance = FindFirstObjectByType<MultiplayerSessionController>();
            if (s_Instance != null)
            {
                return s_Instance;
            }

            var controllerObject = new GameObject("MultiplayerSessionController");
            s_Instance = controllerObject.AddComponent<MultiplayerSessionController>();
            return s_Instance;
        }

        public static bool TryConsumePendingStatus(out string message)
        {
            if (string.IsNullOrWhiteSpace(s_PendingStatusMessage))
            {
                message = string.Empty;
                return false;
            }

            message = s_PendingStatusMessage;
            s_PendingStatusMessage = string.Empty;
            return true;
        }

        public string CurrentStatus => _currentStatus;
        public bool IsBusy => _isBusy;
        public bool HasActiveSession => _session != null && _session.State == SessionState.Connected;
        public bool IsHostSession => _sessionKind == SessionKind.Host && HasActiveSession;
        public bool IsClientSession => _sessionKind == SessionKind.Client && HasActiveSession;
        public string SessionCode => _session?.Code ?? string.Empty;
        public string SessionName => _session?.Name ?? string.Empty;
        public string LocalPlayerId => _playerId ?? string.Empty;
        public int SessionPlayerCount => _session?.PlayerCount ?? (_networkManager != null && _networkManager.IsListening ? _networkManager.ConnectedClientsIds.Count : 0);
        public int SessionMaxPlayers => _session?.MaxPlayers ?? MaxPlayers;

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
            {
                s_Instance = null;
            }

            DetachSession();

            if (_networkManager == null)
            {
                return;
            }

            UnregisterNetworkCallbacks();
            if (Application.isPlaying)
            {
                Destroy(_networkManager.gameObject);
            }
            else
            {
                DestroyImmediate(_networkManager.gameObject);
            }
        }

        public async Task<bool> StartHostAsync()
        {
            if (_isBusy)
            {
                SetStatus("이미 멀티플레이 요청이 진행 중입니다.");
                return false;
            }

            if (HasActiveSession)
            {
                SetStatus("이미 멀티플레이 세션이 실행 중입니다.");
                return false;
            }

            _isBusy = true;
            try
            {
                if (!PrepareNetworkManager())
                {
                    return false;
                }

                await EnsureServicesReadyAsync();

                var sessionOptions = new SessionOptions
                {
                    Type = SessionType,
                    Name = BuildSessionName(),
                    MaxPlayers = MaxPlayers,
                    IsPrivate = true,
                    PlayerProperties = BuildPlayerProperties("host"),
                }.WithRelayNetwork();

                SetStatus("릴레이 세션 생성 중...");
                var hostSession = await MultiplayerService.Instance.CreateSessionAsync(sessionOptions);

                AttachSession(hostSession, SessionKind.Host);
                SetStatus(BuildConnectedStatus("호스트 준비 완료"));

                _networkManager.SceneManager.LoadScene(MultiplayerSceneName, LoadSceneMode.Single);
                return true;
            }
            catch (Exception exception)
            {
                await ResetRuntimeAfterFailureAsync();
                SetStatus(FormatExceptionMessage(exception, "멀티플레이 세션 생성에 실패했습니다."));
                return false;
            }
            finally
            {
                _isBusy = false;
            }
        }

        public async Task<bool> JoinByCodeAsync(string sessionCode)
        {
            if (_isBusy)
            {
                SetStatus("이미 멀티플레이 요청이 진행 중입니다.");
                return false;
            }

            if (HasActiveSession)
            {
                SetStatus("이미 멀티플레이 세션이 실행 중입니다.");
                return false;
            }

            sessionCode = string.IsNullOrWhiteSpace(sessionCode)
                ? string.Empty
                : sessionCode.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(sessionCode))
            {
                SetStatus("올바른 참가 코드를 입력하세요.");
                return false;
            }

            _isBusy = true;
            try
            {
                if (!PrepareNetworkManager())
                {
                    return false;
                }

                await EnsureServicesReadyAsync();

                var joinOptions = new JoinSessionOptions
                {
                    Type = SessionType,
                    PlayerProperties = BuildPlayerProperties("client"),
                };

                SetStatus($"세션 {sessionCode}에 참가하는 중...");
                var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode, joinOptions);
                AttachSession(session, SessionKind.Client);
                SetStatus(BuildConnectedStatus("연결됨"));
                return true;
            }
            catch (Exception exception)
            {
                await ResetRuntimeAfterFailureAsync();
                SetStatus(FormatExceptionMessage(exception, "멀티플레이 세션 참가에 실패했습니다."));
                return false;
            }
            finally
            {
                _isBusy = false;
            }
        }

        public void LeaveSession(string reason = "세션이 종료되었습니다.")
        {
            StorePendingStatus(reason);
            _ = ShutdownSessionAsync(loadTitleScene: true, reason, deleteForHost: IsHostSession);
        }

        private bool PrepareNetworkManager()
        {
            EnsureNetworkManagerObject();

            var playerPrefab = Resources.Load<GameObject>(PlayerPrefabResourcePath);
            var sharedEnemyPrefab = Resources.Load<GameObject>(SharedEnemyPrefabResourcePath);
            var sharedProjectilePrefab = Resources.Load<GameObject>(SharedProjectilePrefabResourcePath);
            var sharedExperienceOrbPrefab = Resources.Load<GameObject>(SharedExperienceOrbPrefabResourcePath);
            if (playerPrefab == null || sharedEnemyPrefab == null || sharedProjectilePrefab == null || sharedExperienceOrbPrefab == null)
            {
                SetStatus("멀티플레이 네트워크 프리팹이 없습니다.");
                return false;
            }

            UnregisterNetworkCallbacks();

            if (_networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }

            var config = new NetworkConfig
            {
                NetworkTransport = _transport,
                PlayerPrefab = playerPrefab,
                TickRate = 30,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                ForceSamePrefabs = true,
                LoadSceneTimeOut = 30,
                NetworkTopology = NetworkTopologyTypes.ClientServer,
            };
            config.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
            config.Prefabs.Add(new NetworkPrefab { Prefab = sharedEnemyPrefab });
            config.Prefabs.Add(new NetworkPrefab { Prefab = sharedProjectilePrefab });
            config.Prefabs.Add(new NetworkPrefab { Prefab = sharedExperienceOrbPrefab });

            _networkManager.NetworkConfig = config;
            _networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;

            Application.runInBackground = true;
            return true;
        }

        private async Task EnsureServicesReadyAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                SetStatus("온라인 서비스 초기화 중...");
                await UnityServices.InitializeAsync();
            }
            else
            {
                while (UnityServices.State == ServicesInitializationState.Initializing)
                {
                    await Task.Yield();
                }
            }

            if (MultiplayerService.Instance == null)
            {
                throw new InvalidOperationException("멀티플레이 서비스를 사용할 수 없습니다.");
            }

            var authentication = AuthenticationService.Instance;

            if (string.IsNullOrWhiteSpace(_authProfile))
            {
                _authProfile = BuildAuthProfile();
            }

            if (!authentication.IsSignedIn)
            {
                if (!string.Equals(authentication.Profile, _authProfile, StringComparison.Ordinal))
                {
                    authentication.SwitchProfile(_authProfile);
                }

                SetStatus("익명 로그인 중...");
                await authentication.SignInAnonymouslyAsync();
            }

            _playerId = authentication.PlayerId;
        }

        private void EnsureNetworkManagerObject()
        {
            if (_networkManager != null)
            {
                if (_transport == null)
                {
                    _transport = _networkManager.GetComponent<UnityTransport>();
                    if (_transport == null)
                    {
                        _transport = _networkManager.gameObject.AddComponent<UnityTransport>();
                    }
                }

                return;
            }

            var managerObject = new GameObject("NetworkManager");
            DontDestroyOnLoad(managerObject);

            _networkManager = managerObject.AddComponent<NetworkManager>();
            _transport = managerObject.AddComponent<UnityTransport>();
        }

        private void AttachSession(ISession session, SessionKind kind)
        {
            DetachSession();

            _session = session;
            _sessionKind = kind;

            if (_session == null)
            {
                return;
            }

            _session.Changed += HandleSessionChanged;
            _session.StateChanged += HandleSessionStateChanged;
            _session.PlayerJoined += HandlePlayerJoined;
            _session.PlayerHasLeft += HandlePlayerLeft;
            _session.RemovedFromSession += HandleRemovedFromSession;
            _session.Deleted += HandleSessionDeleted;
            _session.SessionHostChanged += HandleSessionHostChanged;
        }

        private void DetachSession()
        {
            if (_session == null)
            {
                return;
            }

            _session.Changed -= HandleSessionChanged;
            _session.StateChanged -= HandleSessionStateChanged;
            _session.PlayerJoined -= HandlePlayerJoined;
            _session.PlayerHasLeft -= HandlePlayerLeft;
            _session.RemovedFromSession -= HandleRemovedFromSession;
            _session.Deleted -= HandleSessionDeleted;
            _session.SessionHostChanged -= HandleSessionHostChanged;
            _session = null;
        }

        private async Task ResetRuntimeAfterFailureAsync()
        {
            DetachSession();
            _sessionKind = SessionKind.None;
            await Task.Yield();
            CleanupNetworkManager();
        }

        private async Task ShutdownSessionAsync(bool loadTitleScene, string reason, bool deleteForHost)
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;

            try
            {
                var session = _session;
                var shouldDeleteSession = deleteForHost && session is IHostSession;

                DetachSession();
                _sessionKind = SessionKind.None;

                if (session != null)
                {
                    try
                    {
                        if (shouldDeleteSession)
                        {
                            await ((IHostSession)session).DeleteAsync();
                        }
                        else
                        {
                            await session.LeaveAsync();
                        }
                    }
                    catch (Exception exception)
                    {
                        UnityEngine.Debug.LogWarning($"Multiplayer session shutdown warning: {exception.Message}");
                    }
                }

                CleanupNetworkManager();
                SetStatus(reason);

                if (loadTitleScene && SceneManager.GetActiveScene().name != TitleSceneName)
                {
                    SceneManager.LoadScene(TitleSceneName);
                }
            }
            finally
            {
                _isShuttingDown = false;
            }
        }

        private void CleanupNetworkManager()
        {
            if (_networkManager == null)
            {
                return;
            }

            UnregisterNetworkCallbacks();

            if (_networkManager.IsListening)
            {
                _networkManager.Shutdown();
            }
        }

        private void UnregisterNetworkCallbacks()
        {
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _networkManager.ConnectionApprovalCallback = null;
        }

        private void HandleConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var connectedCount = _networkManager != null ? _networkManager.ConnectedClientsIds.Count : 0;
            if (connectedCount >= MaxPlayers)
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Pending = false;
                response.Reason = "방이 가득 찼습니다.";
                return;
            }

            var coop = MultiplayerCoopController.Instance;
            if (coop != null && !coop.AllowsJoin)
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Pending = false;
                response.Reason = "런이 이미 진행 중입니다.";
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Pending = false;
            response.Position = GetSpawnPosition(request.ClientNetworkId);
            response.Rotation = Quaternion.identity;
        }

        private Vector3 GetSpawnPosition(ulong clientId)
        {
            var index = (int)(clientId % MaxPlayers);
            var angle = (Mathf.PI * 2f * index) / MaxPlayers;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * SpawnRadius;
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (_networkManager == null)
            {
                return;
            }

            if (clientId == _networkManager.LocalClientId)
            {
                SetStatus(BuildConnectedStatus(_networkManager.IsHost ? "호스트 준비 완료" : "연결됨"));
                return;
            }

            if (_networkManager.IsHost)
            {
                SetStatus(BuildConnectedStatus($"플레이어 입장 ({SessionPlayerCount}/{SessionMaxPlayers})"));
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (_networkManager == null || _isShuttingDown)
            {
                return;
            }

            if (clientId != _networkManager.LocalClientId)
            {
                if (_networkManager.IsHost)
                {
                    SetStatus(BuildConnectedStatus($"플레이어 퇴장 ({SessionPlayerCount}/{SessionMaxPlayers})"));
                }

                return;
            }

            var reason = string.IsNullOrWhiteSpace(_networkManager.DisconnectReason)
                ? "멀티플레이 세션과 연결이 끊어졌습니다."
                : _networkManager.DisconnectReason;

            StorePendingStatus(reason);
            _ = ShutdownSessionAsync(loadTitleScene: true, reason, deleteForHost: false);
        }

        private void HandleSessionChanged()
        {
            if (_isShuttingDown || _session == null || _session.State != SessionState.Connected)
            {
                return;
            }

            SetStatus(BuildConnectedStatus(IsHostSession ? "호스트 준비 완료" : "연결됨"));
        }

        private void HandleSessionStateChanged(SessionState state)
        {
            if (_isShuttingDown)
            {
                return;
            }

            switch (state)
            {
                case SessionState.Connected:
                    SetStatus(BuildConnectedStatus(IsHostSession ? "호스트 준비 완료" : "연결됨"));
                    break;

                case SessionState.Disconnected:
                    StorePendingStatus("멀티플레이 세션과 연결이 끊어졌습니다.");
                    _ = ShutdownSessionAsync(loadTitleScene: true, "멀티플레이 세션과 연결이 끊어졌습니다.", deleteForHost: false);
                    break;

                case SessionState.Deleted:
                    StorePendingStatus("멀티플레이 세션이 종료되었습니다.");
                    _ = ShutdownSessionAsync(loadTitleScene: true, "멀티플레이 세션이 종료되었습니다.", deleteForHost: false);
                    break;
            }
        }

        private void HandlePlayerJoined(string playerId)
        {
            if (_isShuttingDown)
            {
                return;
            }

            SetStatus(BuildConnectedStatus($"플레이어 입장 ({SessionPlayerCount}/{SessionMaxPlayers})"));
        }

        private void HandlePlayerLeft(string playerId)
        {
            if (_isShuttingDown)
            {
                return;
            }

            SetStatus(BuildConnectedStatus($"플레이어 퇴장 ({SessionPlayerCount}/{SessionMaxPlayers})"));
        }

        private void HandleRemovedFromSession()
        {
            if (_isShuttingDown)
            {
                return;
            }

            StorePendingStatus("멀티플레이 세션에서 제외되었습니다.");
            _ = ShutdownSessionAsync(loadTitleScene: true, "멀티플레이 세션에서 제외되었습니다.", deleteForHost: false);
        }

        private void HandleSessionDeleted()
        {
            if (_isShuttingDown)
            {
                return;
            }

            StorePendingStatus("멀티플레이 세션이 삭제되었습니다.");
            _ = ShutdownSessionAsync(loadTitleScene: true, "멀티플레이 세션이 삭제되었습니다.", deleteForHost: false);
        }

        private void HandleSessionHostChanged(string hostPlayerId)
        {
            if (_isShuttingDown)
            {
                return;
            }

            SetStatus(BuildConnectedStatus("호스트가 변경되었습니다"));
        }

        private void SetStatus(string message)
        {
            _currentStatus = string.IsNullOrWhiteSpace(message) ? "준비 완료." : message;
            StatusChanged?.Invoke(_currentStatus);
        }

        private static void StorePendingStatus(string message)
        {
            s_PendingStatusMessage = message;
            StatusChanged?.Invoke(message);
        }

        private string BuildConnectedStatus(string prefix)
        {
            var code = SessionCode;
            if (!string.IsNullOrWhiteSpace(code))
            {
                return $"{prefix}. 코드: {code}";
            }

            return prefix;
        }

        private static Dictionary<string, PlayerProperty> BuildPlayerProperties(string role)
        {
            return new Dictionary<string, PlayerProperty>
            {
                { RolePropertyKey, new PlayerProperty(role, VisibilityPropertyOptions.Member) },
            };
        }

        private static string BuildSessionName()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            return $"{SessionNamePrefix}-{suffix}";
        }

        private static string BuildAuthProfile()
        {
            var runtimeScope = Application.isEditor ? "ed" : "pl";

            try
            {
                return $"{AuthProfilePrefix}_{runtimeScope}_{Process.GetCurrentProcess().Id}";
            }
            catch
            {
                return $"{AuthProfilePrefix}_{runtimeScope}";
            }
        }

        private static string FormatExceptionMessage(Exception exception, string fallbackMessage)
        {
            if (exception is SessionException sessionException)
            {
                return $"{fallbackMessage} [{sessionException.Error}]";
            }

            if (exception is RequestFailedException requestFailedException)
            {
                return $"{fallbackMessage} [{requestFailedException.ErrorCode}]";
            }

            return string.IsNullOrWhiteSpace(exception.Message) ? fallbackMessage : $"{fallbackMessage} {exception.Message}";
        }
    }
}
