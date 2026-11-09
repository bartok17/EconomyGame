using MonopolyGame.Cards;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class EventCardTargetingTests
    {
        [Test]
        public void TryGetPoorest_ReturnsLowestBalance()
        {
            var context = BuildContext(drawingSlot: 0, Player(0, 1500), Player(1, 400), Player(2, 900));

            Assert.IsTrue(context.TryGetPoorest(out EventCardPlayerState player));
            Assert.AreEqual(1, player.PawnSlot);
        }

        [Test]
        public void TryGetPoorest_TiesBreakByLowestSeat()
        {
            var context = BuildContext(drawingSlot: 2, Player(0, 400), Player(1, 400), Player(2, 400));

            Assert.IsTrue(context.TryGetPoorest(out EventCardPlayerState player));
            Assert.AreEqual(0, player.PawnSlot);
        }

        [Test]
        public void TryGetRichest_ReturnsHighestBalance()
        {
            var context = BuildContext(drawingSlot: 0, Player(0, 1500), Player(1, 2100), Player(2, 900));

            Assert.IsTrue(context.TryGetRichest(out EventCardPlayerState player));
            Assert.AreEqual(1, player.PawnSlot);
        }

        [Test]
        public void TryGetNextAfter_ReturnsFollowingSeat()
        {
            var context = BuildContext(drawingSlot: 1, Player(0, 100), Player(1, 100), Player(2, 100));

            Assert.IsTrue(context.TryGetNextAfter(1, out EventCardPlayerState player));
            Assert.AreEqual(2, player.PawnSlot);
        }

        [Test]
        public void TryGetNextAfter_WrapsAroundToFirstSeat()
        {
            var context = BuildContext(drawingSlot: 2, Player(0, 100), Player(1, 100), Player(2, 100));

            Assert.IsTrue(context.TryGetNextAfter(2, out EventCardPlayerState player));
            Assert.AreEqual(0, player.PawnSlot);
        }

        [Test]
        public void TryGetNextAfter_WithSinglePlayer_ReturnsFalse()
        {
            var context = BuildContext(drawingSlot: 0, Player(0, 100));

            Assert.IsFalse(context.TryGetNextAfter(0, out _));
        }

        [Test]
        public void TryGetDrawingPlayer_ResolvesFromSlot()
        {
            var context = BuildContext(drawingSlot: 2, Player(0, 100), Player(1, 100), Player(2, 100));

            Assert.IsTrue(context.TryGetDrawingPlayer(out EventCardPlayerState player));
            Assert.AreEqual(2, player.PawnSlot);
        }

        [Test]
        public void PlayerWithoutDisplayName_GetsReadableLabel()
        {
            var player = new EventCardPlayerState(3, 3, "player-3", string.Empty, 0);

            Assert.AreEqual("Player 4", player.Label);
        }

        private static EventCardTriggerContext BuildContext(
            int drawingSlot,
            params EventCardPlayerState[] players)
        {
            return new EventCardTriggerContext(drawingSlot, players, new TestCardActions());
        }

        private static EventCardPlayerState Player(int slot, int balance)
        {
            return new EventCardPlayerState(slot, slot, $"player-{slot}", $"Player {slot + 1}", balance);
        }
    }
}
