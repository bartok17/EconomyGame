using System.Collections.Generic;
using MonopolyGame.Debugging;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class DebugTargetsTests
    {
        [Test]
        public void TryParsePlayers_MeResolvesToTheCallerSlot()
        {
            var targets = new List<int>();

            Assert.IsTrue(DebugTargets.TryParsePlayers("me", 3, 2, targets, out string error));
            Assert.IsNull(error);
            CollectionAssert.AreEqual(new[] { 2 }, targets);
        }

        [Test]
        public void TryParsePlayers_MeIsCaseInsensitive()
        {
            var targets = new List<int>();

            Assert.IsTrue(DebugTargets.TryParsePlayers("ME", 3, 1, targets, out _));
            CollectionAssert.AreEqual(new[] { 1 }, targets);
        }

        [Test]
        public void TryParsePlayers_AllExpandsToEverySeat()
        {
            var targets = new List<int>();

            Assert.IsTrue(DebugTargets.TryParsePlayers("all", 3, 0, targets, out _));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, targets);
        }

        [Test]
        public void TryParsePlayers_NumberResolvesToThatSlot()
        {
            var targets = new List<int>();

            Assert.IsTrue(DebugTargets.TryParsePlayers("2", 4, 0, targets, out _));
            CollectionAssert.AreEqual(new[] { 2 }, targets);
        }

        [Test]
        public void TryParsePlayers_RejectsOutOfRangeSlot()
        {
            var targets = new List<int>();

            Assert.IsFalse(DebugTargets.TryParsePlayers("4", 4, 0, targets, out string error));
            StringAssert.Contains("Unknown player", error);
            Assert.AreEqual(0, targets.Count);
        }

        [Test]
        public void TryParsePlayers_RejectsGarbage()
        {
            var targets = new List<int>();

            Assert.IsFalse(DebugTargets.TryParsePlayers("bob", 4, 0, targets, out string error));
            StringAssert.Contains("Unknown player", error);
        }

        [Test]
        public void TryParsePlayers_RejectsMeWhenNotSeated()
        {
            var targets = new List<int>();

            Assert.IsFalse(DebugTargets.TryParsePlayers("me", 4, -1, targets, out string error));
            StringAssert.Contains("not seated", error);
        }

        [Test]
        public void TryParsePlayers_RejectsEmptyToken()
        {
            var targets = new List<int>();

            Assert.IsFalse(DebugTargets.TryParsePlayers("  ", 2, 0, targets, out string error));
            StringAssert.Contains("Expected a player", error);
        }

        [Test]
        public void TryParsePlayers_RejectsAnEmptySession()
        {
            var targets = new List<int>();

            Assert.IsFalse(DebugTargets.TryParsePlayers("me", 0, 0, targets, out string error));
            StringAssert.Contains("No players", error);
        }

        [Test]
        public void TryParseIndex_AcceptsAnIndexInsideTheBoard()
        {
            Assert.IsTrue(DebugTargets.TryParseIndex("39", 40, out int index, out string error));
            Assert.AreEqual(39, index);
            Assert.IsNull(error);
        }

        [Test]
        public void TryParseIndex_RejectsPastTheLastSpace()
        {
            Assert.IsFalse(DebugTargets.TryParseIndex("40", 40, out _, out string error));
            StringAssert.Contains("between 0 and 39", error);
        }

        [Test]
        public void TryParseIndex_RejectsGarbage()
        {
            Assert.IsFalse(DebugTargets.TryParseIndex("start", 40, out _, out _));
        }

        [Test]
        public void TryParseIndex_RejectsAnEmptyBoard()
        {
            Assert.IsFalse(DebugTargets.TryParseIndex("0", 0, out _, out string error));
            StringAssert.Contains("no spaces", error);
        }
    }
}
