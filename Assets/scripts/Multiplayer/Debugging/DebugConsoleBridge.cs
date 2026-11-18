using System;
using System.Collections.Generic;
using MonopolyGame.Debugging;
using MonopolyGame.Multiplayer.Gameplay;

namespace MonopolyGame.Multiplayer.Debugging
{
    /// <summary>
    /// Runs console lines: local commands immediately, server commands on the host,
    /// directly there and through one RPC from a client.
    /// </summary>
    public sealed class DebugConsoleBridge : IDisposable
    {
        private readonly MultiplayerGameSessionController session;
        private readonly List<DebugScenario> scenarios;
        private readonly DebugConsole console;

        private int scenarioDepth;

        public DebugConsoleBridge(
            MultiplayerGameSessionController session,
            IEnumerable<DebugScenario> scenarios = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.scenarios = scenarios != null
                ? new List<DebugScenario>(scenarios)
                : DebugScenarios.CreateDefault();

            console = DebugCommandTable.Create(session, ExecuteScenarioLine, this.scenarios);
            session.DebugConsoleResponded += HandleServerResponse;
        }

        public event Action<string, DebugCommandResult> ResponseReceived;

        public DebugConsole Console => console;

        public IReadOnlyList<DebugScenario> Scenarios => scenarios;

        public IReadOnlyList<string> Complete(string prefix)
        {
            return console.Complete(prefix);
        }

        public DebugCommandResult Execute(string line)
        {
            IReadOnlyList<string> tokens = DebugConsole.Tokenize(line);

            if (tokens.Count == 0)
            {
                return DebugCommandResult.Ok();
            }

            if (console.TryGetCommand(tokens[0], out DebugCommandDefinition command) &&
                command.Mode == DebugCommandExecutionMode.Server &&
                !session.IsServer)
            {
                session.SendDebugCommandToServer(line);
                return DebugCommandResult.Pending(null);
            }

            return ExecuteOnServer(line);
        }

        public DebugCommandResult ExecuteOnServer(string line)
        {
            return console.Execute(line);
        }

        public void Dispose()
        {
            session.DebugConsoleResponded -= HandleServerResponse;
        }

        private DebugCommandResult ExecuteScenarioLine(string line)
        {
            if (scenarioDepth > 0)
            {
                return DebugCommandResult.Fail("Scenarios cannot run other scenarios.");
            }

            scenarioDepth++;

            try
            {
                return Execute(line);
            }
            finally
            {
                scenarioDepth--;
            }
        }

        private void HandleServerResponse(string line, DebugCommandResult result)
        {
            ResponseReceived?.Invoke(line, result);
        }
    }
}
