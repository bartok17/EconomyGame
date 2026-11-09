namespace MonopolyGame.Cards
{
    /// <summary>
    /// One action-card type. Concrete cards override <see cref="Play"/> so their
    /// mechanics can differ freely while deck and hand logic stays generic.
    /// Adding a card means adding a subclass and registering it in
    /// <see cref="ActionCardCatalog"/>.
    /// </summary>
    public abstract class ActionCardDefinition : CardDefinition
    {
        protected ActionCardDefinition(
            string cardId,
            string displayName,
            string description,
            string shortLabel,
            int quantity)
            : base(cardId, displayName, description, shortLabel, quantity)
        {
        }

        public abstract void Play(ActionCardPlayContext context);
    }
}
