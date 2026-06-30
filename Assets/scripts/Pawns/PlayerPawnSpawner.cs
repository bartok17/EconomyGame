using System.Collections.Generic;
using MonopolyGame.Board;
using MonopolyGame.Multiplayer;
using MonopolyGame.Multiplayer.Gameplay;
using UnityEngine;

namespace MonopolyGame.Pawns
{
    public sealed class PlayerPawnSpawner : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private MultiplayerFlowCoordinator coordinator;
        [SerializeField] private Transform pawnRoot;
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private PawnVisualConfig pawnVisualConfig;

        [Header("Fallback Test Data")]
        [SerializeField] private int fallbackPawnCount = 2;
        [SerializeField] private int startSpaceIndex = 0;
        [SerializeField] private bool autoSpawnOnStart = false;

        private readonly List<PlayerPawnNetworkSync> spawnedPawnSyncs = new List<PlayerPawnNetworkSync>();

        private void Start()
        {
            if (autoSpawnOnStart)
            {
                SpawnInitialPawns();
            }
        }

        public void BindDependencies(BoardManager boardManager, MultiplayerFlowCoordinator coordinator)
        {
            this.boardManager = boardManager;
            this.coordinator = coordinator;
        }

        [ContextMenu("Spawn Initial Pawns")]
        public IReadOnlyList<PlayerPawnNetworkSync> SpawnInitialPawns()
        {
            if (!ResolveDependencies())
            {
                return spawnedPawnSyncs;
            }

            ClearPawnRoot();

            int pawnCount = GetPawnCountFromLobby();
            if (pawnCount <= 0)
            {
                pawnCount = fallbackPawnCount;
            }

            pawnCount = Mathf.Clamp(pawnCount, 1, 4);

            for (int i = 0; i < pawnCount; i++)
            {
                SpawnPawn(i, GetDisplayName(i));
            }

            return spawnedPawnSyncs;
        }

        public IReadOnlyList<PlayerPawnNetworkSync> SpawnPawns(IReadOnlyList<string> displayNames, int spawnStartSpaceIndex = -1)
        {
            if (!ResolveDependencies())
            {
                return spawnedPawnSyncs;
            }

            ClearPawnRoot();

            int pawnCount = displayNames != null ? displayNames.Count : 0;
            if (pawnCount <= 0)
            {
                pawnCount = fallbackPawnCount;
            }

            pawnCount = Mathf.Clamp(pawnCount, 1, 4);

            int effectiveStartSpace = spawnStartSpaceIndex >= 0 ? spawnStartSpaceIndex : startSpaceIndex;

            for (int i = 0; i < pawnCount; i++)
            {
                string displayName = displayNames != null && i < displayNames.Count
                    ? displayNames[i]
                    : GetDisplayName(i);

                SpawnPawn(i, displayName, effectiveStartSpace);
            }

            return spawnedPawnSyncs;
        }

        private bool ResolveDependencies()
        {
            if (pawnRoot == null)
            {
                GameObject rootObject = new GameObject("Pawns");
                rootObject.transform.SetParent(transform, false);
                pawnRoot = rootObject.transform;
            }

            if (boardManager == null)
            {
                Debug.LogError($"[{nameof(PlayerPawnSpawner)}] Cannot spawn pawns because no BoardManager is assigned.");
                return false;
            }

            return true;
        }

        private int GetPawnCountFromLobby()
        {
            LobbySnapshot snapshot = coordinator != null ? coordinator.CurrentLobbySnapshot : null;

            return snapshot?.PlayerDisplayNames?.Count ?? 0;
        }

        private string GetDisplayName(int index)
        {
            LobbySnapshot snapshot = coordinator != null ? coordinator.CurrentLobbySnapshot : null;

            if (snapshot?.PlayerDisplayNames != null && index < snapshot.PlayerDisplayNames.Count)
            {
                return snapshot.PlayerDisplayNames[index];
            }

            return $"Player {index + 1}";
        }

        private void SpawnPawn(int pawnSlot, string displayName, int spawnSpaceIndex = -1)
        {
            if (pawnPrefab == null)
            {
                Debug.LogError($"[{nameof(PlayerPawnSpawner)}] Cannot spawn pawns because no pawn prefab is assigned.");
                return;
            }

            PlayerPawnNetworkSync pawnSync = PawnFactory.CreatePawn(
                pawnPrefab,
                pawnRoot,
                pawnSlot,
                displayName,
                spawnSpaceIndex >= 0 ? spawnSpaceIndex : startSpaceIndex,
                boardManager,
                pawnVisualConfig);

            if (pawnSync == null)
            {
                return;
            }

            spawnedPawnSyncs.Add(pawnSync);
        }

        private void ClearPawnRoot()
        {
            spawnedPawnSyncs.Clear();

            if (pawnRoot == null)
            {
                return;
            }

            for (int i = pawnRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(pawnRoot.GetChild(i).gameObject);
            }
        }

        public IReadOnlyList<PlayerPawnNetworkSync> GetSpawnedPawnSyncs()
        {
            return spawnedPawnSyncs;
        }
    }
}