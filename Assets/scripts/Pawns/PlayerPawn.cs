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

        private BoardManager boardManager;

        public string PlayerId => playerId;
        public string DisplayName => displayName;
        public int PawnSlot => pawnSlot;
        public int CurrentSpaceIndex => currentSpaceIndex;

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
        }
    }
}