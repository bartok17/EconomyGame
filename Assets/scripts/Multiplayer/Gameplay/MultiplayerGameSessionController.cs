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

namespace MonopolyGame.Multiplayer.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed partial class MultiplayerGameSessionController : NetworkBehaviour
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
        private IPlayerEconomyService _economyService;
        private TurnResolutionService _turnResolution;
        private ActionCardSessionService _actionCardService;
        private EventCardSessionService _eventCardService;
        private CardGameActions _cardGameActions;
        private readonly List<ActionCardHandEntry> localActionCardHand = new List<ActionCardHandEntry>();
        private readonly List<CardMoveRequest> pendingCardMoves = new List<CardMoveRequest>();
        private readonly List<PendingPaymentState> queuedPayments = new List<PendingPaymentState>();

        private bool isResolvingCard;
        private Coroutine cardResolutionCoroutine;

        private readonly struct CardMoveRequest
        {
            public CardMoveRequest(int pawnSlot, int spaces)
            {
                PawnSlot = pawnSlot;
                Spaces = spaces;
            }

            public int PawnSlot { get; }
            public int Spaces { get; }
        }

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

        private readonly NetworkVariable<bool> cardResolutionNet = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<PendingPaymentState> pendingPaymentNet = new NetworkVariable<PendingPaymentState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action EconomyChanged;
        public event Action ActionCardsChanged;
        public event Action EventCardsChanged;
        public event Action CardResolutionChanged;
        public event Action PaymentsChanged;

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
                () => EconomyChanged?.Invoke(),
                QueuePendingPayment);
            _actionCardService = new ActionCardSessionService(actionCardPileStateNet, HandleActionCardHandChanged);
            _eventCardService = new EventCardSessionService(eventCardPileStateNet);
            _cardGameActions = new CardGameActions(
                _economyService,
                () => EconomyChanged?.Invoke(),
                QueueCardPawnMove,
                QueuePendingPayment);
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
            cardResolutionNet.OnValueChanged += HandleCardResolutionChanged;
            pendingPaymentNet.OnValueChanged += HandlePendingPaymentChanged;

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
            cardResolutionNet.OnValueChanged -= HandleCardResolutionChanged;
            pendingPaymentNet.OnValueChanged -= HandlePendingPaymentChanged;

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
            return new EventCardTriggerContext(drawingPawnSlot, BuildCardPlayerStates(), _cardGameActions);
        }

        private IReadOnlyList<CardPlayerState> BuildCardPlayerStates()
        {
            IReadOnlyList<PlayerEconomyState> states = _economyService != null
                ? _economyService.GetAllEconomyStates()
                : Array.Empty<PlayerEconomyState>();
            var players = new List<CardPlayerState>(states.Count);

            for (int i = 0; i < states.Count; i++)
            {
                PlayerEconomyState state = states[i];
                players.Add(new CardPlayerState(
                    state.PawnSlot,
                    state.PawnSlot,
                    state.PlayerId.ToString(),
                    state.DisplayName.ToString(),
                    state.Balance));
            }

            return players;
        }

        // ── Action card activation ──────────────────────────────────────────────────

        /// <summary>True while a card is resolving; the turn is paused meanwhile.</summary>
        public bool IsResolvingCardAction => cardResolutionNet.Value;

        /// <summary>
        /// True when the card's activation window covers the current phase and
        /// it is this client's turn. The server re-checks everything.
        /// </summary>
        public bool IsActionCardWindowOpen(CardActivationWindow window)
        {
            if (!IsInitialized || IsResolvingCardAction)
            {
                return false;
            }

            ulong localClientId = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.LocalClientId
                : ulong.MaxValue;

            if (ActiveClientId != localClientId)
            {
                return false;
            }

            return CardActivationWindows.IsOpen(
                window,
                CurrentPhase == TurnPhase.AwaitingRoll,
                CurrentPhase == TurnPhase.WaitingForEndTurn);
        }

        // ── Pending payments ────────────────────────────────────────────────────────

        public PendingPaymentState PendingPayment => pendingPaymentNet.Value;

        public bool HasPendingPayment => pendingPaymentNet.Value.HasPayment;

        public bool CanPayPendingPayment
        {
            get
            {
                PendingPaymentState payment = pendingPaymentNet.Value;

                return payment.HasPayment &&
                       !IsResolvingCardAction &&
                       payment.PayerPawnSlot == GetLocalPawnSlot();
            }
        }

        public string GetPayButtonLabel()
        {
            PendingPaymentState payment = pendingPaymentNet.Value;

            if (!payment.HasPayment)
            {
                return "Pay";
            }

            return $"Pay {payment.Amount}";
        }

        /// <summary>Confirms the pending payment for the local player.</summary>
        public void RequestPayPendingPayment()
        {
            if (!CanPayPendingPayment)
            {
                return;
            }

            if (IsServer)
            {
                HandlePayPendingPaymentServer(NetworkManager.Singleton != null
                    ? NetworkManager.Singleton.LocalClientId
                    : 0);
                return;
            }

            PayPendingPaymentServerRpc();
        }

        /// <summary>Snapshot of every player, used by the activation UI.</summary>
        public IReadOnlyList<CardPlayerState> GetCardPlayers()
        {
            return BuildCardPlayerStates();
        }

        public int GetPlayerSpaceIndex(int pawnSlot)
        {
            PlayerPawnNetworkSync pawn = Registry.FindBySlot(pawnSlot);
            return pawn != null ? pawn.CurrentSpaceIndex : -1;
        }

        public int NormalizeBoardIndex(int spaceIndex)
        {
            return boardManager != null ? boardManager.NormalizeIndex(spaceIndex) : -1;
        }

        public string GetBoardSpaceName(int spaceIndex)
        {
            if (boardManager == null || spaceIndex < 0 || spaceIndex >= boardManager.SpaceCount)
            {
                return string.Empty;
            }

            BoardSpaceView space = boardManager.GetSpace(spaceIndex);
            return space != null ? space.displayName : string.Empty;
        }

        /// <summary>
        /// Plays a card from the local player's hand. Returns false when the
        /// card's activation window is closed or it is not this client's turn.
        /// </summary>
        public bool RequestPlayActionCard(int instanceId, ActionCardParameters parameters)
        {
            if (!TryGetLocalCardDefinition(instanceId, out ActionCardDefinition definition))
            {
                return false;
            }

            if (definition.Timing != CardTiming.Active ||
                !IsActionCardWindowOpen(definition.ActivationWindow))
            {
                return false;
            }

            int pawnSlot = GetLocalPawnSlot();

            if (pawnSlot < 0)
            {
                return false;
            }

            if (IsServer)
            {
                HandlePlayActionCardServer(pawnSlot, instanceId, parameters);
            }
            else
            {
                PlayActionCardServerRpc(instanceId, parameters.TargetPawnSlot, parameters.Amount);
            }

            return true;
        }

        private bool TryGetLocalCardDefinition(int instanceId, out ActionCardDefinition definition)
        {
            definition = null;

            for (int i = 0; i < localActionCardHand.Count; i++)
            {
                if (localActionCardHand[i].InstanceId == instanceId)
                {
                    return ActionCardCatalog.TryGetDefinition(localActionCardHand[i].CardId.ToString(), out definition);
                }
            }

            return false;
        }

        public bool CanBuyCurrentProperty()
        {
            if (!IsInitialized || IsResolvingCardAction)
            {
                return false;
            }

            if (CurrentPhase != TurnPhase.WaitingForEndTurn && CurrentPhase != TurnPhase.AwaitingRoll)
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
            if (IsResolvingCardAction)
            {
                return "Resolving card";
            }

            if (pendingPaymentNet.Value.HasPayment)
            {
                return "Payment due";
            }

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
        private void PlayActionCardServerRpc(
            int instanceId,
            int targetPawnSlot,
            int amount,
            ServerRpcParams serverRpcParams = default)
        {
            int pawnSlot = GetPawnSlotForClient(serverRpcParams.Receive.SenderClientId);

            if (pawnSlot < 0)
            {
                return;
            }

            HandlePlayActionCardServer(pawnSlot, instanceId, new ActionCardParameters(targetPawnSlot, amount));
        }

        [ServerRpc(RequireOwnership = false)]
        private void PayPendingPaymentServerRpc(ServerRpcParams serverRpcParams = default)
        {
            HandlePayPendingPaymentServer(serverRpcParams.Receive.SenderClientId);
        }

        /// <summary>
        /// Server-authoritative card play. Rejects everything the client should
        /// not be able to do: wrong turn, wrong phase, resolution in flight.
        /// </summary>
        private void HandlePlayActionCardServer(int actingPawnSlot, int instanceId, ActionCardParameters parameters)
        {
            if (!IsServer || !IsInitialized || isResolvingCard)
            {
                return;
            }

            bool beforeRoll = CurrentPhase == TurnPhase.AwaitingRoll;
            bool afterRoll = CurrentPhase == TurnPhase.WaitingForEndTurn;

            if (!beforeRoll && !afterRoll)
            {
                return;
            }

            PlayerPawnNetworkSync actingPawn = Registry.FindBySlot(actingPawnSlot);

            if (actingPawn == null || actingPawn.NetworkObject == null)
            {
                return;
            }

            if (!turnStateMachine.IsAuthorizedTurnRequest(actingPawn.NetworkObject.OwnerClientId))
            {
                return;
            }

            BeginCardResolution();

            ActionCardPlayResult result;

            try
            {
                result = _actionCardService.TryPlayCard(
                    actingPawnSlot,
                    instanceId,
                    parameters,
                    BuildCardPlayerStates(),
                    _cardGameActions,
                    beforeRoll,
                    afterRoll);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                pendingCardMoves.Clear();
                EndCardResolution();
                return;
            }

            if (!result.Succeeded)
            {
                pendingCardMoves.Clear();
                EndCardResolution();
                Debug.LogWarning($"[Cards] Play rejected: {result.Outcome} {result.Error}".Trim());
                return;
            }

            string message = BuildActionCardPlayMessage(actingPawnSlot, result);

            if (!string.IsNullOrEmpty(message))
            {
                _turnResolution.SetEconomyMessage(message);
            }

            if (pendingCardMoves.Count == 0)
            {
                EndCardResolution();
                return;
            }

            cardResolutionCoroutine = StartCoroutine(ResolveCardMovesRoutine());
        }

        private void EndCardResolution()
        {
            isResolvingCard = false;
            cardResolutionNet.Value = false;
            cardResolutionCoroutine = null;
        }

        /// <summary>Blocks roll, end turn and further card plays until the resolution ends.</summary>
        private void BeginCardResolution()
        {
            isResolvingCard = true;
            cardResolutionNet.Value = true;
            pendingCardMoves.Clear();
        }

        private void QueueCardPawnMove(string playerId, int spaces)
        {
            if (!isResolvingCard)
            {
                Debug.LogWarning("[Cards] Movement outside a card resolution is not supported yet.");
                return;
            }

            if (!_cardGameActions.TryGetPawnSlot(playerId, out int pawnSlot))
            {
                Debug.LogWarning($"[Cards] Cannot move unknown player '{playerId}'.");
                return;
            }

            pendingCardMoves.Add(new CardMoveRequest(pawnSlot, spaces));
        }

        /// <summary>
        /// Runs the moves a card asked for, one at a time, resolving each
        /// landing exactly like a dice move without touching the turn phase.
        /// </summary>
        private IEnumerator ResolveCardMovesRoutine()
        {
            try
            {
                while (pendingCardMoves.Count > 0)
                {
                    CardMoveRequest request = pendingCardMoves[0];
                    pendingCardMoves.RemoveAt(0);

                    PlayerPawnNetworkSync pawn = Registry.FindBySlot(request.PawnSlot);

                    if (pawn == null || boardManager == null || boardManager.SpaceCount <= 0)
                    {
                        continue;
                    }

                    int rawTarget = pawn.CurrentSpaceIndex + request.Spaces;
                    bool passedStart = rawTarget >= boardManager.SpaceCount;
                    int targetSpaceIndex = boardManager.NormalizeIndex(rawTarget);

                    MovePawnClientRpc(pawn.PawnSlot, targetSpaceIndex, pawnMoveDuration);
                    yield return ResolveLandingEffectsRoutine(pawn, targetSpaceIndex, passedStart, changeTurnPhase: false);
                }
            }
            finally
            {
                pendingCardMoves.Clear();
                EndCardResolution();
            }
        }

        private string BuildActionCardPlayMessage(int actingPawnSlot, ActionCardPlayResult result)
        {
            PlayerPawnNetworkSync actor = Registry.FindBySlot(actingPawnSlot);
            string actorName = actor != null && !string.IsNullOrWhiteSpace(actor.DisplayName)
                ? actor.DisplayName
                : $"Player {actingPawnSlot + 1}";
            string cardName = result.Card != null ? result.Card.Definition.DisplayName : "a card";
            string effect = result.Messages.Count > 0 ? string.Join(" ", result.Messages) : string.Empty;
            return $"{actorName} played {cardName}. {effect}".Trim();
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
            if (isResolvingCard) return;
            if (HasPendingPayment) return;
            if (CurrentPhase != TurnPhase.AwaitingRoll) return;
            if (!turnStateMachine.IsAuthorizedTurnRequest(senderClientId)) return;

            PlayerPawnNetworkSync activePawn = Registry.GetAtTurnIndex(CurrentTurnIndex);
            if (activePawn == null) return;

            pendingPurchaseSpaceIndexNet.Value = -1;

            int dice = debugForcedDice > 0 ? debugForcedDice : UnityEngine.Random.Range(1, 7);
            debugForcedDice = 0;
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
            if (isResolvingCard) return;
            if (HasPendingPayment) return;
            if (CurrentPhase != TurnPhase.WaitingForEndTurn) return;
            if (!turnStateMachine.IsAuthorizedTurnRequest(senderClientId)) return;

            AdvanceTurnServer();
        }

        private IEnumerator ResolveAfterMoveRoutine(PlayerPawnNetworkSync activePawn, int targetSpaceIndex, bool passedStart)
        {
            yield return ResolveLandingEffectsRoutine(activePawn, targetSpaceIndex, passedStart, changeTurnPhase: true);

            turnStateMachine.BeginWaitingForEndTurn();
            SetServerState(turnStateMachine.State);

            if (autoAdvanceTurns) AdvanceTurnServer();

            turnCoroutine = null;
        }

        /// <summary>
        /// Resolves everything that happens when a pawn finishes a move: the
        /// landing effects, jail, card spaces and the purchase prompt. Shared
        /// by dice moves (which change the turn phase) and card-driven moves
        /// (which leave the turn where it was).
        /// </summary>
        private IEnumerator ResolveLandingEffectsRoutine(
            PlayerPawnNetworkSync activePawn,
            int targetSpaceIndex,
            bool passedStart,
            bool changeTurnPhase)
        {
            yield return new WaitForSeconds(pawnMoveDuration);

            if (activePawn != null && !activePawn.NetworkObject.IsOwner)
            {
                activePawn.ForceSpaceIndex(targetSpaceIndex);
            }

            // Landing signal for local systems (e.g. camera focus). Clients get
            // the equivalent notification from HandlePhaseChanged below.
            PawnMoved?.Invoke(activePawn != null ? activePawn.Pawn : null, targetSpaceIndex);

            if (changeTurnPhase)
            {
                turnStateMachine.BeginResolve();
                SetServerState(turnStateMachine.State);
            }

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
                _turnResolution.ResolveRent(activePawn, result);
                _turnResolution.ResolveSpecialSpaceMessage(result);
                ResolveActionCardLanding(activePawn, result);
                ResolveEventCardLanding(activePawn, result);
                _turnResolution.UpdatePendingPurchase(result, boardManager);
            }

            yield return new WaitForSeconds(resolveDelay);
        }

        private void AdvanceTurnServer()
        {
            if (!IsInitialized || Registry.Count == 0) return;
            
            pendingPurchaseSpaceIndexNet.Value = -1;
            pendingCardMoves.Clear();
            queuedPayments.Clear();
            pendingPaymentNet.Value = default;

            if (cardResolutionCoroutine != null)
            {
                StopCoroutine(cardResolutionCoroutine);
            }

            if (isResolvingCard || cardResolutionNet.Value)
            {
                EndCardResolution();
            }

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

        private void HandleCardResolutionChanged(bool previousValue, bool newValue)
        {
            CardResolutionChanged?.Invoke();
        }

        private void HandlePendingPaymentChanged(PendingPaymentState previousValue, PendingPaymentState newValue)
        {
            PaymentsChanged?.Invoke();
        }

        /// <summary>
        /// Queues a payment another player owes. Payments stay pending until the
        /// payer confirms, so reactions (future cards) can happen first.
        /// </summary>
        private void QueuePendingPayment(int payerPawnSlot, int recipientPawnSlot, int amount, string reason)
        {
            if (!IsServer || amount <= 0 || payerPawnSlot < 0)
            {
                return;
            }

            var payment = new PendingPaymentState(payerPawnSlot, recipientPawnSlot, amount, reason);
            queuedPayments.Add(payment);

            if (!pendingPaymentNet.Value.HasPayment)
            {
                PromoteNextPayment();
            }
        }

        private void PromoteNextPayment()
        {
            if (queuedPayments.Count == 0)
            {
                pendingPaymentNet.Value = default;
                return;
            }

            PendingPaymentState next = queuedPayments[0];
            queuedPayments.RemoveAt(0);
            pendingPaymentNet.Value = next;
        }

        private void HandlePayPendingPaymentServer(ulong senderClientId)
        {
            if (!IsServer)
            {
                return;
            }

            PendingPaymentState payment = pendingPaymentNet.Value;

            if (!payment.HasPayment)
            {
                return;
            }

            int payerSlot = GetPawnSlotForClient(senderClientId);

            if (payerSlot != payment.PayerPawnSlot)
            {
                return;
            }

            int amount = Mathf.Min(payment.Amount, _economyService.GetBalance(payerSlot));

            if (amount > 0)
            {
                _economyService.DeductBalance(payerSlot, amount);

                if (payment.RecipientPawnSlot >= 0)
                {
                    _economyService.AddBalance(payment.RecipientPawnSlot, amount);
                }
            }

            PlayerEconomyState payer = _economyService.GetPlayerState(payerSlot);
            string payerName = string.IsNullOrWhiteSpace(payer.DisplayName.ToString())
                ? $"Player {payerSlot + 1}"
                : payer.DisplayName.ToString();

            string message = payment.RecipientPawnSlot >= 0
                ? $"{payerName} paid {amount} to {RecipientName(payment.RecipientPawnSlot)}. {payment.Reason}"
                : $"{payerName} paid {amount}. {payment.Reason}";
            _turnResolution.SetEconomyMessage(message);
            EconomyChanged?.Invoke();
            PromoteNextPayment();
        }

        private string RecipientName(int pawnSlot)
        {
            PlayerEconomyState recipient = _economyService.GetPlayerState(pawnSlot);
            return string.IsNullOrWhiteSpace(recipient.DisplayName.ToString())
                ? $"Player {pawnSlot + 1}"
                : recipient.DisplayName.ToString();
        }

        private void HandleBuyPropertyOnServer(ulong senderClientId)
        {
            if (!EnsureReadyForTurn()) return;
            if (isResolvingCard) return;
            if (CurrentPhase != TurnPhase.WaitingForEndTurn && CurrentPhase != TurnPhase.AwaitingRoll) return;
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
