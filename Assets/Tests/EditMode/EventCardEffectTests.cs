using MonopolyGame.Cards;
using MonopolyGame.Cards.Definitions;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class EventCardEffectTests
    {
        [Test]
        public void CharityEventCard_FundsThePoorestPlayer()
        {
            var actions = new TestCardActions();
            var context = BuildContext(actions, drawingSlot: 0, Player(0, 1500), Player(1, 300), Player(2, 900));

            new CharityEventCard(1).Trigger(context);

            Assert.AreEqual(1, actions.FundsAdded.Count);
            Assert.AreEqual("player-1", actions.FundsAdded[0].PlayerId);
            Assert.AreEqual(CharityEventCard.Amount, actions.FundsAdded[0].Amount);
            Assert.AreEqual(0, actions.FundsDeducted.Count);
        }

        [Test]
        public void CharityEventCard_ReportsWhatHappened()
        {
            var actions = new TestCardActions();
            var context = BuildContext(actions, drawingSlot: 0, Player(0, 1500), Player(1, 300));

            new CharityEventCard(1).Trigger(context);

            Assert.AreEqual(1, context.Messages.Count);
            StringAssert.Contains("Player 2", context.Messages[0]);
            StringAssert.Contains(CharityEventCard.Amount.ToString(), context.Messages[0]);
        }

        [Test]
        public void LateFeeEventCard_ChargesThePlayerAfterTheDrawer()
        {
            var actions = new TestCardActions();
            var context = BuildContext(actions, drawingSlot: 0, Player(0, 1500), Player(1, 300), Player(2, 900));

            new LateFeeEventCard(1).Trigger(context);

            Assert.AreEqual(1, actions.FundsDeducted.Count);
            Assert.AreEqual("player-1", actions.FundsDeducted[0].PlayerId);
            Assert.AreEqual(LateFeeEventCard.Amount, actions.FundsDeducted[0].Amount);
            Assert.AreEqual(0, actions.FundsAdded.Count);
        }

        [Test]
        public void LateFeeEventCard_WhenAlone_ChargesNobody()
        {
            var actions = new TestCardActions();
            var context = BuildContext(actions, drawingSlot: 0, Player(0, 1500));

            new LateFeeEventCard(1).Trigger(context);

            Assert.AreEqual(0, actions.FundsDeducted.Count);
            Assert.AreEqual(1, context.Messages.Count);
        }

        private static EventCardTriggerContext BuildContext(
            TestCardActions actions,
            int drawingSlot,
            params EventCardPlayerState[] players)
        {
            return new EventCardTriggerContext(drawingSlot, players, actions);
        }

        private static EventCardPlayerState Player(int slot, int balance)
        {
            return new EventCardPlayerState(slot, slot, $"player-{slot}", $"Player {slot + 1}", balance);
        }
    }
}
