namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class TurnParticipant
    {
        public TurnParticipant(int turnIndex, string displayName, ulong clientId)
        {
            TurnIndex = turnIndex;
            DisplayName = displayName ?? string.Empty;
            ClientId = clientId;
            IsConnected = true;
        }

        public int TurnIndex { get; set; }
        public string DisplayName { get; }
        public ulong ClientId { get; set;  }
        public bool IsConnected { get; set; }
    }
}
