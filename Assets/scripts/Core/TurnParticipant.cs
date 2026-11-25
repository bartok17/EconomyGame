namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class TurnParticipant
    {
        public TurnParticipant(
            int turnIndex,
            string displayName,
            ulong clientId,
            string authPlayerId = "",
            bool isConnected = true,
            bool isEliminated = false)
        {
            TurnIndex = turnIndex;
            DisplayName = displayName ?? string.Empty;
            ClientId = clientId;
            AuthPlayerId = authPlayerId ?? string.Empty;
            IsConnected = isConnected;
            IsEliminated = isEliminated;
        }

        public int TurnIndex { get; }
        public string DisplayName { get; }
        public string AuthPlayerId { get; }
        public ulong ClientId { get; set; }
        public bool IsConnected { get; set; }
        public bool IsEliminated { get; set; }
    }
}