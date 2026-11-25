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

        public int HousesBuilt;

        public PropertyOwnershipState(int spaceIndex, int ownerPawnSlot, string ownerPlayerId, string ownerName, int housesBuilt = 0)
        {
            SpaceIndex = spaceIndex;
            OwnerPawnSlot = ownerPawnSlot;
            OwnerPlayerId = new FixedString64Bytes(ownerPlayerId ?? string.Empty);
            OwnerName = new FixedString64Bytes(ownerName ?? string.Empty);
            HousesBuilt = housesBuilt;
        }

        public PropertyOwnershipState WithHouses(int housesBuilt)
        {
            return new PropertyOwnershipState(SpaceIndex, OwnerPawnSlot, OwnerPlayerId.ToString(), OwnerName.ToString(), housesBuilt);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SpaceIndex);
            serializer.SerializeValue(ref OwnerPawnSlot);
            serializer.SerializeValue(ref OwnerPlayerId);
            serializer.SerializeValue(ref OwnerName);
            serializer.SerializeValue(ref HousesBuilt);
        }

        public bool Equals(PropertyOwnershipState other)
        {
            return SpaceIndex == other.SpaceIndex &&
                   OwnerPawnSlot == other.OwnerPawnSlot &&
                   OwnerPlayerId.Equals(other.OwnerPlayerId) &&
                   OwnerName.Equals(other.OwnerName) &&
                   HousesBuilt == other.HousesBuilt;
        }
    }
}