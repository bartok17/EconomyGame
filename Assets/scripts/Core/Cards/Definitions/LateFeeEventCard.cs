namespace MonopolyGame.Cards.Definitions
{
    public sealed class LateFeeEventCard : EventCardDefinition
    {
        public const string Id = "late_fee";
        public const int Amount = 100;

        public LateFeeEventCard(int quantity)
            : base(
                Id,
                "Late Fee",
                "The next player after the drawer pays 100 to the bank.",
                "NEXT\n-100",
                quantity)
        {
        }

        public override void Trigger(EventCardTriggerContext context)
        {
            if (!context.TryGetNextAfter(context.DrawingPawnSlot, out EventCardPlayerState target))
            {
                context.Report("No other player to charge.");
                return;
            }

            context.Actions.DeductFunds(target.PlayerId, Amount);
            context.Report($"{target.Label} pays {Amount} to the bank.");
        }
    }
}
