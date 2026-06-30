using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace MonopolyGame.Multiplayer
{
    [Serializable]
    public sealed class MultiplayerStatusEvent : UnityEvent<MultiplayerStatus> { }

    [Serializable]
    public sealed class StringStringEvent : UnityEvent<string, string> { }

    [Serializable]
    public sealed class LobbySummaryListEvent : UnityEvent<LobbySummaryList> { }

    [Serializable]
    public sealed class LobbySnapshotEvent : UnityEvent<LobbySnapshot> { }

    [Serializable]
    public sealed class RoleEvent : UnityEvent<MultiplayerRole> { }

    [Serializable]
    public sealed class RelaySummaryEvent : UnityEvent<RelayConnectionSummary> { }

    [Serializable]
    public sealed class ErrorEvent : UnityEvent<MultiplayerError> { }

    [Serializable]
    public sealed class StringEvent : UnityEvent<string> { }

    [Serializable]
    public sealed class LobbySummaryList
    {
        public LobbySummary[] Items;

        public LobbySummaryList(LobbySummary[] items)
        {
            Items = items;
        }
    }

    public sealed class MultiplayerUnityEventBridge : MonoBehaviour
    {
        [SerializeField] private MultiplayerFlowCoordinator coordinator;

        public MultiplayerStatusEvent StatusChanged = new MultiplayerStatusEvent();
        public StringStringEvent SignedIn = new StringStringEvent();
        public LobbySummaryListEvent LobbyListUpdated = new LobbySummaryListEvent();
        public LobbySnapshotEvent LobbyJoined = new LobbySnapshotEvent();
        public UnityEvent LobbyLeft = new UnityEvent();
        public RelaySummaryEvent RelayReady = new RelaySummaryEvent();
        public RoleEvent NetworkStarted = new RoleEvent();
        public RoleEvent ReadyToEnterGame = new RoleEvent();
        public ErrorEvent ErrorOccurred = new ErrorEvent();
        public StringEvent ErrorMessage = new StringEvent();

        private void OnEnable()
        {
            if (coordinator == null)
            {
                Debug.LogError("[MultiplayerUnityEventBridge] MultiplayerFlowCoordinator not assigned. Wire it in the inspector.");
                enabled = false;
                return;
            }

            coordinator.StatusChanged += HandleStatusChanged;
            coordinator.SignedIn += HandleSignedIn;
            coordinator.LobbyListUpdated += HandleLobbyListUpdated;
            coordinator.LobbyJoined += HandleLobbyJoined;
            coordinator.LobbyLeft += HandleLobbyLeft;
            coordinator.RelayReady += HandleRelayReady;
            coordinator.NetworkStarted += HandleNetworkStarted;
            coordinator.ReadyToEnterGame += HandleReadyToEnterGame;
            coordinator.ErrorOccurred += HandleErrorOccurred;
        }

        private void OnDisable()
        {
            if (coordinator == null)
            {
                return;
            }

            coordinator.StatusChanged -= HandleStatusChanged;
            coordinator.SignedIn -= HandleSignedIn;
            coordinator.LobbyListUpdated -= HandleLobbyListUpdated;
            coordinator.LobbyJoined -= HandleLobbyJoined;
            coordinator.LobbyLeft -= HandleLobbyLeft;
            coordinator.RelayReady -= HandleRelayReady;
            coordinator.NetworkStarted -= HandleNetworkStarted;
            coordinator.ReadyToEnterGame -= HandleReadyToEnterGame;
            coordinator.ErrorOccurred -= HandleErrorOccurred;
        }

        private void HandleStatusChanged(MultiplayerStatus status)
        {
            StatusChanged.Invoke(status);
        }

        private void HandleSignedIn(string playerId, string displayName)
        {
            SignedIn.Invoke(playerId, displayName);
        }

        private void HandleLobbyListUpdated(IReadOnlyList<LobbySummary> lobbies)
        {
            LobbyListUpdated.Invoke(new LobbySummaryList(lobbies is null ? Array.Empty<LobbySummary>() : lobbies.ToArray()));
        }

        private void HandleLobbyJoined(LobbySnapshot snapshot)
        {
            LobbyJoined.Invoke(snapshot);
        }

        private void HandleLobbyLeft()
        {
            LobbyLeft.Invoke();
        }

        private void HandleRelayReady(RelayConnectionSummary summary)
        {
            RelayReady.Invoke(summary);
        }

        private void HandleNetworkStarted(MultiplayerRole role)
        {
            NetworkStarted.Invoke(role);
        }

        private void HandleReadyToEnterGame(MultiplayerRole role)
        {
            ReadyToEnterGame.Invoke(role);
        }

        private void HandleErrorOccurred(MultiplayerError error)
        {
            ErrorOccurred.Invoke(error);

            if (error == null)
            {
                ErrorMessage.Invoke("Unknown multiplayer error.");
                return;
            }

            var message = string.IsNullOrWhiteSpace(error.Message)
                ? error.Code
                : error.Message;

            ErrorMessage.Invoke(message);
        }
    }
}
