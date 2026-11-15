using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    public enum ActionCardPlayOutcome
    {
        Played,
        CardNotInHand,
        InvalidPlayer,
        InvalidParameters,
        NotActivatable,
        NotAllowedNow
    }

    public readonly struct ActionCardPlayResult
    {
        private static readonly string[] NoMessages = new string[0];

        public ActionCardPlayResult(
            ActionCardPlayOutcome outcome,
            ActionCardInstance card = null,
            string error = null,
            IReadOnlyList<string> messages = null,
            bool cancelled = false,
            string cancelledByCardId = null)
        {
            Outcome = outcome;
            Card = card;
            Error = error ?? string.Empty;
            Messages = messages ?? NoMessages;
            Cancelled = cancelled;
            CancelledByCardId = cancelledByCardId ?? string.Empty;
        }

        public ActionCardPlayOutcome Outcome { get; }
        public ActionCardInstance Card { get; }

        /// <summary>Why the play was rejected; empty when it succeeded.</summary>
        public string Error { get; }

        /// <summary>Human-readable lines produced by the card and any reaction.</summary>
        public IReadOnlyList<string> Messages { get; }

        /// <summary>True when a passive card cancelled the play.</summary>
        public bool Cancelled { get; }

        public string CancelledByCardId { get; }

        public bool Succeeded => Outcome == ActionCardPlayOutcome.Played;
    }
}
