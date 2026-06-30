using System.Collections;
using MonopolyGame.Board;
using MonopolyGame.Multiplayer.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace MonopolyGame.Pawns
{
    public sealed class PlayerPawn : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string playerId;
        [SerializeField] private string displayName;
        [SerializeField] private int pawnSlot;

        [Header("Board State")]
        [SerializeField] private int currentSpaceIndex;

        [Header("Visuals")]
        [SerializeField] private bool buildDefaultVisual = true;

        private BoardManager boardManager;
        private Transform visualRoot;

        public string PlayerId => playerId;
        public string DisplayName => displayName;
        public int PawnSlot => pawnSlot;
        public int CurrentSpaceIndex => currentSpaceIndex;
        public bool IsMoving { get; private set; }

        public void Initialize(
            string playerId,
            string displayName,
            int pawnSlot,
            int startSpaceIndex,
            BoardManager boardManager)
        {
            this.playerId = playerId;
            this.displayName = displayName;
            this.pawnSlot = pawnSlot;
            this.boardManager = boardManager;

            EnsureVisualExists();

            PlaceOnSpace(startSpaceIndex);
        }

        public void PlaceOnSpace(int spaceIndex)
        {
            if (boardManager == null)
            {
                Debug.LogError($"[{nameof(PlayerPawn)}] Cannot place pawn because BoardManager is missing.");
                return;
            }

            currentSpaceIndex = boardManager.NormalizeIndex(spaceIndex);
            transform.position = boardManager.GetPawnWorldPosition(currentSpaceIndex, pawnSlot);
            UpdateVisualRoot();
        }

        public IEnumerator MoveToSpaceRoutine(int targetSpaceIndex, float duration = 0.35f)
        {
            if (boardManager == null)
            {
                Debug.LogError($"[{nameof(PlayerPawn)}] Cannot move pawn because BoardManager is missing.");
                yield break;
            }

            int normalizedTarget = boardManager.NormalizeIndex(targetSpaceIndex);
            Vector3 startPosition = transform.position;
            Vector3 endPosition = boardManager.GetPawnWorldPosition(normalizedTarget, pawnSlot);

            IsMoving = true;

            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(startPosition, endPosition, t);
                yield return null;
            }

            currentSpaceIndex = normalizedTarget;
            transform.position = endPosition;
            UpdateVisualRoot();
            IsMoving = false;
        }

        private void EnsureVisualExists()
        {
            if (!buildDefaultVisual || visualRoot != null)
            {
                return;
            }

            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Visual";
            capsule.transform.SetParent(transform, false);
            capsule.transform.localPosition = Vector3.zero;
            capsule.transform.localRotation = Quaternion.identity;
            capsule.transform.localScale = new Vector3(1f, 1f, 1f);

            Collider collider = capsule.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            visualRoot = capsule.transform;
        }

        private void UpdateVisualRoot()
        {
            if (visualRoot != null)
            {
                visualRoot.localRotation = Quaternion.identity;
            }
        }
    }

    public static class PawnFactory
    {
        public static PlayerPawnNetworkSync CreatePawn(
            GameObject pawnPrefab,
            Transform pawnRoot,
            int pawnSlot,
            string displayName,
            int startSpaceIndex,
            BoardManager boardManager,
            PawnVisualConfig visualConfig)
        {
            if (pawnPrefab == null || pawnRoot == null || boardManager == null)
            {
                return null;
            }

            GameObject pawnObject = Object.Instantiate(pawnPrefab, pawnRoot);
            pawnObject.name = $"PlayerPawn_{pawnSlot + 1}_{displayName}";

            ApplyVisualStyle(pawnObject, pawnSlot, visualConfig);

            PlayerPawn pawn = pawnObject.GetComponent<PlayerPawn>();
            if (pawn == null)
            {
                Debug.LogError(
                    $"[PawnFactory] Pawn prefab '{pawnPrefab.name}' is missing a PlayerPawn component. " +
                    "Open the prefab in the inspector and add PlayerPawn manually.");
                Object.Destroy(pawnObject);
                return null;
            }

            pawn.Initialize(
                $"player-{pawnSlot + 1}",
                displayName,
                pawnSlot,
                startSpaceIndex,
                boardManager);

            PlayerPawnNetworkSync pawnSync = pawnObject.GetComponent<PlayerPawnNetworkSync>();
            if (pawnSync == null)
            {
                // PlayerPawnNetworkSync MUST be a pre-added component on the prefab.
                // Dynamically adding a NetworkBehaviour at runtime only affects the server
                // instance; clients instantiate the registered prefab asset which lacks the
                // component, so OnNetworkSpawn never fires for them and pawns are invisible.
                Debug.LogError(
                    $"[PawnFactory] Pawn prefab '{pawnPrefab.name}' is missing a " +
                    $"PlayerPawnNetworkSync component. Open the prefab in the inspector, add " +
                    $"PlayerPawnNetworkSync (and PlayerPawn) manually, then re-register it in " +
                    $"the NetworkManager prefab list.");
                Object.Destroy(pawnObject);
                return null;
            }

            pawnSync.Initialize(
                pawn,
                boardManager,
                pawnSlot,
                $"player-{pawnSlot + 1}",
                displayName);

            NetworkObject networkObject = pawnObject.GetComponent<NetworkObject>();
            if (networkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                networkObject.Spawn();
            }

            return pawnSync;
        }

        private static void ApplyVisualStyle(GameObject pawnObject, int pawnSlot, PawnVisualConfig visualConfig)
        {
            float pawnHeight = visualConfig != null ? visualConfig.PawnHeight : 0.8f;
            float pawnRadius = visualConfig != null ? visualConfig.PawnRadius : 0.28f;

            pawnObject.transform.localScale = new Vector3(pawnRadius, pawnHeight / 2f, pawnRadius);

            Color color = visualConfig != null ? visualConfig.GetPawnColor(pawnSlot) : Color.white;
            Renderer[] renderers = pawnObject.GetComponentsInChildren<Renderer>(true);
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;

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