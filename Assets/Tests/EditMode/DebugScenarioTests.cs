using System.Collections.Generic;
using MonopolyGame.Debugging;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class DebugScenarioTests
    {
        [Test]
        public void Run_ExecutesEveryLineInOrder()
        {
            var executed = new List<string>();
            var runner = new DebugScenarioRunner(line =>
            {
                executed.Add(line);
                return DebugCommandResult.Ok($"ran {line}");
            });

            var scenario = new DebugScenario("demo", "Demo scenario", "money all 100", "own 1 0");
            DebugScenarioResult result = runner.Run(scenario);

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "money all 100", "own 1 0" }, executed);
            StringAssert.Contains("ran money all 100", string.Join("\n", result.Lines));
            StringAssert.Contains("finished", string.Join("\n", result.Lines));
        }

        [Test]
        public void Run_SkipsCommentsAndBlankLines()
        {
            var executed = new List<string>();
            var runner = new DebugScenarioRunner(line =>
            {
                executed.Add(line);
                return DebugCommandResult.Ok();
            });

            var scenario = new DebugScenario("demo", string.Empty, "# comment", "   ", "state");

            runner.Run(scenario);

            CollectionAssert.AreEqual(new[] { "state" }, executed);
        }

        [Test]
        public void Run_StopsAtTheFirstFailure()
        {
            var executed = new List<string>();
            var runner = new DebugScenarioRunner(line =>
            {
                executed.Add(line);

                return line == "bad"
                    ? DebugCommandResult.Fail("that failed")
                    : DebugCommandResult.Ok();
            });

            var scenario = new DebugScenario("demo", string.Empty, "first", "bad", "never");
            DebugScenarioResult result = runner.Run(scenario);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("bad", result.FailedCommand);
            CollectionAssert.AreEqual(new[] { "first", "bad" }, executed);
            StringAssert.Contains("that failed", string.Join("\n", result.Lines));
        }

        [Test]
        public void Run_ContinuesWhileCommandsArePending()
        {
            int calls = 0;
            var runner = new DebugScenarioRunner(line =>
            {
                calls++;
                return DebugCommandResult.Pending($"> {line}");
            });

            var scenario = new DebugScenario("demo", string.Empty, "one", "two");
            DebugScenarioResult result = runner.Run(scenario);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void Run_NullScenarioFails()
        {
            var runner = new DebugScenarioRunner(_ => DebugCommandResult.Ok());

            DebugScenarioResult result = runner.Run(null);

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void Run_NullResultStopsTheRun()
        {
            var runner = new DebugScenarioRunner(_ => null);

            DebugScenarioResult result = runner.Run(new DebugScenario("demo", string.Empty, "one"));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("one", result.FailedCommand);
        }

        [Test]
        public void Constructor_RejectsBlankName()
        {
            Assert.Throws<System.ArgumentException>(() => new DebugScenario("  ", string.Empty));
        }
    }
}
