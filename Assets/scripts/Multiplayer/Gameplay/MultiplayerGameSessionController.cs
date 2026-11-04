using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private const Key DevGrantCardKey = Key.F8;
        
        [Header("Disconnect & Reconnect")]
        [Tooltip("Czas na powrót gracza (w sekundach) przed eliminacją.")]
        [SerializeField] private float disconnectTimeout = 60f;

        private GameSessionPawnRegistry pawnRegistry;
        private readonly TurnStateMachine turnStateMachine = new TurnStateMachine();
        private readonly BoardRuleResolver boardRuleResolver = new BoardRuleResolver();
        private IPlayerEconomyService _economyService;
        private TurnResolutionService _turnResolution;
        private ActionCardSessionService _actionCardService;
        private readonly List<ActionCardHandEntry> localActionCardHand = new List<ActionCardHandEntry>();

        private Coroutine bootstrapCoroutine;
        private Coroutine turnCoroutine;
        private readonly Dictionary<int, Coroutine> _disconnectCoroutines = new Dictionary<int, Coroutine>();
        private bool _isEndingGame;

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

        private readonly NetworkVariable<ActionCardPileState> actionCardPileStateNet = new NetworkVariable<ActionCardPileState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public event Action EconomyChanged;
        public event Action ActionCardsChanged;

        // ── Public state ────────────────────────────────────────────────────────────

        public TurnPhase CurrentPhase => currentPhaseNet != null ? (TurnPhase)currentPhaseNet.Value : TurnPhase.WaitingForSetup;
        public int CurrentTurnIndex => currentTurnIndexNet != null ? currentTurnIndexNet.Value : 0;
        public int LastDiceRoll => lastDiceRollNet != null ? lastDiceRollNet.Value : 0;
        public string ActivePlayerName => activePlayerNameNet != null ? activePlayerNameNet.Value.ToString() : string.Empty;
        public ulong ActiveClientId => activeClientIdNet != null ? activeClientIdNet.Value : 0;
        public bool IsInitialized => initializedNet != null && initializedNet.Value;
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
        }

        private void Update()
        {
            if (!enableDevGrantCardShortcut || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current[DevGrantCardKey].wasPressedThisFrame)
            {
                RequestDevActionCardGrant();
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
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
            }

            SyncLocalStateFromNetwork();

            if (IsServer)
            {
                if (bootstrapCoroutine == null)
                {
                    bootstrapCoroutine = StartCoroutine(BootstrapWhenReady());
                }
            }
            else
            {
                StartCoroutine(ClientReconnectAnnouncementRoutine());
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
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }

            localActionCardHand.Clear();
        }
        
        private void HandleClientConnected(ulong newClientId)
        {
            if (!IsServer) return;
            if (newClientId == NetworkManager.ServerClientId) return;

            Debug.Log($"[MultiplayerGameSession] Server detected client connected: {newClientId}");

            TurnParticipant disconnectedParticipant = null;
            if (turnStateMachine != null && turnStateMachine.Participants != null)
            {
                disconnectedParticipant = turnStateMachine.Participants.FirstOrDefault(p => !p.IsConnected);
            }

            if (disconnectedParticipant != null)
            {
                Debug.Log($"[MultiplayerGameSession] Auto-reconnecting client {newClientId} to participant {disconnectedParticipant.DisplayName} (slot {disconnectedParticipant.TurnIndex})");
                HandlePlayerReconnectedOnServer(newClientId, disconnectedParticipant.DisplayName);
            }
        }

        private void HandleClientDisconnected(ulong disconnectedClientId)
        {
            if (_isEndingGame) return;

            if (IsServer)
            {
                if (disconnectedClientId == NetworkManager.ServerClientId) return;

                int slot = GetPawnSlotForClient(disconnectedClientId);
                if (slot >= 0)
                {
                    HandlePlayerDisconnectedOnServer(slot);
                }
            }
            else
            {
                if (disconnectedClientId == NetworkManager.ServerClientId)
                {
                    lastEconomyMessageNet.Value = new FixedString128Bytes("Host disconnected. Returning to menu...");
                    StartCoroutine(ReturnToMenuDelayedRoutine(2.5f));
                }
            }
        }

        private void HandlePlayerDisconnectedOnServer(int slotIndex)
        {
            if (_isEndingGame) return;
            if (!turnStateMachine.IsParticipantConnected(slotIndex) && _disconnectCoroutines.ContainsKey(slotIndex))
            {
                return;
            }
            turnStateMachine.SetParticipantConnected(slotIndex, false);
            string pName = turnStateMachine.GetParticipantName(slotIndex);
            lastEconomyMessageNet.Value = new FixedString128Bytes($"{pName} disconnected! Reconnect time: {disconnectTimeout:0}s...");
            if (_disconnectCoroutines.TryGetValue(slotIndex, out Coroutine existing) && existing != null)
            {
                StopCoroutine(existing);
            }
            _disconnectCoroutines[slotIndex] = StartCoroutine(DisconnectedPlayerCountdownRoutine(slotIndex, disconnectTimeout));
        }

        private IEnumerator DisconnectedPlayerCountdownRoutine(int slotIndex, float duration)
        {
            float remaining = duration;
            string pName = turnStateMachine.GetParticipantName(slotIndex);
            while (remaining > 0f)
            {
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
                if (remaining == 45f || remaining == 30f || remaining == 20f || remaining == 15f || (remaining <= 10f && remaining > 0f))
                {
                    lastEconomyMessageNet.Value = new FixedString128Bytes($"{pName} offline. Time remaining: {remaining:0}s");
                }
            }
            EliminateDisconnectedPlayer(slotIndex);
        }

        private void EliminateDisconnectedPlayer(int slotIndex)
        {
            if (_disconnectCoroutines.TryGetValue(slotIndex, out Coroutine c) && c != null)
            {
                StopCoroutine(c);
                _disconnectCoroutines.Remove(slotIndex);
            }
            string pName = turnStateMachine.GetParticipantName(slotIndex);
            if (propertyOwnershipNet != null)
            {
                for (int i = 0; i < propertyOwnershipNet.Count; i++)
                {
                    if (propertyOwnershipNet[i].OwnerPawnSlot == slotIndex)
                    {
                        propertyOwnershipNet[i] = new PropertyOwnershipState(propertyOwnershipNet[i].SpaceIndex, -1, string.Empty, string.Empty);
                    }
                }
                ApplyPropertyOwnersToBoard();
            }
            PlayerPawnNetworkSync pawnSync = Registry.FindBySlot(slotIndex);
            if (pawnSync != null)
            {
                pawnSync.gameObject.SetActive(false);
            }
            turnStateMachine.RemoveParticipant(slotIndex);
            lastEconomyMessageNet.Value = new FixedString128Bytes($"{pName} eliminated (timeout expired).");
            if (turnStateMachine.ParticipantCount <= 1)
            {
                string winnerName = turnStateMachine.ParticipantCount == 1
                    ? turnStateMachine.GetParticipantName(0)
                    : "None";
                lastEconomyMessageNet.Value = new FixedString128Bytes($"Game Over! Winner: {winnerName}!");
                StartCoroutine(EndGameDelayedRoutine(4f, winnerName));
                return;
            }
            if (CurrentTurnIndex == slotIndex)
            {
                AdvanceTurnServer();
            }
        }

        private IEnumerator EndGameDelayedRoutine(float delay, string winnerName)
        {
            _isEndingGame = true;
            NotifyGameEndedClientRpc(winnerName);
            yield return new WaitForSeconds(delay);
            ReturnToMenu();
        }

        public void RequestLeaveGame()
        {
            if (IsServer)
            {
                lastEconomyMessageNet.Value = new FixedString128Bytes("Host ended the match.");
                StartCoroutine(EndGameDelayedRoutine(1.5f, "None (Host left)"));
            }
            else
            {
                NotifyPlayerForfeitServerRpc();
                ReturnToMenu();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void NotifyPlayerForfeitServerRpc(ServerRpcParams serverRpcParams = default)
        {
            ulong senderId = serverRpcParams.Receive.SenderClientId;
            int slot = GetPawnSlotForClient(senderId);
            if (slot >= 0)
            {
                string pName = turnStateMachine.GetParticipantName(slot);
                lastEconomyMessageNet.Value = new FixedString128Bytes($"{pName} forfeited and left the game.");
                EliminateDisconnectedPlayer(slot);
            }
        }

        [ClientRpc]
        private void NotifyGameEndedClientRpc(string winnerName)
        {
            lastEconomyMessageNet.Value = new FixedString128Bytes($"Game Over! Winner: {winnerName}");
            if (!IsServer)
            {
                StartCoroutine(ReturnToMenuDelayedRoutine(4f));
            }
        }

        private IEnumerator ReturnToMenuDelayedRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToMenu();
        }

        public void ReturnToMenu()
        {
            StartCoroutine(ReturnToMenuRoutine());
        }

        private IEnumerator ReturnToMenuRoutine()
        {
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }
            if (coordinator != null)
            {
                System.Threading.Tasks.Task leaveTask = coordinator.LeaveLobbyAsync();
                while (!leaveTask.IsCompleted)
                {
                    yield return null;
                }
            }
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        private IEnumerator ClientReconnectAnnouncementRoutine()
        {
            while (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            {
                yield return null;
            }

            string myName = string.Empty;
            if (coordinator != null)
            {
                myName = coordinator.LocalDisplayName;
            }
            if (string.IsNullOrWhiteSpace(myName) && MultiplayerFlowCoordinator.Instance != null)
            {
                myName = MultiplayerFlowCoordinator.Instance.LocalDisplayName;
            }

            RequestReconnectServerRpc(myName ?? string.Empty);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestReconnectServerRpc(string displayName, ServerRpcParams serverRpcParams = default)
        {
            ulong senderClientId = serverRpcParams.Receive.SenderClientId;
            HandlePlayerReconnectedOnServer(senderClientId, displayName);
        }

        private void HandlePlayerReconnectedOnServer(ulong newClientId, string displayName)
        {
            TurnParticipant participant = null;

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                participant = turnStateMachine.FindParticipantByName(displayName);
            }
            
            if (participant == null && turnStateMachine != null && turnStateMachine.Participants != null)
            {
                participant = turnStateMachine.Participants.FirstOrDefault(p => !p.IsConnected);
            }

            if (participant == null)
            {
                Debug.LogWarning($"[MultiplayerGameSession] Reconnect from client {newClientId} ('{displayName}') could not be matched!");
                return;
            }

            int slot = participant.TurnIndex;
            
            // 1. ZATRZYMAJ ODLICZANIE 60s
            if (_disconnectCoroutines.TryGetValue(slot, out Coroutine c) && c != null)
            {
                StopCoroutine(c);
                _disconnectCoroutines.Remove(slot);
            }

            // 2. OZNACZ JAKO POŁĄCZONEGO I ZAKTUALIZUJ CLIENT ID
            turnStateMachine.SetParticipantConnected(slot, true);
            turnStateMachine.UpdateParticipantClientId(slot, newClientId);
            
            // 3. PRZYPISZ WŁASNOŚĆ PIONKA
            PlayerPawnNetworkSync pawnSync = Registry.FindBySlot(slot);
            if (pawnSync != null)
            {
                pawnSync.gameObject.SetActive(true);
                if (pawnSync.NetworkObject != null && pawnSync.NetworkObject.IsSpawned)
                {
                    pawnSync.NetworkObject.DontDestroyWithOwner = true;
                    pawnSync.NetworkObject.ChangeOwnership(newClientId);
                }
                pawnSync.ForceSpaceIndex(pawnSync.CurrentSpaceIndex);
            }

            // 4. JEŚLI TO TURA TEGO GRACZA - USTAW AKTYWNY CLIENT ID
            if (CurrentTurnIndex == slot)
            {
                activeClientIdNet.Value = newClientId;
            }

            SetServerState(turnStateMachine.State);
            lastEconomyMessageNet.Value = new FixedString128Bytes($"{participant.DisplayName} reconnected to the game!");
            PublishTurnState();
        }

        public string GetRoomCode()
        {
            if (coordinator != null && coordinator.CurrentLobbySnapshot != null && !string.IsNullOrWhiteSpace(coordinator.CurrentLobbySnapshot.LobbyCode))
            {
                return coordinator.CurrentLobbySnapshot.LobbyCode;
            }
            return coordinator != null ? coordinator.LastActiveLobbyCode : string.Empty;
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
            int pawnSlot = GetLocalPawnSlot();
            if (pawnSlot < 0 || _economyService == null)
            {
                return 0;
            }

            return _economyService.GetBalance(pawnSlot);
        }

        public int GetLocalPawnSlot()
        {
            foreach (var sync in PlayerPawnNetworkSync.GetSpawnedPawnSyncs())
            {
                if (sync != null && sync.IsOwner && sync.PawnSlot >= 0)
                {
                    return sync.PawnSlot;
                }
            }

            string localName = coordinator != null ? coordinator.LocalDisplayName : string.Empty;
            if (!string.IsNullOrWhiteSpace(localName) && playerEconomyNet != null)
            {
                for (int i = 0; i < playerEconomyNet.Count; i++)
                {
                    if (string.Equals(playerEconomyNet[i].DisplayName.ToString(), localName, StringComparison.OrdinalIgnoreCase))
                    {
                        return playerEconomyNet[i].PawnSlot;
                    }
                }
            }

            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            return GetPawnSlotForClient(localClientId);
        }

        public IReadOnlyList<PlayerEconomyState> GetPlayerEconomyStates()
        {
            return _economyService != null
                ? _economyService.GetAllEconomyStates()
                : Array.Empty<PlayerEconomyState>();
        }

        // ── Action Cards API ────────────────────────────────────────────────────────

        public int ActionCardDrawPileCount => actionCardPileStateNet.Value.DrawPileCount;
        public int ActionCardDiscardPileCount => actionCardPileStateNet.Value.DiscardPileCount;
        public int ActionCardHandCapacity => ActionCardHand.DefaultCapacity;

        public string ActionCardTopDiscardLabel
        {
            get
            {
                ActionCardPileState state = actionCardPileStateNet.Value;
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

        [ServerRpc(RequireOwnership = false)]
        private void RequestActionCardHandServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer)
            {
                return;
            }

            ulong senderClientId = serverRpcParams.Receive.SenderClientId;
            int pawnSlot = GetPawnSlotForClient(senderClientId);

            if (pawnSlot < 0)
            {
                return;
            }

            SendActionCardHandToClient(pawnSlot, _actionCardService.GetHandEntries(pawnSlot), senderClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDevGrantActionCardServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer || !enableDevGrantCardShortcut)
            {
                return;
            }

            ulong senderClientId = serverRpcParams.Receive.SenderClientId;
            int pawnSlot = GetPawnSlotForClient(senderClientId);

            if (pawnSlot < 0)
            {
                return;
            }

            ActionCardGrantResult result = GrantActionCard(pawnSlot);
            Debug.Log($"[Dev] Action card grant for client {senderClientId} (slot {pawnSlot}): {result.Outcome}.");
        }

        [ClientRpc]
        private void SyncLocalActionCardHandClientRpc(
            ActionCardHandEntry[] entries,
            ClientRpcParams clientRpcParams = default)
        {
            localActionCardHand.Clear();

            if (entries != null)
            {
                localActionCardHand.AddRange(entries);
            }

            ActionCardsChanged?.Invoke();
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

        // ── Bootstrap ───────────────────────────────────────────────────────────────

        private IEnumerator BootstrapWhenReady()
        {
            while (coordinator == null || boardManager == null || pawnSpawner == null)
            {
                if (coordinator == null)
                {
                    coordinator = MultiplayerFlowCoordinator.Instance ?? FindAnyObjectByType<MultiplayerFlowCoordinator>();
                }
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
                _actionCardService.Initialize(snapshot.PlayerCount);
                turnStateMachine.StartGame(0);
                lastResolvedSpaceIndexNet.Value = startSpaceIndex;
                lastEconomyMessageNet.Value = new FixedString128Bytes("Game started.");
                SetServerState(turnStateMachine.State);
                SendInitialActionCardHands();
            }
            else
            {
                Registry.Refresh();
                if (snapshot.PlayerDisplayNames != null && snapshot.PlayerDisplayNames.Count > 0)
                {
                    List<TurnParticipant> clientParticipants = new List<TurnParticipant>();
                    for (int i = 0; i < snapshot.PlayerDisplayNames.Count; i++)
                    {
                        clientParticipants.Add(new TurnParticipant(i, snapshot.PlayerDisplayNames[i], (ulong)i));
                    }
                    turnStateMachine.SetParticipants(clientParticipants);
                }

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
            
            bool isOfflinePlayerTurn = !turnStateMachine.IsParticipantConnected(CurrentTurnIndex);
            if (!isOfflinePlayerTurn && !turnStateMachine.IsAuthorizedTurnRequest(senderClientId)) return;

            PlayerPawnNetworkSync activePawn = Registry.GetAtTurnIndex(CurrentTurnIndex);
            if (activePawn == null) return;

            int dice = UnityEngine.Random.Range(1, 7);
            int rawTargetSpaceIndex = activePawn.CurrentSpaceIndex + dice;
            bool passedStart = boardManager != null && boardManager.SpaceCount > 0 && rawTargetSpaceIndex >= boardManager.SpaceCount;
            int targetSpaceIndex = boardManager.NormalizeIndex(activePawn.CurrentSpaceIndex + dice);

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
                _turnResolution.UpdatePendingPurchase(result, boardManager);
            }

            yield return new WaitForSeconds(resolveDelay);

            turnStateMachine.BeginWaitingForEndTurn();
            SetServerState(turnStateMachine.State);

            if (autoAdvanceTurns)
            {
                AdvanceTurnServer();
            }

            turnCoroutine = null;
        }

        private void AdvanceTurnServer()
        {
            if (!IsInitialized || Registry.Count == 0) return;
            
            pendingPurchaseSpaceIndexNet.Value = -1;
            
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
            turnStateMachine.SetState(new TurnState(CurrentTurnIndex, CurrentPhase, LastDiceRoll, ActivePlayerName, ActiveClientId, IsInitialized));
            PublishTurnState();
        }

        private void HandleTurnIndexChanged(int _, int newValue)    { PublishTurnState(); }
        private void HandlePhaseChanged(int _, int newValue)
        {
            PhaseChanged?.Invoke(CurrentPhase);

            if (CurrentPhase == TurnPhase.ResolvingSpace && !IsServer)
            {
                PlayerPawnNetworkSync activePawn = Registry.GetAtTurnIndex(CurrentTurnIndex);
                if (activePawn != null)
                {
                    PawnMoved?.Invoke(activePawn.Pawn, activePawn.CurrentSpaceIndex);
                }
            }
        }
        private void HandleDiceRollChanged(int _, int newValue)     { DiceRolled?.Invoke(newValue); }
        private void HandleActivePlayerChanged(FixedString64Bytes _, FixedString64Bytes newValue) { PublishTurnState(); }
        private void HandleActiveClientChanged(ulong _, ulong newValue) { PublishTurnState(); }
        private void HandleInitializedChanged(bool _, bool newValue)
        {
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

        private void HandleActionCardPileStateChanged(ActionCardPileState previousValue, ActionCardPileState newValue)
        {
            ActionCardsChanged?.Invoke();
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
            if (turnStateMachine != null && turnStateMachine.Participants != null)
            {
                for (int i = 0; i < turnStateMachine.Participants.Count; i++)
                {
                    if (turnStateMachine.Participants[i].ClientId == clientId)
                    {
                        return turnStateMachine.Participants[i].TurnIndex;
                    }
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