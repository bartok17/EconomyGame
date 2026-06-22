using System.Collections.Generic;
using MonopolyGame.Board;
using MonopolyGame.Multiplayer;
using MonopolyGame.Multiplayer.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace MonopolyGame.Pawns
{
    public sealed class PlayerPawnSpawner : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private Transform pawnRoot;
        [SerializeField] private GameObject pawnPrefab;

        [Header("Fallback Test Data")]
        [SerializeField] private int fallbackPawnCount = 2;
        [SerializeField] private int startSpaceIndex = 0;
        [SerializeField] private bool autoSpawnOnStart = false;

        [Header("Visuals")]
        [SerializeField] private float pawnHeight = 0.8f;
        [SerializeField] private float pawnRadius = 0.28f;

        private readonly List<PlayerPawnNetworkSync> spawnedPawnSyncs = new List<PlayerPawnNetworkSync>();

        private static readonly Color[] PawnColors =
        {
            new Color(0.10f, 0.75f, 0.25f),
            new Color(0.85f, 0.12f, 0.12f),
            new Color(0.12f, 0.35f, 0.90f),
            new Color(0.95f, 0.82f, 0.15f)
        };

        private void Start()
        {
            if (autoSpawnOnStart)
            {
                SpawnInitialPawns();
            }
        }

        public void RegisterNetworkPrefab(NetworkManager networkManager)
        {
            if (networkManager == null || pawnPrefab == null)
            {
                return;
            }

            networkManager.AddNetworkPrefab(pawnPrefab);
        }

        [ContextMenu("Spawn Initial Pawns")]
        public IReadOnlyList<PlayerPawnNetworkSync> SpawnInitialPawns()
        {
            ResolveDependencies();
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
            ResolveDependencies();
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

        private void ResolveDependencies()
        {
            if (boardManager == null)
            {
                boardManager = FindAnyObjectByType<BoardManager>();
            }

            if (pawnRoot == null)
            {
                GameObject rootObject = new GameObject("Pawns");
                rootObject.transform.SetParent(transform, false);
                pawnRoot = rootObject.transform;
            }
        }

        private int GetPawnCountFromLobby()
        {
            MultiplayerFlowCoordinator coordinator = FindAnyObjectByType<MultiplayerFlowCoordinator>();
            LobbySnapshot snapshot = coordinator != null ? coordinator.CurrentLobbySnapshot : null;

            return snapshot?.PlayerDisplayNames?.Count ?? 0;
        }

        private string GetDisplayName(int index)
        {
            MultiplayerFlowCoordinator coordinator = FindAnyObjectByType<MultiplayerFlowCoordinator>();
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

            GameObject pawnObject = Instantiate(pawnPrefab, pawnRoot);
            pawnObject.name = $"PlayerPawn_{pawnSlot + 1}_{displayName}";
            pawnObject.transform.localScale = new Vector3(pawnRadius, pawnHeight / 2f, pawnRadius);

            ApplyPawnColor(pawnObject, PawnColors[pawnSlot % PawnColors.Length]);

            PlayerPawn pawn = pawnObject.GetComponent<PlayerPawn>();
            if (pawn == null)
            {
                pawn = pawnObject.AddComponent<PlayerPawn>();
            }

            pawn.Initialize(
                $"player-{pawnSlot + 1}",
                displayName,
                pawnSlot,
                spawnSpaceIndex >= 0 ? spawnSpaceIndex : startSpaceIndex,
                boardManager);

            PlayerPawnNetworkSync pawnSync = pawnObject.GetComponent<PlayerPawnNetworkSync>();
            if (pawnSync == null)
            {
                pawnSync = pawnObject.AddComponent<PlayerPawnNetworkSync>();
            }

            pawnSync.Initialize(pawn, boardManager);
            spawnedPawnSyncs.Add(pawnSync);

            NetworkObject networkObject = pawnObject.GetComponent<NetworkObject>();
            if (networkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                networkObject.Spawn();
            }
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

        private static Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            return material;
        }

        private void ApplyPawnColor(GameObject pawnObject, Color color)
        {
            Renderer[] renderers = pawnObject.GetComponentsInChildren<Renderer>(true);
            Material material = CreateMaterial(color);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }
    }
}