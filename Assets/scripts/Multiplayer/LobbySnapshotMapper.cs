using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;

namespace MonopolyGame.Multiplayer
{
    public static class LobbySnapshotMapper
    {
        public static LobbySummary ToSummary(Lobby lobby)
        {
            if (lobby == null)
            {
                return null;
            }

            return new LobbySummary(
                lobby.Id,
                lobby.LobbyCode,
                lobby.Name,
                lobby.MaxPlayers,
                lobby.Players?.Count ?? 0,
                lobby.IsPrivate,
                ToData(lobby.Data));
        }

        public static LobbySnapshot ToSnapshot(Lobby lobby)
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

        public static bool IsGameStarted(LobbySnapshot snapshot)
        {
            return snapshot?.Data != null &&
                   snapshot.Data.TryGetValue(MultiplayerKeys.LobbyDataGameStartedKey, out var value) &&
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildAuthErrorMessage(Exception exception, bool isSignUp)
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

                if (detail.Contains("password"))
                {
                    return "Password is not allowed. Try a different password.";
                }

                if (detail.Contains("username"))
                {
                    return "Username is not allowed. Try a different username.";
                }

                return isSignUp ? authException.Message : "Invalid username or password.";
            }

            var message = exception?.Message ?? "An unknown error occurred.";
            var messageLower = message.ToLowerInvariant();

            if (!isSignUp)
            {
                return "Invalid username or password.";
            }

            if (messageLower.Contains("username"))
            {
                return "Username can only contain lowercase a-z and numbers.";
            }

            if (messageLower.Contains("password"))
            {
                return "Password must be at least 8 characters and include uppercase, lowercase, and a special symbol.";
            }

            return message;
        }

        private static IReadOnlyDictionary<string, string> ToData(Dictionary<string, DataObject> data)
        {
            if (data == null)
            {
                return new Dictionary<string, string>();
            }

            return data.ToDictionary(pair => pair.Key, pair => pair.Value.Value);
        }
    }
}
