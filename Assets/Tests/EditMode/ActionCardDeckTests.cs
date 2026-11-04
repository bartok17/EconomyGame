using System;
using System.Collections.Generic;
using MonopolyGame.Cards;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class ActionCardDeckTests
    {
        [Test]
        public void Constructor_BuildsDrawPileFromQuantities()
        {
            var deck = new ActionCardDeck(new[]
            {
                new TestCard("alpha", 2),
                new TestCard("beta", 3)
            }, new Random(1));

            Assert.AreEqual(5, deck.DrawPileCount);
            Assert.AreEqual(0, deck.DiscardPileCount);
        }

        [Test]
        public void Draw_RemovesOneCardAndExposesItsDefinition()
        {
            var deck = new ActionCardDeck(new[] { new TestCard("alpha", 1) }, new Random(1));

            ActionCardInstance card = deck.Draw();

            Assert.IsNotNull(card);
            Assert.AreEqual("alpha", card.Definition.CardId);
            Assert.AreEqual(0, deck.DrawPileCount);
        }

        [Test]
        public void Draw_WhenDrawPileIsEmpty_ReshufflesDiscardPile()
        {
            var deck = new ActionCardDeck(new[] { new TestCard("alpha", 2) }, new Random(1));
            deck.Discard(deck.Draw());
            deck.Discard(deck.Draw());

            Assert.AreEqual(0, deck.DrawPileCount);
            Assert.AreEqual(2, deck.DiscardPileCount);

            ActionCardInstance card = deck.Draw();

            Assert.IsNotNull(card);
            Assert.IsTrue(deck.LastDrawReshuffled);
            Assert.AreEqual(1, deck.DrawPileCount);
            Assert.AreEqual(0, deck.DiscardPileCount);
        }

        [Test]
        public void Draw_WhenBothPilesAreEmpty_ReturnsNull()
        {
            var deck = new ActionCardDeck(Array.Empty<ActionCardDefinition>(), new Random(1));

            Assert.IsNull(deck.Draw());
            Assert.AreEqual(0, deck.TotalCardCount);
        }

        [Test]
        public void Discard_TracksTopDiscard()
        {
            var deck = new ActionCardDeck(new[] { new TestCard("alpha", 2) }, new Random(1));
            ActionCardInstance first = deck.Draw();
            ActionCardInstance second = deck.Draw();

            deck.Discard(first);
            deck.Discard(second);

            Assert.AreEqual(2, deck.DiscardPileCount);
            Assert.AreSame(second, deck.TopDiscard);
        }

        [Test]
        public void DrawnCardsKeepUniqueInstanceIds()
        {
            var deck = new ActionCardDeck(new[] { new TestCard("alpha", 4) }, new Random(1));
            var ids = new HashSet<int>();

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(ids.Add(deck.Draw().InstanceId));
            }
        }

        [Test]
        public void DefaultCatalog_ContainsBothPlaceholderCards()
        {
            var definitions = ActionCardCatalog.CreateDefaultDefinitions();

            Assert.AreEqual(2, definitions.Count);
            Assert.IsTrue(Contains(definitions, "sprinter"));
            Assert.IsTrue(Contains(definitions, "windfall"));
        }

        private static bool Contains(IReadOnlyList<ActionCardDefinition> definitions, string cardId)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].CardId == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class TestCard : ActionCardDefinition
        {
            public TestCard(string cardId, int quantity)
                : base(cardId, cardId, string.Empty, cardId, quantity)
            {
            }

            public override void Play(ActionCardPlayContext context)
            {
            }
        }
    }
}
