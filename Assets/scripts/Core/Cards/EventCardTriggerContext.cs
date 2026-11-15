using System;
using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// Everything an event card needs to resolve: the drawing player, a snapshot
    /// of every player and the operations it may perform. Targets are always
    /// derived from that state, never supplied by a player.
    /// </summary>
    public sealed class EventCardTriggerContext
    {
        private readonly List<string> messages = new List<string>();

        public EventCardTriggerContext(
            int drawingPawnSlot,
            IReadOnlyList<CardPlayerState> players,
            ICardGameActions actions)
        {
            DrawingPawnSlot = drawingPawnSlot;
            Players = players ?? Array.Empty<CardPlayerState>();
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public int DrawingPawnSlot { get; }
        public IReadOnlyList<CardPlayerState> Players { get; }
        public ICardGameActions Actions { get; }

        /// <summary>Human-readable lines produced by the card while resolving.</summary>
        public IReadOnlyList<string> Messages => messages;

        public bool TryGetPlayer(int pawnSlot, out CardPlayerState player)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i].PawnSlot == pawnSlot)
                {
                    player = Players[i];
                    return true;
                }
            }

            player = default;
            return false;
        }

        public bool TryGetDrawingPlayer(out CardPlayerState player)
        {
            return TryGetPlayer(DrawingPawnSlot, out player);
        }

        /// <summary>Lowest balance; the lowest seat wins ties.</summary>
        public bool TryGetPoorest(out CardPlayerState player)
        {
            return TryGetExtreme(out player, findHighest: false);
        }

        /// <summary>Highest balance; the lowest seat wins ties.</summary>
        public bool TryGetRichest(out CardPlayerState player)
        {
            return TryGetExtreme(out player, findHighest: true);
        }

        /// <summary>Next player in seat order, wrapping around; false when alone.</summary>
        public bool TryGetNextAfter(int pawnSlot, out CardPlayerState player)
        {
            player = default;

            if (Players.Count < 2 || !TryGetPlayer(pawnSlot, out CardPlayerState current))
            {
                return false;
            }

            bool hasNext = false;
            CardPlayerState next = default;
            int nextSeat = int.MaxValue;
            bool hasFirst = false;
            CardPlayerState first = default;
            int firstSeat = int.MaxValue;

            for (int i = 0; i < Players.Count; i++)
            {
                CardPlayerState candidate = Players[i];

                if (candidate.PawnSlot == pawnSlot)
                {
                    continue;
                }

                if (candidate.SeatIndex > current.SeatIndex && candidate.SeatIndex < nextSeat)
                {
                    next = candidate;
                    nextSeat = candidate.SeatIndex;
                    hasNext = true;
                }

                if (candidate.SeatIndex < firstSeat)
                {
                    first = candidate;
                    firstSeat = candidate.SeatIndex;
                    hasFirst = true;
                }
            }

            if (!hasNext && !hasFirst)
            {
                return false;
            }

            player = hasNext ? next : first;
            return true;
        }

        public void Report(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }

        private bool TryGetExtreme(out CardPlayerState player, bool findHighest)
        {
            player = default;
            bool found = false;

            for (int i = 0; i < Players.Count; i++)
            {
                CardPlayerState candidate = Players[i];

                if (!found)
                {
                    player = candidate;
                    found = true;
                    continue;
                }

                bool better = findHighest
                    ? candidate.Balance > player.Balance
                    : candidate.Balance < player.Balance;
                bool tie = candidate.Balance == player.Balance && candidate.SeatIndex < player.SeatIndex;

                if (better || tie)
                {
                    player = candidate;
                }
            }

            return found;
        }
    }
}
