using System;

namespace MonopolyGame.Cards
{
    /// <summary>One physical card in a deck or hand.</summary>
    public sealed class ActionCardInstance
    {
        public ActionCardInstance(int instanceId, ActionCardDefinition definition)
        {
            InstanceId = instanceId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public int InstanceId { get; }
        public ActionCardDefinition Definition { get; }
    }
}
