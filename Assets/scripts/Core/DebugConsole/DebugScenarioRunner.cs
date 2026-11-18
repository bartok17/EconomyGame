using System;
using System.Collections.Generic;

namespace MonopolyGame.Debugging
{
    public sealed class DebugScenarioResult
    {
        private static readonly string[] NoLines = Array.Empty<string>();

        public DebugScenarioResult(bool isSuccess, string failedCommand, IReadOnlyList<string> lines)
        {
            IsSuccess = isSuccess;
            FailedCommand = failedCommand;
            Lines = lines ?? NoLines;
        }

        public bool IsSuccess { get; }

        public string FailedCommand { get; }

        public IReadOnlyList<string> Lines { get; }
    }

    /// <summary>Runs a scenario line by line, stopping at the first real failure.</summary>
    public sealed class DebugScenarioRunner
    {
        private readonly Func<string, DebugCommandResult> execute;

        public DebugScenarioRunner(Func<string, DebugCommandResult> execute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public DebugScenarioResult Run(DebugScenario scenario)
        {
            var lines = new List<string>();

            if (scenario == null)
            {
                return new DebugScenarioResult(false, null, new[] { "No scenario given." });
            }

            lines.Add($"Running scenario '{scenario.Name}'...");

            for (int i = 0; i < scenario.Lines.Length; i++)
            {
                string commandLine = scenario.Lines[i];

                if (string.IsNullOrWhiteSpace(commandLine) || commandLine.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                lines.Add($"> {commandLine.Trim()}");

                DebugCommandResult result = execute(commandLine);

                if (result == null)
                {
                    lines.Add("The command produced no result.");
                    return new DebugScenarioResult(false, commandLine, lines);
                }

                lines.AddRange(result.Lines);

                if (!result.IsSuccess && !result.IsPending)
                {
                    return new DebugScenarioResult(false, commandLine, lines);
                }
            }

            lines.Add($"Scenario '{scenario.Name}' finished.");
            return new DebugScenarioResult(true, null, lines);
        }
    }
}
