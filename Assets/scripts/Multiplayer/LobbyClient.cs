using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    public sealed class LobbyClient : ILobbyClient
    {
        private const int DefaultQueryCount = 25;
        private const float DefaultHeartbeatSeconds = 15f;
        private const float DefaultPollSeconds = 3f;

        private CancellationTokenSource _heartbeatCts;
        private CancellationTokenSource _pollCts;

        public Lobby CurrentLobby { get; private set; }

        public event Action<Lobby> LobbyUpdated;
        public event Action LobbyLeft;

        public async Task<IReadOnlyList<Lobby>> QueryLobbiesAsync(int maxResults = DefaultQueryCount)
        {
            var options = new QueryLobbiesOptions
            {
                Count = maxResults
            };

            var result = await LobbyService.Instance.QueryLobbiesAsync(options);
            return result.Results;
        }

        public async Task<Lobby> CreateLobbyAsync(string lobbyName, int maxPlayers, bool isPrivate, string displayName)
        {
            var createOptions = new CreateLobbyOptions
            {
                IsPrivate = isPrivate,
                Player = BuildPlayer(displayName)
            };

            CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createOptions);
            LobbyUpdated?.Invoke(CurrentLobby);
            return CurrentLobby;
        }

        public async Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode, string displayName)
        {
            Debug.Log($"[LobbyClient] Joining lobby by code: {lobbyCode}");
            var joinOptions = new JoinLobbyByCodeOptions
            {
                Player = BuildPlayer(displayName)
            };

            try
            {
                CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);
            }
            catch (LobbyServiceException ex) when (ex.Reason == LobbyExceptionReason.LobbyConflict || ex.ErrorCode == 16003)
            {
                Debug.Log("[LobbyClient] Player already in lobby in UGS. Recovering lobby membership by code.");
                CurrentLobby = await TryRecoverLobbyFromConflictAsync(
                    lobby => string.Equals(lobby.LobbyCode, lobbyCode, StringComparison.OrdinalIgnoreCase));

                if (CurrentLobby == null)
                {
                    throw;
                }
            }
            
            Debug.Log($"[LobbyClient] Joined lobby: id={CurrentLobby.Id}, code={CurrentLobby.LobbyCode}, name={CurrentLobby.Name}, players={CurrentLobby.Players?.Count ?? 0}/{CurrentLobby.MaxPlayers}");
            LobbyUpdated?.Invoke(CurrentLobby);
            return CurrentLobby;
        }

        public async Task<Lobby> JoinLobbyByIdAsync(string lobbyId, string displayName)
        {
            Debug.Log($"[LobbyClient] Joining lobby by id: {lobbyId}");
            var joinOptions = new JoinLobbyByIdOptions
            {
                Player = BuildPlayer(displayName)
            };

            try
            {
                CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, joinOptions);
            }
            catch (LobbyServiceException ex) when (ex.Reason == LobbyExceptionReason.LobbyConflict || ex.ErrorCode == 16003)
            {
                Debug.Log("[LobbyClient] Player already in lobby in UGS. Recovering lobby membership by id.");
                CurrentLobby = await TryRecoverLobbyFromConflictAsync(lobby => lobby.Id == lobbyId);

                if (CurrentLobby == null)
                {
                    throw;
                }
            }
            
            Debug.Log($"[LobbyClient] Joined lobby: id={CurrentLobby.Id}, code={CurrentLobby.LobbyCode}, name={CurrentLobby.Name}, players={CurrentLobby.Players?.Count ?? 0}/{CurrentLobby.MaxPlayers}");
            LobbyUpdated?.Invoke(CurrentLobby);
            return CurrentLobby;
        }

        public async Task LeaveLobbyAsync(string playerId)
        {
            StopLoops();
            
            if (CurrentLobby == null)
            {
                return;
            }

            string lobbyId = CurrentLobby.Id;
            bool isHost = string.Equals(CurrentLobby.HostId, playerId, StringComparison.Ordinal);
            
            try
            {
                if (isHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
                }
            }
            catch (LobbyServiceException ex) when (
                ex.Reason == LobbyExceptionReason.LobbyNotFound ||
                ex.Reason == LobbyExceptionReason.PlayerNotFound ||
                ex.Reason == LobbyExceptionReason.EntityNotFound)
            {
                Debug.Log($"[LobbyClient] Lobby or player already gone while leaving ({ex.Reason}).");
            }
            finally
            {
                CurrentLobby = null;
                LobbyLeft?.Invoke();
            }
        }

        public async Task UpdateLobbyDataAsync(Dictionary<string, DataObject> data)
        {
            if (CurrentLobby == null)
            {
                throw new InvalidOperationException("Cannot update lobby data because no current lobby is set.");
            }

            if (data == null || data.Count == 0)
            {
                throw new ArgumentException("Lobby data update payload cannot be empty.", nameof(data));
            }

            var options = new UpdateLobbyOptions
            {
                Data = data
            };

            CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, options);
            LobbyUpdated?.Invoke(CurrentLobby);
        }

        public async Task<Lobby> RefreshCurrentLobbyAsync()
        {
            if (CurrentLobby == null)
            {
                throw new InvalidOperationException("Cannot refresh lobby because no current lobby is set.");
            }

            Debug.Log($"[LobbyClient] Refreshing current lobby: id={CurrentLobby.Id}, code={CurrentLobby.LobbyCode}");
            CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
            Debug.Log($"[LobbyClient] Refreshed lobby: id={CurrentLobby.Id}, code={CurrentLobby.LobbyCode}, dataKeys={(CurrentLobby.Data != null ? string.Join(",", CurrentLobby.Data.Keys) : "<none>")}");
            LobbyUpdated?.Invoke(CurrentLobby);
            return CurrentLobby;
        }

        public void StartHeartbeatLoop()
        {
            if (_heartbeatCts != null)
            {
                return;
            }

            _heartbeatCts = new CancellationTokenSource();
            _ = HeartbeatLoopAsync(_heartbeatCts.Token);
        }

        public void StartPollingLoop()
        {
            if (_pollCts != null)
            {
                return;
            }

            _pollCts = new CancellationTokenSource();
            _ = PollLoopAsync(_pollCts.Token);
        }

        public void StopLoops()
        {
            _heartbeatCts?.Cancel();
            _heartbeatCts?.Dispose();
            _heartbeatCts = null;

            _pollCts?.Cancel();
            _pollCts?.Dispose();
            _pollCts = null;
        }

        private async Task HeartbeatLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (CurrentLobby != null)
                {
                    try
                    {
                        await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Lobby heartbeat failed: {ex.Message}");
                    }
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(DefaultHeartbeatSeconds), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task PollLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (CurrentLobby != null)
                {
                    try
                    {
                        CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                        Debug.Log($"[LobbyClient] Poll update: id={CurrentLobby.Id}, code={CurrentLobby.LobbyCode}, dataKeys={(CurrentLobby.Data != null ? string.Join(",", CurrentLobby.Data.Keys) : "<none>")}");
                        LobbyUpdated?.Invoke(CurrentLobby);
                    }
                    catch (LobbyServiceException ex)
                    {
                        Debug.LogWarning($"Lobby poll failed: {ex.Message}");
                        CurrentLobby = null;
                        LobbyLeft?.Invoke();
                        break;
                    }
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(DefaultPollSeconds), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
        
        private async Task<Lobby> TryRecoverLobbyFromConflictAsync(Func<Lobby, bool> predicate)
        {
            try
            {
                List<string> joinedIds = await LobbyService.Instance.GetJoinedLobbiesAsync();
                if (joinedIds != null)
                {
                    for (int i = 0; i < joinedIds.Count; i++)
                    {
                        try
                        {
                            Lobby candidate = await LobbyService.Instance.GetLobbyAsync(joinedIds[i]);
                            if (candidate != null && predicate(candidate))
                            {
                                return await ReconnectOrUseLobbyAsync(candidate);
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Debug.LogWarning($"[LobbyClient] Failed checking joined lobby '{joinedIds[i]}': {innerEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyClient] GetJoinedLobbiesAsync failed: {ex.Message}");
            }

            IReadOnlyList<Lobby> publicLobbies = await QueryLobbiesAsync();
            for (int i = 0; i < publicLobbies.Count; i++)
            {
                if (predicate(publicLobbies[i]))
                {
                    Lobby fetched = await LobbyService.Instance.GetLobbyAsync(publicLobbies[i].Id);
                    return await ReconnectOrUseLobbyAsync(fetched);
                }
            }

            return null;
        }

        private static async Task<Lobby> ReconnectOrUseLobbyAsync(Lobby candidate)
        {
            if (candidate == null)
            {
                return null;
            }

            try
            {
                return await LobbyService.Instance.ReconnectToLobbyAsync(candidate.Id);
            }
            catch (LobbyServiceException ex)
            {
                Debug.Log($"[LobbyClient] ReconnectToLobbyAsync fallback to fetched lobby ({ex.Reason}).");
                return candidate;
            }
        }

        private static Player BuildPlayer(string displayName)
        {
            var playerData = new Dictionary<string, PlayerDataObject>
            {
                {
                    MultiplayerKeys.PlayerDataDisplayNameKey,
                    new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, displayName ?? string.Empty)
                }
            };

            return new Player
            {
                Data = playerData
            };
        }
    }
}
