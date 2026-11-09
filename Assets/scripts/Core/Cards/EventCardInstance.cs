namespace MonopolyGame.Cards
{
    /// <summary>One physical event card in a deck.</summary>
    public sealed class EventCardInstance : CardInstance
    {
        public EventCardInstance(int instanceId, EventCardDefinition definition)
            : base(instanceId, definition)
        {
        }

        public new EventCardDefinition Definition => (EventCardDefinition)base.Definition;
    }
}
