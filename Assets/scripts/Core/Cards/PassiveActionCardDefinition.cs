namespace MonopolyGame.Cards
{
    /// <summary>
    /// A card that resolves by itself when its condition is met. Passive cards
    /// are never activated from the hand: the server checks the holder's hand
    /// at the moment another player's card would affect them, so nothing is
    /// revealed and nobody has to wait for a response.
    /// </summary>
    public abstract class PassiveActionCardDefinition : ActionCardDefinition
    {
        protected PassiveActionCardDefinition(
            string cardId,
            string displayName,
            string description,
            string shortLabel,
            int quantity)
            : base(cardId, displayName, description, shortLabel, quantity)
        {
        }

        public sealed override CardTiming Timing => CardTiming.Passive;

        /// <summary>Passive cards are never played from the hand.</summary>
        public sealed override void Play(ActionCardPlayContext context)
        {
        }

        /// <summary>
        /// Called on the server when another player's card may affect the
        /// holder. Return true when the card fires (it is then consumed).
        /// </summary>
        public abstract bool TryReact(ActionCardReactionContext context);
    }
}
