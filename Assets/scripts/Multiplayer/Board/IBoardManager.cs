using UnityEngine;

namespace MonopolyGame.Board
{
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
