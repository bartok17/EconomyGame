namespace MonopolyGame.Board
{
    public sealed class BoardLandingResult
    {
        public BoardLandingResult(
            int spaceIndex,
            string displayName,
            BoardSpaceType spaceType,
            string playerId,
            string ownerId,
            int baseRent,
            string message)
        {
            SpaceIndex = spaceIndex;
            DisplayName = displayName ?? string.Empty;
            SpaceType = spaceType;
            PlayerId = playerId ?? string.Empty;
            OwnerId = ownerId ?? string.Empty;
            BaseRent = baseRent;
            Message = message ?? string.Empty;
        }

        public int SpaceIndex { get; }
        public string DisplayName { get; }
        public BoardSpaceType SpaceType { get; }
        public string PlayerId { get; }
        public string OwnerId { get; }
        public int BaseRent { get; }
        public string Message { get; }
    }
}
