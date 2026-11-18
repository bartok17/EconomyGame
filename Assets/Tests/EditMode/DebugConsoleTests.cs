using System;
using System.Collections.Generic;
using MonopolyGame.Debugging;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class DebugConsoleTests
    {
        [Test]
        public void Tokenize_SplitsOnWhitespace()
        {
            IReadOnlyList<string> tokens = DebugConsole.Tokenize("  money   all   500  ");

            CollectionAssert.AreEqual(new[] { "money", "all", "500" }, tokens);
        }

        [Test]
        public void Tokenize_KeepsQuotedTextTogether()
        {
            IReadOnlyList<string> tokens = DebugConsole.Tokenize("echo \"hello world\" tail");

            CollectionAssert.AreEqual(new[] { "echo", "hello world", "tail" }, tokens);
        }

        [Test]
        public void Tokenize_EmptyQuotesProduceAnEmptyToken()
        {
            IReadOnlyList<string> tokens = DebugConsole.Tokenize("say \"\"");

            CollectionAssert.AreEqual(new[] { "say", string.Empty }, tokens);
        }

        [Test]
        public void Tokenize_StopsAtComment()
        {
            IReadOnlyList<string> tokens = DebugConsole.Tokenize("money me 100 # give it all");

            CollectionAssert.AreEqual(new[] { "money", "me", "100" }, tokens);
        }

        [Test]
        public void Execute_EmptyLine_ProducesNoOutput()
        {
            DebugConsole console = CreateConsole(AlwaysOk);

            DebugCommandResult result = console.Execute("   ");

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Lines.Count);
        }

        [Test]
        public void Execute_UnknownCommand_Fails()
        {
            DebugConsole console = CreateConsole(AlwaysOk);

            DebugCommandResult result = console.Execute("nope");

            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("Unknown command 'nope'", result.Lines[0]);
        }

        [Test]
        public void Execute_ResolvesAliasAndPassesArguments()
        {
            var seen = new List<string>();
            var console = new DebugConsole(new[]
            {
                new DebugCommandDefinition(
                    "teleport",
                    "Move.",
                    "teleport <player> <space>",
                    "Movement",
                    DebugCommandExecutionMode.Server,
                    args =>
                    {
                        seen.Add(args.CommandName);
                        seen.Add(args.Get(0));
                        seen.Add(args.Get(1));
                        return DebugCommandResult.Ok("done");
                    },
                    "tp")
            });

            DebugCommandResult result = console.Execute("TP 2 9");

            Assert.IsTrue(result.IsSuccess);
            CollectionAssert.AreEqual(new[] { "TP", "2", "9" }, seen);
        }

        [Test]
        public void Execute_HandlerExceptionBecomesFailure()
        {
            DebugConsole console = CreateConsole(_ => throw new InvalidOperationException("boom"));

            DebugCommandResult result = console.Execute("test");

            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("boom", result.Lines[0]);
        }

        [Test]
        public void Execute_NullHandlerResultBecomesFailure()
        {
            DebugConsole console = CreateConsole(_ => null);

            DebugCommandResult result = console.Execute("test");

            Assert.IsFalse(result.IsSuccess);
            StringAssert.Contains("produced no result", result.Lines[0]);
        }

        [Test]
        public void Execute_CommandModeIsPreservedOnLookup()
        {
            DebugConsole console = CreateConsole(AlwaysOk, mode: DebugCommandExecutionMode.Local, aliases: "t");

            Assert.IsTrue(console.TryGetCommand("test", out DebugCommandDefinition direct));
            Assert.IsTrue(console.TryGetCommand("t", out DebugCommandDefinition aliased));
            Assert.AreEqual(DebugCommandExecutionMode.Local, direct.Mode);
            Assert.AreSame(direct, aliased);
        }

        [Test]
        public void Complete_ReturnsNamesAndAliasesThatMatch()
        {
            var console = new DebugConsole(new[]
            {
                Command("money", "setmoney"),
                Command("move"),
                Command("mode")
            });

            CollectionAssert.AreEqual(new[] { "mode", "money", "move" }, console.Complete("mo"));
            CollectionAssert.AreEqual(new[] { "setmoney" }, console.Complete("set"));
        }

        [Test]
        public void Complete_UnknownPrefixReturnsNothing()
        {
            DebugConsole console = CreateConsole(AlwaysOk);

            Assert.AreEqual(0, console.Complete("zzz").Count);
        }

        [Test]
        public void DescribeAll_GroupsCommandsByCategory()
        {
            var console = new DebugConsole(new[]
            {
                new DebugCommandDefinition("money", "Set a balance.", "money <player> <amount>", "Economy",
                    DebugCommandExecutionMode.Server, AlwaysOk),
                new DebugCommandDefinition("roll", "Roll the dice.", "roll [1-6]", "Turn",
                    DebugCommandExecutionMode.Server, AlwaysOk)
            });

            string listing = string.Join("\n", console.DescribeAll());

            StringAssert.Contains("Console", listing);
            StringAssert.Contains("Economy", listing);
            StringAssert.Contains("Turn", listing);
            StringAssert.Contains("money <player> <amount>", listing);
            StringAssert.Contains("Set a balance.", listing);
        }

        [Test]
        public void Describe_UnknownCommandFails()
        {
            DebugConsole console = CreateConsole(AlwaysOk);

            DebugCommandResult result = console.Describe("nope");

            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public void Describe_IncludesUsageAliasesAndExecutionMode()
        {
            DebugConsole console = CreateConsole(
                AlwaysOk,
                name: "money",
                mode: DebugCommandExecutionMode.Server,
                aliases: "setmoney");

            string text = string.Join("\n", console.Describe("money").Lines);

            StringAssert.Contains("money <value>", text);
            StringAssert.Contains("setmoney", text);
            StringAssert.Contains("Runs on the host", text);
        }

        [Test]
        public void Add_DuplicateNameThrows()
        {
            var console = new DebugConsole(new[] { Command("money") });

            Assert.Throws<InvalidOperationException>(() => console.Add(Command("money")));
        }

        [Test]
        public void Add_DuplicateAliasThrows()
        {
            var console = new DebugConsole(new[] { Command("money", "cash") });

            Assert.Throws<InvalidOperationException>(() => console.Add(Command("coins", "cash")));
        }

        private static DebugConsole CreateConsole(
            Func<DebugConsoleArgs, DebugCommandResult> handler,
            string name = "test",
            DebugCommandExecutionMode mode = DebugCommandExecutionMode.Server,
            params string[] aliases)
        {
            return new DebugConsole(new[]
            {
                new DebugCommandDefinition(name, "Test command.", $"{name} <value>", "Testing", mode, handler, aliases)
            });
        }

        private static DebugCommandDefinition Command(string name, params string[] aliases)
        {
            return new DebugCommandDefinition(name, "Test command.", name, "Testing",
                DebugCommandExecutionMode.Local, AlwaysOk, aliases);
        }

        private static DebugCommandResult AlwaysOk(DebugConsoleArgs args)
        {
            return DebugCommandResult.Ok();
        }
    }
}
