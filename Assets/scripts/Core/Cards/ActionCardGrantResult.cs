namespace MonopolyGame.Cards
{
    public enum ActionCardGrantOutcome
    {
        Added,
        HandFullDiscarded,
        DeckEmpty,
        InvalidPlayer
    }

    public readonly struct ActionCardGrantResult
    {
        public ActionCardGrantResult(
            ActionCardGrantOutcome outcome,
            ActionCardInstance card = null,
            bool reshuffled = false)
        {
            Outcome = outcome;
            Card = card;
            Reshuffled = reshuffled;
        }

        public ActionCardGrantOutcome Outcome { get; }
        public ActionCardInstance Card { get; }
        public bool Reshuffled { get; }
    }
}
