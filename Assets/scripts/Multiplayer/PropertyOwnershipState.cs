using System;
using Unity.Collections;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public struct PropertyOwnershipState : INetworkSerializable, IEquatable<PropertyOwnershipState>
    {
        public int SpaceIndex;
        public int OwnerPawnSlot;
        public FixedString64Bytes OwnerPlayerId;
        public FixedString64Bytes OwnerName;

        public PropertyOwnershipState(int spaceIndex, int ownerPawnSlot, string ownerPlayerId, string ownerName)
        {
            SpaceIndex = spaceIndex;
            OwnerPawnSlot = ownerPawnSlot;
            OwnerPlayerId = new FixedString64Bytes(ownerPlayerId ?? string.Empty);
            OwnerName = new FixedString64Bytes(ownerName ?? string.Empty);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SpaceIndex);
            serializer.SerializeValue(ref OwnerPawnSlot);
            serializer.SerializeValue(ref OwnerPlayerId);
            serializer.SerializeValue(ref OwnerName);
        }

        public bool Equals(PropertyOwnershipState other)
        {
            return SpaceIndex == other.SpaceIndex &&
                   OwnerPawnSlot == other.OwnerPawnSlot &&
                   OwnerPlayerId.Equals(other.OwnerPlayerId) &&
                   OwnerName.Equals(other.OwnerName);
        }
    }
}