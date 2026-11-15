namespace MonopolyGame.Cards.Definitions
{
    /// <summary>
    /// Passive counter: cancels an action card another player plays against its
    /// holder. It fires by itself, so the table never has to wait for a
    /// response and nobody learns it was in hand unless it fires.
    /// </summary>
    public sealed class GuardActionCard : PassiveActionCardDefinition
    {
        public const string Id = "guard";

        public GuardActionCard(int quantity)
            : base(
                Id,
                "Guard",
                "Passive: cancels an action card another player plays against you.",
                "GUARD\nPASSIVE",
                quantity)
        {
        }

        public override bool TryReact(ActionCardReactionContext context)
        {
            if (!context.TargetsReactingPlayer ||
                context.ActingPlayer.PawnSlot == context.ReactingPlayer.PawnSlot)
            {
                return false;
            }

            context.Cancel(
                $"{context.ReactingPlayer.Label}'s Guard cancelled {context.IncomingCard.DisplayName}.");
            return true;
        }
    }
}
