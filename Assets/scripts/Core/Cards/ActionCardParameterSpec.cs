namespace MonopolyGame.Cards
{
    public enum ActionCardParameterKind
    {
        PlayerTarget,
        Amount
    }

    /// <summary>
    /// Declares one choice a player must make to activate a card. The same spec
    /// drives the activation UI (control type and bounds) and the server-side
    /// validation, so the two can never disagree.
    /// </summary>
    public sealed class ActionCardParameterSpec
    {
        private ActionCardParameterSpec(
            ActionCardParameterKind kind,
            string label,
            int min,
            int max,
            int step,
            int defaultValue,
            string suffix,
            bool allowSelf)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Min = min;
            Max = max;
            Step = step > 0 ? step : 1;
            DefaultValue = defaultValue;
            Suffix = suffix ?? string.Empty;
            AllowSelf = allowSelf;
        }

        public ActionCardParameterKind Kind { get; }
        public string Label { get; }
        public int Min { get; }
        public int Max { get; }
        public int Step { get; }
        public int DefaultValue { get; }
        public string Suffix { get; }

        /// <summary>Player targets only: may the activating player choose themselves?</summary>
        public bool AllowSelf { get; }

        public static ActionCardParameterSpec PlayerTarget(string label, bool allowSelf = true)
        {
            return new ActionCardParameterSpec(
                ActionCardParameterKind.PlayerTarget,
                label,
                ActionCardParameters.NoTarget,
                ActionCardParameters.NoTarget,
                1,
                ActionCardParameters.NoTarget,
                string.Empty,
                allowSelf);
        }

        public static ActionCardParameterSpec Amount(
            string label,
            int min,
            int max,
            int step,
            int defaultValue,
            string suffix = "")
        {
            return new ActionCardParameterSpec(
                ActionCardParameterKind.Amount,
                label,
                min,
                max,
                step,
                defaultValue,
                suffix,
                true);
        }

        public bool IsValidAmount(int amount)
        {
            return amount >= Min &&
                   amount <= Max &&
                   (amount - Min) % Step == 0;
        }

        /// <summary>Snaps a value to the nearest valid step and keeps it in range.</summary>
        public int ClampAmount(int amount)
        {
            int offset = amount - Min;
            int steps = (offset + Step / 2) / Step;
            int snapped = Min + steps * Step;
            int largestValid = Min + ((Max - Min) / Step) * Step;

            if (snapped > largestValid)
            {
                return largestValid;
            }

            return snapped < Min ? Min : snapped;
        }
    }
}
