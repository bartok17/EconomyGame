namespace MonopolyGame.Cards
{
    /// <summary>
    /// Choice a player makes when activating a card. Cards without parameters
    /// simply ignore both values.
    /// </summary>
    public readonly struct ActionCardParameters
    {
        public const int NoTarget = -1;

        public static readonly ActionCardParameters None = new ActionCardParameters(NoTarget, 0);

        public ActionCardParameters(int targetPawnSlot, int amount)
        {
            TargetPawnSlot = targetPawnSlot;
            Amount = amount;
        }

        /// <summary>Chosen player, or <see cref="NoTarget"/> when the card has no player parameter.</summary>
        public int TargetPawnSlot { get; }

        public int Amount { get; }

        public bool HasTarget => TargetPawnSlot != NoTarget;

        public ActionCardParameters WithTarget(int targetPawnSlot)
        {
            return new ActionCardParameters(targetPawnSlot, Amount);
        }

        public ActionCardParameters WithAmount(int amount)
        {
            return new ActionCardParameters(TargetPawnSlot, amount);
        }
    }
}
