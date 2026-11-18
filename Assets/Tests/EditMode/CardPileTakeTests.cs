using System;
using System.Collections.Generic;
using MonopolyGame.Cards;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class CardPileTakeTests
    {
        [Test]
        public void TryTake_RemovesTheMatchingCardFromTheDrawPile()
        {
            var deck = new ActionCardDeck(new ActionCardDefinition[]
            {
                new TakeTestCard("alpha", 2),
                new TakeTestCard("beta", 1)
            }, new Random(1));

            bool taken = deck.TryTake("alpha", out ActionCardInstance card);

            Assert.IsTrue(taken);
            Assert.IsNotNull(card);
            Assert.AreEqual("alpha", card.Definition.CardId);
            Assert.AreEqual(2, deck.DrawPileCount);
            Assert.AreEqual(0, deck.DiscardPileCount);
        }

        [Test]
        public void TryTake_ReshufflesWhenTheDrawPileIsEmpty()
        {
            var deck = new ActionCardDeck(new ActionCardDefinition[] { new TakeTestCard("alpha", 2) }, new Random(1));

            while (deck.DrawPileCount > 0)
            {
                deck.Discard(deck.Draw());
            }

            bool taken = deck.TryTake("alpha", out ActionCardInstance card);

            Assert.IsTrue(taken);
            Assert.IsNotNull(card);
            Assert.IsTrue(deck.LastDrawReshuffled);
            Assert.AreEqual(1, deck.DrawPileCount);
            Assert.AreEqual(0, deck.DiscardPileCount);
        }

        [Test]
        public void TryTake_UnknownCardReturnsFalse()
        {
            var deck = new ActionCardDeck(new ActionCardDefinition[] { new TakeTestCard("alpha", 1) }, new Random(1));

            Assert.IsFalse(deck.TryTake("beta", out ActionCardInstance card));
            Assert.IsNull(card);
            Assert.AreEqual(1, deck.DrawPileCount);
        }

        [Test]
        public void TryTake_EmptyIdReturnsFalse()
        {
            var deck = new ActionCardDeck(new ActionCardDefinition[] { new TakeTestCard("alpha", 1) }, new Random(1));

            Assert.IsFalse(deck.TryTake(null, out _));
            Assert.IsFalse(deck.TryTake("   ", out _));
        }

        [Test]
        public void TryTake_IsCaseInsensitive()
        {
            var deck = new ActionCardDeck(new ActionCardDefinition[] { new TakeTestCard("alpha", 1) }, new Random(1));

            Assert.IsTrue(deck.TryTake("ALPHA", out ActionCardInstance card));
            Assert.AreEqual("alpha", card.Definition.CardId);
        }

        [Test]
        public void TryTake_KeepsTheTotalCardCountStable()
        {
            var deck = new ActionCardDeck(new ActionCardDefinition[] { new TakeTestCard("alpha", 3) }, new Random(1));

            deck.TryTake("alpha", out _);

            Assert.AreEqual(2, deck.TotalCardCount);
            Assert.AreEqual(2, deck.DrawPileCount);
            Assert.AreEqual(0, deck.DiscardPileCount);
        }

        [Test]
        public void TryTakeFromDraw_RejectsANullMatcher()
        {
            var deck = new ActionCardDeck(new ActionCardDefinition[] { new TakeTestCard("alpha", 1) }, new Random(1));

            Assert.IsFalse(deck.TryTakeFromDraw(null, out _));
        }

        private sealed class TakeTestCard : ActionCardDefinition
        {
            public TakeTestCard(string cardId, int quantity)
                : base(cardId, cardId, string.Empty, cardId, quantity)
            {
            }

            public override void Play(ActionCardPlayContext context)
            {
            }
        }
    }
}
