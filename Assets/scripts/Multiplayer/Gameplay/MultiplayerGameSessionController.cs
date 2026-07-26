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
        [SerializeField] private int startingBalance = 1500;
        [SerializeField] private int startSpaceIndex = 0;
        [SerializeField] private int jailSpaceIndex = 10;
        [SerializeField] private int passStartReward = 200;
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
        
        private NetworkList<PlayerEconomyState> playerEconomyNet;
        private NetworkList<PropertyOwnershipState> propertyOwnershipNet;

        private readonly NetworkVariable<int> pendingPurchaseSpaceIndexNet = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        
        private readonly NetworkVariable<int> lastResolvedSpaceIndexNet = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        
        private readonly NetworkVariable<FixedString128Bytes> lastEconomyMessageNet = new NetworkVariable<FixedString128Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action EconomyChanged;

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
        
        private void Awake()
        {
            playerEconomyNet = new NetworkList<PlayerEconomyState>();
            propertyOwnershipNet = new NetworkList<PropertyOwnershipState>();
        }

        public override void OnNetworkSpawn()
        {
            currentTurnIndexNet.OnValueChanged += HandleTurnIndexChanged;
            currentPhaseNet.OnValueChanged += HandlePhaseChanged;
            lastDiceRollNet.OnValueChanged += HandleDiceRollChanged;
            activePlayerNameNet.OnValueChanged += HandleActivePlayerChanged;
            activeClientIdNet.OnValueChanged += HandleActiveClientChanged;
            initializedNet.OnValueChanged += HandleInitializedChanged;
            playerEconomyNet.OnListChanged += HandleEconomyChanged;
            propertyOwnershipNet.OnListChanged += HandlePropertyOwnershipChanged;
            pendingPurchaseSpaceIndexNet.OnValueChanged += HandlePendingPurchaseChanged;
            lastResolvedSpaceIndexNet.OnValueChanged += HandleLastResolvedSpaceChanged;
            lastEconomyMessageNet.OnValueChanged += HandleLastEconomyMessageChanged;

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
            playerEconomyNet.OnListChanged -= HandleEconomyChanged;
            propertyOwnershipNet.OnListChanged -= HandlePropertyOwnershipChanged;
            pendingPurchaseSpaceIndexNet.OnValueChanged -= HandlePendingPurchaseChanged;
            lastResolvedSpaceIndexNet.OnValueChanged -= HandleLastResolvedSpaceChanged;
            lastEconomyMessageNet.OnValueChanged -= HandleLastEconomyMessageChanged;
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
        
        public void RequestBuyCurrentProperty()
        {
            if (IsServer)
            {
                HandleBuyPropertyOnServer(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0);
                return;
            }

            RequestBuyCurrentPropertyServerRpc();
        }

        public IReadOnlyList<PlayerPawn> GetSpawnedPawns() => Registry.GetAllPawns();
        
        public int GetLocalPlayerBalance()
        {
            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            int pawnSlot = GetPawnSlotForClient(localClientId);

            if (pawnSlot < 0 && localClientId <= int.MaxValue)
            {
                pawnSlot = (int)localClientId;
            }
            
            if (pawnSlot < 0)
            {
                return 0;
            }

            int economyIndex = FindPlayerEconomyIndex(pawnSlot);
            return economyIndex >= 0 ? playerEconomyNet[economyIndex].Balance : 0;
        }

        public bool CanBuyCurrentProperty()
        {
            if (!IsInitialized || CurrentPhase != TurnPhase.WaitingForEndTurn)
            {
                return false;
            }

            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            if (ActiveClientId != localClientId)
            {
                return false;
            }

            int spaceIndex = pendingPurchaseSpaceIndexNet.Value;
            if (spaceIndex < 0 || boardManager == null)
            {
                return false;
            }

            BoardSpaceView space = boardManager.GetSpace(spaceIndex);
            if (space == null || space.type != BoardSpaceType.Property)
            {
                return false;
            }

            return GetLocalPlayerBalance() >= space.price;
        }
        
        public string GetBuyButtonLabel()
        {
            int spaceIndex = pendingPurchaseSpaceIndexNet.Value;
            if (spaceIndex < 0 || boardManager == null)
            {
                return "Buy";
            }

            BoardSpaceView space = boardManager.GetSpace(spaceIndex);
            if (space == null || space.type != BoardSpaceType.Property)
            {
                return "Buy";
            }

            return $"Buy ({space.price})";
        }

        public string GetLastEconomyMessage()
        {
            string message = lastEconomyMessageNet.Value.ToString();
            return string.IsNullOrWhiteSpace(message) ? "No economy action yet." : message;
        }

        public string GetCurrentPropertyLabel()
        {
            int spaceIndex = lastResolvedSpaceIndexNet.Value;
            if (spaceIndex < 0 || boardManager == null)
            {
                return "No field resolved";
            }

            BoardSpaceView space = boardManager.GetSpace(spaceIndex);
            
            if (space == null)
            {
                return "Field not found";
            }

            if (space.type == BoardSpaceType.Property)
            {
                return $"{space.displayName} - Price: {space.price}, Rent: {space.baseRent}";
            }
            
            if (space.type == BoardSpaceType.Tax)
            {
                return $"{space.displayName} - Tax: {space.price}";
            }

            return $"{space.displayName} ({space.type})";
        }

        public string GetCurrentPropertyOwnerLabel()
        {
            int spaceIndex = lastResolvedSpaceIndexNet.Value;
            if (spaceIndex < 0 || boardManager == null)
            {
                return "Owner: -";
            }

            BoardSpaceView space = boardManager.GetSpace(spaceIndex);
            if (space == null)
            {
                return "Owner: -";
            }
            
            if (space.type != BoardSpaceType.Property)
            {
                return "Owner: not a property";
            }

            int propertyIndex = FindPropertyIndex(spaceIndex);
            if (propertyIndex < 0)
            {
                return "Owner: -";
            }

            PropertyOwnershipState property = propertyOwnershipNet[propertyIndex];
            if (property.OwnerPawnSlot < 0)
            {
                return "Owner: none";
            }

            return $"Owner: {property.OwnerName}";
        }

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
        
        [ServerRpc(RequireOwnership = false)]
        private void RequestBuyCurrentPropertyServerRpc(ServerRpcParams serverRpcParams = default)
        {
            HandleBuyPropertyOnServer(serverRpcParams.Receive.SenderClientId);
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
                InitializeEconomyState();
                turnStateMachine.StartGame(0);
                lastResolvedSpaceIndexNet.Value = startSpaceIndex;
                lastEconomyMessageNet.Value = new FixedString128Bytes("Game started.");
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
            int rawTargetSpaceIndex = activePawn.CurrentSpaceIndex + dice;
            bool passedStart = boardManager != null && boardManager.SpaceCount > 0 && rawTargetSpaceIndex >= boardManager.SpaceCount;
            int targetSpaceIndex = boardManager.NormalizeIndex(activePawn.CurrentSpaceIndex + dice);
            ulong activeClientId = turnStateMachine.GetParticipantClientId(CurrentTurnIndex);

            turnStateMachine.BeginRoll(dice);
            SetServerState(turnStateMachine.State);
            DiceRolled?.Invoke(dice);

            MovePawnClientRpc(activePawn.PawnSlot, targetSpaceIndex, pawnMoveDuration);

            if (turnCoroutine != null) StopCoroutine(turnCoroutine);
            turnCoroutine = StartCoroutine(ResolveAfterMoveRoutine(activePawn, targetSpaceIndex, passedStart));
        }

        private void HandleEndTurnOnServer(ulong senderClientId)
        {
            if (!EnsureReadyForTurn()) return;
            if (CurrentPhase != TurnPhase.WaitingForEndTurn) return;
            if (!turnStateMachine.IsAuthorizedTurnRequest(senderClientId)) return;

            AdvanceTurnServer();
        }

        private IEnumerator ResolveAfterMoveRoutine(PlayerPawnNetworkSync activePawn, int targetSpaceIndex, bool passedStart)
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
            
            if (IsServer && result != null)
            {
                lastResolvedSpaceIndexNet.Value = result.SpaceIndex;
            }
            
            ResolvePassStartReward(activePawn, passedStart);
            
            bool sentToJail = ResolveGoToJail(activePawn, result);
            if (sentToJail)
            {
                yield return new WaitForSeconds(pawnMoveDuration);
            }
            else
            {
                ResolveTaxPayment(activePawn, result);
                ResolveAutomaticRent(activePawn, result);
                ResolveSpecialSpaceMessage(result);
                UpdatePendingPurchase(result);
            }

            yield return new WaitForSeconds(resolveDelay);

            turnStateMachine.BeginWaitingForEndTurn();
            SetServerState(turnStateMachine.State);

            if (autoAdvanceTurns) AdvanceTurnServer();

            turnCoroutine = null;
        }

        private void AdvanceTurnServer()
        {
            if (!IsInitialized || Registry.Count == 0) return;
            
            pendingPurchaseSpaceIndexNet.Value = -1;

            turnStateMachine.SetParticipants(Registry.BuildParticipants());
            turnStateMachine.AdvanceTurn();
            
            PlayerPawnNetworkSync activePawn = Registry.GetAtTurnIndex(turnStateMachine.State.TurnIndex);
            if (activePawn != null)
            {
                lastResolvedSpaceIndexNet.Value = activePawn.CurrentSpaceIndex;
            }
            
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

        private void InitializeEconomyState()
        {
            if (!IsServer)
            {
                return;
            }

            playerEconomyNet.Clear();
            propertyOwnershipNet.Clear();

            for (int i = 0; i < Registry.Count; i++)
            {
                PlayerPawnNetworkSync pawn = Registry.GetAtTurnIndex(i);
                if (pawn != null)
                {
                    playerEconomyNet.Add(new PlayerEconomyState(pawn.PawnSlot, pawn.PlayerId, pawn.DisplayName, startingBalance));
                }
            }

            int spaceCount = boardManager != null ? boardManager.SpaceCount : 0;
            for (int i = 0; i < spaceCount; i++)
            {
                propertyOwnershipNet.Add(new PropertyOwnershipState(i, -1, string.Empty, string.Empty));
            }

            pendingPurchaseSpaceIndexNet.Value = -1;
        }

        private void HandleBuyPropertyOnServer(ulong senderClientId)
        {
            if (!EnsureReadyForTurn()) return;
            if (CurrentPhase != TurnPhase.WaitingForEndTurn) return;
            if (!turnStateMachine.IsAuthorizedTurnRequest(senderClientId)) return;

            int spaceIndex = pendingPurchaseSpaceIndexNet.Value;
            if (spaceIndex < 0) return;

            BoardState boardState = boardManager.CaptureBoardState();
            BoardSpaceSnapshot space = boardState.GetSpace(spaceIndex);
            if (space == null || space.SpaceType != BoardSpaceType.Property) return;

            int propertyIndex = FindPropertyIndex(spaceIndex);
            if (propertyIndex < 0 || propertyOwnershipNet[propertyIndex].OwnerPawnSlot >= 0) return;

            PlayerPawnNetworkSync pawn = Registry.GetAtTurnIndex(CurrentTurnIndex);
            if (pawn == null) return;

            int playerIndex = FindPlayerEconomyIndex(pawn.PawnSlot);
            if (playerIndex < 0) return;

            PlayerEconomyState economy = playerEconomyNet[playerIndex];
            if (economy.Balance < space.Price) return;

            playerEconomyNet[playerIndex] = economy.WithBalance(economy.Balance - space.Price);
            propertyOwnershipNet[propertyIndex] = new PropertyOwnershipState(spaceIndex, pawn.PawnSlot, pawn.PlayerId, pawn.DisplayName);

            boardManager.SetSpaceOwner(spaceIndex, pawn.PlayerId);
            pendingPurchaseSpaceIndexNet.Value = -1;

            SetEconomyMessage($"{pawn.DisplayName} bought {space.DisplayName} for {space.Price}.");
            EconomyChanged?.Invoke();
        }
        
        private bool ResolveGoToJail(PlayerPawnNetworkSync activePawn, BoardLandingResult result)
        {
            if (!IsServer || activePawn == null || result == null)
            {
                return false;
            }

            if (result.SpaceType != BoardSpaceType.GoToJail)
            {
                return false;
            }

            int resolvedJailSpaceIndex = FindFirstSpaceIndexByType(BoardSpaceType.Jail, jailSpaceIndex);

            pendingPurchaseSpaceIndexNet.Value = -1;
            lastResolvedSpaceIndexNet.Value = resolvedJailSpaceIndex;

            MovePawnClientRpc(activePawn.PawnSlot, resolvedJailSpaceIndex, pawnMoveDuration);

            SetEconomyMessage($"{activePawn.DisplayName} was sent to Jail.");
            EconomyChanged?.Invoke();

            return true;
        }

        private void ResolveSpecialSpaceMessage(BoardLandingResult result)
        {
            if (!IsServer || result == null)
            {
                return;
            }

            switch (result.SpaceType)
            {
                case BoardSpaceType.Jail:
                    SetEconomyMessage($"{result.DisplayName}: just visiting.");
                    break;

                case BoardSpaceType.Parking:
                    SetEconomyMessage($"{result.DisplayName}: no action.");
                    break;

                case BoardSpaceType.ActionCard:
                    SetEconomyMessage($"{result.DisplayName}: action cards are not implemented yet.");
                    break;

                case BoardSpaceType.EventCard:
                    SetEconomyMessage($"{result.DisplayName}: event cards are not implemented yet.");
                    break;
            }
        }
        
        private void ResolvePassStartReward(PlayerPawnNetworkSync activePawn, bool passedStart)
        {
            if (!IsServer || activePawn == null || !passedStart || passStartReward <= 0)
            {
                return;
            }

            int playerIndex = FindPlayerEconomyIndex(activePawn.PawnSlot);
            if (playerIndex < 0)
            {
                return;
            }

            PlayerEconomyState economy = playerEconomyNet[playerIndex];
            playerEconomyNet[playerIndex] = economy.WithBalance(economy.Balance + passStartReward);

            SetEconomyMessage($"{economy.DisplayName} received {passStartReward} for passing Start.");
            EconomyChanged?.Invoke();
        }
        
        private void ResolveTaxPayment(PlayerPawnNetworkSync activePawn, BoardLandingResult result)
        {
            if (!IsServer || activePawn == null || result == null)
            {
                return;
            }

            if (result.SpaceType != BoardSpaceType.Tax || result.Price <= 0)
            {
                return;
            }

            int playerIndex = FindPlayerEconomyIndex(activePawn.PawnSlot);
            if (playerIndex < 0)
            {
                return;
            }

            PlayerEconomyState economy = playerEconomyNet[playerIndex];
            int taxToPay = Mathf.Min(result.Price, economy.Balance);

            if (taxToPay <= 0)
            {
                return;
            }

            playerEconomyNet[playerIndex] = economy.WithBalance(economy.Balance - taxToPay);

            SetEconomyMessage($"{economy.DisplayName} paid {taxToPay} tax on {result.DisplayName}.");
            Debug.Log($"[Economy] {economy.DisplayName} paid {taxToPay} tax on {result.DisplayName}.");

            EconomyChanged?.Invoke();
        }
        
        private void ResolveAutomaticRent(PlayerPawnNetworkSync activePawn, BoardLandingResult result)
        {
            if (!IsServer || activePawn == null || result == null)
            {
                return;
            }

            if (result.SpaceType != BoardSpaceType.Property || result.BaseRent <= 0)
            {
                return;
            }

            int propertyIndex = FindPropertyIndex(result.SpaceIndex);
            if (propertyIndex < 0)
            {
                return;
            }

            PropertyOwnershipState ownership = propertyOwnershipNet[propertyIndex];
            if (ownership.OwnerPawnSlot < 0)
            {
                return;
            }

            if (ownership.OwnerPawnSlot == activePawn.PawnSlot)
            {
                return;
            }

            int payerIndex = FindPlayerEconomyIndex(activePawn.PawnSlot);
            int ownerIndex = FindPlayerEconomyIndex(ownership.OwnerPawnSlot);

            if (payerIndex < 0 || ownerIndex < 0)
            {
                return;
            }

            PlayerEconomyState payer = playerEconomyNet[payerIndex];
            PlayerEconomyState owner = playerEconomyNet[ownerIndex];

            int rentToPay = Mathf.Min(result.BaseRent, payer.Balance);
            if (rentToPay <= 0)
            {
                return;
            }

            playerEconomyNet[payerIndex] = payer.WithBalance(payer.Balance - rentToPay);
            playerEconomyNet[ownerIndex] = owner.WithBalance(owner.Balance + rentToPay);

            SetEconomyMessage($"{payer.DisplayName} paid {rentToPay} rent to {owner.DisplayName} for {result.DisplayName}.");
            Debug.Log($"[Economy] {payer.DisplayName} paid {rentToPay} rent to {owner.DisplayName} for {result.DisplayName}.");

            EconomyChanged?.Invoke();
        }
        
        private void UpdatePendingPurchase(BoardLandingResult result)
        {
            if (!IsServer)
            {
                return;
            }

            pendingPurchaseSpaceIndexNet.Value = -1;

            if (result == null || result.SpaceType != BoardSpaceType.Property || result.Price <= 0)
            {
                return;
            }

            int propertyIndex = FindPropertyIndex(result.SpaceIndex);
            if (propertyIndex >= 0 && propertyOwnershipNet[propertyIndex].OwnerPawnSlot < 0)
            {
                pendingPurchaseSpaceIndexNet.Value = result.SpaceIndex;
            }
        }
        
        private int FindFirstSpaceIndexByType(BoardSpaceType type, int fallbackIndex)
        {
            if (boardManager == null)
            {
                return fallbackIndex;
            }

            for (int i = 0; i < boardManager.SpaceCount; i++)
            {
                BoardSpaceView space = boardManager.GetSpace(i);
                if (space != null && space.type == type)
                {
                    return space.index;
                }
            }

            return boardManager.NormalizeIndex(fallbackIndex);
        }

        private int FindPropertyIndex(int spaceIndex)
        {
            for (int i = 0; i < propertyOwnershipNet.Count; i++)
            {
                if (propertyOwnershipNet[i].SpaceIndex == spaceIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetPawnSlotForClient(ulong clientId)
        {
            IReadOnlyList<TurnParticipant> participants = Registry.BuildParticipants();

            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].ClientId == clientId)
                {
                    return participants[i].TurnIndex;
                }
            }

            return -1;
        }
        
        private int FindPlayerEconomyIndex(int pawnSlot)
        {
            for (int i = 0; i < playerEconomyNet.Count; i++)
            {
                if (playerEconomyNet[i].PawnSlot == pawnSlot)
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandleEconomyChanged(NetworkListEvent<PlayerEconomyState> changeEvent)
        {
            EconomyChanged?.Invoke();
        }

        private void HandlePropertyOwnershipChanged(NetworkListEvent<PropertyOwnershipState> changeEvent)
        {
            ApplyPropertyOwnersToBoard();
            EconomyChanged?.Invoke();
        }

        private void HandlePendingPurchaseChanged(int previousValue, int newValue)
        {
            EconomyChanged?.Invoke();
        }
        
        private void HandleLastResolvedSpaceChanged(int previousValue, int newValue)
        {
            EconomyChanged?.Invoke();
        }
        
        private void HandleLastEconomyMessageChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
        {
            EconomyChanged?.Invoke();
        }

        private void SetEconomyMessage(string message)
        {
            if (!IsServer)
            {
                return;
            }

            lastEconomyMessageNet.Value = new FixedString128Bytes(message ?? string.Empty);
        }

        private void ApplyPropertyOwnersToBoard()
        {
            if (boardManager == null)
            {
                return;
            }

            for (int i = 0; i < propertyOwnershipNet.Count; i++)
            {
                PropertyOwnershipState owner = propertyOwnershipNet[i];
                boardManager.SetSpaceOwner(owner.SpaceIndex, owner.OwnerPlayerId.ToString());
            }
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
