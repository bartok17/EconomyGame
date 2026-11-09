using System.Collections.Generic;
using MonopolyGame.Cards;

namespace MonopolyGame.Core.Tests
{
    /// <summary>Records card effect calls so tests can assert what a card did.</summary>
    internal sealed class TestCardActions : ICardGameActions
    {
        public List<EffectCall> FundsAdded { get; } = new List<EffectCall>();
        public List<EffectCall> FundsDeducted { get; } = new List<EffectCall>();
        public List<EffectCall> Transfers { get; } = new List<EffectCall>();
        public List<string> MovedPlayers { get; } = new List<string>();

        public void MovePlayer(string playerId, int spaces)
        {
            MovedPlayers.Add(playerId);
        }

        public void AddFunds(string playerId, int amount)
        {
            FundsAdded.Add(new EffectCall(playerId, amount));
        }

        public void DeductFunds(string playerId, int amount)
        {
            FundsDeducted.Add(new EffectCall(playerId, amount));
        }

        public void TransferFunds(string fromPlayerId, string toPlayerId, int amount)
        {
            Transfers.Add(new EffectCall(fromPlayerId, amount));
        }

        public void RedirectNextPayment(string payerPlayerId, string recipientPlayerId)
        {
        }

        public readonly struct EffectCall
        {
            public EffectCall(string playerId, int amount)
            {
                PlayerId = playerId;
                Amount = amount;
            }

            public string PlayerId { get; }
            public int Amount { get; }
        }
    }
}
