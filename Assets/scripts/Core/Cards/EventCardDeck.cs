using System;
using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// Event-card draw and discard piles. Event cards leave the deck immediately
    /// after resolving, so the discard pile is the only pile besides the draw
    /// pile that ever fills.
    /// </summary>
    public sealed class EventCardDeck : CardPile<EventCardInstance>
    {
        public EventCardDeck(IEnumerable<EventCardDefinition> definitions, Random random = null)
            : base(BuildCards(definitions, (instanceId, definition) => new EventCardInstance(instanceId, definition)), random)
        {
        }
    }
}
