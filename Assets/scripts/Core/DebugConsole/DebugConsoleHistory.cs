using System;
using System.Collections.Generic;

namespace MonopolyGame.Debugging
{
    /// <summary>Command history for the input line; the cursor starts past the newest entry.</summary>
    public sealed class DebugConsoleHistory
    {
        private readonly List<string> entries = new List<string>();
        private int cursor;

        public DebugConsoleHistory(int capacity = 50)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "History capacity must be at least one.");
            }

            Capacity = capacity;
            cursor = 0;
        }

        public int Capacity { get; }

        public int Count => entries.Count;

        public IReadOnlyList<string> Entries => entries;

        public int Cursor => cursor;

        public void Add(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            string trimmed = line.Trim();

            if (entries.Count == 0 ||
                !string.Equals(entries[entries.Count - 1], trimmed, StringComparison.Ordinal))
            {
                entries.Add(trimmed);

                while (entries.Count > Capacity)
                {
                    entries.RemoveAt(0);
                }
            }

            ResetCursor();
        }

        public void ResetCursor()
        {
            cursor = entries.Count;
        }

        /// <summary>Steps back one entry; false when the oldest entry is already shown.</summary>
        public bool TryMovePrevious(out string line)
        {
            if (entries.Count == 0 || cursor <= 0)
            {
                line = null;
                return false;
            }

            cursor--;
            line = entries[cursor];
            return true;
        }

        /// <summary>
        /// Steps forward one entry; false when a fresh line is already shown. On the
        /// step back to a fresh line the returned line is empty.
        /// </summary>
        public bool TryMoveNext(out string line)
        {
            if (cursor >= entries.Count)
            {
                line = null;
                return false;
            }

            cursor++;
            line = cursor >= entries.Count ? string.Empty : entries[cursor];
            return true;
        }

        public void Clear()
        {
            entries.Clear();
            ResetCursor();
        }
    }
}
