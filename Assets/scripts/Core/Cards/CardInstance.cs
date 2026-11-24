using System;

namespace MonopolyGame.Cards
{
    public abstract class CardInstance
    {
        protected CardInstance(int instanceId, CardDefinition definition)
        {
            InstanceId = instanceId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public int InstanceId { get; }
        public CardDefinition Definition { get; }
    }
}
