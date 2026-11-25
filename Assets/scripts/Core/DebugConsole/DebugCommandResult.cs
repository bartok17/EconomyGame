using System;
using System.Collections.Generic;

namespace MonopolyGame.Debugging
{
    public sealed class DebugCommandResult
    {
        private static readonly string[] NoLines = Array.Empty<string>();

        private DebugCommandResult(bool isSuccess, bool isPending, bool clearsConsole, IReadOnlyList<string> lines)
        {
            IsSuccess = isSuccess;
            IsPending = isPending;
            ClearsConsole = clearsConsole;
            Lines = lines ?? NoLines;
        }

        public bool IsSuccess { get; }

        public bool IsPending { get; }

        public bool ClearsConsole { get; }

        public IReadOnlyList<string> Lines { get; }

        public static DebugCommandResult Ok(params string[] lines)
        {
            return new DebugCommandResult(true, false, false, Clean(lines));
        }

        public static DebugCommandResult OkLines(IReadOnlyList<string> lines)
        {
            return new DebugCommandResult(true, false, false, Clean(lines));
        }

        public static DebugCommandResult Fail(string message)
        {
            return new DebugCommandResult(
                false,
                false,
                false,
                new[] { string.IsNullOrWhiteSpace(message) ? "Command failed." : message });
        }

        public static DebugCommandResult Pending(string message)
        {
            return new DebugCommandResult(true, true, false, Clean(new[] { message }));
        }

        public static DebugCommandResult Clear()
        {
            return new DebugCommandResult(true, false, true, NoLines);
        }

        private static IReadOnlyList<string> Clean(IEnumerable<string> lines)
        {
            if (lines == null)
            {
                return NoLines;
            }

            var cleaned = new List<string>();

            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    cleaned.Add(line);
                }
            }

            return cleaned;
        }
    }
}
