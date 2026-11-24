using System;
using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    public sealed class ActionCardDeck : CardPile<ActionCardInstance>
    {
        public ActionCardDeck(IEnumerable<ActionCardDefinition> definitions, Random random = null)
            : base(BuildCards(definitions, (instanceId, definition) => new ActionCardInstance(instanceId, definition)), random)
        {
        }

        /// <summary>Pulls one card with the given id out of the draw pile instead of the top card.</summary>
        public bool TryTake(string cardId, out ActionCardInstance card)
        {
            card = null;

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            return TryTakeFromDraw(
                candidate => candidate != null &&
                             string.Equals(candidate.Definition.CardId, cardId, StringComparison.OrdinalIgnoreCase),
                out card);
        }
    }
}
