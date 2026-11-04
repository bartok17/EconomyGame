using System;
using Unity.Collections;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>Public deck information replicated to every client.</summary>
    public struct ActionCardPileState : INetworkSerializable, IEquatable<ActionCardPileState>
    {
        public int DrawPileCount;
        public int DiscardPileCount;
        public FixedString64Bytes TopDiscardCardId;
        public FixedString64Bytes TopDiscardDisplayName;
        public FixedString64Bytes TopDiscardShortLabel;

        public ActionCardPileState(
            int drawPileCount,
            int discardPileCount,
            string topDiscardCardId = "",
            string topDiscardDisplayName = "",
            string topDiscardShortLabel = "")
        {
            DrawPileCount = drawPileCount;
            DiscardPileCount = discardPileCount;
            TopDiscardCardId = new FixedString64Bytes(topDiscardCardId ?? string.Empty);
            TopDiscardDisplayName = new FixedString64Bytes(topDiscardDisplayName ?? string.Empty);
            TopDiscardShortLabel = new FixedString64Bytes(topDiscardShortLabel ?? string.Empty);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref DrawPileCount);
            serializer.SerializeValue(ref DiscardPileCount);
            serializer.SerializeValue(ref TopDiscardCardId);
            serializer.SerializeValue(ref TopDiscardDisplayName);
            serializer.SerializeValue(ref TopDiscardShortLabel);
        }

        public bool Equals(ActionCardPileState other)
        {
            return DrawPileCount == other.DrawPileCount &&
                   DiscardPileCount == other.DiscardPileCount &&
                   TopDiscardCardId.Equals(other.TopDiscardCardId) &&
                   TopDiscardDisplayName.Equals(other.TopDiscardDisplayName) &&
                   TopDiscardShortLabel.Equals(other.TopDiscardShortLabel);
        }
    }
}
