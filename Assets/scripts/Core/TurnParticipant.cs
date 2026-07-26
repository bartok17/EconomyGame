namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class TurnParticipant
    {
        public TurnParticipant(int turnIndex, string displayName, ulong clientId)
        {
            TurnIndex = turnIndex;
            DisplayName = displayName ?? string.Empty;
            ClientId = clientId;
        }

        public int TurnIndex { get; }
        public string DisplayName { get; }
        public ulong ClientId { get; }
    }
}
