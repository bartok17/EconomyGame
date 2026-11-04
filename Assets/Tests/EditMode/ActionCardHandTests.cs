using MonopolyGame.Cards;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class ActionCardHandTests
    {
        [Test]
        public void TryAdd_StopsAtCapacityOfFive()
        {
            var hand = new ActionCardHand();
            var definition = new TestCard();

            for (int i = 0; i < ActionCardHand.DefaultCapacity; i++)
            {
                Assert.IsTrue(hand.TryAdd(new ActionCardInstance(i + 1, definition)));
            }

            Assert.AreEqual(5, hand.Count);
            Assert.IsTrue(hand.IsFull);
            Assert.IsFalse(hand.TryAdd(new ActionCardInstance(99, definition)));
        }

        [Test]
        public void Remove_FreesCapacity()
        {
            var hand = new ActionCardHand();
            var definition = new TestCard();

            for (int i = 0; i < ActionCardHand.DefaultCapacity; i++)
            {
                hand.TryAdd(new ActionCardInstance(i + 1, definition));
            }

            Assert.IsTrue(hand.Remove(3));
            Assert.AreEqual(4, hand.Count);
            Assert.IsTrue(hand.TryAdd(new ActionCardInstance(99, definition)));
        }

        [Test]
        public void Remove_UnknownInstance_ReturnsFalse()
        {
            var hand = new ActionCardHand();

            Assert.IsFalse(hand.Remove(123));
        }

        private sealed class TestCard : ActionCardDefinition
        {
            public TestCard()
                : base("test", "Test", string.Empty, "TEST", 1)
            {
            }

            public override void Play(ActionCardPlayContext context)
            {
            }
        }
    }
}
