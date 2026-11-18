using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MonopolyGame.Debugging
{
    /// <summary>One parsed command line: token 0 is the command name, the rest are arguments.</summary>
    public sealed class DebugConsoleArgs
    {
        private readonly IReadOnlyList<string> tokens;

        public DebugConsoleArgs(string rawLine, IReadOnlyList<string> tokens)
        {
            RawLine = rawLine ?? string.Empty;
            this.tokens = tokens ?? Array.Empty<string>();
        }

        public string RawLine { get; }

        public string CommandName => tokens.Count > 0 ? tokens[0] : string.Empty;

        public int Count => Math.Max(0, tokens.Count - 1);

        public bool IsEmpty => Count == 0;

        public bool TryGet(int index, out string value)
        {
            int tokenIndex = index + 1;

            if (index < 0 || tokenIndex >= tokens.Count)
            {
                value = null;
                return false;
            }

            value = tokens[tokenIndex];
            return true;
        }

        public string Get(int index)
        {
            return TryGet(index, out string value) ? value : null;
        }

        public bool TryGetInt(int index, out int value)
        {
            value = 0;

            return TryGet(index, out string token) &&
                   int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public string Rest(int index)
        {
            if (index < 0 || index >= Count)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            for (int i = index; i < Count; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(Get(i));
            }

            return builder.ToString();
        }
    }
}
