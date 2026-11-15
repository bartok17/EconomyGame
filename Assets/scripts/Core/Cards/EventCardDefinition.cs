namespace MonopolyGame.Cards
{
    /// <summary>
    /// One event-card type. Event cards resolve the instant they are drawn, so
    /// there is no hand and no player choice: each card picks its own targets
    /// from the game state inside <see cref="Trigger"/>.
    /// </summary>
    public abstract class EventCardDefinition : CardDefinition
    {
        protected EventCardDefinition(
            string cardId,
            string displayName,
            string description,
            string shortLabel,
            int quantity)
            : base(cardId, displayName, description, shortLabel, quantity)
        {
        }

        public abstract void Trigger(EventCardTriggerContext context);
    }
}
