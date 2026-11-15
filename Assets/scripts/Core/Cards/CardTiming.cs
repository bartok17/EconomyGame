namespace MonopolyGame.Cards
{
    /// <summary>
    /// How a card enters play. Active cards are chosen and activated by their
    /// owner; passive cards resolve by themselves when their condition is met.
    /// </summary>
    public enum CardTiming
    {
        Active,
        Passive
    }
}
