using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    public sealed class MultiplayerFlowCoordinator : MonoBehaviour
    {
        private const string RelayConnectionType = "dtls";
        private const int DefaultMaxPlayers = 4;
        private const float RelayJoinCodeTimeoutSeconds = 30f;

        private readonly MultiplayerStatusStateMachine _statusStateMachine = new MultiplayerStatusStateMachine();

        private IAuthClient _authClient;
        private ILobbyClient _lobbyClient;
        private IRelayClient _relayClient;
        private IRelayTransportService _relayTransportService;

        private CancellationTokenSource _waitRelayCts;
        private NetworkManager _networkManager;

        private string _currentLobbyHostPlayerId;
        private bool _networkStartRequested;

        public MultiplayerStatus Status => _statusStateMachine.Status;
        public MultiplayerError LastError => _statusStateMachine.LastError;

        public string LocalPlayerId => _authClient.PlayerId;
        public string LocalDisplayName => _authClient.DisplayName;
        
        public bool IsLocalPlayerHost =>
            !string.IsNullOrWhiteSpace(_currentLobbyHostPlayerId) && _currentLobbyHostPlayerId == LocalPlayerId;
        
        public LobbySnapshot CurrentLobbySnapshot { get; private set; }

        public event Action<MultiplayerStatus> StatusChanged;
        public event Action<string, string> SignedIn;
        public event Action<IReadOnlyList<LobbySummary>> LobbyListUpdated;
        public event Action<LobbySnapshot> LobbyJoined;
        public event Action LobbyLeft;
        public event Action<RelayConnectionSummary> RelayReady;
        public event Action<MultiplayerRole> NetworkStarted;
        public event Action<MultiplayerRole> ReadyToEnterGame;
        public event Action<MultiplayerError> ErrorOccurred;

        private void Awake()
        {
            _statusStateMachine.StatusChanged += status => StatusChanged?.Invoke(status);
        }

        private void OnDestroy()
        {
            _statusStateMachine.StatusChanged -= status => StatusChanged?.Invoke(status);
        }

        public void InjectDependencies(IAuthClient authClient, ILobbyClient lobbyClient, IRelayClient relayClient, IRelayTransportService relayTransportService)
        {
            _authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
            _lobbyClient = lobbyClient ?? throw new ArgumentNullException(nameof(lobbyClient));
            _relayClient = relayClient ?? throw new ArgumentNullException(nameof(relayClient));
            _relayTransportService = relayTransportService ?? throw new ArgumentNullException(nameof(relayTransportService));
        }

        public async Task InitializeAsync()
        {
            _statusStateMachine.ClearLastError();

            if (Status != MultiplayerStatus.Idle)
            {
                return;
            }

            _statusStateMachine.TransitionTo(MultiplayerStatus.Initializing);

            try
            {
                await UnityServices.InitializeAsync();
                _statusStateMachine.TransitionTo(_authClient.IsSignedIn ? MultiplayerStatus.SignedIn : MultiplayerStatus.SignedOut);
                HookLobbyEvents();
            }
            catch (Exception ex)
            {
                RaiseError("init_failed", "Unity Services initialization failed.", ex);
            }
        }

        public async Task SignUpAsync(string username, string password, string displayName)
        {
            var validationError = InputValidator.ValidateSignUp(username, password);
            if (validationError != null)
            {
                RaiseError(validationError.Code, validationError.Message, null);
                return;
            }

            _statusStateMachine.BeginWorkflowStep(MultiplayerStatus.SigningIn);

            try
            {
                await _authClient.SignUpAsync(username, password, displayName);
                _statusStateMachine.TransitionTo(MultiplayerStatus.SignedIn);
                SignedIn?.Invoke(_authClient.PlayerId, _authClient.DisplayName);
            }
            catch (Exception ex)
            {
                RaiseError("signup_failed", LobbySnapshotMapper.BuildAuthErrorMessage(ex, isSignUp: true), ex);
            }
        }

        public async Task SignInAsync(string username, string password)
        {
            var validationError = InputValidator.ValidateSignIn(username, password);
            if (validationError != null)
            {
                RaiseError(validationError.Code, validationError.Message, null);
                return;
            }

            _statusStateMachine.BeginWorkflowStep(MultiplayerStatus.SigningIn);

            try
            {
                await _authClient.SignInAsync(username, password);
                _statusStateMachine.TransitionTo(MultiplayerStatus.SignedIn);
                SignedIn?.Invoke(_authClient.PlayerId, _authClient.DisplayName);
            }
            catch (Exception ex)
            {
                RaiseError("signin_failed", LobbySnapshotMapper.BuildAuthErrorMessage(ex, isSignUp: false), ex);
            }
        }

        public async Task SetDisplayNameAsync(string displayName)
        {
            _statusStateMachine.ClearLastError();

            try
            {
                await _authClient.SetDisplayNameAsync(displayName);
                if (_lobbyClient.CurrentLobby != null)
                {
                    await UpdatePlayerDataAsync(displayName);
                }
            }
            catch (Exception ex)
            {
                RaiseError("display_name_failed", "Failed to update display name.", ex);
            }
        }

        public void SignOut()
        {
            _statusStateMachine.ClearLastError();
            _authClient.SignOut();
            _statusStateMachine.TransitionTo(MultiplayerStatus.SignedOut);
        }

        public async Task QueryLobbiesAsync(int maxResults = 25)
        {
            _statusStateMachine.BeginWorkflowStep(MultiplayerStatus.LobbyQuerying);

            try
            {
                var lobbies = await _lobbyClient.QueryLobbiesAsync(maxResults);
                LobbyListUpdated?.Invoke(lobbies.Select(LobbySnapshotMapper.ToSummary).ToList());
                _statusStateMachine.TransitionTo(MultiplayerStatus.SignedIn);
            }
            catch (Exception ex)
            {
                RaiseError("lobby_query_failed", "Lobby query failed.", ex);
            }
        }

        public async Task CreateLobbyAsHostAsync(string lobbyName, int maxPlayers = DefaultMaxPlayers, bool isPrivate = false)
        {
            _statusStateMachine.BeginWorkflowStep(MultiplayerStatus.LobbyJoining);

            try
            {
                var lobby = await _lobbyClient.CreateLobbyAsync(lobbyName, maxPlayers, isPrivate, _authClient.DisplayName);
                _currentLobbyHostPlayerId = lobby.HostId;
                CurrentLobbySnapshot = LobbySnapshotMapper.ToSnapshot(lobby);
                _lobbyClient.StartHeartbeatLoop();
                _lobbyClient.StartPollingLoop();
                _statusStateMachine.TransitionTo(MultiplayerStatus.LobbyJoined);
                LobbyJoined?.Invoke(CurrentLobbySnapshot);
            }
            catch (Exception ex)
            {
                RaiseError("lobby_create_failed", "Lobby creation failed.", ex);
            }
        }

        public async Task JoinLobbyByCodeAsync(string lobbyCode)
        {
            _statusStateMachine.BeginWorkflowStep(MultiplayerStatus.LobbyJoining);

            try
            {
                var lobby = await _lobbyClient.JoinLobbyByCodeAsync(lobbyCode, _authClient.DisplayName);
                _currentLobbyHostPlayerId = lobby.HostId;
                CurrentLobbySnapshot = LobbySnapshotMapper.ToSnapshot(lobby);
                _lobbyClient.StartPollingLoop();
                _statusStateMachine.TransitionTo(MultiplayerStatus.LobbyJoined);
                LobbyJoined?.Invoke(CurrentLobbySnapshot);
            }
            catch (Exception ex)
            {
                RaiseError("lobby_join_failed", "Lobby join failed.", ex);
            }
        }

        public async Task JoinLobbyByIdAsync(string lobbyId)
        {
            _statusStateMachine.BeginWorkflowStep(MultiplayerStatus.LobbyJoining);

            try
            {
                var lobby = await _lobbyClient.JoinLobbyByIdAsync(lobbyId, _authClient.DisplayName);
                _currentLobbyHostPlayerId = lobby.HostId;
                CurrentLobbySnapshot = LobbySnapshotMapper.ToSnapshot(lobby);
                _lobbyClient.StartPollingLoop();
                _statusStateMachine.TransitionTo(MultiplayerStatus.LobbyJoined);
                LobbyJoined?.Invoke(CurrentLobbySnapshot);
            }
            catch (Exception ex)
            {
                RaiseError("lobby_join_failed", "Lobby join failed.", ex);
            }
        }

        public async Task LeaveLobbyAsync()
        {
            _statusStateMachine.ClearLastError();

            try
            {
                _waitRelayCts?.Cancel();
                _lobbyClient.StopLoops();
                await _lobbyClient.LeaveLobbyAsync(_authClient.PlayerId);
            }
            catch (Exception ex)
            {
                RaiseError("lobby_leave_failed", "Lobby leave failed.", ex);
            }
        }

        public async Task StartHostFlowAsync(string lobbyName, int maxPlayers = DefaultMaxPlayers, bool isPrivate = false)
        {
            await CreateLobbyAsHostAsync(lobbyName, maxPlayers, isPrivate);
        }

        public async Task StartClientFlowAsync(string lobbyCode)
        {
            await JoinLobbyByCodeAsync(lobbyCode);
        }

        public async Task StartGameAsHostAsync()
        {
            _statusStateMachine.ClearLastError();
            
            if (_lobbyClient.CurrentLobby == null || CurrentLobbySnapshot == null)
            {
                RaiseError("start_game_no_lobby", "Cannot start game because no lobby is active.", null);
                return;
            }

            if (!IsLocalPlayerHost)
            {
                RaiseError("start_game_not_host", "Only the lobby host can start the game.", null);
                return;
            }
            
            if (CurrentLobbySnapshot.PlayerCount < 2)
            {
                RaiseError("start_game_not_enough_players", "At least 2 players are required to start.", null);
                return;
            }

            if (_networkStartRequested)
            {
                return;
            }

            _networkStartRequested = true;

            try
            {
                _statusStateMachine.TransitionTo(MultiplayerStatus.RelayAllocating);
                Debug.Log("[MultiplayerFlow] Allocating relay for game start.");

                var summary = await _relayTransportService.CreateAndConfigureHostAsync(
                    CurrentLobbySnapshot.MaxPlayers - 1, RelayConnectionType);

                Debug.Log($"[MultiplayerFlow] Host relay created. code={summary.JoinCode}");

                await PublishGameStartDataAsync(summary.JoinCode);
                Debug.Log("[MultiplayerFlow] Game start data published to lobby.");

                _statusStateMachine.TransitionTo(MultiplayerStatus.NetworkStarting);
                _networkManager.StartHost();

                RelayReady?.Invoke(summary);
                _statusStateMachine.TransitionTo(MultiplayerStatus.NetworkStarted);
                NetworkStarted?.Invoke(MultiplayerRole.Host);
                ReadyToEnterGame?.Invoke(MultiplayerRole.Host);
            }
            catch (Exception ex)
            {
                _networkStartRequested = false;
                RaiseError("start_game_failed", $"Start game failed: {ex.Message}", ex);
            }
        }

        private async Task StartClientFromCurrentLobbyAsync()
        {
            if (CurrentLobbySnapshot == null || string.IsNullOrWhiteSpace(CurrentLobbySnapshot.RelayJoinCode))
            {
                return;
            }
            
            _networkStartRequested = true;

            try
            {
                Debug.Log($"[MultiplayerFlow] Starting client from current lobby.");

                var joinCode = await WaitForRelayJoinCodeAsync(TimeSpan.FromSeconds(RelayJoinCodeTimeoutSeconds));
                Debug.Log($"[MultiplayerFlow] Relay join code received. length={joinCode?.Length ?? 0}");

                var summary = await _relayTransportService.JoinAndConfigureClientAsync(joinCode, RelayConnectionType);
                Debug.Log($"[MultiplayerFlow] Relay allocation joined. region={summary.Region}");

                _statusStateMachine.TransitionTo(MultiplayerStatus.NetworkStarting);
                Debug.Log("[MultiplayerFlow] Starting NetworkManager client.");
                _networkManager.StartClient();

                RelayReady?.Invoke(summary);
                _statusStateMachine.TransitionTo(MultiplayerStatus.NetworkStarted);
                NetworkStarted?.Invoke(MultiplayerRole.Client);
                ReadyToEnterGame?.Invoke(MultiplayerRole.Client);
                Debug.Log("[MultiplayerFlow] Client relay/network startup completed.");
            }
            catch (Exception ex)
            {
                _networkStartRequested = false;
                RaiseError("client_start_failed", $"Client start failed: {ex.Message}", ex);
            }
        }

        private async Task PublishGameStartDataAsync(string joinCode)
        {
            await _lobbyClient.RefreshCurrentLobbyAsync();

            var data = new Dictionary<string, DataObject>
            {
                {
                    MultiplayerKeys.LobbyDataRelayJoinCodeKey,
                    new DataObject(DataObject.VisibilityOptions.Public, joinCode)
                },
                {
                    MultiplayerKeys.LobbyDataGameStartedKey,
                    new DataObject(DataObject.VisibilityOptions.Public, "true")
                }
            };

            await _lobbyClient.UpdateLobbyDataAsync(data);
        }

        public void AssignNetworkManager(NetworkManager manager)
        {
            _networkManager = manager;
            _relayTransportService.BindNetworkManager(manager);
        }

        private void HookLobbyEvents()
        {
            _lobbyClient.LobbyUpdated -= HandleLobbyUpdated;
            _lobbyClient.LobbyLeft -= HandleLobbyLeft;
            _lobbyClient.LobbyUpdated += HandleLobbyUpdated;
            _lobbyClient.LobbyLeft += HandleLobbyLeft;
        }

        private void HandleLobbyUpdated(Lobby lobby)
        {
            _currentLobbyHostPlayerId = lobby.HostId;
            CurrentLobbySnapshot = LobbySnapshotMapper.ToSnapshot(lobby);
            Debug.Log($"[MultiplayerFlow] Lobby updated: name={CurrentLobbySnapshot.Name}, players={CurrentLobbySnapshot.PlayerCount}/{CurrentLobbySnapshot.MaxPlayers}, relayCode={(string.IsNullOrWhiteSpace(CurrentLobbySnapshot.RelayJoinCode) ? "missing" : "present")}");
            LobbyJoined?.Invoke(CurrentLobbySnapshot);

            if (!IsLocalPlayerHost && LobbySnapshotMapper.IsGameStarted(CurrentLobbySnapshot) && !_networkStartRequested)
            {
                _ = StartClientFromCurrentLobbyAsync();
            }
        }

        private void HandleLobbyLeft()
        {
            CurrentLobbySnapshot = null;
            LobbyLeft?.Invoke();
        }

        private async Task UpdatePlayerDataAsync(string displayName)
        {
            var data = new Dictionary<string, PlayerDataObject>
            {
                {
                    MultiplayerKeys.PlayerDataDisplayNameKey,
                    new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, displayName)
                }
            };

            await Unity.Services.Lobbies.LobbyService.Instance.UpdatePlayerAsync(
                _lobbyClient.CurrentLobby.Id,
                _authClient.PlayerId,
                new UpdatePlayerOptions { Data = data });
        }

        private async Task<string> WaitForRelayJoinCodeAsync(TimeSpan timeout)
        {
            _waitRelayCts?.Cancel();
            _waitRelayCts = new CancellationTokenSource(timeout);
            var waitStart = Time.time;

            while (!_waitRelayCts.IsCancellationRequested)
            {
                var joinCode = CurrentLobbySnapshot?.RelayJoinCode;
                if (!string.IsNullOrWhiteSpace(joinCode))
                {
                    return joinCode;
                }

                Debug.Log($"[MultiplayerFlow] Relay join code not ready yet after {Time.time - waitStart:0.0}s. Current lobby snapshot present={CurrentLobbySnapshot != null}");

                await Task.Delay(250, _waitRelayCts.Token);
            }

            throw new TimeoutException("Relay join code was not set in time.");
        }

        private void RaiseError(string code, string message, Exception exception)
        {
            _statusStateMachine.RaiseError(code, message, exception);
            ErrorOccurred?.Invoke(_statusStateMachine.LastError);
        }
    }
}
