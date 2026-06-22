using System.Collections;
using MonopolyGame.Board;
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
}