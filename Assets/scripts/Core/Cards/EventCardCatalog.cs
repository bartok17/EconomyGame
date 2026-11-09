using System;
using System.Collections.Generic;
using MonopolyGame.Cards.Definitions;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// Default event deck composition. Register new event card definitions here
    /// with the quantity that should exist in a fresh deck.
    /// </summary>
    public static class EventCardCatalog
    {
        public const int DefaultCardQuantity = 6;

        public static IReadOnlyList<EventCardDefinition> CreateDefaultDefinitions()
        {
            return new List<EventCardDefinition>
            {
                new CharityEventCard(DefaultCardQuantity),
                new LateFeeEventCard(DefaultCardQuantity)
            };
        }

        public static EventCardDeck CreateDefaultDeck(Random random = null)
        {
            return new EventCardDeck(CreateDefaultDefinitions(), random);
        }
    }
}
