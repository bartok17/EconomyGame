using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// One action-card type. Concrete cards override <see cref="Play"/> so their
    /// mechanics can differ freely while deck and hand logic stays generic.
    /// Cards that need a player or a number declare it through
    /// <see cref="Parameters"/>; the activation UI and the server both validate
    /// against those specs.
    /// Adding a card means adding a subclass and registering it in
    /// <see cref="ActionCardCatalog"/>.
    /// </summary>
    public abstract class ActionCardDefinition : CardDefinition
    {
        private static readonly ActionCardParameterSpec[] NoParameters = new ActionCardParameterSpec[0];

        protected ActionCardDefinition(
            string cardId,
            string displayName,
            string description,
            string shortLabel,
            int quantity)
            : base(cardId, displayName, description, shortLabel, quantity)
        {
        }

        public virtual CardTiming Timing => CardTiming.Active;

        public virtual CardActivationWindow ActivationWindow => CardActivationWindow.AnyTime;

        public virtual IReadOnlyList<ActionCardParameterSpec> Parameters => NoParameters;

        /// <summary>
        /// Declarative validation of the player's choices. Override to add
        /// card-specific rules and call the base first.
        /// </summary>
        public virtual bool TryValidate(
            int actingPawnSlot,
            ActionCardParameters parameters,
            IReadOnlyList<CardPlayerState> players,
            out string error)
        {
            error = null;
            IReadOnlyList<ActionCardParameterSpec> specs = Parameters;

            for (int i = 0; i < specs.Count; i++)
            {
                ActionCardParameterSpec spec = specs[i];

                if (spec.Kind == ActionCardParameterKind.Amount && !spec.IsValidAmount(parameters.Amount))
                {
                    error = $"{spec.Label} must be between {spec.Min} and {spec.Max}.";
                    return false;
                }

                if (spec.Kind != ActionCardParameterKind.PlayerTarget)
                {
                    continue;
                }

                if (!parameters.HasTarget || !TryFindPlayer(players, parameters.TargetPawnSlot, out CardPlayerState target))
                {
                    error = "Choose a player.";
                    return false;
                }

                if (!spec.AllowSelf && target.PawnSlot == actingPawnSlot)
                {
                    error = "This card cannot target its owner.";
                    return false;
                }
            }

            return true;
        }

        public abstract void Play(ActionCardPlayContext context);

        /// <summary>
        /// One-line description of the outcome for the activation UI, e.g. the
        /// destination tile of a movement card. Empty when nothing useful can
        /// be previewed.
        /// </summary>
        public virtual string DescribePreview(
            ActionCardParameters parameters,
            IReadOnlyList<CardPlayerState> players,
            ICardPreviewBoard board)
        {
            return string.Empty;
        }

        public static bool TryFindPlayer(
            IReadOnlyList<CardPlayerState> players,
            int pawnSlot,
            out CardPlayerState player)
        {
            player = default;

            if (players == null)
            {
                return false;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].PawnSlot == pawnSlot)
                {
                    player = players[i];
                    return true;
                }
            }

            return false;
        }
    }
}
