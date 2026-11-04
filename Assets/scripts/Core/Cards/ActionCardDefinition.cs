using System;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// One action-card type. Concrete cards override <see cref="Play"/> so their
    /// mechanics can differ freely while deck and hand logic stays generic.
    /// Adding a card means adding a subclass and registering it in
    /// <see cref="ActionCardCatalog"/>.
    /// </summary>
    public abstract class ActionCardDefinition
    {
        protected ActionCardDefinition(
            string cardId,
            string displayName,
            string description,
            string shortLabel,
            int quantity)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                throw new ArgumentException("A card id is required.", nameof(cardId));
            }

            if (quantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "Card quantity cannot be negative.");
            }

            CardId = cardId;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            ShortLabel = shortLabel ?? string.Empty;
            Quantity = quantity;
        }

        public string CardId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string ShortLabel { get; }
        public int Quantity { get; }

        public abstract void Play(ActionCardPlayContext context);
    }
}
