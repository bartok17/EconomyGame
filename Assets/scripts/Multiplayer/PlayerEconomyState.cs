using System;
using Unity.Collections;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public struct PlayerEconomyState : INetworkSerializable, IEquatable<PlayerEconomyState>
    {
        public int PawnSlot;
        public FixedString64Bytes PlayerId;
        public FixedString64Bytes DisplayName;
        public int Balance;

        public PlayerEconomyState(int pawnSlot, string playerId, string displayName, int balance)
        {
            PawnSlot = pawnSlot;
            PlayerId = new FixedString64Bytes(playerId ?? string.Empty);
            DisplayName = new FixedString64Bytes(displayName ?? string.Empty);
            Balance = balance;
        }

        public PlayerEconomyState WithBalance(int balance)
        {
            return new PlayerEconomyState(PawnSlot, PlayerId.ToString(), DisplayName.ToString(), balance);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PawnSlot);
            serializer.SerializeValue(ref PlayerId);
            serializer.SerializeValue(ref DisplayName);
            serializer.SerializeValue(ref Balance);
        }

        public bool Equals(PlayerEconomyState other)
        {
            return PawnSlot == other.PawnSlot &&
                   PlayerId.Equals(other.PlayerId) &&
                   DisplayName.Equals(other.DisplayName) &&
                   Balance == other.Balance;
        }
    }
}