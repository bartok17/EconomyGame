using System;
using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// Draw and discard piles shared by every card deck. The draw pile is built
    /// from each definition's quantity, and an empty draw pile automatically
    /// reshuffles the used/discard pile back into play.
    /// </summary>
    public abstract class CardPile<TCard> where TCard : class
    {
        private readonly List<TCard> drawPile = new List<TCard>();
        private readonly List<TCard> discardPile = new List<TCard>();
        private readonly Random random;

        protected CardPile(IEnumerable<TCard> initialCards, Random random = null)
        {
            this.random = random ?? new Random();

            if (initialCards != null)
            {
                drawPile.AddRange(initialCards);
            }

            Shuffle(drawPile);
        }

        public int DrawPileCount => drawPile.Count;
        public int DiscardPileCount => discardPile.Count;
        public int TotalCardCount => drawPile.Count + discardPile.Count;
        public TCard TopDiscard => discardPile.Count > 0 ? discardPile[discardPile.Count - 1] : null;

        /// <summary>True when the most recent draw had to reshuffle the discard pile.</summary>
        public bool LastDrawReshuffled { get; private set; }

        public TCard Draw()
        {
            LastDrawReshuffled = false;

            if (drawPile.Count == 0)
            {
                LastDrawReshuffled = ReshuffleDiscardIntoDraw();
            }

            if (drawPile.Count == 0)
            {
                return null;
            }

            int topIndex = drawPile.Count - 1;
            TCard card = drawPile[topIndex];
            drawPile.RemoveAt(topIndex);
            return card;
        }

        public void Discard(TCard card)
        {
            if (card != null)
            {
                discardPile.Add(card);
            }
        }

        public bool ReshuffleDiscardIntoDraw()
        {
            if (discardPile.Count == 0)
            {
                return false;
            }

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(drawPile);
            return true;
        }

        /// <summary>
        /// Expands card quantities into physical card instances with sequential
        /// instance ids, so every deck builds its piles the same way.
        /// </summary>
        protected static List<TCard> BuildCards<TDefinition>(
            IEnumerable<TDefinition> definitions,
            Func<int, TDefinition, TCard> createCard)
            where TDefinition : CardDefinition
        {
            var cards = new List<TCard>();

            if (definitions == null || createCard == null)
            {
                return cards;
            }

            int instanceId = 1;

            foreach (TDefinition definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }

                for (int i = 0; i < definition.Quantity; i++)
                {
                    cards.Add(createCard(instanceId++, definition));
                }
            }

            return cards;
        }

        private void Shuffle(List<TCard> pile)
        {
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (pile[i], pile[swapIndex]) = (pile[swapIndex], pile[i]);
            }
        }
    }
}
