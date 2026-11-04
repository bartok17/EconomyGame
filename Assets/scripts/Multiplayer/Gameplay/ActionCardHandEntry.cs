using System;
using Unity.Collections;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// One card in a player's private hand. Only the owning client receives these
    /// entries; other clients only see public pile state.
    /// </summary>
    public struct ActionCardHandEntry : INetworkSerializable, IEquatable<ActionCardHandEntry>
    {
        public int PawnSlot;
        public int InstanceId;
        public FixedString64Bytes CardId;
        public FixedString64Bytes DisplayName;
        public FixedString64Bytes ShortLabel;
        public FixedString128Bytes Description;

        public ActionCardHandEntry(
            int pawnSlot,
            int instanceId,
            string cardId,
            string displayName,
            string shortLabel,
            string description)
        {
            PawnSlot = pawnSlot;
            InstanceId = instanceId;
            CardId = new FixedString64Bytes(cardId ?? string.Empty);
            DisplayName = new FixedString64Bytes(displayName ?? string.Empty);
            ShortLabel = new FixedString64Bytes(shortLabel ?? string.Empty);
            Description = new FixedString128Bytes(description ?? string.Empty);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PawnSlot);
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref CardId);
            serializer.SerializeValue(ref DisplayName);
            serializer.SerializeValue(ref ShortLabel);
            serializer.SerializeValue(ref Description);
        }

        public bool Equals(ActionCardHandEntry other)
        {
            return PawnSlot == other.PawnSlot &&
                   InstanceId == other.InstanceId &&
                   CardId.Equals(other.CardId) &&
                   DisplayName.Equals(other.DisplayName) &&
                   ShortLabel.Equals(other.ShortLabel) &&
                   Description.Equals(other.Description);
        }
    }
}
