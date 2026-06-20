using MonopolyGame.Board;
using MonopolyGame.Multiplayer;
using UnityEngine;

namespace MonopolyGame.Pawns
{
    public sealed class PlayerPawnSpawner : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private Transform pawnRoot;

        [Header("Fallback Test Data")]
        [SerializeField] private int fallbackPawnCount = 2;
        [SerializeField] private int startSpaceIndex = 0;

        [Header("Visuals")]
        [SerializeField] private float pawnHeight = 0.8f;
        [SerializeField] private float pawnRadius = 0.28f;

        private static readonly Color[] PawnColors =
        {
            new Color(0.10f, 0.75f, 0.25f),
            new Color(0.85f, 0.12f, 0.12f),
            new Color(0.12f, 0.35f, 0.90f),
            new Color(0.95f, 0.82f, 0.15f)
        };

        private void Start()
        {
            SpawnInitialPawns();
        }

        [ContextMenu("Spawn Initial Pawns")]
        public void SpawnInitialPawns()
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

        private void SpawnPawn(int pawnSlot, string displayName)
        {
            GameObject pawnObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pawnObject.name = $"PlayerPawn_{pawnSlot + 1}_{displayName}";
            pawnObject.transform.SetParent(pawnRoot, false);
            pawnObject.transform.localScale = new Vector3(pawnRadius, pawnHeight / 2f, pawnRadius);

            Renderer renderer = pawnObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateMaterial(PawnColors[pawnSlot % PawnColors.Length]);
            }

            PlayerPawn pawn = pawnObject.AddComponent<PlayerPawn>();
            pawn.Initialize(
                $"player-{pawnSlot + 1}",
                displayName,
                pawnSlot,
                startSpaceIndex,
                boardManager);
        }

        private void ClearPawnRoot()
        {
            if (pawnRoot == null)
            {
                return;
            }

            for (int i = pawnRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(pawnRoot.GetChild(i).gameObject);
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            return material;
        }
    }
}