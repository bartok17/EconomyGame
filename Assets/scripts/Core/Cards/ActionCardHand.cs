using System;
using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    public sealed class ActionCardHand
    {
        public const int DefaultCapacity = 5;

        private readonly List<ActionCardInstance> cards = new List<ActionCardInstance>();

        public ActionCardHand(int capacity = DefaultCapacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Hand capacity must be at least one.");
            }

            Capacity = capacity;
        }

        public int Capacity { get; }
        public int Count => cards.Count;
        public bool IsFull => cards.Count >= Capacity;
        public IReadOnlyList<ActionCardInstance> Cards => cards;

        public bool TryAdd(ActionCardInstance card)
        {
            if (card == null || IsFull)
            {
                return false;
            }

            cards.Add(card);
            return true;
        }

        public bool Remove(int instanceId)
        {
            return TryTake(instanceId, out _);
        }

        public bool TryTake(int instanceId, out ActionCardInstance card)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].InstanceId == instanceId)
                {
                    card = cards[i];
                    cards.RemoveAt(i);
                    return true;
                }
            }

            card = null;
            return false;
        }

        public bool Contains(int instanceId)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].InstanceId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            cards.Clear();
        }
    }
}
