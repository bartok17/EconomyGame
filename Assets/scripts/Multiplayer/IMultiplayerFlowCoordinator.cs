using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer
{
    public interface IMultiplayerFlowCoordinator
    {
        MultiplayerStatus Status { get; }
        MultiplayerError LastError { get; }
        string LocalPlayerId { get; }
        string LocalDisplayName { get; }
        bool IsLocalPlayerHost { get; }
        LobbySnapshot CurrentLobbySnapshot { get; }
        IReadOnlyList<RememberedAccount> RememberedAccounts { get; }

        event Action<MultiplayerStatus> StatusChanged;
        event Action<IReadOnlyList<RememberedAccount>> RememberedAccountsChanged;
        event Action<string, string> SignedIn;
        event Action<IReadOnlyList<LobbySummary>> LobbyListUpdated;
        event Action<LobbySnapshot> LobbyJoined;
        event Action LobbyLeft;
        event Action<RelayConnectionSummary> RelayReady;
        event Action<MultiplayerRole> NetworkStarted;
        event Action<MultiplayerRole> ReadyToEnterGame;
        event Action<MultiplayerError> ErrorOccurred;

        Task InitializeAsync();
        Task SignUpAsync(string username, string password, string displayName, bool remember = false);
        Task SignInAsync(string username, string password, bool remember = false);
        Task TrySignInRememberedAsync(string accountId);
        void ForgetRememberedAccount(string accountId);
        Task SetDisplayNameAsync(string displayName);
        void SignOut();
        Task QueryLobbiesAsync(int maxResults = 25);
        Task CreateLobbyAsHostAsync(string lobbyName, int maxPlayers = 4, bool isPrivate = false);
        Task JoinLobbyByCodeAsync(string lobbyCode);
        Task JoinLobbyByIdAsync(string lobbyId);
        Task LeaveLobbyAsync();
        Task StartGameAsHostAsync();
        void AssignNetworkManager(NetworkManager manager);
    }
}
