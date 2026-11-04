namespace MonopolyGame.Cards.Definitions
{
    public sealed class WindfallActionCard : ActionCardDefinition
    {
        public const string Id = "windfall";
        public const int Amount = 200;

        public WindfallActionCard(int quantity)
            : base(
                Id,
                "Windfall",
                "Collect 200 from the bank.",
                "CASH\n+200",
                quantity)
        {
        }

        public override void Play(ActionCardPlayContext context)
        {
            context.Actions.AddFunds(context.ActingPlayerId, Amount);
        }
    }
}
