namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class TurnState
    {
        public TurnState(int turnIndex, TurnPhase phase, int diceRoll, string activePlayerName, ulong activeClientId, bool isInitialized)
        {
            TurnIndex = turnIndex;
            Phase = phase;
            DiceRoll = diceRoll;
            ActivePlayerName = activePlayerName ?? string.Empty;
            ActiveClientId = activeClientId;
            IsInitialized = isInitialized;
        }

        public int TurnIndex { get; }
        public TurnPhase Phase { get; }
        public int DiceRoll { get; }
        public string ActivePlayerName { get; }
        public ulong ActiveClientId { get; }
        public bool IsInitialized { get; }
    }
}
