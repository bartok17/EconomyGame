using System;
using System.Collections.Generic;
using System.Globalization;

namespace MonopolyGame.Debugging
{
    /// <summary>Shared parsing for player and board-space tokens.</summary>
    public static class DebugTargets
    {
        public const string SelfKeyword = "me";
        public const string AllKeyword = "all";

        /// <summary>Resolves 'me', 'all' or a slot number into the slots to affect.</summary>
        public static bool TryParsePlayers(
            string token,
            int playerCount,
            int selfPawnSlot,
            List<int> targets,
            out string error)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            targets.Clear();

            if (playerCount <= 0)
            {
                error = "No players are in the session yet.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                error = "Expected a player: 'me', 'all' or a slot number.";
                return false;
            }

            string trimmed = token.Trim();

            if (string.Equals(trimmed, SelfKeyword, StringComparison.OrdinalIgnoreCase))
            {
                if (selfPawnSlot < 0 || selfPawnSlot >= playerCount)
                {
                    error = "Your player is not seated yet.";
                    return false;
                }

                targets.Add(selfPawnSlot);
                error = null;
                return true;
            }

            if (string.Equals(trimmed, AllKeyword, StringComparison.OrdinalIgnoreCase))
            {
                for (int slot = 0; slot < playerCount; slot++)
                {
                    targets.Add(slot);
                }

                error = null;
                return true;
            }

            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pawnSlot) ||
                pawnSlot < 0 ||
                pawnSlot >= playerCount)
            {
                error = $"Unknown player '{token}'. Use 'me', 'all' or a slot between 0 and {playerCount - 1}.";
                return false;
            }

            targets.Add(pawnSlot);
            error = null;
            return true;
        }

        public static bool TryParseIndex(string token, int maxExclusive, out int index, out string error)
        {
            index = -1;

            if (maxExclusive <= 0)
            {
                error = "The board has no spaces yet.";
                return false;
            }

            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ||
                parsed < 0 ||
                parsed >= maxExclusive)
            {
                error = $"Expected a board space between 0 and {maxExclusive - 1}, got '{token}'.";
                return false;
            }

            index = parsed;
            error = null;
            return true;
        }
    }
}
