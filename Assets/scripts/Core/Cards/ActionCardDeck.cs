using System;
using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// Action-card draw and discard piles. The public surface is unchanged from
    /// the original implementation; the pile mechanics live in
    /// <see cref="CardPile{TCard}"/> so other decks behave identically.
    /// </summary>
    public sealed class ActionCardDeck : CardPile<ActionCardInstance>
    {
        public ActionCardDeck(IEnumerable<ActionCardDefinition> definitions, Random random = null)
            : base(BuildCards(definitions, (instanceId, definition) => new ActionCardInstance(instanceId, definition)), random)
        {
        }
    }
}
