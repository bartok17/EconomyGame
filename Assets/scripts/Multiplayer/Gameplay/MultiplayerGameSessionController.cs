using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonopolyGame.Board;
using MonopolyGame.Multiplayer;
using MonopolyGame.Pawns;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class MultiplayerGameSessionController : NetworkBehaviour
    {
        public enum TurnPhase
        {
            WaitingForSetup,
            AwaitingRoll,
            MovingPawn,
            ResolvingSpace,
            WaitingForEndTurn
        }

        [Header("Dependencies")]
        [SerializeField] private MultiplayerFlowCoordinator coordinator;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private PlayerPawnSpawner pawnSpawner;

        [Header("Gameplay")]
        [SerializeField] private int startSpaceIndex = 0;
        [SerializeField] private float pawnMoveDuration = 0.45f;
        [SerializeField] private float resolveDelay = 0.35f;
        [SerializeField] private bool autoAdvanceTurns;

        private readonly List<PlayerPawnNetworkSync> spawnedPawnSyncs = new List<PlayerPawnNetworkSync>();
        private readonly List<ulong> seatedClientIds = new List<ulong>();

        private Coroutine bootstrapCoroutine;
        private Coroutine turnCoroutine;

        private readonly NetworkVariable<int> currentTurnIndexNet = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> currentPhaseNet = new NetworkVariable<int>(
            (int)TurnPhase.WaitingForSetup,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> lastDiceRollNet = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString64Bytes> activePlayerNameNet = new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> activeClientIdNet = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> initializedNet = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

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

        public void BindFromScene()
        {
            ResolveDependencies();

            if (bootstrapCoroutine == null && isActiveAndEnabled)
            {
                bootstrapCoroutine = StartCoroutine(BootstrapWhenReady());
            }
        }

        private IEnumerator BootstrapWhenReady()
        {
            while (!ResolveDependencies() || coordinator == null || boardManager == null || pawnSpawner == null)
            {
                yield return null;
            }

            while (coordinator.CurrentLobbySnapshot == null)
            {
                yield return null;
            }

            InitializeFromLobbySnapshot(coordinator.CurrentLobbySnapshot);
        }

        private bool ResolveDependencies()
        {
            if (coordinator == null)
            {
                coordinator = FindAnyObjectByType<MultiplayerFlowCoordinator>();
            }

            if (boardManager == null)
            {
                boardManager = FindAnyObjectByType<BoardManager>();
            }

            if (pawnSpawner == null)
            {
                pawnSpawner = FindAnyObjectByType<PlayerPawnSpawner>();
            }

            return coordinator != null && boardManager != null && pawnSpawner != null;
        }

        private void InitializeFromLobbySnapshot(LobbySnapshot snapshot)
        {
            if (snapshot == null || IsInitialized)
            {
                return;
            }

            if (IsServer)
            {
                spawnedPawnSyncs.Clear();
                spawnedPawnSyncs.AddRange(pawnSpawner.SpawnPawns(snapshot.PlayerDisplayNames, startSpaceIndex));
                RefreshSeatedClientIds();
                SetServerState(0, TurnPhase.AwaitingRoll, 0, GetActivePlayerName(0), GetActiveClientId(0), true);
            }
            else
            {
                RefreshSpawnedPawnCacheFromScene();
                CurrentTurnIndex = currentTurnIndexNet.Value;
                CurrentPhase = (TurnPhase)currentPhaseNet.Value;
                LastDiceRoll = lastDiceRollNet.Value;
                ActivePlayerName = activePlayerNameNet.Value.ToString();
                ActiveClientId = activeClientIdNet.Value;
                IsInitialized = initializedNet.Value;
                PublishTurnState();
            }
        }

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

        public IReadOnlyList<PlayerPawn> GetSpawnedPawns()
        {
            RefreshSpawnedPawnCacheFromScene();
            return spawnedPawnSyncs.Select(sync => sync != null ? sync.GetComponent<PlayerPawn>() : null).Where(pawn => pawn != null).ToList();
        }

        public string GetPhaseLabel()
        {
            return CurrentPhase switch
            {
                TurnPhase.WaitingForSetup => "Setting up",
                TurnPhase.AwaitingRoll => "Awaiting roll",
                TurnPhase.MovingPawn => "Moving pawn",
                TurnPhase.ResolvingSpace => "Resolving space",
                TurnPhase.WaitingForEndTurn => "Waiting for end turn",
                _ => "Unknown"
            };
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRollServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer)
            {
                return;
            }

            HandleRollOnServer(serverRpcParams.Receive.SenderClientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestEndTurnServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer)
            {
                return;
            }

            HandleEndTurnOnServer(serverRpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void MovePawnClientRpc(int pawnSlot, int targetSpaceIndex, float duration)
        {
            PlayerPawnNetworkSync pawnSync = FindPawnSyncBySlot(pawnSlot);
            if (pawnSync == null)
            {
                return;
            }

            pawnSync.PlayAuthoritativeMove(targetSpaceIndex, duration);
        }

        private void HandleRollOnServer(ulong senderClientId)
        {
            if (!EnsureReadyForTurn())
            {
                return;
            }

            if (CurrentPhase != TurnPhase.AwaitingRoll)
            {
                return;
            }

            if (!IsAuthorizedTurnRequest(senderClientId))
            {
                return;
            }

            PlayerPawnNetworkSync activePawnSync = GetActivePawnSync();
            if (activePawnSync == null)
            {
                return;
            }

            int dice = UnityEngine.Random.Range(1, 7);
            int targetSpaceIndex = boardManager.NormalizeIndex(activePawnSync.CurrentSpaceIndex + dice);
            string activePlayerName = activePawnSync.DisplayName;
            ulong activeClientId = GetActiveClientId();

            SetServerState(CurrentTurnIndex, TurnPhase.MovingPawn, dice, activePlayerName, activeClientId, true);
            DiceRolled?.Invoke(dice);
            MovePawnClientRpc(activePawnSync.PawnSlot, targetSpaceIndex, pawnMoveDuration);

            if (turnCoroutine != null)
            {
                StopCoroutine(turnCoroutine);
            }

            turnCoroutine = StartCoroutine(ResolveAfterMoveRoutine(activePawnSync, targetSpaceIndex));
        }

        private void HandleEndTurnOnServer(ulong senderClientId)
        {
            if (!EnsureReadyForTurn())
            {
                return;
            }

            if (CurrentPhase != TurnPhase.WaitingForEndTurn)
            {
                return;
            }

            if (!IsAuthorizedTurnRequest(senderClientId))
            {
                return;
            }

            AdvanceTurnServer();
        }

        private IEnumerator ResolveAfterMoveRoutine(PlayerPawnNetworkSync activePawnSync, int targetSpaceIndex)
        {
            yield return new WaitForSeconds(pawnMoveDuration);

            SetServerState(CurrentTurnIndex, TurnPhase.ResolvingSpace, LastDiceRoll, ActivePlayerName, ActiveClientId, true);
            boardManager.GetSpace(targetSpaceIndex).OnPlayerLanded(activePawnSync.PlayerId);

            yield return new WaitForSeconds(resolveDelay);

            SetServerState(CurrentTurnIndex, TurnPhase.WaitingForEndTurn, LastDiceRoll, ActivePlayerName, ActiveClientId, true);

            if (autoAdvanceTurns)
            {
                AdvanceTurnServer();
            }

            turnCoroutine = null;
        }

        private void AdvanceTurnServer()
        {
            if (!IsInitialized || spawnedPawnSyncs.Count == 0)
            {
                return;
            }

            int nextTurnIndex = (CurrentTurnIndex + 1) % spawnedPawnSyncs.Count;
            SetServerState(nextTurnIndex, TurnPhase.AwaitingRoll, 0, GetActivePlayerName(nextTurnIndex), GetActiveClientId(nextTurnIndex), true);
        }

        private void SetServerState(int turnIndex, TurnPhase phase, int diceRoll, string activePlayerName, ulong activeClientId, bool isInitialized)
        {
            currentTurnIndexNet.Value = turnIndex;
            currentPhaseNet.Value = (int)phase;
            lastDiceRollNet.Value = diceRoll;
            activePlayerNameNet.Value = new FixedString64Bytes(activePlayerName ?? string.Empty);
            activeClientIdNet.Value = activeClientId;
            initializedNet.Value = isInitialized;

            SyncLocalStateFromNetwork();
        }

        private void SyncLocalStateFromNetwork()
        {
            CurrentTurnIndex = currentTurnIndexNet.Value;
            CurrentPhase = (TurnPhase)currentPhaseNet.Value;
            LastDiceRoll = lastDiceRollNet.Value;
            ActivePlayerName = activePlayerNameNet.Value.ToString();
            ActiveClientId = activeClientIdNet.Value;
            IsInitialized = initializedNet.Value;

            PublishTurnState();
        }

        private void HandleTurnIndexChanged(int previousValue, int newValue)
        {
            CurrentTurnIndex = newValue;
            PublishTurnState();
        }

        private void HandlePhaseChanged(int previousValue, int newValue)
        {
            CurrentPhase = (TurnPhase)newValue;
            PhaseChanged?.Invoke(CurrentPhase);
        }

        private void HandleDiceRollChanged(int previousValue, int newValue)
        {
            LastDiceRoll = newValue;
            DiceRolled?.Invoke(newValue);
        }

        private void HandleActivePlayerChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
        {
            ActivePlayerName = newValue.ToString();
            PublishTurnState();
        }

        private void HandleActiveClientChanged(ulong previousValue, ulong newValue)
        {
            ActiveClientId = newValue;
        }

        private void HandleInitializedChanged(bool previousValue, bool newValue)
        {
            IsInitialized = newValue;
        }

        private void PublishTurnState()
        {
            string activeName = string.IsNullOrWhiteSpace(ActivePlayerName)
                ? GetActivePlayerName(CurrentTurnIndex)
                : ActivePlayerName;

            TurnChanged?.Invoke(CurrentTurnIndex, activeName);
            PhaseChanged?.Invoke(CurrentPhase);
        }

        private bool EnsureReadyForTurn()
        {
            if (!ResolveDependencies())
            {
                return false;
            }

            if (!IsInitialized)
            {
                LobbySnapshot snapshot = coordinator != null ? coordinator.CurrentLobbySnapshot : null;
                if (snapshot != null)
                {
                    InitializeFromLobbySnapshot(snapshot);
                }
            }

            return spawnedPawnSyncs.Count > 0 && boardManager != null;
        }

        private PlayerPawnNetworkSync GetActivePawnSync()
        {
            RefreshSpawnedPawnCacheFromScene();

            if (spawnedPawnSyncs.Count == 0)
            {
                return null;
            }

            int slot = CurrentTurnIndex % spawnedPawnSyncs.Count;
            return spawnedPawnSyncs[slot];
        }

        private PlayerPawnNetworkSync FindPawnSyncBySlot(int pawnSlot)
        {
            RefreshSpawnedPawnCacheFromScene();

            for (int i = 0; i < spawnedPawnSyncs.Count; i++)
            {
                PlayerPawnNetworkSync pawnSync = spawnedPawnSyncs[i];
                if (pawnSync != null && pawnSync.PawnSlot == pawnSlot)
                {
                    return pawnSync;
                }
            }

            return null;
        }

        private void RefreshSeatedClientIds()
        {
            seatedClientIds.Clear();

            if (NetworkManager.Singleton == null)
            {
                return;
            }

            seatedClientIds.AddRange(NetworkManager.Singleton.ConnectedClientsIds);
        }

        private void RefreshSpawnedPawnCacheFromScene()
        {
            PlayerPawnNetworkSync[] foundSyncs = FindObjectsByType<PlayerPawnNetworkSync>(FindObjectsInactive.Exclude);

            if (foundSyncs == null || foundSyncs.Length == 0)
            {
                return;
            }

            spawnedPawnSyncs.Clear();
            spawnedPawnSyncs.AddRange(foundSyncs.OrderBy(sync => sync.PawnSlot));
        }

        private ulong GetActiveClientId(int? turnIndexOverride = null)
        {
            if (seatedClientIds.Count == 0)
            {
                RefreshSeatedClientIds();
            }

            if (seatedClientIds.Count == 0)
            {
                return NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            }

            int turnIndex = turnIndexOverride ?? CurrentTurnIndex;
            int slot = Mathf.Abs(turnIndex) % seatedClientIds.Count;
            return seatedClientIds[slot];
        }

        private bool IsAuthorizedTurnRequest(ulong senderClientId)
        {
            ulong expectedClientId = GetActiveClientId();

            if (expectedClientId == 0)
            {
                return true;
            }

            if (senderClientId != expectedClientId)
            {
                Debug.LogWarning($"[MultiplayerGameSession] Rejected turn request from client {senderClientId}. Expected {expectedClientId}.");
                return false;
            }

            return true;
        }

        private string GetActivePlayerName(int turnIndex)
        {
            if (spawnedPawnSyncs.Count == 0)
            {
                return $"Player {turnIndex + 1}";
            }

            int index = Mathf.Abs(turnIndex) % spawnedPawnSyncs.Count;
            PlayerPawnNetworkSync pawnSync = spawnedPawnSyncs[index];
            return pawnSync != null ? pawnSync.DisplayName : $"Player {index + 1}";
        }
    }
}
