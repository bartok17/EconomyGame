namespace MonopolyGame.Cards
{
    /// <summary>
    /// Read-only board information a card may use to describe its outcome
    /// before the player confirms it.
    /// </summary>
    public interface ICardPreviewBoard
    {
        /// <summary>Board index the player is standing on, or -1 when unknown.</summary>
        int GetPlayerSpaceIndex(int pawnSlot);

        int NormalizeIndex(int spaceIndex);

        string GetSpaceName(int spaceIndex);
    }
}
