using System;
using System.Collections.Generic;

namespace MonopolyGame.Multiplayer
{
    public enum MultiplayerStatus
    {
        Idle,
        Initializing,
        SignedOut,
        SigningIn,
        SignedIn,
        LobbyQuerying,
        LobbyJoining,
        LobbyJoined,
        RelayAllocating,
        RelayJoining,
        RelayReady,
        NetworkStarting,
        NetworkStarted,
        Error
    }

    public enum MultiplayerRole
    {
        Host,
        Client
    }

    public static class MultiplayerKeys
    {
        public const string LobbyDataRelayJoinCodeKey = "relayJoinCode";
        public const string LobbyDataGameStartedKey = "gameStarted";
        public const string LobbyDataGameVersionKey = "gameVersion";
        public const string PlayerDataDisplayNameKey = "displayName";
    }

    public sealed class MultiplayerError
    {
        public string Code { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public MultiplayerError(string code, string message, Exception exception = null)
        {
            Code = code;
            Message = message;
            Exception = exception;
        }
    }

    public sealed class LobbySummary
    {
        public string LobbyId { get; }
        public string LobbyCode { get; }
        public string Name { get; }
        public int MaxPlayers { get; }
        public int PlayerCount { get; }
        public bool IsPrivate { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        public LobbySummary(
            string lobbyId,
            string lobbyCode,
            string name,
            int maxPlayers,
            int playerCount,
            bool isPrivate,
            IReadOnlyDictionary<string, string> data)
        {
            LobbyId = lobbyId;
            LobbyCode = lobbyCode;
            Name = name;
            MaxPlayers = maxPlayers;
            PlayerCount = playerCount;
            IsPrivate = isPrivate;
            Data = data;
        }
    }

    public sealed class LobbySnapshot
    {
        public string LobbyId { get; }
        public string LobbyCode { get; }
        public string Name { get; }
        public int MaxPlayers { get; }
        public int PlayerCount { get; }
        public bool IsPrivate { get; }
        public string RelayJoinCode { get; }
        public IReadOnlyList<string> PlayerDisplayNames { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        public LobbySnapshot(
            string lobbyId,
            string lobbyCode,
            string name,
            int maxPlayers,
            int playerCount,
            bool isPrivate,
            string relayJoinCode,
            IReadOnlyList<string> playerDisplayNames,
            IReadOnlyDictionary<string, string> data)
        {
            LobbyId = lobbyId;
            LobbyCode = lobbyCode;
            Name = name;
            MaxPlayers = maxPlayers;
            PlayerCount = playerCount;
            IsPrivate = isPrivate;
            RelayJoinCode = relayJoinCode;
            PlayerDisplayNames = playerDisplayNames;
            Data = data;
        }
    }

    public sealed class RelayConnectionSummary
    {
        public string AllocationId { get; }
        public string JoinCode { get; }
        public string Region { get; }
        public string ConnectionType { get; }

        public RelayConnectionSummary(string allocationId, string joinCode, string region, string connectionType)
        {
            AllocationId = allocationId;
            JoinCode = joinCode;
            Region = region;
            ConnectionType = connectionType;
        }
    }
}
