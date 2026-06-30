using System;
using System.Collections;
using System.Collections.Generic;
using MonopolyGame.Board;
using MonopolyGame.Multiplayer;
using MonopolyGame.Pawns;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Server-authoritative game session coordinator.
    /// Owns the NetworkVariables, bootstraps the session, and orchestrates turn flow.
    /// Pawn tracking → <see cref="GameSessionPawnRegistry"/>.
    /// Turn logic     → <see cref="TurnStateMachine"/>.
    /// Board rules    → <see cref="BoardRuleResolver"/>.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MultiplayerGameSessionController : NetworkBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MultiplayerFlowCoordinator coordinator;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private PlayerPawnSpawner pawnSpawner;

        [Header("Gameplay")]
        [SerializeField] private int startSpaceIndex = 0;
        [SerializeField] private float pawnMoveDuration = 0.45f;
        [SerializeField] private float resolveDelay = 0.35f;
        [SerializeField] private bool autoAdvanceTurns;

        private GameSessionPawnRegistry pawnRegistry;
        private readonly TurnStateMachine turnStateMachine = new TurnStateMachine();
        private readonly BoardRuleResolver boardRuleResolver = new BoardRuleResolver();

        private Coroutine bootstrapCoroutine;
        private Coroutine turnCoroutine;

        // ── NetworkVariables ────────────────────────────────────────────────────────

        private readonly NetworkVariable<int> currentTurnIndexNet = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> currentPhaseNet = new NetworkVariable<int>(
            (int)TurnPhase.WaitingForSetup, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> lastDiceRollNet = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString64Bytes> activePlayerNameNet = new NetworkVariable<FixedString64Bytes>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> activeClientIdNet = new NetworkVariable<ulong>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> initializedNet = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ── Public state ────────────────────────────────────────────────────────────

        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.WaitingForSetup;
        public int CurrentTurnIndex { get; private set; }
        public int LastDiceRoll { get; private set; }
        public string ActivePlayerName { get; private set; } = string.Empty;
        public ulong ActiveClientId { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool IsHostAuthority => IsServer;

        public event Action<TurnPhase> PhaseChanged;
        public event Action<int, string> TurnChanged;
        public event Action<int> DiceRolled;
        public event Action<PlayerPawn, int> PawnMoved;

        // ── NGO lifecycle ───────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            currentTurnIndexNet.OnValueChanged += HandleTurnIndexChanged;
            currentPhaseNet.OnValueChanged += HandlePhaseChanged;
            lastDiceRollNet.OnValueChanged += HandleDiceRollChanged;
            activePlayerNameNet.OnValueChanged += HandleActivePlayerChanged;
            activeClientIdNet.OnValueChanged += HandleActiveClientChanged;
            initializedNet.OnValueChanged += HandleInitializedChanged;

            SyncLocalStateFromNetwork();

            if (bootstrapCoroutine == null)
            {
                bootstrapCoroutine = StartCoroutine(BootstrapWhenReady());
            }
        }

        public override void OnNetworkDespawn()
        {
            currentTurnIndexNet.OnValueChanged -= HandleTurnIndexChanged;
            currentPhaseNet.OnValueChanged -= HandlePhaseChanged;
            lastDiceRollNet.OnValueChanged -= HandleDiceRollChanged;
            activePlayerNameNet.OnValueChanged -= HandleActivePlayerChanged;
            activeClientIdNet.OnValueChanged -= HandleActiveClientChanged;
            initializedNet.OnValueChanged -= HandleInitializedChanged;
        }

        // ── Dependency wiring ───────────────────────────────────────────────────────

        public void BindDependencies(MultiplayerFlowCoordinator coordinator, BoardManager boardManager, PlayerPawnSpawner pawnSpawner)
        {
            this.coordinator = coordinator;
            this.boardManager = boardManager;
            this.pawnSpawner = pawnSpawner;
            pawnRegistry = new GameSessionPawnRegistry(pawnSpawner);
        }

        // ── Public API ──────────────────────────────────────────────────────────────

        public void RequestRoll()
        {
            if (IsServer)
            {
                HandleRollOnServer(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
                return;
            }

            RequestRollServerRpc();
        }

        public void RequestEndTurn()
        {
            if (IsServer)
            {
                HandleEndTurnOnServer(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
                return;
            }

            RequestEndTurnServerRpc();
        }

        public IReadOnlyList<PlayerPawn> GetSpawnedPawns() => Registry.GetAllPawns();

        public string GetPhaseLabel()
        {
            return CurrentPhase switch
            {
                TurnPhase.WaitingForSetup   => "Setting up",
                TurnPhase.AwaitingRoll      => "Awaiting roll",
                TurnPhase.MovingPawn        => "Moving pawn",
                TurnPhase.ResolvingSpace    => "Resolving space",
                TurnPhase.WaitingForEndTurn => "Waiting for end turn",
                _                           => "Unknown"
            };
        }

        // ── RPCs ────────────────────────────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        private void RequestRollServerRpc(ServerRpcParams serverRpcParams = default)
        {
            HandleRollOnServer(serverRpcParams.Receive.SenderClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestEndTurnServerRpc(ServerRpcParams serverRpcParams = default)
        {
            HandleEndTurnOnServer(serverRpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void MovePawnClientRpc(int pawnSlot, int targetSpaceIndex, float duration, ClientRpcParams clientRpcParams = default)
        {
            PlayerPawnNetworkSync pawnSync = Registry.FindBySlot(pawnSlot);
            pawnSync?.PlayAuthoritativeMove(targetSpaceIndex, duration);
        }

        // ── Bootstrap ───────────────────────────────────────────────────────────────

        private IEnumerator BootstrapWhenReady()
        {
            while (coordinator == null || boardManager == null || pawnSpawner == null)
            {
                yield return null;
            }

            while (coordinator.CurrentLobbySnapshot == null)
            {
                yield return null;
            }

            if (IsServer)
            {
                int expectedPlayers = coordinator.CurrentLobbySnapshot.PlayerCount;
                while (NetworkManager.Singleton.ConnectedClientsIds.Count < expectedPlayers)
                {
                    yield return null;
                }
            }

            InitializeFromLobbySnapshot(coordinator.CurrentLobbySnapshot);
        }

        private void InitializeFromLobbySnapshot(LobbySnapshot snapshot)
        {
            if (snapshot == null) return;
            if (IsServer && IsInitialized) return;

            if (IsServer)
            {
                Registry.Populate(pawnSpawner.SpawnPawns(snapshot.PlayerDisplayNames, startSpaceIndex));
                Registry.AssignOwnerships();
                turnStateMachine.SetParticipants(Registry.BuildParticipants());
                turnStateMachine.StartGame(0);
                SetServerState(turnStateMachine.State);
            }
            else
            {
                Registry.Refresh();
                CurrentTurnIndex = currentTurnIndexNet.Value;
                CurrentPhase     = (TurnPhase)currentPhaseNet.Value;
                LastDiceRoll     = lastDiceRollNet.Value;
                ActivePlayerName = activePlayerNameNet.Value.ToString();
                ActiveClientId   = activeClientIdNet.Value;
                IsInitialized    = initializedNet.Value;
                turnStateMachine.SetState(new TurnState(CurrentTurnIndex, CurrentPhase, LastDiceRoll, ActivePlayerName, ActiveClientId, IsInitialized));
                PublishTurnState();
            }
        }

        // ── Turn execution (server only) ─────────────────────────────────────────────

        private void HandleRollOnServer(ulong senderClientId)
        {
            if (!EnsureReadyForTurn()) return;
            if (CurrentPhase != TurnPhase.AwaitingRoll) return;
            if (!turnStateMachine.IsAuthorizedTurnRequest(senderClientId)) return;

            PlayerPawnNetworkSync activePawn = Registry.GetAtTurnIndex(CurrentTurnIndex);
            if (activePawn == null) return;

            int dice = UnityEngine.Random.Range(1, 7);
            int targetSpaceIndex = boardManager.NormalizeIndex(activePawn.CurrentSpaceIndex + dice);
            ulong activeClientId = turnStateMachine.GetParticipantClientId(CurrentTurnIndex);

            turnStateMachine.BeginRoll(dice);
            SetServerState(turnStateMachine.State);
            DiceRolled?.Invoke(dice);

            MovePawnClientRpc(activePawn.PawnSlot, targetSpaceIndex, pawnMoveDuration,
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { activeClientId } } });

            if (turnCoroutine != null) StopCoroutine(turnCoroutine);
            turnCoroutine = StartCoroutine(ResolveAfterMoveRoutine(activePawn, targetSpaceIndex));
        }

        private void HandleEndTurnOnServer(ulong senderClientId)
        {
            if (!EnsureReadyForTurn()) return;
            if (CurrentPhase != TurnPhase.WaitingForEndTurn) return;
            if (!turnStateMachine.IsAuthorizedTurnRequest(senderClientId)) return;

            AdvanceTurnServer();
        }

        private IEnumerator ResolveAfterMoveRoutine(PlayerPawnNetworkSync activePawn, int targetSpaceIndex)
        {
            yield return new WaitForSeconds(pawnMoveDuration);

            if (activePawn != null && !activePawn.NetworkObject.IsOwner)
            {
                activePawn.ForceSpaceIndex(targetSpaceIndex);
            }

            turnStateMachine.BeginResolve();
            SetServerState(turnStateMachine.State);

            BoardState boardState = boardManager != null ? boardManager.CaptureBoardState() : null;
            BoardLandingResult result = boardRuleResolver.Resolve(boardState, targetSpaceIndex, activePawn.PlayerId);
            boardManager.GetSpace(targetSpaceIndex).OnPlayerLanded(result);

            yield return new WaitForSeconds(resolveDelay);

            turnStateMachine.BeginWaitingForEndTurn();
            SetServerState(turnStateMachine.State);

            if (autoAdvanceTurns) AdvanceTurnServer();

            turnCoroutine = null;
        }

        private void AdvanceTurnServer()
        {
            if (!IsInitialized || Registry.Count == 0) return;

            turnStateMachine.SetParticipants(Registry.BuildParticipants());
            turnStateMachine.AdvanceTurn();
            SetServerState(turnStateMachine.State);
        }

        // ── Network state sync ──────────────────────────────────────────────────────

        private void SetServerState(TurnState state)
        {
            if (state == null) return;

            currentTurnIndexNet.Value = state.TurnIndex;
            currentPhaseNet.Value     = (int)state.Phase;
            lastDiceRollNet.Value     = state.DiceRoll;
            activePlayerNameNet.Value = new FixedString64Bytes(state.ActivePlayerName ?? string.Empty);
            activeClientIdNet.Value   = state.ActiveClientId;
            initializedNet.Value      = state.IsInitialized;

            SyncLocalStateFromNetwork();
        }

        private void SyncLocalStateFromNetwork()
        {
            CurrentTurnIndex = currentTurnIndexNet.Value;
            CurrentPhase     = (TurnPhase)currentPhaseNet.Value;
            LastDiceRoll     = lastDiceRollNet.Value;
            ActivePlayerName = activePlayerNameNet.Value.ToString();
            ActiveClientId   = activeClientIdNet.Value;
            IsInitialized    = initializedNet.Value;
            turnStateMachine.SetState(new TurnState(CurrentTurnIndex, CurrentPhase, LastDiceRoll, ActivePlayerName, ActiveClientId, IsInitialized));

            PublishTurnState();
        }

        private void HandleTurnIndexChanged(int _, int newValue)    { CurrentTurnIndex = newValue; PublishTurnState(); }
        private void HandlePhaseChanged(int _, int newValue)        { CurrentPhase = (TurnPhase)newValue; PhaseChanged?.Invoke(CurrentPhase); }
        private void HandleDiceRollChanged(int _, int newValue)     { LastDiceRoll = newValue; DiceRolled?.Invoke(newValue); }
        private void HandleActivePlayerChanged(FixedString64Bytes _, FixedString64Bytes newValue) { ActivePlayerName = newValue.ToString(); PublishTurnState(); }
        private void HandleActiveClientChanged(ulong _, ulong newValue) { ActiveClientId = newValue; PublishTurnState(); }
        private void HandleInitializedChanged(bool _, bool newValue) { IsInitialized = newValue; }

        private void PublishTurnState()
        {
            string name = string.IsNullOrWhiteSpace(ActivePlayerName)
                ? turnStateMachine.GetParticipantName(CurrentTurnIndex)
                : ActivePlayerName;

            TurnChanged?.Invoke(CurrentTurnIndex, name);
            PhaseChanged?.Invoke(CurrentPhase);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        private bool EnsureReadyForTurn()
        {
            if (coordinator == null || boardManager == null || pawnSpawner == null) return false;

            if (!IsInitialized)
            {
                LobbySnapshot snapshot = coordinator.CurrentLobbySnapshot;
                if (snapshot != null) InitializeFromLobbySnapshot(snapshot);
            }

            return Registry.Count > 0 && boardManager != null;
        }

        private GameSessionPawnRegistry Registry
        {
            get
            {
                if (pawnRegistry == null) pawnRegistry = new GameSessionPawnRegistry(pawnSpawner);
                return pawnRegistry;
            }
        }
    }
}
