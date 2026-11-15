using System.Collections.Generic;
using MonopolyGame.Cards;
using MonopolyGame.Cards.Definitions;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class ActionCardActivationTests
    {
        private static readonly CardPlayerState[] Players =
        {
            new CardPlayerState(0, 0, "player-0", "Alice", 1500),
            new CardPlayerState(1, 1, "player-1", "Bob", 900)
        };

        [Test]
        public void Sprinter_MovesTheChosenPlayerByTheChosenAmount()
        {
            var actions = new TestCardActions();
            var context = new ActionCardPlayContext(
                0, "player-0", actions, new ActionCardParameters(1, 4), Players);

            new SprinterActionCard(1).Play(context);

            Assert.AreEqual(1, actions.Moves.Count);
            Assert.AreEqual("player-1", actions.Moves[0].PlayerId);
            Assert.AreEqual(4, actions.Moves[0].Amount);
            Assert.AreEqual(1, context.Messages.Count);
            StringAssert.Contains("Bob", context.Messages[0]);
        }

        [Test]
        public void Windfall_CreditsTheChosenPlayer()
        {
            var actions = new TestCardActions();
            var context = new ActionCardPlayContext(
                1, "player-1", actions, new ActionCardParameters(0, 300), Players);

            new WindfallActionCard(1).Play(context);

            Assert.AreEqual(1, actions.FundsAdded.Count);
            Assert.AreEqual("player-0", actions.FundsAdded[0].PlayerId);
            Assert.AreEqual(300, actions.FundsAdded[0].Amount);
            Assert.AreEqual(0, actions.Moves.Count);
        }

        [Test]
        public void TryGetTarget_FallsBackToTheActingPlayer()
        {
            var actions = new TestCardActions();
            var context = new ActionCardPlayContext(
                1, "player-1", actions, ActionCardParameters.None, Players);

            Assert.IsTrue(context.TryGetTarget(out CardPlayerState target));
            Assert.AreEqual(1, target.PawnSlot);
        }

        [Test]
        public void Sprinter_PreviewShowsTheDestinationTile()
        {
            var card = new SprinterActionCard(1);
            var board = new FakePreviewBoard(spaceCount: 20, currentIndex: 17);

            string preview = card.DescribePreview(new ActionCardParameters(1, 4), Players, board);

            StringAssert.Contains("Bob", preview);
            StringAssert.Contains("Space 17", preview);
            StringAssert.Contains("Space 1", preview);
        }

        [Test]
        public void Windfall_PreviewShowsTheAmount()
        {
            var card = new WindfallActionCard(1);

            string preview = card.DescribePreview(new ActionCardParameters(0, 250), Players, null);

            StringAssert.Contains("Alice", preview);
            StringAssert.Contains("250", preview);
        }

        [Test]
        public void PassiveCards_AreNotActivatedByPlay()
        {
            var actions = new TestCardActions();
            var context = new ActionCardPlayContext(0, "player-0", actions);
            var guard = new GuardActionCard(1);

            guard.Play(context);

            Assert.AreEqual(CardTiming.Passive, guard.Timing);
            Assert.AreEqual(0, context.Messages.Count);
            Assert.AreEqual(0, actions.FundsAdded.Count);
            Assert.AreEqual(0, actions.Moves.Count);
        }

        private sealed class FakePreviewBoard : ICardPreviewBoard
        {
            private readonly int spaceCount;
            private readonly int currentIndex;

            public FakePreviewBoard(int spaceCount, int currentIndex)
            {
                this.spaceCount = spaceCount;
                this.currentIndex = currentIndex;
            }

            public int GetPlayerSpaceIndex(int pawnSlot)
            {
                return pawnSlot == 1 ? currentIndex : 0;
            }

            public int NormalizeIndex(int spaceIndex)
            {
                int normalized = spaceIndex % spaceCount;
                return normalized < 0 ? normalized + spaceCount : normalized;
            }

            public string GetSpaceName(int spaceIndex)
            {
                return $"Space {spaceIndex}";
            }
        }
    }
}
