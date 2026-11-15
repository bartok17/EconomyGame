namespace MonopolyGame.Cards
{
    /// <summary>
    /// Window evaluation shared by the session (authority) and the HUD (UX) so
    /// a card is never shown as playable when the server would reject it.
    /// </summary>
    public static class CardActivationWindows
    {
        public static bool IsOpen(CardActivationWindow window, bool beforeRoll, bool afterRoll)
        {
            return window switch
            {
                CardActivationWindow.BeforeRoll => beforeRoll,
                CardActivationWindow.AfterRoll => afterRoll,
                _ => beforeRoll || afterRoll
            };
        }
    }
}
