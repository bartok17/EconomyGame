namespace MonopolyGame.Cards.Definitions
{
    public sealed class CharityEventCard : EventCardDefinition
    {
        public const string Id = "charity";
        public const int Amount = 150;

        public CharityEventCard(int quantity)
            : base(
                Id,
                "Charity Drive",
                "The poorest player collects 150 from the bank.",
                "POOREST\n+150",
                quantity)
        {
        }

        public override void Trigger(EventCardTriggerContext context)
        {
            if (!context.TryGetPoorest(out EventCardPlayerState target))
            {
                return;
            }

            context.Actions.AddFunds(target.PlayerId, Amount);
            context.Report($"{target.Label} (poorest) collects {Amount} from the bank.");
        }
    }
}
