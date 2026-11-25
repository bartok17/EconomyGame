using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public enum ParticipantSessionStatus : byte
    {
        Connected = 0,
        Disconnected = 1,
        Eliminated = 2
    }

    public struct ParticipantNetState : INetworkSerializable, IEquatable<ParticipantNetState>
    {
        public int PawnSlot;
        public ulong ClientId;
        public byte Status;

        public ParticipantNetState(int pawnSlot, ulong clientId, ParticipantSessionStatus status)
        {
            PawnSlot = pawnSlot;
            ClientId = clientId;
            Status = (byte)status;
        }

        public ParticipantSessionStatus SessionStatus => (ParticipantSessionStatus)Status;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PawnSlot);
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Status);
        }

        public bool Equals(ParticipantNetState other)
        {
            return PawnSlot == other.PawnSlot &&
                   ClientId == other.ClientId &&
                   Status == other.Status;
        }
    }

    public sealed partial class MultiplayerGameSessionController
    {
        [Header("Disconnect & Reconnect")]
        [SerializeField] private float disconnectTimeoutSeconds = 60f;

        private readonly Dictionary<int, Coroutine> disconnectCoroutines = new Dictionary<int, Coroutine>();
        private bool isEndingGame;
        private bool isReturningToMenu;
        private string localStatusOverrideMessage = string.Empty;

        public event Action ParticipantsChanged;

        // ── Lifecycle wiring ────────────────────────────────────────────────────────

        private void InitializeReconnectCallbacks()
        {
            isEndingGame = false;
            isReturningToMenu = false;
            localStatusOverrideMessage = string.Empty;

            if (participantStatesNet != null)
            {
                participantStatesNet.OnListChanged += HandleParticipantStatesChanged;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            PlayerPawnNetworkSync.OwnershipChanged += HandlePawnOwnershipChanged;

            if (!IsServer)
            {
                StartCoroutine(ClientIdentityAnnouncementRoutine());
            }
        }

        private void CleanupReconnectCallbacks()
        {
            if (participantStatesNet != null)
            {
                participantStatesNet.OnListChanged -= HandleParticipantStatesChanged;
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            PlayerPawnNetworkSync.OwnershipChanged -= HandlePawnOwnershipChanged;

            foreach (Coroutine routine in disconnectCoroutines.Values)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
            }

            disconnectCoroutines.Clear();
        }

        // ── Public queries & leave API ──────────────────────────────────────────────

        public ParticipantSessionStatus GetParticipantStatus(int pawnSlot)
        {
            if (IsServer && turnStateMachine.ParticipantCount > 0)
            {
                TurnParticipant participant = turnStateMachine.GetParticipant(pawnSlot);
                if (participant != null)
                {
                    if (participant.IsEliminated) return ParticipantSessionStatus.Eliminated;
                    return participant.IsConnected ? ParticipantSessionStatus.Connected : ParticipantSessionStatus.Disconnected;
                }
            }

            if (participantStatesNet != null)
            {
                for (int i = 0; i < participantStatesNet.Count; i++)
                {
                    if (participantStatesNet[i].PawnSlot == pawnSlot)
                    {
                        return participantStatesNet[i].SessionStatus;
                    }
                }
            }

            return ParticipantSessionStatus.Connected;
        }

        public bool IsParticipantEliminated(int pawnSlot)
        {
            return GetParticipantStatus(pawnSlot) == ParticipantSessionStatus.Eliminated;
        }

        public string GetRoomCode()
        {
            MultiplayerFlowCoordinator activeCoordinator = coordinator != null ? coordinator : MultiplayerFlowCoordinator.Instance;

            if (activeCoordinator != null &&
                activeCoordinator.CurrentLobbySnapshot != null &&
                !string.IsNullOrWhiteSpace(activeCoordinator.CurrentLobbySnapshot.LobbyCode))
            {
                return activeCoordinator.CurrentLobbySnapshot.LobbyCode;
            }

            return activeCoordinator != null ? activeCoordinator.LastActiveLobbyCode : string.Empty;
        }

        public void RequestLeaveGame()
        {
            if (isReturningToMenu)
            {
                return;
            }

            if (IsServer)
            {
                isEndingGame = true;
                _turnResolution?.SetEconomyMessage("Host ended the match.");
                StartCoroutine(EndGameDelayedRoutine(1.5f, "None (Host left)"));
            }
            else
            {
                isReturningToMenu = true;
                StartCoroutine(ClientLeaveRoutine());
            }
        }

        // ── Synced participant states ───────────────────────────────────────────────

        private void InitializeParticipantStatesNet()
        {
            if (!IsServer || participantStatesNet == null)
            {
                return;
            }

            participantStatesNet.Clear();

            for (int i = 0; i < turnStateMachine.Participants.Count; i++)
            {
                TurnParticipant p = turnStateMachine.Participants[i];
                ParticipantSessionStatus status = p.IsEliminated
                    ? ParticipantSessionStatus.Eliminated
                    : (p.IsConnected ? ParticipantSessionStatus.Connected : ParticipantSessionStatus.Disconnected);

                participantStatesNet.Add(new ParticipantNetState(p.TurnIndex, p.ClientId, status));
            }
        }

        private void SyncParticipantNetState(int slotIndex)
        {
            if (!IsServer || participantStatesNet == null)
            {
                return;
            }

            TurnParticipant p = turnStateMachine.GetParticipant(slotIndex);
            if (p == null)
            {
                return;
            }

            ParticipantSessionStatus status = p.IsEliminated
                ? ParticipantSessionStatus.Eliminated
                : (p.IsConnected ? ParticipantSessionStatus.Connected : ParticipantSessionStatus.Disconnected);

            ParticipantNetState updated = new ParticipantNetState(p.TurnIndex, p.ClientId, status);

            for (int i = 0; i < participantStatesNet.Count; i++)
            {
                if (participantStatesNet[i].PawnSlot == slotIndex)
                {
                    participantStatesNet[i] = updated;
                    return;
                }
            }

            participantStatesNet.Add(updated);
        }

        private bool TryGetSlotFromParticipantStatesNet(ulong clientId, out int pawnSlot)
        {
            if (participantStatesNet != null)
            {
                for (int i = 0; i < participantStatesNet.Count; i++)
                {
                    ParticipantNetState state = participantStatesNet[i];
                    if (state.SessionStatus == ParticipantSessionStatus.Connected && state.ClientId == clientId)
                    {
                        pawnSlot = state.PawnSlot;
                        return true;
                    }
                }
            }

            pawnSlot = -1;
            return false;
        }

        private void HandleParticipantStatesChanged(NetworkListEvent<ParticipantNetState> _)
        {
            ParticipantsChanged?.Invoke();
            EconomyChanged?.Invoke();
        }

        private void HandlePawnOwnershipChanged(PlayerPawnNetworkSync _)
        {
            ParticipantsChanged?.Invoke();
            EconomyChanged?.Invoke();
            ActionCardsChanged?.Invoke();
        }

        // ── Client announcement & Reconnect on Server ───────────────────────────────

        private IEnumerator ClientIdentityAnnouncementRoutine()
        {
            while (!IsSpawned ||
                   !IsInitialized ||
                   NetworkManager.Singleton == null ||
                   !NetworkManager.Singleton.IsConnectedClient)
            {
                if (isEndingGame || isReturningToMenu)
                {
                    yield break;
                }

                yield return null;
            }

            float waitDeadline = Time.unscaledTime + 1f;
            while (Time.unscaledTime < waitDeadline)
            {
                if (coordinator == null)
                {
                    coordinator = MultiplayerFlowCoordinator.Instance ?? FindAnyObjectByType<MultiplayerFlowCoordinator>();
                }

                if (coordinator != null && !string.IsNullOrWhiteSpace(coordinator.LocalPlayerId))
                {
                    break;
                }

                yield return null;
            }

            string authPlayerId = coordinator != null ? coordinator.LocalPlayerId : string.Empty;
            string displayName = coordinator != null ? coordinator.LocalDisplayName : string.Empty;

            if (!isEndingGame && !isReturningToMenu && IsSpawned)
            {
                AnnouncePlayerIdentityServerRpc(authPlayerId, displayName);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void AnnouncePlayerIdentityServerRpc(
            string authPlayerId,
            string displayName,
            ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer || !IsInitialized || isEndingGame)
            {
                return;
            }

            ulong senderClientId = serverRpcParams.Receive.SenderClientId;

            if (turnStateMachine.FindConnectedParticipantByClientId(senderClientId) != null)
            {
                return;
            }

            HandlePlayerReconnectedOnServer(senderClientId, authPlayerId, displayName);
        }

        private void HandlePlayerReconnectedOnServer(ulong newClientId, string authPlayerId, string displayName)
        {
            TurnParticipant participant = turnStateMachine.FindReconnectCandidate(authPlayerId, displayName);

            if (participant == null || participant.IsEliminated)
            {
                Debug.LogWarning($"[Reconnect] Rejected client {newClientId} ('{displayName}', auth='{authPlayerId}').");
                RejectReconnectClientRpc(
                    "Unable to rejoin this match.",
                    BuildSingleClientParams(newClientId));
                return;
            }

            int slot = participant.TurnIndex;
            bool wasOfflineOrDifferentClient = !participant.IsConnected || participant.ClientId != newClientId;

            if (disconnectCoroutines.TryGetValue(slot, out Coroutine timer) && timer != null)
            {
                StopCoroutine(timer);
                disconnectCoroutines.Remove(slot);
            }

            turnStateMachine.UpdateParticipantClientId(slot, newClientId);
            SyncParticipantNetState(slot);

            PlayerPawnNetworkSync pawnSync = Registry.FindBySlot(slot);
            if (pawnSync != null)
            {
                pawnSync.SetEliminated(false);

                if (pawnSync.NetworkObject != null && pawnSync.NetworkObject.IsSpawned)
                {
                    pawnSync.NetworkObject.DontDestroyWithOwner = true;

                    if (pawnSync.NetworkObject.OwnerClientId != newClientId)
                    {
                        pawnSync.NetworkObject.ChangeOwnership(newClientId);
                    }
                }

                pawnSync.ForceSpaceIndex(pawnSync.CurrentSpaceIndex);
            }

            if (CurrentTurnIndex == slot)
            {
                SetServerState(turnStateMachine.State);
            }
            else
            {
                PublishTurnState();
            }

            SendActionCardHandToClient(slot, _actionCardService.GetHandEntries(slot), newClientId);
            ConfirmReconnectClientRpc(slot, BuildSingleClientParams(newClientId));

            if (wasOfflineOrDifferentClient)
            {
                _turnResolution?.SetEconomyMessage($"{participant.DisplayName} reconnected to the game!");
            }

            ParticipantsChanged?.Invoke();
        }

        [ClientRpc]
        private void ConfirmReconnectClientRpc(int slotIndex, ClientRpcParams clientRpcParams = default)
        {
            Registry.Refresh();
            ApplyPropertyOwnersToBoard();
            SyncLocalStateFromNetwork();
            ParticipantsChanged?.Invoke();
            EconomyChanged?.Invoke();
            ActionCardsChanged?.Invoke();
            Debug.Log($"[Reconnect] Confirmed reconnection to pawn slot {slotIndex}.");
        }

        [ClientRpc]
        private void RejectReconnectClientRpc(string reason, ClientRpcParams clientRpcParams = default)
        {
            isEndingGame = true;
            localStatusOverrideMessage = string.IsNullOrWhiteSpace(reason)
                ? "Cannot rejoin this match. Returning to menu..."
                : $"{reason} Returning to menu...";
            EconomyChanged?.Invoke();
            StartCoroutine(ReturnToMenuDelayedRoutine(2.5f));
        }

        // ── Disconnect handling & elimination ───────────────────────────────────────

        private void HandleClientDisconnected(ulong disconnectedClientId)
        {
            if (isEndingGame || isReturningToMenu)
            {
                return;
            }

            if (IsServer)
            {
                if (disconnectedClientId == NetworkManager.ServerClientId)
                {
                    return;
                }

                TurnParticipant participant = turnStateMachine.FindConnectedParticipantByClientId(disconnectedClientId);
                if (participant != null)
                {
                    HandlePlayerDisconnectedOnServer(participant.TurnIndex);
                }
            }
            else
            {
                ulong localClientId = NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : ulong.MaxValue;

                if (disconnectedClientId == localClientId || disconnectedClientId == NetworkManager.ServerClientId)
                {
                    isEndingGame = true;
                    localStatusOverrideMessage = "Host disconnected. Returning to menu...";
                    EconomyChanged?.Invoke();
                    StartCoroutine(ReturnToMenuDelayedRoutine(2.5f));
                }
            }
        }

        private void HandlePlayerDisconnectedOnServer(int slotIndex)
        {
            if (isEndingGame || turnStateMachine.IsParticipantEliminated(slotIndex))
            {
                return;
            }

            if (!turnStateMachine.IsParticipantConnected(slotIndex) && disconnectCoroutines.ContainsKey(slotIndex))
            {
                return;
            }

            turnStateMachine.SetParticipantConnected(slotIndex, false);
            SyncParticipantNetState(slotIndex);

            string playerName = turnStateMachine.GetParticipantName(slotIndex);
            _turnResolution?.SetEconomyMessage($"{playerName} disconnected! Reconnect time: {disconnectTimeoutSeconds:0}s...");

            if (disconnectCoroutines.TryGetValue(slotIndex, out Coroutine existing) && existing != null)
            {
                StopCoroutine(existing);
            }

            disconnectCoroutines[slotIndex] = StartCoroutine(DisconnectedPlayerCountdownRoutine(slotIndex, disconnectTimeoutSeconds));
            ParticipantsChanged?.Invoke();
        }

        private IEnumerator DisconnectedPlayerCountdownRoutine(int slotIndex, float duration)
        {
            float remaining = duration;
            string playerName = turnStateMachine.GetParticipantName(slotIndex);

            while (remaining > 0f)
            {
                yield return new WaitForSeconds(1f);

                if (isEndingGame || turnStateMachine.IsParticipantConnected(slotIndex) || turnStateMachine.IsParticipantEliminated(slotIndex))
                {
                    disconnectCoroutines.Remove(slotIndex);
                    yield break;
                }

                remaining -= 1f;
                if (remaining == 45f || remaining == 30f || remaining == 20f || remaining == 15f || (remaining <= 10f && remaining > 0f))
                {
                    _turnResolution?.SetEconomyMessage($"{playerName} offline. Time remaining: {remaining:0}s");
                }
            }

            EliminatePlayerOnServer(slotIndex, "timeout expired");
        }

        private void EliminatePlayerOnServer(int slotIndex, string reason)
        {
            if (!IsServer || isEndingGame || turnStateMachine.IsParticipantEliminated(slotIndex))
            {
                return;
            }

            if (disconnectCoroutines.TryGetValue(slotIndex, out Coroutine timer) && timer != null)
            {
                StopCoroutine(timer);
                disconnectCoroutines.Remove(slotIndex);
            }

            string playerName = turnStateMachine.GetParticipantName(slotIndex);
            turnStateMachine.MarkParticipantEliminated(slotIndex);
            SyncParticipantNetState(slotIndex);

            CleanupPaymentsForEliminatedPlayer(slotIndex);
            _actionCardService?.ClearHand(slotIndex);
            ReleaseEliminatedPlayerAssets(slotIndex);

            PlayerPawnNetworkSync pawnSync = Registry.FindBySlot(slotIndex);
            if (pawnSync != null)
            {
                pawnSync.SetEliminated(true);
            }

            if (CurrentTurnIndex == slotIndex)
            {
                pendingPurchaseSpaceIndexNet.Value = -1;
                pendingCardMoves.Clear();

                if (turnCoroutine != null)
                {
                    StopCoroutine(turnCoroutine);
                    turnCoroutine = null;
                }

                if (cardResolutionCoroutine != null)
                {
                    StopCoroutine(cardResolutionCoroutine);
                    cardResolutionCoroutine = null;
                }

                if (isResolvingCard || cardResolutionNet.Value)
                {
                    EndCardResolution();
                }
            }

            _turnResolution?.SetEconomyMessage($"{playerName} eliminated ({reason}).");
            ParticipantsChanged?.Invoke();

            if (turnStateMachine.ActiveParticipantCount <= 1)
            {
                TurnParticipant winner = turnStateMachine.GetFirstActiveParticipant();
                string winnerName = winner != null ? winner.DisplayName : "None";
                _turnResolution?.SetEconomyMessage($"Game Over! Winner: {winnerName}!");
                StartCoroutine(EndGameDelayedRoutine(4f, winnerName));
                return;
            }

            if (CurrentTurnIndex == slotIndex)
            {
                AdvanceTurnServer();
            }
        }

        private void CleanupPaymentsForEliminatedPlayer(int slotIndex)
        {
            queuedPayments.RemoveAll(p => p.PayerPawnSlot == slotIndex || p.RecipientPawnSlot == slotIndex);

            PendingPaymentState activePayment = pendingPaymentNet.Value;
            if (activePayment.HasPayment &&
                (activePayment.PayerPawnSlot == slotIndex || activePayment.RecipientPawnSlot == slotIndex))
            {
                PromoteNextPayment();
            }
        }

        private void ReleaseEliminatedPlayerAssets(int slotIndex)
        {
            _economyService?.SetBalance(slotIndex, 0);

            if (propertyOwnershipNet != null)
            {
                for (int i = 0; i < propertyOwnershipNet.Count; i++)
                {
                    if (propertyOwnershipNet[i].OwnerPawnSlot == slotIndex)
                    {
                        propertyOwnershipNet[i] = new PropertyOwnershipState(
                            propertyOwnershipNet[i].SpaceIndex,
                            -1,
                            string.Empty,
                            string.Empty);
                    }
                }

                ApplyPropertyOwnersToBoard();
            }

            EconomyChanged?.Invoke();
        }

        // ── Forfeit & Match Shutdown ────────────────────────────────────────────────

        private IEnumerator ClientLeaveRoutine()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                NotifyPlayerForfeitServerRpc();
                yield return new WaitForSeconds(0.15f);
            }

            yield return ReturnToMenuRoutine();
        }

        [ServerRpc(RequireOwnership = false)]
        private void NotifyPlayerForfeitServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer || isEndingGame)
            {
                return;
            }

            TurnParticipant participant = turnStateMachine.FindConnectedParticipantByClientId(serverRpcParams.Receive.SenderClientId);
            if (participant != null)
            {
                EliminatePlayerOnServer(participant.TurnIndex, "left the game");
            }
        }

        private IEnumerator EndGameDelayedRoutine(float delay, string winnerName)
        {
            isEndingGame = true;

            foreach (Coroutine routine in disconnectCoroutines.Values)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
            }
            disconnectCoroutines.Clear();

            NotifyGameEndedClientRpc(winnerName);
            yield return new WaitForSeconds(delay);
            ReturnToMenu();
        }

        [ClientRpc]
        private void NotifyGameEndedClientRpc(string winnerName)
        {
            isEndingGame = true;

            if (!IsServer)
            {
                localStatusOverrideMessage = $"Game Over! Winner: {winnerName}";
                EconomyChanged?.Invoke();

                if (!isReturningToMenu)
                {
                    StartCoroutine(ReturnToMenuDelayedRoutine(3.5f));
                }
            }
        }

        private IEnumerator ReturnToMenuDelayedRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToMenu();
        }

        public void ReturnToMenu()
        {
            if (isReturningToMenu && !IsServer)
            {
                // Avoid starting multiple load routines if already leaving
            }

            StartCoroutine(ReturnToMenuRoutine());
        }

        private IEnumerator ReturnToMenuRoutine()
        {
            isReturningToMenu = true;

            if (NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }

            MultiplayerFlowCoordinator activeCoordinator = coordinator != null ? coordinator : MultiplayerFlowCoordinator.Instance;
            if (activeCoordinator != null)
            {
                System.Threading.Tasks.Task leaveTask = activeCoordinator.LeaveLobbyAsync();
                while (!leaveTask.IsCompleted)
                {
                    yield return null;
                }
            }

            SceneManager.LoadScene("Menu", LoadSceneMode.Single);
        }

        private static ClientRpcParams BuildSingleClientParams(ulong targetClientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };
        }
    }
}