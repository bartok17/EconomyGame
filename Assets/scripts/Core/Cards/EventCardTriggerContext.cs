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
            IReadOnlyList<EventCardPlayerState> players,
            ICardGameActions actions)
        {
            DrawingPawnSlot = drawingPawnSlot;
            Players = players ?? Array.Empty<EventCardPlayerState>();
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public int DrawingPawnSlot { get; }
        public IReadOnlyList<EventCardPlayerState> Players { get; }
        public ICardGameActions Actions { get; }

        /// <summary>Human-readable lines produced by the card while resolving.</summary>
        public IReadOnlyList<string> Messages => messages;

        public bool TryGetPlayer(int pawnSlot, out EventCardPlayerState player)
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

        public bool TryGetDrawingPlayer(out EventCardPlayerState player)
        {
            return TryGetPlayer(DrawingPawnSlot, out player);
        }

        /// <summary>Lowest balance; the lowest seat wins ties.</summary>
        public bool TryGetPoorest(out EventCardPlayerState player)
        {
            return TryGetExtreme(out player, findHighest: false);
        }

        /// <summary>Highest balance; the lowest seat wins ties.</summary>
        public bool TryGetRichest(out EventCardPlayerState player)
        {
            return TryGetExtreme(out player, findHighest: true);
        }

        /// <summary>Next player in seat order, wrapping around; false when alone.</summary>
        public bool TryGetNextAfter(int pawnSlot, out EventCardPlayerState player)
        {
            player = default;

            if (Players.Count < 2 || !TryGetPlayer(pawnSlot, out EventCardPlayerState current))
            {
                return false;
            }

            bool hasNext = false;
            EventCardPlayerState next = default;
            int nextSeat = int.MaxValue;
            bool hasFirst = false;
            EventCardPlayerState first = default;
            int firstSeat = int.MaxValue;

            for (int i = 0; i < Players.Count; i++)
            {
                EventCardPlayerState candidate = Players[i];

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

        private bool TryGetExtreme(out EventCardPlayerState player, bool findHighest)
        {
            player = default;
            bool found = false;

            for (int i = 0; i < Players.Count; i++)
            {
                EventCardPlayerState candidate = Players[i];

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
