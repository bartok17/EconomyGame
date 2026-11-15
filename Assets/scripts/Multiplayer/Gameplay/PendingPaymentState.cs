using System;
using Unity.Collections;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// A payment a player owes to another player. It stays pending until the
    /// payer confirms it, so cards can react before the money moves.
    /// </summary>
    public struct PendingPaymentState : INetworkSerializable, IEquatable<PendingPaymentState>
    {
        public int PayerPawnSlot;
        public int RecipientPawnSlot;
        public int Amount;
        public FixedString128Bytes Reason;

        public PendingPaymentState(int payerPawnSlot, int recipientPawnSlot, int amount, string reason)
        {
            PayerPawnSlot = payerPawnSlot;
            RecipientPawnSlot = recipientPawnSlot;
            Amount = amount;
            Reason = new FixedString128Bytes(reason ?? string.Empty);
        }

        public bool HasPayment => PayerPawnSlot >= 0 && Amount > 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PayerPawnSlot);
            serializer.SerializeValue(ref RecipientPawnSlot);
            serializer.SerializeValue(ref Amount);
            serializer.SerializeValue(ref Reason);
        }

        public bool Equals(PendingPaymentState other)
        {
            return PayerPawnSlot == other.PayerPawnSlot &&
                   RecipientPawnSlot == other.RecipientPawnSlot &&
                   Amount == other.Amount &&
                   Reason.Equals(other.Reason);
        }
    }
}
