using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonopolyGame.Board;
using MonopolyGame.Pawns;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerPawnNetworkSync : NetworkBehaviour
    {
        private static readonly List<PlayerPawnNetworkSync> spawnedPawnSyncs = new List<PlayerPawnNetworkSync>();

        [SerializeField] private PlayerPawn pawn;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private PawnVisualConfig pawnVisualConfig;

        private int pendingPawnSlot = -1;
        private string pendingPlayerId = string.Empty;
        private string pendingDisplayName = string.Empty;

        private readonly NetworkVariable<int> pawnSlotNet = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString64Bytes> playerIdNet = new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString64Bytes> displayNameNet = new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> syncedSpaceIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> isMovingNet = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public string PlayerId => pawn != null && !string.IsNullOrWhiteSpace(pawn.PlayerId) ? pawn.PlayerId : playerIdNet.Value.ToString();
        public string DisplayName => pawn != null && !string.IsNullOrWhiteSpace(pawn.DisplayName) ? pawn.DisplayName : displayNameNet.Value.ToString();
        public int PawnSlot => pawn != null && pawn.PawnSlot >= 0 ? pawn.PawnSlot : pawnSlotNet.Value;
        public int CurrentSpaceIndex => pawn != null ? pawn.CurrentSpaceIndex : syncedSpaceIndex.Value;

        public void Initialize(PlayerPawn pawn, BoardManager boardManager, int pawnSlot, string playerId, string displayName)
        {
            this.pawn = pawn;
            this.boardManager = boardManager;
            pendingPawnSlot = pawnSlot;
            pendingPlayerId = playerId ?? string.Empty;
            pendingDisplayName = displayName ?? string.Empty;

            if (pawn != null && boardManager != null)
            {
                pawn.Initialize(pendingPlayerId, pendingDisplayName, pendingPawnSlot, syncedSpaceIndex.Value, boardManager);
            }

            TryApplyPawnState();
        }

        public override void OnNetworkSpawn()
        {
            syncedSpaceIndex.OnValueChanged += HandleSyncedSpaceIndexChanged;
            isMovingNet.OnValueChanged += HandleIsMovingChanged;
            pawnSlotNet.OnValueChanged += HandlePawnSlotChanged;
            playerIdNet.OnValueChanged += HandlePlayerIdChanged;
            displayNameNet.OnValueChanged += HandleDisplayNameChanged;

            RegisterSpawnedSync(this);

            if (IsServer)
            {
                pawnSlotNet.Value = pendingPawnSlot;
                playerIdNet.Value = new FixedString64Bytes(pendingPlayerId);
                displayNameNet.Value = new FixedString64Bytes(pendingDisplayName);
            }

            TryApplyPawnState();
        }

        // Start() runs after all Awake() calls complete, so it is the safe retry point
        // for the case where FindAnyObjectByType<BoardManager>() returned null during
        // OnNetworkSpawn because BoardManager.Awake had not yet executed.
        private void Start()
        {
            if (pawn == null || boardManager == null)
            {
                TryApplyPawnState();
            }
        }

        public override void OnNetworkDespawn()
        {
            syncedSpaceIndex.OnValueChanged -= HandleSyncedSpaceIndexChanged;
            isMovingNet.OnValueChanged -= HandleIsMovingChanged;
            pawnSlotNet.OnValueChanged -= HandlePawnSlotChanged;
            playerIdNet.OnValueChanged -= HandlePlayerIdChanged;
            displayNameNet.OnValueChanged -= HandleDisplayNameChanged;
            UnregisterSpawnedSync(this);
        }

        public void PlayAuthoritativeMove(int targetSpaceIndex, float duration)
        {
            if (pawn == null || boardManager == null)
            {
                return;
            }

            int normalizedTarget = boardManager.NormalizeIndex(targetSpaceIndex);

            if (IsServer)
            {
                isMovingNet.Value = true;
                StartCoroutine(AnimateMoveRoutine(normalizedTarget, duration, writeBackToNetwork: true));
            }
            else
            {
                StartCoroutine(AnimateMoveRoutine(normalizedTarget, duration, writeBackToNetwork: false));
            }
        }

        public void ForceSpaceIndex(int targetSpaceIndex)
        {
            if (pawn == null || boardManager == null)
            {
                return;
            }

            int normalizedTarget = boardManager.NormalizeIndex(targetSpaceIndex);
            pawn.PlaceOnSpace(normalizedTarget);

            if (IsServer)
            {
                syncedSpaceIndex.Value = normalizedTarget;
                isMovingNet.Value = false;
            }
        }

        private IEnumerator AnimateMoveRoutine(int targetSpaceIndex, float duration, bool writeBackToNetwork)
        {
            if (pawn == null || boardManager == null)
            {
                yield break;
            }

            yield return StartCoroutine(pawn.MoveToSpaceRoutine(targetSpaceIndex, duration));

            if (writeBackToNetwork && IsServer)
            {
                syncedSpaceIndex.Value = targetSpaceIndex;
                isMovingNet.Value = false;
            }
        }

        private void HandleSyncedSpaceIndexChanged(int previousValue, int newValue)
        {
            if (pawn == null || boardManager == null)
            {
                return;
            }

            if (!IsServer && !isMovingNet.Value)
            {
                pawn.PlaceOnSpace(newValue);
            }
        }

        private void HandleIsMovingChanged(bool previousValue, bool newValue)
        {
            if (!newValue && pawn != null && boardManager != null && !IsServer)
            {
                pawn.PlaceOnSpace(syncedSpaceIndex.Value);
            }
        }

        private void HandlePawnSlotChanged(int previousValue, int newValue)
        {
            TryApplyPawnState();
        }

        private void HandlePlayerIdChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
        {
            TryApplyPawnState();
        }

        private void HandleDisplayNameChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
        {
            TryApplyPawnState();
        }

        private void TryApplyPawnState()
        {
            ResolveLocalReferences();

            if (pawn == null)
            {
                Debug.LogWarning($"[PlayerPawnNetworkSync] {gameObject.name}: PlayerPawn component is null after ResolveLocalReferences. " +
                                 "Ensure the pawn prefab has PlayerPawn pre-added.");
                return;
            }

            if (boardManager == null)
            {
                Debug.LogWarning($"[PlayerPawnNetworkSync] {gameObject.name}: BoardManager not found. " +
                                 "Pawn initialization deferred to Start(). If this persists, ensure BoardManager is in the scene.");
                return;
            }

            int resolvedPawnSlot = IsServer ? pendingPawnSlot : pawnSlotNet.Value;
            string resolvedPlayerId = IsServer ? pendingPlayerId : playerIdNet.Value.ToString();
            string resolvedDisplayName = IsServer ? pendingDisplayName : displayNameNet.Value.ToString();

            if (resolvedPawnSlot < 0)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(resolvedPlayerId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(resolvedDisplayName))
            {
                return;
            }

            pawn.Initialize(resolvedPlayerId, resolvedDisplayName, resolvedPawnSlot, syncedSpaceIndex.Value, boardManager);
            ApplyVisualStyleFromConfig(resolvedPawnSlot);

            Debug.Log($"[PlayerPawnNetworkSync] {gameObject.name}: pawn initialized. slot={resolvedPawnSlot}, player={resolvedDisplayName}, space={syncedSpaceIndex.Value}, isServer={IsServer}");
        }

        private void ApplyVisualStyleFromConfig(int pawnSlot)
        {
            float height = pawnVisualConfig != null ? pawnVisualConfig.PawnHeight : 0.8f;
            float radius = pawnVisualConfig != null ? pawnVisualConfig.PawnRadius : 0.28f;
            gameObject.transform.localScale = new Vector3(radius, height / 2f, radius);

            Color color = pawnVisualConfig != null ? pawnVisualConfig.GetPawnColor(pawnSlot) : Color.white;
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;

            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.sharedMaterial = material;
                }
            }
        }

        private void ResolveLocalReferences()
        {
            if (pawn == null)
            {
                pawn = GetComponent<PlayerPawn>();
            }

            if (boardManager == null)
            {
                boardManager = GetComponentInParent<BoardManager>();

                if (boardManager == null)
                {
                    boardManager = Object.FindAnyObjectByType<BoardManager>();
                }
            }
        }

        public static IReadOnlyList<PlayerPawnNetworkSync> GetSpawnedPawnSyncs()
        {
            return spawnedPawnSyncs.Where(sync => sync != null).OrderBy(sync => sync.PawnSlot).ToList();
        }

        public static bool TryGetPawnSyncBySlot(int pawnSlot, out PlayerPawnNetworkSync pawnSync)
        {
            pawnSync = spawnedPawnSyncs.FirstOrDefault(sync => sync != null && sync.PawnSlot == pawnSlot);
            return pawnSync != null;
        }

        private static void RegisterSpawnedSync(PlayerPawnNetworkSync pawnSync)
        {
            if (pawnSync == null || spawnedPawnSyncs.Contains(pawnSync))
            {
                return;
            }

            spawnedPawnSyncs.Add(pawnSync);
        }

        private static void UnregisterSpawnedSync(PlayerPawnNetworkSync pawnSync)
        {
            if (pawnSync == null)
            {
                return;
            }

            spawnedPawnSyncs.Remove(pawnSync);
        }
    }
}