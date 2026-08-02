using System;
using MonopolyGame.Pawns;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public interface IGameSessionController
    {
        TurnPhase CurrentPhase { get; }
        int CurrentTurnIndex { get; }
        int LastDiceRoll { get; }
        string ActivePlayerName { get; }
        ulong ActiveClientId { get; }
        bool IsInitialized { get; }

        event Action<TurnPhase> PhaseChanged;
        event Action<int, string> TurnChanged;
        event Action<int> DiceRolled;
        event Action EconomyChanged;
        event Action<PlayerPawn, int> PawnMoved;

        void RequestRoll();
        void RequestEndTurn();
        void RequestBuyCurrentProperty();
        int GetLocalPlayerBalance();
        int GetLocalPlayerCurrentSpaceIndex();
        int GetPropertyOwnerForCurrentSpace();
        bool CanBuyCurrentProperty();
    }
}
