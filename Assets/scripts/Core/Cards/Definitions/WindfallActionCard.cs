using System.Collections.Generic;

namespace MonopolyGame.Cards.Definitions
{
    public sealed class WindfallActionCard : ActionCardDefinition
    {
        public const string Id = "windfall";
        public const int MinAmount = 50;
        public const int MaxAmount = 500;
        public const int AmountStep = 50;
        public const int DefaultAmount = 200;

        private static readonly ActionCardParameterSpec[] CardParameters =
        {
            ActionCardParameterSpec.PlayerTarget("Who collects"),
            ActionCardParameterSpec.Amount("Amount", MinAmount, MaxAmount, AmountStep, DefaultAmount)
        };

        public WindfallActionCard(int quantity)
            : base(
                Id,
                "Windfall",
                "Choose a player; they collect 50-500 from the bank.",
                "CASH\n50-500",
                quantity)
        {
        }

        public override IReadOnlyList<ActionCardParameterSpec> Parameters => CardParameters;

        public override void Play(ActionCardPlayContext context)
        {
            if (!context.TryGetTarget(out CardPlayerState target))
            {
                return;
            }

            int amount = context.Parameters.Amount;
            context.Actions.AddFunds(target.PlayerId, amount);
            context.Report($"{target.Label} collects {amount} from the bank.");
        }

        public override CardActivationWindow ActivationWindow => CardActivationWindow.AnyTime;

        public override string DescribePreview(
            ActionCardParameters parameters,
            IReadOnlyList<CardPlayerState> players,
            ICardPreviewBoard board)
        {
            return ActionCardDefinition.TryFindPlayer(players, parameters.TargetPawnSlot, out CardPlayerState target)
                ? $"{target.Label} · +{parameters.Amount}"
                : string.Empty;
        }
    }
}
