using System;
using System.Collections;
using System.Collections.Generic;
using MonopolyGame.Board;
using MonopolyGame.Cards;
using MonopolyGame.Multiplayer;
using MonopolyGame.Pawns;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MonopolyGame.Multiplayer.Gameplay
{
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

        [Header("Developer")]
        [SerializeField] private bool enableDevGrantCardShortcut = true;
        [SerializeField] private bool enableDevTriggerEventShortcut = true;

        private const Key DevGrantCardKey = Key.F8;
        private const Key DevTriggerEventKey = Key.F9;

        private GameSessionPawnRegistry pawnRegistry;
        private readonly TurnStateMachine turnStateMachine = new TurnStateMachine();
        private readonly BoardRuleResolver boardRuleResolver = new BoardRuleResolver();
        private IPlayerEconomyService _economyService;
        private TurnResolutionService _turnResolution;
        private ActionCardSessionService _actionCardService;
        private EventCardSessionService _eventCardService;
        private EventCardGameActions _eventCardActions;
        private readonly List<ActionCardHandEntry> localActionCardHand = new List<ActionCardHandEntry>();

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

        private readonly NetworkVariable<CardPileState> actionCardPileStateNet = new NetworkVariable<CardPileState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<CardPileState> eventCardPileStateNet = new NetworkVariable<CardPileState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action EconomyChanged;
        public event Action ActionCardsChanged;
        public event Action EventCardsChanged;

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
            _economyService = new PlayerEconomyService(playerEconomyNet, propertyOwnershipNet);
            _turnResolution = new TurnResolutionService(
                _economyService,
                pendingPurchaseSpaceIndexNet,
                lastResolvedSpaceIndexNet,
                lastEconomyMessageNet,
                passStartReward,
                jailSpaceIndex,
                pawnMoveDuration,
                () => EconomyChanged?.Invoke());
            _actionCardService = new ActionCardSessionService(actionCardPileStateNet, HandleActionCardHandChanged);
            _eventCardService = new EventCardSessionService(eventCardPileStateNet);
            _eventCardActions = new EventCardGameActions(_economyService, () => EconomyChanged?.Invoke());
        }

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (enableDevGrantCardShortcut && Keyboard.current[DevGrantCardKey].wasPressedThisFrame)
            {
                RequestDevActionCardGrant();
            }

            if (enableDevTriggerEventShortcut && Keyboard.current[DevTriggerEventKey].wasPressedThisFrame)
            {
                RequestDevEventCardTrigger();
            }
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
            actionCardPileStateNet.OnValueChanged += HandleActionCardPileStateChanged;
            eventCardPileStateNet.OnValueChanged += HandleEventCardPileStateChanged;

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
            actionCardPileStateNet.OnValueChanged -= HandleActionCardPileStateChanged;
            eventCardPileStateNet.OnValueChanged -= HandleEventCardPileStateChanged;

            localActionCardHand.Clear();
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
            
            if (pawnSlot < 0 || _economyService == null)
            {
                return 0;
            }

            return _economyService.GetBalance(pawnSlot);
        }

        public int GetLocalPawnSlot()
        {
            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            return GetPawnSlotForClient(localClientId);
        }

        public IReadOnlyList<PlayerEconomyState> GetPlayerEconomyStates()
        {
            return _economyService != null
                ? _economyService.GetAllEconomyStates()
                : Array.Empty<PlayerEconomyState>();
        }

        public int ActionCardDrawPileCount => actionCardPileStateNet.Value.DrawPileCount;
        public int ActionCardDiscardPileCount => actionCardPileStateNet.Value.DiscardPileCount;
        public int ActionCardHandCapacity => ActionCardHand.DefaultCapacity;

        public string ActionCardTopDiscardLabel
        {
            get
            {
                CardPileState state = actionCardPileStateNet.Value;
                string shortLabel = state.TopDiscardShortLabel.ToString();
                return string.IsNullOrEmpty(shortLabel)
                    ? state.TopDiscardDisplayName.ToString()
                    : shortLabel;
            }
        }

        public IReadOnlyList<ActionCardHandEntry> GetLocalActionCardHand()
        {
            return localActionCardHand;
        }

        /// <summary>
        /// Server-side entry point for granting an action card from any future
        /// system. Landing on an ActionCard space uses the same path.
        /// </summary>
        public ActionCardGrantResult GrantActionCard(int pawnSlot)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[MultiplayerGameSessionController] Action cards can only be granted by the server.");
                return new ActionCardGrantResult(ActionCardGrantOutcome.InvalidPlayer);
            }

            return _actionCardService.GrantCard(pawnSlot);
        }

        private void RequestDevActionCardGrant()
        {
            if (!IsInitialized)
            {
                return;
            }

            int pawnSlot = GetLocalPawnSlot();

            if (pawnSlot < 0)
            {
                return;
            }

            if (IsServer)
            {
                ActionCardGrantResult result = GrantActionCard(pawnSlot);
                Debug.Log($"[Dev] Action card grant for slot {pawnSlot}: {result.Outcome}.");
            }
            else
            {
                RequestDevGrantActionCardServerRpc();
            }
        }

        public int EventCardDrawPileCount => eventCardPileStateNet.Value.DrawPileCount;
        public int EventCardDiscardPileCount => eventCardPileStateNet.Value.DiscardPileCount;

        public string EventCardTopDiscardLabel
        {
            get
            {
                CardPileState state = eventCardPileStateNet.Value;
                string shortLabel = state.TopDiscardShortLabel.ToString();
                return string.IsNullOrEmpty(shortLabel)
                    ? state.TopDiscardDisplayName.ToString()
                    : shortLabel;
            }
        }

        /// <summary>
        /// Server-side entry point for triggering an event card from any future
        /// system. Landing on an EventCard space uses the same path.
        /// </summary>
        public EventCardDrawResult TriggerEventCard(int pawnSlot)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[MultiplayerGameSessionController] Event cards can only be triggered by the server.");
                return new EventCardDrawResult(EventCardOutcome.DeckEmpty);
            }

            return _eventCardService.TriggerCard(BuildEventCardTriggerContext(pawnSlot));
        }

        private EventCardTriggerContext BuildEventCardTriggerContext(int drawingPawnSlot)
        {
            IReadOnlyList<PlayerEconomyState> states = _economyService != null
                ? _economyService.GetAllEconomyStates()
                : Array.Empty<PlayerEconomyState>();
            var players = new List<EventCardPlayerState>(states.Count);

            for (int i = 0; i < states.Count; i++)
            {
                PlayerEconomyState state = states[i];
                players.Add(new EventCardPlayerState(
                    state.PawnSlot,
                    state.PawnSlot,
                    state.PlayerId.ToString(),
                    state.DisplayName.ToString(),
                    state.Balance));
            }

            return new EventCardTriggerContext(drawingPawnSlot, players, _eventCardActions);
        }

        private void RequestDevEventCardTrigger()
        {
            if (!IsInitialized)
            {
                return;
            }

            int pawnSlot = GetLocalPawnSlot();

            if (pawnSlot < 0)
            {
                return;
            }

            if (IsServer)
            {
                EventCardDrawResult result = TriggerEventCard(pawnSlot);
                Debug.Log($"[Dev] Event card trigger for slot {pawnSlot}: {result.Outcome}.");
            }
            else
            {
                RequestDevTriggerEventCardServerRpc();
            }
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

        public bool HasEconomyMessage()
        {
            return !string.IsNullOrWhiteSpace(lastEconomyMessageNet.Value.ToString());
        }

        public string GetCurrentPropertyName()
        {
            BoardSpaceView space = GetLastResolvedSpace();
            return space != null ? space.displayName : string.Empty;
        }

        public string GetCurrentPropertyDetails()
        {
            BoardSpaceView space = GetLastResolvedSpace();
            if (space == null)
            {
                return string.Empty;
            }

            if (space.type == BoardSpaceType.Property)
            {
                return $"Price {space.price} · Rent {space.baseRent}";
            }

            if (space.type == BoardSpaceType.Tax)
            {
                return $"Tax {space.price}";
            }

            return space.type.ToString();
        }

        public string GetCurrentPropertyOwnerName()
        {
            BoardSpaceView space = GetLastResolvedSpace();
            if (space == null)
            {
                return "-";
            }

            if (space.type != BoardSpaceType.Property)
            {
                return "not a property";
            }

            if (_economyService == null)
            {
                return "-";
            }

            int spaceIndex = lastResolvedSpaceIndexNet.Value;
            int ownerSlot = _economyService.GetPropertyOwnerPawnSlot(spaceIndex);
            if (ownerSlot < 0)
            {
                return "none";
            }

            return _economyService.GetPropertyOwnerName(spaceIndex);
        }

        private BoardSpaceView GetLastResolvedSpace()
        {
            int spaceIndex = lastResolvedSpaceIndexNet.Value;
            if (spaceIndex < 0 || boardManager == null)
            {
                return null;
            }

            return boardManager.GetSpace(spaceIndex);
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

            int ownerSlot = _economyService.GetPropertyOwnerPawnSlot(spaceIndex);
            if (ownerSlot < 0)
            {
                return "Owner: none";
            }

            return $"Owner: {_economyService.GetPropertyOwnerName(spaceIndex)}";
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

        [ServerRpc(RequireOwnership = false)]
        private void RequestActionCardHandServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer)
            {
                return;
            }

            ulong senderClientId = serverRpcParams.Receive.SenderClientId;
            int pawnSlot = GetPawnSlotForClient(senderClientId);

            if (pawnSlot >= 0)
            {
                SendActionCardHandToClient(pawnSlot, _actionCardService.GetHandEntries(pawnSlot), senderClientId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDevGrantActionCardServerRpc(ServerRpcParams serverRpcParams = default)
        {
            int pawnSlot = GetPawnSlotForClient(serverRpcParams.Receive.SenderClientId);

            if (pawnSlot >= 0)
            {
                ActionCardGrantResult result = GrantActionCard(pawnSlot);
                Debug.Log($"[Dev] Action card grant for slot {pawnSlot}: {result.Outcome}.");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDevTriggerEventCardServerRpc(ServerRpcParams serverRpcParams = default)
        {
            int pawnSlot = GetPawnSlotForClient(serverRpcParams.Receive.SenderClientId);

            if (pawnSlot >= 0)
            {
                EventCardDrawResult result = TriggerEventCard(pawnSlot);
                Debug.Log($"[Dev] Event card trigger for slot {pawnSlot}: {result.Outcome}.");
            }
        }

        [ClientRpc]
        private void SyncLocalActionCardHandClientRpc(
            ActionCardHandEntry[] entries,
            ClientRpcParams clientRpcParams = default)
        {
            int localSlot = GetLocalPawnSlot();

            if (entries == null || entries.Length == 0)
            {
                localActionCardHand.Clear();
                ActionCardsChanged?.Invoke();
                return;
            }

            if (entries[0].PawnSlot != localSlot)
            {
                return;
            }

            localActionCardHand.Clear();
            localActionCardHand.AddRange(entries);
            ActionCardsChanged?.Invoke();
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
                _actionCardService.Initialize(Registry.Count);
                _eventCardService.Initialize();
                turnStateMachine.StartGame(0);
                lastResolvedSpaceIndexNet.Value = startSpaceIndex;
                lastEconomyMessageNet.Value = new FixedString128Bytes("Game started.");
                SetServerState(turnStateMachine.State);
                SendInitialActionCardHands();
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

                if (IsInitialized)
                {
                    RequestActionCardHandServerRpc();
                }
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

            // Landing signal for local systems (e.g. camera focus). Clients get
            // the equivalent notification from HandlePhaseChanged below.
            PawnMoved?.Invoke(activePawn != null ? activePawn.Pawn : null, targetSpaceIndex);

            turnStateMachine.BeginResolve();
            SetServerState(turnStateMachine.State);

            BoardState boardState = boardManager != null ? boardManager.CaptureBoardState() : null;
            BoardLandingResult result = boardRuleResolver.Resolve(boardState, targetSpaceIndex, activePawn.PlayerId);
            boardManager.GetSpace(targetSpaceIndex).OnPlayerLanded(result);
            
            if (IsServer && result != null)
            {
                _turnResolution.SetLastResolvedSpaceIndex(result.SpaceIndex);
            }
            
            _turnResolution.ResolvePassStartReward(activePawn, passedStart);
            
            bool sentToJail = _turnResolution.ResolveGoToJail(activePawn, result, boardManager,
                (slot, index, duration) => MovePawnClientRpc(slot, index, duration));
            if (sentToJail)
            {
                yield return new WaitForSeconds(pawnMoveDuration);
            }
            else
            {
                _turnResolution.ResolveTaxPayment(activePawn, result);
                _turnResolution.ResolveAutomaticRent(activePawn, result);
                _turnResolution.ResolveSpecialSpaceMessage(result);
                ResolveActionCardLanding(activePawn, result);
                ResolveEventCardLanding(activePawn, result);
                _turnResolution.UpdatePendingPurchase(result, boardManager);
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
        private void HandlePhaseChanged(int _, int newValue)
        {
            CurrentPhase = (TurnPhase)newValue;
            PhaseChanged?.Invoke(CurrentPhase);

            // Server fires PawnMoved directly in ResolveAfterMoveRoutine; on
            // clients the landing is derived from the replicated phase change.
            if (CurrentPhase == TurnPhase.ResolvingSpace && !IsServer)
            {
                PlayerPawnNetworkSync activePawn = Registry.GetAtTurnIndex(CurrentTurnIndex);
                if (activePawn != null)
                {
                    PawnMoved?.Invoke(activePawn.Pawn, activePawn.CurrentSpaceIndex);
                }
            }
        }
        private void HandleDiceRollChanged(int _, int newValue)     { LastDiceRoll = newValue; DiceRolled?.Invoke(newValue); }
        private void HandleActivePlayerChanged(FixedString64Bytes _, FixedString64Bytes newValue) { ActivePlayerName = newValue.ToString(); PublishTurnState(); }
        private void HandleActiveClientChanged(ulong _, ulong newValue) { ActiveClientId = newValue; PublishTurnState(); }
        private void HandleInitializedChanged(bool _, bool newValue)
        {
            IsInitialized = newValue;

            if (newValue && !IsServer)
            {
                RequestActionCardHandServerRpc();
            }
        }

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

            int spaceCount = boardManager != null ? boardManager.SpaceCount : 0;
            var pawns = new List<PlayerPawnNetworkSync>();
            for (int i = 0; i < Registry.Count; i++)
            {
                pawns.Add(Registry.GetAtTurnIndex(i));
            }

            _turnResolution.InitializeEconomyState(pawns, spaceCount, startingBalance);
        }

        private void ResolveActionCardLanding(PlayerPawnNetworkSync pawn, BoardLandingResult result)
        {
            if (pawn == null || result == null || result.SpaceType != BoardSpaceType.ActionCard)
            {
                return;
            }

            ActionCardGrantResult grant = GrantActionCard(pawn.PawnSlot);
            string message = BuildActionCardMessage(pawn, grant);

            if (!string.IsNullOrEmpty(message))
            {
                _turnResolution.SetEconomyMessage(message);
            }
        }

        private static string BuildActionCardMessage(PlayerPawnNetworkSync pawn, ActionCardGrantResult grant)
        {
            string playerName = string.IsNullOrWhiteSpace(pawn.DisplayName)
                ? $"Player {pawn.PawnSlot + 1}"
                : pawn.DisplayName;
            string reshufflePrefix = grant.Reshuffled ? "Discard pile reshuffled. " : string.Empty;

            switch (grant.Outcome)
            {
                case ActionCardGrantOutcome.Added:
                    return $"{reshufflePrefix}{playerName} drew {grant.Card.Definition.DisplayName}.";

                case ActionCardGrantOutcome.HandFullDiscarded:
                    return $"{reshufflePrefix}{playerName}'s hand is full - {grant.Card.Definition.DisplayName} was discarded.";

                case ActionCardGrantOutcome.DeckEmpty:
                    return $"{reshufflePrefix}No action cards are available.";

                default:
                    return string.Empty;
            }
        }

        private void ResolveEventCardLanding(PlayerPawnNetworkSync pawn, BoardLandingResult result)
        {
            if (pawn == null || result == null || result.SpaceType != BoardSpaceType.EventCard)
            {
                return;
            }

            EventCardDrawResult draw = TriggerEventCard(pawn.PawnSlot);
            string message = BuildEventCardMessage(pawn, draw);

            if (!string.IsNullOrEmpty(message))
            {
                _turnResolution.SetEconomyMessage(message);
            }
        }

        private static string BuildEventCardMessage(PlayerPawnNetworkSync pawn, EventCardDrawResult draw)
        {
            string reshufflePrefix = draw.Reshuffled ? "Discard pile reshuffled. " : string.Empty;

            if (draw.Outcome == EventCardOutcome.DeckEmpty)
            {
                return $"{reshufflePrefix}No event cards are available.";
            }

            string playerName = string.IsNullOrWhiteSpace(pawn.DisplayName)
                ? $"Player {pawn.PawnSlot + 1}"
                : pawn.DisplayName;
            string effect = draw.Messages.Count > 0 ? string.Join(" ", draw.Messages) : string.Empty;

            return $"{reshufflePrefix}{playerName} drew {draw.Card.Definition.DisplayName}. {effect}".Trim();
        }

        private void SendInitialActionCardHands()
        {
            if (!IsServer)
            {
                return;
            }

            for (int i = 0; i < Registry.Count; i++)
            {
                PlayerPawnNetworkSync pawn = Registry.GetAtTurnIndex(i);

                if (pawn == null || pawn.NetworkObject == null)
                {
                    continue;
                }

                IReadOnlyList<ActionCardHandEntry> entries = _actionCardService.GetHandEntries(pawn.PawnSlot);
                SendActionCardHandToClient(pawn.PawnSlot, entries, pawn.NetworkObject.OwnerClientId);
                ApplyLocalActionCardHandIfOwned(pawn.PawnSlot, entries);
            }
        }

        private void HandleActionCardHandChanged(int pawnSlot, IReadOnlyList<ActionCardHandEntry> entries)
        {
            if (!IsServer)
            {
                return;
            }

            PlayerPawnNetworkSync pawn = Registry.GetAtTurnIndex(pawnSlot);

            if (pawn != null && pawn.NetworkObject != null)
            {
                SendActionCardHandToClient(pawnSlot, entries, pawn.NetworkObject.OwnerClientId);
            }

            ApplyLocalActionCardHandIfOwned(pawnSlot, entries);
        }

        private void SendActionCardHandToClient(
            int pawnSlot,
            IReadOnlyList<ActionCardHandEntry> entries,
            ulong ownerClientId)
        {
            var payload = new ActionCardHandEntry[entries != null ? entries.Count : 0];

            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = entries[i];
            }

            SyncLocalActionCardHandClientRpc(payload, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { ownerClientId }
                }
            });
        }

        private void ApplyLocalActionCardHandIfOwned(int pawnSlot, IReadOnlyList<ActionCardHandEntry> entries)
        {
            if (pawnSlot != GetLocalPawnSlot())
            {
                return;
            }

            localActionCardHand.Clear();

            if (entries != null)
            {
                localActionCardHand.AddRange(entries);
            }

            ActionCardsChanged?.Invoke();
        }

        private void HandleActionCardPileStateChanged(CardPileState previousValue, CardPileState newValue)
        {
            ActionCardsChanged?.Invoke();
        }

        private void HandleEventCardPileStateChanged(CardPileState previousValue, CardPileState newValue)
        {
            EventCardsChanged?.Invoke();
        }

        private void HandleBuyPropertyOnServer(ulong senderClientId)
        {
            if (!EnsureReadyForTurn()) return;
            if (CurrentPhase != TurnPhase.WaitingForEndTurn) return;
            if (!turnStateMachine.IsAuthorizedTurnRequest(senderClientId)) return;

            int spaceIndex = pendingPurchaseSpaceIndexNet.Value;
            if (spaceIndex < 0) return;

            PlayerPawnNetworkSync pawn = Registry.GetAtTurnIndex(CurrentTurnIndex);
            if (pawn == null) return;

            _turnResolution.TryBuyProperty(spaceIndex, pawn, boardManager);
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

        private void ApplyPropertyOwnersToBoard()
        {
            if (boardManager == null)
            {
                return;
            }

            for (int i = 0; i < propertyOwnershipNet.Count; i++)
            {
                PropertyOwnershipState owner = propertyOwnershipNet[i];
                boardManager.SetSpaceOwner(owner.SpaceIndex, owner.OwnerPlayerId.ToString(), owner.OwnerPawnSlot);
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
