using System;
using System.Collections.Generic;
using MonopolyGame.Cards;
using MonopolyGame.Debugging;
using MonopolyGame.Multiplayer.Gameplay;

namespace MonopolyGame.Multiplayer.Debugging
{
    /// <summary>
    /// The single place every debug command is declared; handlers forward to the
    /// server-only Debug* methods on the session controller.
    /// </summary>
    public static class DebugCommandTable
    {
        public static DebugConsole Create(
            MultiplayerGameSessionController session,
            Func<string, DebugCommandResult> scenarioDispatch,
            IReadOnlyList<DebugScenario> scenarios)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var console = new DebugConsole();

            AddConsoleCommands(console, scenarioDispatch, scenarios);
            AddInspectCommands(session, console);
            AddEconomyCommands(session, console);
            AddCardCommands(session, console);
            AddMovementCommands(session, console);
            AddPropertyCommands(session, console);
            AddTurnCommands(session, console);
            AddResetCommands(session, console);

            return console;
        }

        // ── Command groups ─────────────────────────────────────────────────────────

        private static void AddConsoleCommands(
            DebugConsole console,
            Func<string, DebugCommandResult> scenarioDispatch,
            IReadOnlyList<DebugScenario> scenarios)
        {
            console.Add(Command(
                "clear",
                "Clear the console window.",
                "clear",
                "Console",
                DebugCommandExecutionMode.Local,
                args => DebugCommandResult.Clear()));

            console.Add(Command(
                "echo",
                "Print a line.",
                "echo <text>",
                "Console",
                DebugCommandExecutionMode.Local,
                args => DebugCommandResult.Ok(args.Rest(0))));

            console.Add(Command(
                "scenario",
                "List the built-in scenarios, or run one.",
                "scenario [name]",
                "Console",
                DebugCommandExecutionMode.Local,
                args => RunScenario(args, scenarioDispatch, scenarios)));
        }

        private static void AddInspectCommands(MultiplayerGameSessionController session, DebugConsole console)
        {
            console.Add(Command(
                "state",
                "Dump the game state, including every hand.",
                "state",
                "Inspect",
                DebugCommandExecutionMode.Server,
                args => session.DebugDescribeState(),
                "dump"));

            console.Add(Command(
                "players",
                "List players with balance and board position.",
                "players",
                "Inspect",
                DebugCommandExecutionMode.Server,
                args => session.DebugDescribePlayers()));

            console.Add(Command(
                "hand",
                "Show one player's hand, including opponents'.",
                "hand [player]",
                "Inspect",
                DebugCommandExecutionMode.Server,
                args => DescribeHand(session, args)));

            console.Add(Command(
                "piles",
                "Show both decks' pile counts and top discard.",
                "piles",
                "Inspect",
                DebugCommandExecutionMode.Server,
                args => session.DebugDescribePiles(),
                "decks"));

            console.Add(Command(
                "board",
                "List every property with its owner.",
                "board",
                "Inspect",
                DebugCommandExecutionMode.Server,
                args => session.DebugDescribeBoard()));
        }

        private static void AddEconomyCommands(MultiplayerGameSessionController session, DebugConsole console)
        {
            console.Add(Command(
                "money",
                "Set a balance.",
                "money <player|all> <amount>",
                "Economy",
                DebugCommandExecutionMode.Server,
                args => ChangeMoney(session, args, add: false),
                "setmoney"));

            console.Add(Command(
                "addmoney",
                "Add to a balance (negative values subtract).",
                "addmoney <player|all> <amount>",
                "Economy",
                DebugCommandExecutionMode.Server,
                args => ChangeMoney(session, args, add: true),
                "add"));
        }

        private static void AddCardCommands(MultiplayerGameSessionController session, DebugConsole console)
        {
            console.Add(Command(
                "givecard",
                "Move a named card from the draw pile into a hand.",
                "givecard [player] [cardId]",
                "Cards",
                DebugCommandExecutionMode.Server,
                args => GiveCard(session, args),
                "card"));

            console.Add(Command(
                "drawcard",
                "Draw the top action card into a hand.",
                "drawcard [player]",
                "Cards",
                DebugCommandExecutionMode.Server,
                args => DrawCard(session, args)));

            console.Add(Command(
                "clearhand",
                "Empty a hand into the discard pile.",
                "clearhand [player]",
                "Cards",
                DebugCommandExecutionMode.Server,
                args => ClearHand(session, args)));

            console.Add(Command(
                "event",
                "Trigger an event card immediately.",
                "event [player]",
                "Cards",
                DebugCommandExecutionMode.Server,
                args => TriggerEvent(session, args),
                "triggerevent"));
        }

