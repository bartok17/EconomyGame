using System;

namespace MonopolyGame.Debugging
{
    public sealed class DebugScenario
    {
        private static readonly string[] NoLines = Array.Empty<string>();

        public DebugScenario(string name, string description, params string[] lines)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A scenario needs a name.", nameof(name));
            }

            Name = name.Trim();
            Description = description ?? string.Empty;
            Lines = lines ?? NoLines;
        }

        public string Name { get; }
        public string Description { get; }
        public string[] Lines { get; }
    }
}
