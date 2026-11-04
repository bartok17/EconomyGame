using System;
using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// Draw and discard piles for action cards. The draw pile is built from each
    /// definition's quantity, and an empty draw pile automatically reshuffles the
    /// used/discard pile back into play.
    /// </summary>
    public sealed class ActionCardDeck
    {
        private readonly List<ActionCardInstance> drawPile = new List<ActionCardInstance>();
        private readonly List<ActionCardInstance> discardPile = new List<ActionCardInstance>();
        private readonly Random random;
        private int nextInstanceId = 1;

        public ActionCardDeck(IEnumerable<ActionCardDefinition> definitions, Random random = null)
        {
            this.random = random ?? new Random();

            if (definitions != null)
            {
                foreach (ActionCardDefinition definition in definitions)
                {
                    if (definition == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < definition.Quantity; i++)
                    {
                        drawPile.Add(new ActionCardInstance(nextInstanceId++, definition));
                    }
                }
            }

            Shuffle(drawPile);
        }

        public int DrawPileCount => drawPile.Count;
        public int DiscardPileCount => discardPile.Count;
        public int TotalCardCount => drawPile.Count + discardPile.Count;
        public ActionCardInstance TopDiscard => discardPile.Count > 0 ? discardPile[discardPile.Count - 1] : null;

        /// <summary>True when the most recent draw had to reshuffle the discard pile.</summary>
        public bool LastDrawReshuffled { get; private set; }

        public ActionCardInstance Draw()
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
            ActionCardInstance card = drawPile[topIndex];
            drawPile.RemoveAt(topIndex);
            return card;
        }

        public void Discard(ActionCardInstance card)
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

        private void Shuffle(List<ActionCardInstance> pile)
        {
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (pile[i], pile[swapIndex]) = (pile[swapIndex], pile[i]);
            }
        }
    }
}
