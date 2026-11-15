using MonopolyGame.Cards;
using MonopolyGame.Cards.Definitions;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class PassiveActionCardTests
    {
        private static readonly CardPlayerState Alice = new CardPlayerState(0, 0, "player-0", "Alice", 1500);
        private static readonly CardPlayerState Bob = new CardPlayerState(1, 1, "player-1", "Bob", 900);

        [Test]
        public void Guard_CancelsACardPlayedAgainstItsHolder()
        {
            var context = new ActionCardReactionContext(
                Bob, Alice, new SprinterActionCard(1), new ActionCardParameters(1, 3), targetsReactingPlayer: true);

            Assert.IsTrue(new GuardActionCard(1).TryReact(context));
            Assert.IsTrue(context.IsCancelled);
            StringAssert.Contains("Bob", context.Message);
            StringAssert.Contains("Sprinter", context.Message);
        }

        [Test]
        public void Guard_IgnoresACardThatDoesNotTargetTheHolder()
        {
            var context = new ActionCardReactionContext(
                Bob, Alice, new SprinterActionCard(1), new ActionCardParameters(0, 3), targetsReactingPlayer: false);

            Assert.IsFalse(new GuardActionCard(1).TryReact(context));
            Assert.IsFalse(context.IsCancelled);
        }

        [Test]
        public void Guard_IgnoresItsOwnPlay()
        {
            var context = new ActionCardReactionContext(
                Bob, Bob, new SprinterActionCard(1), new ActionCardParameters(1, 3), targetsReactingPlayer: true);

            Assert.IsFalse(new GuardActionCard(1).TryReact(context));
        }

        [Test]
        public void Guard_IsPassiveAndInTheDefaultDeck()
        {
            Assert.AreEqual(CardTiming.Passive, new GuardActionCard(1).Timing);
            Assert.IsTrue(ActionCardCatalog.TryGetDefinition(GuardActionCard.Id, out ActionCardDefinition definition));
            Assert.AreEqual(CardTiming.Passive, definition.Timing);
        }
    }
}
