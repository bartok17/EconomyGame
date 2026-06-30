namespace MonopolyGame.Board
{
    public sealed class BoardRuleResolver
    {
        public BoardLandingResult Resolve(BoardState boardState, int spaceIndex, string playerId)
        {
            if (boardState == null)
            {
                return new BoardLandingResult(0, string.Empty, BoardSpaceType.Special, playerId, string.Empty, 0, string.Empty);
            }

            BoardSpaceSnapshot space = boardState.GetSpace(spaceIndex);

            if (space == null)
            {
                return new BoardLandingResult(
                    boardState.NormalizeIndex(spaceIndex),
                    string.Empty,
                    BoardSpaceType.Special,
                    playerId,
                    string.Empty,
                    0,
                    string.Empty);
            }

            string message = $"Player {playerId} landed on {space.DisplayName}. (Owner: {space.OwnerId}, Rent: {space.BaseRent})";

            return new BoardLandingResult(
                space.Index,
                space.DisplayName,
                space.SpaceType,
                playerId,
                space.OwnerId,
                space.BaseRent,
                message);
        }
    }
}
