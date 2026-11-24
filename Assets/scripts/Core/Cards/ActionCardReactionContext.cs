namespace MonopolyGame.Cards
{
    /// <summary>
    /// Context a passive card inspects to decide whether it fires. Passive cards
    /// resolve automatically, so this context is always about an action another
    /// player has just played.
    /// </summary>
    public sealed class ActionCardReactionContext
    {
        public ActionCardReactionContext(
            CardPlayerState reactingPlayer,
            CardPlayerState actingPlayer,
            ActionCardDefinition incomingCard,
            ActionCardParameters incomingParameters,
            bool targetsReactingPlayer)
        {
            ReactingPlayer = reactingPlayer;
            ActingPlayer = actingPlayer;
            IncomingCard = incomingCard;
            IncomingParameters = incomingParameters;
            TargetsReactingPlayer = targetsReactingPlayer;
        }

        public CardPlayerState ReactingPlayer { get; }
        public CardPlayerState ActingPlayer { get; }
        public ActionCardDefinition IncomingCard { get; }
        public ActionCardParameters IncomingParameters { get; }

        public bool TargetsReactingPlayer { get; }

        public bool IsCancelled { get; private set; }

        public string Message { get; private set; } = string.Empty;

        public void Cancel(string message)
        {
            IsCancelled = true;

            if (!string.IsNullOrWhiteSpace(message))
            {
                Message = message;
            }
        }
    }
}
