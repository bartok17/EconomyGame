using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    public sealed class MultiplayerFlowCoordinator : MonoBehaviour
    {
        private const string RelayConnectionType = "dtls";
        private const int DefaultMaxPlayers = 4;
        private const float RelayJoinCodeTimeoutSeconds = 30f;
        private static readonly Regex UsernamePattern = new Regex(@"^[a-z0-9]+$", RegexOptions.Compiled);
        private static readonly Regex PasswordPattern = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[^A-Za-z]).{8,}$", RegexOptions.Compiled);

        private readonly AuthClient _authClient = new AuthClient();
        private readonly LobbyClient _lobbyClient = new LobbyClient();
        private readonly RelayClient _relayClient = new RelayClient();

        private CancellationTokenSource _waitRelayCts;
        private NetworkManager _networkManager;
        private UnityTransport _transport;

        public MultiplayerStatus Status { get; private set; } = MultiplayerStatus.Idle;
        public MultiplayerError LastError { get; private set; }

        public string LocalPlayerId => _authClient.PlayerId;
        public string LocalDisplayName => _authClient.DisplayName;
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

        public async Task InitializeAsync()
        {
            ClearLastError();

            if (Status != MultiplayerStatus.Idle)
            {
                return;
            }

            UpdateStatus(MultiplayerStatus.Initializing);

            try
            {
                await UnityServices.InitializeAsync();
                UpdateStatus(_authClient.IsSignedIn ? MultiplayerStatus.SignedIn : MultiplayerStatus.SignedOut);
                HookLobbyEvents();
            }
            catch (Exception ex)
            {
                RaiseError("init_failed", "Unity Services initialization failed.", ex);
            }
        }

        public async Task SignUpAsync(string username, string password, string displayName)
        {
            var validationError = ValidateSignUpInputs(username, password);
            if (validationError != null)
            {
                RaiseError(validationError.Code, validationError.Message, null);
                return;
            }

            BeginWorkflowStep(MultiplayerStatus.SigningIn);

            try
            {
                await _authClient.SignUpAsync(username, password, displayName);
                UpdateStatus(MultiplayerStatus.SignedIn);
                SignedIn?.Invoke(_authClient.PlayerId, _authClient.DisplayName);
            }
            catch (Exception ex)
            {
                RaiseError("signup_failed", BuildAuthErrorMessage(ex, isSignUp: true), ex);
            }
        }

        public async Task SignInAsync(string username, string password)
        {
            var validationError = ValidateSignInInputs(username, password);
            if (validationError != null)
            {
                RaiseError(validationError.Code, validationError.Message, null);
                return;
            }

            BeginWorkflowStep(MultiplayerStatus.SigningIn);

            try
            {
                await _authClient.SignInAsync(username, password);
                UpdateStatus(MultiplayerStatus.SignedIn);
                SignedIn?.Invoke(_authClient.PlayerId, _authClient.DisplayName);
            }
            catch (Exception ex)
            {
                RaiseError("signin_failed", BuildAuthErrorMessage(ex, isSignUp: false), ex);
            }
        }

        public async Task SetDisplayNameAsync(string displayName)
        {
            ClearLastError();

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
            ClearLastError();
            _authClient.SignOut();
            UpdateStatus(MultiplayerStatus.SignedOut);
        }

        public async Task QueryLobbiesAsync(int maxResults = 25)
        {
            BeginWorkflowStep(MultiplayerStatus.LobbyQuerying);

            try
            {
                var lobbies = await _lobbyClient.QueryLobbiesAsync(maxResults);
                LobbyListUpdated?.Invoke(lobbies.Select(ToSummary).ToList());
                UpdateStatus(MultiplayerStatus.SignedIn);
            }
            catch (Exception ex)
            {
                RaiseError("lobby_query_failed", "Lobby query failed.", ex);
            }
        }

        public async Task CreateLobbyAsHostAsync(string lobbyName, int maxPlayers = DefaultMaxPlayers, bool isPrivate = false)
        {
            BeginWorkflowStep(MultiplayerStatus.LobbyJoining);

            try
            {
                var lobby = await _lobbyClient.CreateLobbyAsync(lobbyName, maxPlayers, isPrivate, _authClient.DisplayName);
                CurrentLobbySnapshot = ToSnapshot(lobby);
                _lobbyClient.StartHeartbeatLoop();
                _lobbyClient.StartPollingLoop();
                UpdateStatus(MultiplayerStatus.LobbyJoined);
                LobbyJoined?.Invoke(CurrentLobbySnapshot);
            }
            catch (Exception ex)
            {
                RaiseError("lobby_create_failed", "Lobby creation failed.", ex);
            }
        }

        public async Task JoinLobbyByCodeAsync(string lobbyCode)
        {
            BeginWorkflowStep(MultiplayerStatus.LobbyJoining);

            try
            {
                var lobby = await _lobbyClient.JoinLobbyByCodeAsync(lobbyCode, _authClient.DisplayName);
                CurrentLobbySnapshot = ToSnapshot(lobby);
                _lobbyClient.StartPollingLoop();
                UpdateStatus(MultiplayerStatus.LobbyJoined);
                LobbyJoined?.Invoke(CurrentLobbySnapshot);
            }
            catch (Exception ex)
            {
                RaiseError("lobby_join_failed", "Lobby join failed.", ex);
            }
        }

        public async Task JoinLobbyByIdAsync(string lobbyId)
        {
            BeginWorkflowStep(MultiplayerStatus.LobbyJoining);

            try
            {
                var lobby = await _lobbyClient.JoinLobbyByIdAsync(lobbyId, _authClient.DisplayName);
                CurrentLobbySnapshot = ToSnapshot(lobby);
                _lobbyClient.StartPollingLoop();
                UpdateStatus(MultiplayerStatus.LobbyJoined);
                LobbyJoined?.Invoke(CurrentLobbySnapshot);
            }
            catch (Exception ex)
            {
                RaiseError("lobby_join_failed", "Lobby join failed.", ex);
            }
        }

        public async Task LeaveLobbyAsync()
        {
            ClearLastError();

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
            ClearLastError();
            EnsureNetworkDependencies();
            Debug.Log($"[MultiplayerFlow] StartHostFlowAsync begin. lobbyName='{lobbyName}', maxPlayers={maxPlayers}, isPrivate={isPrivate}, status={Status}");
            await CreateLobbyAsHostAsync(lobbyName, maxPlayers, isPrivate);

            try
            {
                UpdateStatus(MultiplayerStatus.RelayAllocating);
                Debug.Log("[MultiplayerFlow] Allocating relay for host.");

                var allocation = await _relayClient.CreateAllocationAsync(maxPlayers - 1);

                if (allocation == null)
                {
                    throw new InvalidOperationException("Relay allocation returned null.");
                }

                var joinCode = await _relayClient.GetJoinCodeAsync(allocation);

                if (string.IsNullOrWhiteSpace(joinCode))
                {
                    throw new InvalidOperationException("Relay join code was empty.");
                }

                Debug.Log($"[MultiplayerFlow] Host relay join code created. code={joinCode}, region={allocation.Region}, allocationId={allocation.AllocationId}");

                await PublishRelayJoinCodeAsync(joinCode);
                Debug.Log("[MultiplayerFlow] Relay join code published to lobby data.");
                ConfigureTransportForHost(allocation);

                UpdateStatus(MultiplayerStatus.NetworkStarting);
                _networkManager.StartHost();

                var summary = new RelayConnectionSummary(
                    allocation.AllocationId.ToString(),
                    joinCode,
                    allocation.Region,
                    RelayConnectionType);

                RelayReady?.Invoke(summary);
                UpdateStatus(MultiplayerStatus.NetworkStarted);
                NetworkStarted?.Invoke(MultiplayerRole.Host);
                ReadyToEnterGame?.Invoke(MultiplayerRole.Host);
            }
            catch (Exception ex)
            {
                RaiseError("host_flow_failed", $"Host flow failed: {ex.Message}", ex);
            }
        }

        public async Task StartClientFlowAsync(string lobbyCode)
        {
            ClearLastError();
            EnsureNetworkDependencies();
            Debug.Log($"[MultiplayerFlow] StartClientFlowAsync begin. lobbyCode={lobbyCode}, status={Status}, currentLobby={(CurrentLobbySnapshot != null ? CurrentLobbySnapshot.Name : "<none>")}");
            await JoinLobbyByCodeAsync(lobbyCode);

            try
            {
                Debug.Log($"[MultiplayerFlow] Joined lobby. snapshot={(CurrentLobbySnapshot != null ? CurrentLobbySnapshot.Name : "<null>")}, relayCode={(CurrentLobbySnapshot != null && !string.IsNullOrWhiteSpace(CurrentLobbySnapshot.RelayJoinCode) ? "present" : "missing")}");
                UpdateStatus(MultiplayerStatus.RelayJoining);
                Debug.Log("[MultiplayerFlow] Waiting for relay join code from lobby updates.");

                var joinCode = await WaitForRelayJoinCodeAsync(TimeSpan.FromSeconds(RelayJoinCodeTimeoutSeconds));
                Debug.Log($"[MultiplayerFlow] Relay join code received. length={joinCode?.Length ?? 0}");
                var joinAllocation = await _relayClient.JoinAllocationAsync(joinCode);
                Debug.Log($"[MultiplayerFlow] Relay allocation joined. region={joinAllocation.Region}, allocationId={joinAllocation.AllocationId}");

                ConfigureTransportForClient(joinAllocation);

                UpdateStatus(MultiplayerStatus.NetworkStarting);
                Debug.Log("[MultiplayerFlow] Starting NetworkManager client.");
                _networkManager.StartClient();

                var summary = new RelayConnectionSummary(
                    joinAllocation.AllocationId.ToString(),
                    joinCode,
                    joinAllocation.Region,
                    RelayConnectionType);

                RelayReady?.Invoke(summary);
                UpdateStatus(MultiplayerStatus.NetworkStarted);
                NetworkStarted?.Invoke(MultiplayerRole.Client);
                ReadyToEnterGame?.Invoke(MultiplayerRole.Client);
                Debug.Log("[MultiplayerFlow] Client relay/network startup completed.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerFlow] Client flow failed: {ex.Message}\n{ex}");
                RaiseError("client_flow_failed", $"Client flow failed: {ex.Message}", ex);
            }
        }

        public void AssignNetworkManager(NetworkManager manager)
        {
            _networkManager = manager;
            _transport = manager != null ? manager.GetComponent<UnityTransport>() : null;

            if (_networkManager != null && _transport != null)
            {
                _networkManager.NetworkConfig.NetworkTransport = _transport;
            }
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
            CurrentLobbySnapshot = ToSnapshot(lobby);
            Debug.Log($"[MultiplayerFlow] Lobby updated: name={CurrentLobbySnapshot.Name}, players={CurrentLobbySnapshot.PlayerCount}/{CurrentLobbySnapshot.MaxPlayers}, relayCode={(string.IsNullOrWhiteSpace(CurrentLobbySnapshot.RelayJoinCode) ? "missing" : "present")}");
            LobbyJoined?.Invoke(CurrentLobbySnapshot);
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

        private async Task PublishRelayJoinCodeAsync(string joinCode)
        {
            if (_lobbyClient.CurrentLobby == null)
            {
                throw new InvalidOperationException("Cannot publish relay join code because the current lobby is missing.");
            }

            Debug.Log($"[MultiplayerFlow] Publishing relay join code. lobbyId={_lobbyClient.CurrentLobby.Id}, lobbyCode={_lobbyClient.CurrentLobby.LobbyCode}, joinCode={joinCode}");
            await _lobbyClient.RefreshCurrentLobbyAsync();

            var data = new Dictionary<string, DataObject>
            {
                {
                    MultiplayerKeys.LobbyDataRelayJoinCodeKey,
                    new DataObject(DataObject.VisibilityOptions.Public, joinCode)
                }
            };

            await _lobbyClient.UpdateLobbyDataAsync(data);
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

        private void ConfigureTransportForHost(Allocation allocation)
        {
            EnsureNetworkDependencies();
            var relayServerData = allocation.ToRelayServerData(RelayConnectionType);
            _transport.SetRelayServerData(relayServerData);
        }

        private void ConfigureTransportForClient(JoinAllocation joinAllocation)
        {
            EnsureNetworkDependencies();
            var relayServerData = joinAllocation.ToRelayServerData(RelayConnectionType);
            _transport.SetRelayServerData(relayServerData);
        }

        private void EnsureNetworkDependencies()
        {
            if (_networkManager == null)
            {
                _networkManager = NetworkManager.Singleton;
            }

            if (_networkManager == null)
            {
                var networkObject = new GameObject("NetworkManager");
                DontDestroyOnLoad(networkObject);
                _networkManager = networkObject.AddComponent<NetworkManager>();
                _transport = networkObject.AddComponent<UnityTransport>();
            }

            if (_networkManager.NetworkConfig == null)
            {
                _networkManager.NetworkConfig = new NetworkConfig();
            }

            if (_transport == null)
            {
                _transport = _networkManager.GetComponent<UnityTransport>();
            }

            if (_transport == null)
            {
                _transport = _networkManager.gameObject.AddComponent<UnityTransport>();
            }

            _networkManager.NetworkConfig.NetworkTransport = _transport;
        }

        private void UpdateStatus(MultiplayerStatus status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }

        private void RaiseError(string code, string message, Exception exception)
        {
            LastError = new MultiplayerError(code, message, exception);
            RecoverStatusAfterError();
            ErrorOccurred?.Invoke(LastError);
        }

        private void BeginWorkflowStep(MultiplayerStatus nextStatus)
        {
            ClearLastError();
            UpdateStatus(nextStatus);
        }

        private void ClearLastError()
        {
            LastError = null;
        }

        private void RecoverStatusAfterError()
        {
            switch (Status)
            {
                case MultiplayerStatus.Initializing:
                    UpdateStatus(MultiplayerStatus.Idle);
                    break;
                case MultiplayerStatus.SigningIn:
                    UpdateStatus(MultiplayerStatus.SignedOut);
                    break;
                case MultiplayerStatus.LobbyQuerying:
                    UpdateStatus(MultiplayerStatus.SignedIn);
                    break;
                case MultiplayerStatus.LobbyJoining:
                    UpdateStatus(MultiplayerStatus.SignedIn);
                    break;
                case MultiplayerStatus.RelayAllocating:
                    UpdateStatus(MultiplayerStatus.LobbyJoined);
                    break;
                case MultiplayerStatus.RelayJoining:
                    UpdateStatus(MultiplayerStatus.LobbyJoined);
                    break;
                case MultiplayerStatus.NetworkStarting:
                    UpdateStatus(MultiplayerStatus.LobbyJoined);
                    break;
            }
        }

        private static LobbySummary ToSummary(Lobby lobby)
        {
            return new LobbySummary(
                lobby.Id,
                lobby.LobbyCode,
                lobby.Name,
                lobby.MaxPlayers,
                lobby.Players?.Count ?? 0,
                lobby.IsPrivate,
                ToData(lobby.Data));
        }

        private static LobbySnapshot ToSnapshot(Lobby lobby)
        {
            var relayJoinCode = lobby.Data != null && lobby.Data.TryGetValue(MultiplayerKeys.LobbyDataRelayJoinCodeKey, out var obj)
                ? obj.Value
                : null;

            var playerNames = lobby.Players == null
                ? new List<string>()
                : lobby.Players.Select(player =>
                {
                    if (player.Data != null &&
                        player.Data.TryGetValue(MultiplayerKeys.PlayerDataDisplayNameKey, out var displayName))
                    {
                        return displayName.Value;
                    }

                    return "Player";
                }).ToList();

            return new LobbySnapshot(
                lobby.Id,
                lobby.LobbyCode,
                lobby.Name,
                lobby.MaxPlayers,
                lobby.Players?.Count ?? 0,
                lobby.IsPrivate,
                relayJoinCode,
                playerNames,
                ToData(lobby.Data));
        }

        private static IReadOnlyDictionary<string, string> ToData(Dictionary<string, DataObject> data)
        {
            if (data == null)
            {
                return new Dictionary<string, string>();
            }

            return data.ToDictionary(pair => pair.Key, pair => pair.Value.Value);
        }

        private static MultiplayerError ValidateSignUpInputs(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new MultiplayerError("signup_username_blank", "Username cannot be blank.");
            }

            if (!UsernamePattern.IsMatch(username))
            {
                return new MultiplayerError(
                    "signup_username_format",
                    "Username can only contain lowercase a-z and numbers. No spaces or special characters are allowed.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return new MultiplayerError("signup_password_blank", "Password cannot be blank.");
            }

            if (!PasswordPattern.IsMatch(password))
            {
                return new MultiplayerError(
                    "signup_password_format",
                    "Password must be at least 8 characters and include uppercase, lowercase, and a special symbol.");
            }

            return null;
        }

        private static MultiplayerError ValidateSignInInputs(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new MultiplayerError("signin_username_blank", "Username cannot be blank.");
            }

            if (!UsernamePattern.IsMatch(username))
            {
                return new MultiplayerError(
                    "signin_username_format",
                    "Username can only contain lowercase a-z and numbers. No spaces or special characters are allowed.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return new MultiplayerError("signin_password_blank", "Password cannot be blank.");
            }

            return null;
        }

        private static string BuildAuthErrorMessage(Exception exception, bool isSignUp)
        {
            if (exception is AuthenticationException authException)
            {
                var detail = authException.Message ?? string.Empty;
                var detailLower = detail.ToLowerInvariant();

                if (isSignUp && (detailLower.Contains("already") || detailLower.Contains("exists") || detailLower.Contains("taken")))
                {
                    return "Username already exists.";
                }

                if (!isSignUp && (detailLower.Contains("invalid username") || detailLower.Contains("invalid password") || detailLower.Contains("invalid credentials") || detailLower.Contains("invalid username or password") || detailLower.Contains("unauthorized")))
                {
                    return "Invalid username or password.";
                }

                if (isSignUp && detailLower.Contains("username"))
                {
                    return "Username can only contain lowercase a-z and numbers. Password must be at least 8 characters and include uppercase, lowercase, and a special symbol.";
                }

                if (isSignUp && detailLower.Contains("password"))
                {
                    return "Password must be at least 8 characters and include uppercase, lowercase, and a special symbol.";
                }
            }

            var message = exception?.Message;
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            return isSignUp ? "Sign-up failed." : "Sign-in failed.";
        }
    }
}
