namespace MonopolyGame.Cards
{
    public sealed class ActionCardInstance : CardInstance
    {
        public ActionCardInstance(int instanceId, ActionCardDefinition definition)
            : base(instanceId, definition)
        {
        }

        public new ActionCardDefinition Definition => (ActionCardDefinition)base.Definition;
    }
}
