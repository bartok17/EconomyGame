using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies.Models;

namespace MonopolyGame.Multiplayer
{
    public interface ILobbyClient
    {
        Lobby CurrentLobby { get; }

        event Action<Lobby> LobbyUpdated;
        event Action LobbyLeft;

        Task<IReadOnlyList<Lobby>> QueryLobbiesAsync(int maxResults = 25);
        Task<Lobby> CreateLobbyAsync(string lobbyName, int maxPlayers, bool isPrivate, string displayName);
        Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode, string displayName);
        Task<Lobby> JoinLobbyByIdAsync(string lobbyId, string displayName);
        Task LeaveLobbyAsync(string playerId);
        Task UpdateLobbyDataAsync(Dictionary<string, DataObject> data);
        Task<Lobby> RefreshCurrentLobbyAsync();
        void StartHeartbeatLoop();
        void StartPollingLoop();
        void StopLoops();
    }
}
