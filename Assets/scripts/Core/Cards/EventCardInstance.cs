namespace MonopolyGame.Cards
{
    public sealed class EventCardInstance : CardInstance
    {
        public EventCardInstance(int instanceId, EventCardDefinition definition)
            : base(instanceId, definition)
        {
        }

        public new EventCardDefinition Definition => (EventCardDefinition)base.Definition;
    }
}
