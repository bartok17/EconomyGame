using System;
using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    public enum EventCardOutcome
    {
        Triggered,
        DeckEmpty
    }

    public readonly struct EventCardDrawResult
    {
        public EventCardDrawResult(
            EventCardOutcome outcome,
            EventCardInstance card = null,
            bool reshuffled = false,
            IReadOnlyList<string> messages = null)
        {
            Outcome = outcome;
            Card = card;
            Reshuffled = reshuffled;
            Messages = messages ?? Array.Empty<string>();
        }

        public EventCardOutcome Outcome { get; }
        public EventCardInstance Card { get; }
        public bool Reshuffled { get; }
        public IReadOnlyList<string> Messages { get; }
    }
}
