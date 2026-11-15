using MonopolyGame.Cards;
using MonopolyGame.Cards.Definitions;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class ActionCardEffectTests
    {
        private static readonly CardPlayerState[] Players =
        {
            new CardPlayerState(0, 0, "player-0", "Alice", 1500),
            new CardPlayerState(1, 1, "player-1", "Bob", 900)
        };

        [Test]
        public void Sprinter_MovesTheTargetedPlayer()
        {
            var actions = new TestCardActions();
            var context = new ActionCardPlayContext(
                0, "player-0", actions, new ActionCardParameters(0, SprinterActionCard.DefaultSpaces), Players);

            new SprinterActionCard(1).Play(context);

            Assert.AreEqual(1, actions.Moves.Count);
            Assert.AreEqual("player-0", actions.Moves[0].PlayerId);
            Assert.AreEqual(SprinterActionCard.DefaultSpaces, actions.Moves[0].Amount);
        }

        [Test]
        public void Windfall_AddsFundsToTheTargetedPlayer()
        {
            var actions = new TestCardActions();
            var context = new ActionCardPlayContext(
                0, "player-0", actions, new ActionCardParameters(0, WindfallActionCard.DefaultAmount), Players);

            new WindfallActionCard(1).Play(context);

            Assert.AreEqual(1, actions.FundsAdded.Count);
            Assert.AreEqual("player-0", actions.FundsAdded[0].PlayerId);
            Assert.AreEqual(WindfallActionCard.DefaultAmount, actions.FundsAdded[0].Amount);
        }

        [Test]
        public void Cards_ReportWhatTheyDid()
        {
            var actions = new TestCardActions();
            var context = new ActionCardPlayContext(
                0, "player-0", actions, new ActionCardParameters(1, 2), Players);

            new SprinterActionCard(1).Play(context);

            Assert.AreEqual(1, context.Messages.Count);
            StringAssert.Contains("Bob", context.Messages[0]);
            StringAssert.Contains("2", context.Messages[0]);
        }

        [Test]
        public void Cards_WithoutAMatchingTarget_DoNothing()
        {
            var actions = new TestCardActions();
            var context = new ActionCardPlayContext(
                0, "player-0", actions, new ActionCardParameters(9, 3), Players);

            new SprinterActionCard(1).Play(context);

            Assert.AreEqual(0, actions.Moves.Count);
            Assert.AreEqual(0, context.Messages.Count);
        }
    }
}
