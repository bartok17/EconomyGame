using System.Collections.Generic;

namespace MonopolyGame.Cards.Definitions
{
    public sealed class SprinterActionCard : ActionCardDefinition
    {
        public const string Id = "sprinter";
        public const int MinSpaces = 1;
        public const int MaxSpaces = 6;
        public const int DefaultSpaces = 3;

        private static readonly ActionCardParameterSpec[] CardParameters =
        {
            ActionCardParameterSpec.PlayerTarget("Target player"),
            ActionCardParameterSpec.Amount("Spaces", MinSpaces, MaxSpaces, 1, DefaultSpaces, " spaces")
        };

        public SprinterActionCard(int quantity)
            : base(
                Id,
                "Sprinter",
                "Choose a player and move them 1-6 spaces forward.",
                "MOVE\n1-6",
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

            int spaces = context.Parameters.Amount;
            context.Actions.MovePlayer(target.PlayerId, spaces);
            context.Report($"{target.Label} moves {spaces} {(spaces == 1 ? "space" : "spaces")} forward.");
        }

        public override CardActivationWindow ActivationWindow => CardActivationWindow.AnyTime;

        public override string DescribePreview(
            ActionCardParameters parameters,
            IReadOnlyList<CardPlayerState> players,
            ICardPreviewBoard board)
        {
            if (board == null ||
                !ActionCardDefinition.TryFindPlayer(players, parameters.TargetPawnSlot, out CardPlayerState target))
            {
                return string.Empty;
            }

            int from = board.GetPlayerSpaceIndex(target.PawnSlot);

            if (from < 0)
            {
                return $"{target.Label} moves {parameters.Amount} spaces.";
            }

            int to = board.NormalizeIndex(from + parameters.Amount);
            string fromName = board.GetSpaceName(from);
            string toName = board.GetSpaceName(to);

            return string.IsNullOrEmpty(fromName) || string.IsNullOrEmpty(toName)
                ? $"{target.Label} · tile {from} → {to}"
                : $"{target.Label} · {fromName} → {toName}";
        }
    }
}
