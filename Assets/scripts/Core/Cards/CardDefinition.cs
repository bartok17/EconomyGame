using System;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// Shared metadata for every card type. Each deck adds its own resolution
    /// contract: action cards are played from a hand, event cards trigger the
    /// moment they are drawn.
    /// </summary>
    public abstract class CardDefinition
    {
        protected CardDefinition(
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
    }
}
