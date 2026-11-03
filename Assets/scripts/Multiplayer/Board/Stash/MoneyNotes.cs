using System;

namespace MonopolyGame.Stash
{
    /// <summary>
    /// Banknote denominations used by the 3D money visualization and the greedy
    /// breakdown of a balance into note counts. The HUD/inventory remains the
    /// authoritative numeric source; this is presentation-only.
    /// </summary>
    public static class MoneyNotes
    {
        /// <summary>Ascending order used for left-to-right pile placement.</summary>
        public static readonly int[] Denominations = { 5, 10, 20, 50, 100, 500 };

        public static int Count => Denominations.Length;

        /// <summary>
        /// Fills <paramref name="counts"/> with an even note distribution: each
        /// pass takes at most one note of every affordable denomination, so large
        /// balances still show a mix of small notes instead of a single high pile.
        /// Values below 5 are ignored.
        /// </summary>
        public static void Decompose(int balance, int[] counts)
        {
            if (counts == null)
            {
                throw new ArgumentNullException(nameof(counts));
            }

            if (counts.Length < Denominations.Length)
            {
                throw new ArgumentException(
                    $"Expected at least {Denominations.Length} count slots.",
                    nameof(counts));
            }

            Array.Clear(counts, 0, counts.Length);

            int remaining = Math.Max(0, balance);

            while (remaining >= Denominations[0])
            {
                bool tookAny = false;

                for (int i = Denominations.Length - 1; i >= 0; i--)
                {
                    int value = Denominations[i];

                    if (remaining < value)
                    {
                        continue;
                    }

                    counts[i]++;
                    remaining -= value;
                    tookAny = true;
                }

                if (!tookAny)
                {
                    break;
                }
            }
        }
    }
}
