using System;
using System.Collections.Generic;

namespace MonopolyGame.Debugging
{
    public sealed class DebugCommandDefinition
    {
        private static readonly string[] NoAliases = Array.Empty<string>();

        public DebugCommandDefinition(
            string name,
            string summary,
            string usage,
            string category,
            DebugCommandExecutionMode mode,
            Func<DebugConsoleArgs, DebugCommandResult> handler,
            params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A command needs a name.", nameof(name));
            }

            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Name = name.Trim();
            Summary = summary ?? string.Empty;
            Usage = string.IsNullOrWhiteSpace(usage) ? Name : usage.Trim();
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();
            Mode = mode;
            Aliases = aliases != null ? aliases : NoAliases;
        }

        public string Name { get; }
        public string Summary { get; }
        public string Usage { get; }
        public string Category { get; }
        public DebugCommandExecutionMode Mode { get; }
        public IReadOnlyList<string> Aliases { get; }
        public Func<DebugConsoleArgs, DebugCommandResult> Handler { get; }
    }
}
