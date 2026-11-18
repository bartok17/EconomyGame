using MonopolyGame.Debugging;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class DebugConsoleHistoryTests
    {
        [Test]
        public void Add_IgnoresBlankLines()
        {
            var history = new DebugConsoleHistory();

            history.Add(null);
            history.Add("   ");
            history.Add("money me 100");

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("money me 100", history.Entries[0]);
        }

        [Test]
        public void Add_SkipsConsecutiveDuplicates()
        {
            var history = new DebugConsoleHistory();

            history.Add("help");
            history.Add("help");
            history.Add("help");

            Assert.AreEqual(1, history.Count);
        }

        [Test]
        public void Add_KeepsNonConsecutiveDuplicates()
        {
            var history = new DebugConsoleHistory();

            history.Add("help");
            history.Add("state");
            history.Add("help");

            Assert.AreEqual(3, history.Count);
        }

        [Test]
        public void Add_TrimsOldestEntriesAtCapacity()
        {
            var history = new DebugConsoleHistory(capacity: 2);

            history.Add("one");
            history.Add("two");
            history.Add("three");

            CollectionAssert.AreEqual(new[] { "two", "three" }, history.Entries);
        }

        [Test]
        public void Cursor_StartsPastTheNewestEntry()
        {
            var history = new DebugConsoleHistory();
            history.Add("one");

            Assert.AreEqual(1, history.Cursor);
        }

        [Test]
        public void TryMovePrevious_WalksBackAndStopsAtTheOldest()
        {
            var history = new DebugConsoleHistory();
            history.Add("one");
            history.Add("two");

            Assert.IsTrue(history.TryMovePrevious(out string latest));
            Assert.AreEqual("two", latest);

            Assert.IsTrue(history.TryMovePrevious(out string older));
            Assert.AreEqual("one", older);

            Assert.IsFalse(history.TryMovePrevious(out string none));
            Assert.IsNull(none);
        }

        [Test]
        public void TryMoveNext_WalksForwardAndClearsAtTheEnd()
        {
            var history = new DebugConsoleHistory();
            history.Add("one");
            history.Add("two");
            history.TryMovePrevious(out _);
            history.TryMovePrevious(out _);

            Assert.IsTrue(history.TryMoveNext(out string older));
            Assert.AreEqual("two", older);

            Assert.IsTrue(history.TryMoveNext(out string fresh));
            Assert.AreEqual(string.Empty, fresh);

            Assert.IsFalse(history.TryMoveNext(out string none));
            Assert.IsNull(none);
        }

        [Test]
        public void Add_ResetsTheCursor()
        {
            var history = new DebugConsoleHistory();
            history.Add("one");
            history.Add("two");
            history.TryMovePrevious(out _);

            history.Add("three");

            Assert.AreEqual(3, history.Cursor);
        }

        [Test]
        public void Clear_DropsEverything()
        {
            var history = new DebugConsoleHistory();
            history.Add("one");
            history.Add("two");

            history.Clear();

            Assert.AreEqual(0, history.Count);
            Assert.AreEqual(0, history.Cursor);
        }
    }
}
