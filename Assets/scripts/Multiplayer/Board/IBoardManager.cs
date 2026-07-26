using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>
    /// Public contract for board-space queries.
    /// Gameplay systems depend on this instead of the concrete BoardManager.
    /// </summary>
    public interface IBoardManager
    {
        int SpaceCount { get; }
        BoardSpaceView GetSpace(int index);
        Vector3 GetPawnWorldPosition(int spaceIndex, int pawnSlot);
        void SetSpaceOwner(int index, string ownerId);
        BoardState CaptureBoardState();
        int NormalizeIndex(int index);
    }
}