        private static void AddMovementCommands(MultiplayerGameSessionController session, DebugConsole console)
        {
            console.Add(Command(
                "teleport",
                "Place a pawn without resolving the landing.",
                "teleport <player> <space>",
                "Movement",
                DebugCommandExecutionMode.Server,
                args => Teleport(session, args),
                "tp"));

            console.Add(Command(
                "move",
                "Move a pawn and resolve the landing.",
                "move <player> <spaces>",
                "Movement",
                DebugCommandExecutionMode.Server,
                args => Move(session, args)));

            console.Add(Command(
                "land",
                "Place a pawn on a space and resolve the landing.",
                "land <player> <space>",
                "Movement",
                DebugCommandExecutionMode.Server,
                args => Land(session, args)));

            console.Add(Command(
                "jail",
                "Send a pawn to the jail space.",
                "jail <player>",
                "Movement",
                DebugCommandExecutionMode.Server,
                args => Jail(session, args)));
        }

        private static void AddPropertyCommands(MultiplayerGameSessionController session, DebugConsole console)
        {
            console.Add(Command(
                "own",
                "Give a property to a player.",
                "own <space> <player>",
                "Property",
                DebugCommandExecutionMode.Server,
                args => Own(session, args)));

            console.Add(Command(
                "disown",
                "Return a property to the bank.",
                "disown <space>",
                "Property",
                DebugCommandExecutionMode.Server,
                args => Disown(session, args)));

            console.Add(Command(
                "ownall",
                "Give every property to one player.",
                "ownall <player>",
                "Property",
                DebugCommandExecutionMode.Server,
                args => OwnAll(session, args)));
        }

        private static void AddTurnCommands(MultiplayerGameSessionController session, DebugConsole console)
        {
            console.Add(Command(
                "turn",
                "Force the active player.",
                "turn <player>",
                "Turn",
                DebugCommandExecutionMode.Server,
                args => SetTurn(session, args),
                "setturn"));

            console.Add(Command(
                "phase",
                "Force a turn phase.",
                "phase <awaitingroll|waitingforendturn>",
                "Turn",
                DebugCommandExecutionMode.Server,
                args => SetPhase(session, args)));

            console.Add(Command(
                "roll",
                "Roll the dice, optionally forcing the value.",
                "roll [1-6]",
                "Turn",
                DebugCommandExecutionMode.Server,
                args => Roll(session, args)));

            console.Add(Command(
                "endturn",
                "Advance to the next player.",
                "endturn",
                "Turn",
                DebugCommandExecutionMode.Server,
                args => session.DebugEndTurn(),
                "end"));
        }

        private static void AddResetCommands(MultiplayerGameSessionController session, DebugConsole console)
        {
            console.Add(Command(
                "reset",
                "Reset parts of the session ('reset all' needs 'confirm').",
                "reset <economy|board|decks|turn|all> [confirm]",
                "Reset",
                DebugCommandExecutionMode.Server,
                args => Reset(session, args)));
        }

        // ── Handlers ───────────────────────────────────────────────────────────────

