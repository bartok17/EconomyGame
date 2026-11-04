namespace MonopolyGame.Cards.Definitions
{
    public sealed class SprinterActionCard : ActionCardDefinition
    {
        public const string Id = "sprinter";
        public const int MoveSpaces = 3;

        public SprinterActionCard(int quantity)
            : base(
                Id,
                "Sprinter",
                "Move your pawn 3 spaces forward.",
                "MOVE\n+3",
                quantity)
        {
        }

        public override void Play(ActionCardPlayContext context)
        {
            context.Actions.MovePlayer(context.ActingPlayerId, MoveSpaces);
        }
    }
}
