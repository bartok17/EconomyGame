namespace MonopolyGame.Cards
{
    /// <summary>
    /// Snapshot of one player as an event card sees them. <see cref="SeatIndex"/>
    /// drives "next player" style conditions; <see cref="PawnSlot"/> identifies
    /// the player for game operations.
    /// </summary>
    public readonly struct EventCardPlayerState
    {
        public EventCardPlayerState(int pawnSlot, int seatIndex, string playerId, string displayName, int balance)
        {
            PawnSlot = pawnSlot;
            SeatIndex = seatIndex;
            PlayerId = playerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Balance = balance;
        }

        public int PawnSlot { get; }
        public int SeatIndex { get; }
        public string PlayerId { get; }
        public string DisplayName { get; }
        public int Balance { get; }

        public string Label => string.IsNullOrWhiteSpace(DisplayName)
            ? $"Player {PawnSlot + 1}"
            : DisplayName;
    }
}
