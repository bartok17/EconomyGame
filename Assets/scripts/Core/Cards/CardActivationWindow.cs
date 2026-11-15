namespace MonopolyGame.Cards
{
    /// <summary>
    /// When during its owner's turn a card may be activated. Cards that can be
    /// played any time are still limited to their owner's turn.
    /// </summary>
    public enum CardActivationWindow
    {
        BeforeRoll,
        AfterRoll,
        AnyTime
    }
}
