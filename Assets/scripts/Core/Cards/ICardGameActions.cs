namespace MonopolyGame.Cards
{
    /// <summary>
    /// Operations a card may use when it resolves. Extend this interface when a
    /// new mechanic needs a game operation that does not exist yet; cards can
    /// also compose several of these operations.
    /// </summary>
    public interface ICardGameActions
    {
        void MovePlayer(string playerId, int spaces);
        void AddFunds(string playerId, int amount);
        void DeductFunds(string playerId, int amount);
        void TransferFunds(string fromPlayerId, string toPlayerId, int amount);
        void RedirectNextPayment(string payerPlayerId, string recipientPlayerId);
    }
}