        private static DebugCommandResult ChangeMoney(
            MultiplayerGameSessionController session,
            DebugConsoleArgs args,
            bool add)
        {
            if (!args.TryGet(0, out string token))
            {
                return DebugCommandResult.Fail($"Usage: {(add ? "addmoney" : "money")} <player|all> <amount>");
            }

            if (!args.TryGetInt(1, out int amount))
            {
                return DebugCommandResult.Fail("The amount must be a whole number.");
            }

            if (!TryResolvePlayers(session, token, out List<int> targets, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            var lines = new List<string>();

            foreach (int slot in targets)
            {
                DebugCommandResult result = add
                    ? session.DebugAddBalance(slot, amount)
                    : session.DebugSetBalance(slot, amount);

                if (!result.IsSuccess)
                {
                    return result;
                }

                lines.AddRange(result.Lines);
            }

            return DebugCommandResult.OkLines(lines);
        }

        private static DebugCommandResult GiveCard(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            var targets = new List<int>();
            string cardId = null;

            if (args.IsEmpty || IsKnownCard(args.Get(0)))
            {
                if (!args.IsEmpty)
                {
                    cardId = args.Get(0);
                }

                if (args.Count > 1)
                {
                    return DebugCommandResult.Fail("Usage: givecard [player] [cardId]");
                }

                if (!TryResolvePlayers(session, DebugTargets.SelfKeyword, out targets, out string error))
                {
                    return DebugCommandResult.Fail(error);
                }
            }
            else
            {
                if (args.Count > 2)
                {
                    return DebugCommandResult.Fail("Usage: givecard [player] [cardId]");
                }

                if (!TryResolvePlayers(session, args.Get(0), out targets, out string error))
                {
                    return DebugCommandResult.Fail(error);
                }

                if (args.Count > 1)
                {
                    cardId = args.Get(1);
                }
            }

            var lines = new List<string>();

            foreach (int slot in targets)
            {
                DebugCommandResult result = session.DebugGrantCard(slot, cardId);

                if (!result.IsSuccess)
                {
                    return result;
                }

                lines.AddRange(result.Lines);
            }

            return DebugCommandResult.OkLines(lines);
        }

        private static DebugCommandResult DrawCard(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryResolveOptionalPlayer(session, args, 0, out int slot, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugGrantCard(slot, null);
        }

        private static DebugCommandResult ClearHand(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryResolveOptionalPlayer(session, args, 0, out int slot, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugClearHand(slot);
        }

        private static DebugCommandResult TriggerEvent(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryResolveOptionalPlayer(session, args, 0, out int slot, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugTriggerEvent(slot);
        }

        private static DebugCommandResult DescribeHand(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryResolveOptionalPlayer(session, args, 0, out int slot, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugDescribeHand(slot);
        }

        private static DebugCommandResult Teleport(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryReadPlayer(session, args, 0, out int slot, out string error) ||
                !TryReadSpace(session, args, 1, out int spaceIndex, out error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugTeleport(slot, spaceIndex);
        }

        private static DebugCommandResult Move(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryReadPlayer(session, args, 0, out int slot, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            if (!args.TryGetInt(1, out int spaces))
            {
                return DebugCommandResult.Fail("Usage: move <player> <spaces>");
            }

            return session.DebugMove(slot, spaces);
        }

        private static DebugCommandResult Land(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryReadPlayer(session, args, 0, out int slot, out string error) ||
                !TryReadSpace(session, args, 1, out int spaceIndex, out error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugLand(slot, spaceIndex);
        }

        private static DebugCommandResult Jail(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryReadPlayer(session, args, 0, out int slot, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugTeleport(slot, session.JailSpaceIndex);
        }

        private static DebugCommandResult Own(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryReadSpace(session, args, 0, out int spaceIndex, out string error) ||
                !TryReadPlayer(session, args, 1, out int slot, out error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugSetPropertyOwner(spaceIndex, slot);
        }

        private static DebugCommandResult Disown(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryReadSpace(session, args, 0, out int spaceIndex, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugClearProperty(spaceIndex);
        }

        private static DebugCommandResult OwnAll(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryReadPlayer(session, args, 0, out int slot, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugGrantAllProperties(slot);
        }

        private static DebugCommandResult SetTurn(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!TryReadPlayer(session, args, 0, out int slot, out string error))
            {
                return DebugCommandResult.Fail(error);
            }

            return session.DebugSetActiveTurn(slot);
        }

        private static DebugCommandResult SetPhase(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!args.TryGet(0, out string token))
            {
                return DebugCommandResult.Fail("Usage: phase <awaitingroll|waitingforendturn>");
            }

            switch (token.Trim().ToLowerInvariant())
            {
                case "awaitingroll":
                    return session.DebugSetPhase(TurnPhase.AwaitingRoll);

                case "waitingforendturn":
                    return session.DebugSetPhase(TurnPhase.WaitingForEndTurn);

                default:
                    return DebugCommandResult.Fail(
                        $"Unknown phase '{token}'. Use 'awaitingroll' or 'waitingforendturn'.");
            }
        }

        private static DebugCommandResult Roll(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            int forcedDice = 0;

            if (args.Count > 0 && !args.TryGetInt(0, out forcedDice))
            {
                return DebugCommandResult.Fail("The dice value must be between 1 and 6.");
            }

            return session.DebugRoll(forcedDice);
        }

        private static DebugCommandResult Reset(MultiplayerGameSessionController session, DebugConsoleArgs args)
        {
            if (!args.TryGet(0, out string token))
            {
                return DebugCommandResult.Fail("Usage: reset <economy|board|decks|turn|all> [confirm]");
            }

            bool confirmed = args.Count > 1 &&
                             string.Equals(args.Get(1), "confirm", StringComparison.OrdinalIgnoreCase);

            switch (token.Trim().ToLowerInvariant())
            {
                case "economy":
                    return session.DebugResetEconomy();

                case "board":
                    return session.DebugResetBoard();

                case "decks":
                    return session.DebugResetDecks();

                case "turn":
                    return session.DebugResetTurn();

                case "all":
                    return ResetAll(session, confirmed);

                default:
                    return DebugCommandResult.Fail($"Unknown reset target '{token}'.");
            }
        }

        private static DebugCommandResult ResetAll(MultiplayerGameSessionController session, bool confirmed)
        {
            if (!confirmed)
            {
                return DebugCommandResult.Fail("'reset all' wipes the session. Run 'reset all confirm'.");
            }

            var lines = new List<string>();
            DebugCommandResult[] results =
            {
                session.DebugResetTurn(),
                session.DebugResetEconomy(),
                session.DebugResetBoard(),
                session.DebugResetDecks()
            };

            foreach (DebugCommandResult result in results)
            {
                if (!result.IsSuccess)
                {
                    return result;
                }

                lines.AddRange(result.Lines);
            }

            return DebugCommandResult.OkLines(lines);
        }

        private static DebugCommandResult RunScenario(
            DebugConsoleArgs args,
            Func<string, DebugCommandResult> scenarioDispatch,
            IReadOnlyList<DebugScenario> scenarios)
        {
            IReadOnlyList<DebugScenario> available = scenarios ?? Array.Empty<DebugScenario>();

            if (args.IsEmpty)
            {
                var lines = new List<string>();

                foreach (DebugScenario scenario in available)
                {
                    lines.Add($"  {scenario.Name,-12} {scenario.Description}");
                }

                return DebugCommandResult.OkLines(lines);
            }

            string name = args.Get(0).Trim();
            DebugScenario selected = null;

            foreach (DebugScenario scenario in available)
            {
                if (string.Equals(scenario.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    selected = scenario;
                    break;
                }
            }

            if (selected == null)
            {
                return DebugCommandResult.Fail($"Unknown scenario '{name}'. Type 'scenario' for the list.");
            }

            DebugScenarioResult run = new DebugScenarioRunner(scenarioDispatch).Run(selected);

            if (run.IsSuccess)
            {
                return DebugCommandResult.OkLines(run.Lines);
            }

            var failure = new List<string>(run.Lines)
            {
                $"Scenario '{selected.Name}' stopped at: {run.FailedCommand}"
            };

            return DebugCommandResult.Fail(string.Join("\n", failure));
        }

        // ── Argument helpers ───────────────────────────────────────────────────────

        private static DebugCommandDefinition Command(
            string name,
            string summary,
            string usage,
            string category,
            DebugCommandExecutionMode mode,
            Func<DebugConsoleArgs, DebugCommandResult> handler,
            params string[] aliases)
        {
            return new DebugCommandDefinition(name, summary, usage, category, mode, handler, aliases);
        }

        private static bool IsKnownCard(string token)
        {
            return ActionCardCatalog.TryGetDefinition(token, out _);
        }

        private static bool TryResolvePlayers(
            MultiplayerGameSessionController session,
            string token,
            out List<int> targets,
            out string error)
        {
            targets = new List<int>();
            return DebugTargets.TryParsePlayers(token, session.PlayerCount, session.GetLocalPawnSlot(), targets, out error);
        }

        private static bool TryResolveOptionalPlayer(
            MultiplayerGameSessionController session,
            DebugConsoleArgs args,
            int index,
            out int pawnSlot,
            out string error)
        {
            pawnSlot = session.GetLocalPawnSlot();

            if (!args.TryGet(index, out string token))
            {
                error = null;
                return true;
            }

            return TryResolveSinglePlayer(session, token, out pawnSlot, out error);
        }

        private static bool TryReadPlayer(
            MultiplayerGameSessionController session,
            DebugConsoleArgs args,
            int index,
            out int pawnSlot,
            out string error)
        {
            pawnSlot = -1;

            if (!args.TryGet(index, out string token))
            {
                error = "Expected a player: 'me', 'all' or a slot number.";
                return false;
            }

            return TryResolveSinglePlayer(session, token, out pawnSlot, out error);
        }

        private static bool TryResolveSinglePlayer(
            MultiplayerGameSessionController session,
            string token,
            out int pawnSlot,
            out string error)
        {
            pawnSlot = -1;

            if (!TryResolvePlayers(session, token, out List<int> targets, out error))
            {
                return false;
            }

            if (targets.Count != 1)
            {
                error = "This command affects exactly one player.";
                return false;
            }

            pawnSlot = targets[0];
            return true;
        }

        private static bool TryReadSpace(
            MultiplayerGameSessionController session,
            DebugConsoleArgs args,
            int index,
            out int spaceIndex,
            out string error)
        {
            spaceIndex = -1;

            if (!args.TryGet(index, out string token))
            {
                error = "Expected a board space index.";
                return false;
            }

            return DebugTargets.TryParseIndex(token, session.BoardSpaceCount, out spaceIndex, out error);
        }
    }
}
