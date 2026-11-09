namespace MonopolyGame.Cards
{
    /// <summary>One physical action card in a deck or hand.</summary>
    public sealed class ActionCardInstance : CardInstance
    {
        public ActionCardInstance(int instanceId, ActionCardDefinition definition)
            : base(instanceId, definition)
        {
        }

        public new ActionCardDefinition Definition => (ActionCardDefinition)base.Definition;
    }
}
