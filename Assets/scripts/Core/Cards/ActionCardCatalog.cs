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
        public const int PassiveCardQuantity = 4;

        private static readonly object SyncRoot = new object();
        private static IReadOnlyList<ActionCardDefinition> cachedDefinitions;

        public static IReadOnlyList<ActionCardDefinition> CreateDefaultDefinitions()
        {
            return new List<ActionCardDefinition>
            {
                new SprinterActionCard(DefaultCardQuantity),
                new WindfallActionCard(DefaultCardQuantity),
                new GuardActionCard(PassiveCardQuantity)
            };
        }

        public static ActionCardDeck CreateDefaultDeck(Random random = null)
        {
            return new ActionCardDeck(CreateDefaultDefinitions(), random);
        }

        /// <summary>
        /// Client-side lookup used by the activation UI to describe a card it
        /// only knows by id. Definitions are immutable, so they are cached.
        /// </summary>
        public static bool TryGetDefinition(string cardId, out ActionCardDefinition definition)
        {
            definition = null;

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            IReadOnlyList<ActionCardDefinition> definitions = GetDefinitions();

            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].CardId == cardId)
                {
                    definition = definitions[i];
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<ActionCardDefinition> GetDefinitions()
        {
            if (cachedDefinitions != null)
            {
                return cachedDefinitions;
            }

            lock (SyncRoot)
            {
                cachedDefinitions ??= CreateDefaultDefinitions();
                return cachedDefinitions;
            }
        }
    }
}
