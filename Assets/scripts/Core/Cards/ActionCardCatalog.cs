using System;
using System.Collections.Generic;
using MonopolyGame.Cards.Definitions;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// Default deck composition. Register new card definitions here with the
    /// quantity that should exist in a fresh deck.
    /// </summary>
    public static class ActionCardCatalog
    {
        public const int DefaultCardQuantity = 6;

        public static IReadOnlyList<ActionCardDefinition> CreateDefaultDefinitions()
        {
            return new List<ActionCardDefinition>
            {
                new SprinterActionCard(DefaultCardQuantity),
                new WindfallActionCard(DefaultCardQuantity)
            };
        }

        public static ActionCardDeck CreateDefaultDeck(Random random = null)
        {
            return new ActionCardDeck(CreateDefaultDefinitions(), random);
        }
    }
}
