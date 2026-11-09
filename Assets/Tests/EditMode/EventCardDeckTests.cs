using System;
using System.Collections.Generic;
using MonopolyGame.Cards;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class EventCardDeckTests
    {
        [Test]
        public void Constructor_BuildsDrawPileFromQuantities()
        {
            var deck = new EventCardDeck(new[]
            {
                new TestEventCard("alpha", 2),
                new TestEventCard("beta", 3)
            }, new Random(1));

            Assert.AreEqual(5, deck.DrawPileCount);
            Assert.AreEqual(0, deck.DiscardPileCount);
        }

        [Test]
        public void Draw_RemovesOneCardAndExposesItsDefinition()
        {
            var deck = new EventCardDeck(new[] { new TestEventCard("alpha", 1) }, new Random(1));

            EventCardInstance card = deck.Draw();

            Assert.IsNotNull(card);
            Assert.AreEqual("alpha", card.Definition.CardId);
            Assert.AreEqual(0, deck.DrawPileCount);
        }

        [Test]
        public void Draw_WhenDrawPileIsEmpty_ReshufflesDiscardPile()
        {
            var deck = new EventCardDeck(new[] { new TestEventCard("alpha", 2) }, new Random(1));
            deck.Discard(deck.Draw());
            deck.Discard(deck.Draw());

            Assert.AreEqual(0, deck.DrawPileCount);
            Assert.AreEqual(2, deck.DiscardPileCount);

            EventCardInstance card = deck.Draw();

            Assert.IsNotNull(card);
            Assert.IsTrue(deck.LastDrawReshuffled);
            Assert.AreEqual(1, deck.DrawPileCount);
            Assert.AreEqual(0, deck.DiscardPileCount);
        }

        [Test]
        public void Draw_WhenBothPilesAreEmpty_ReturnsNull()
        {
            var deck = new EventCardDeck(Array.Empty<EventCardDefinition>(), new Random(1));

            Assert.IsNull(deck.Draw());
            Assert.AreEqual(0, deck.TotalCardCount);
        }

        [Test]
        public void Discard_TracksTopDiscard()
        {
            var deck = new EventCardDeck(new[] { new TestEventCard("alpha", 2) }, new Random(1));
            EventCardInstance first = deck.Draw();
            EventCardInstance second = deck.Draw();

            deck.Discard(first);
            deck.Discard(second);

            Assert.AreEqual(2, deck.DiscardPileCount);
            Assert.AreSame(second, deck.TopDiscard);
        }

        [Test]
        public void DrawnCardsKeepUniqueInstanceIds()
        {
            var deck = new EventCardDeck(new[] { new TestEventCard("alpha", 4) }, new Random(1));
            var ids = new HashSet<int>();

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(ids.Add(deck.Draw().InstanceId));
            }
        }

        [Test]
        public void DefaultCatalog_ContainsBothPlaceholderCards()
        {
            var definitions = EventCardCatalog.CreateDefaultDefinitions();

            Assert.AreEqual(2, definitions.Count);
            Assert.IsTrue(Contains(definitions, "charity"));
            Assert.IsTrue(Contains(definitions, "late_fee"));
            Assert.AreEqual(
                EventCardCatalog.DefaultCardQuantity * 2,
                EventCardCatalog.CreateDefaultDeck().DrawPileCount);
        }

        private static bool Contains(IReadOnlyList<EventCardDefinition> definitions, string cardId)
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

        private sealed class TestEventCard : EventCardDefinition
        {
            public TestEventCard(string cardId, int quantity)
                : base(cardId, cardId, string.Empty, cardId, quantity)
            {
            }

            public override void Trigger(EventCardTriggerContext context)
            {
            }
        }
    }
}
