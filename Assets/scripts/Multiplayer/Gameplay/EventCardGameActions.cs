using System;
using MonopolyGame.Cards;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Server-side implementation of the card effect surface for event cards.
    /// Movement and payment redirection are reserved for later event cards; they
    /// log a warning instead of silently doing nothing.
    /// </summary>
    public sealed class EventCardGameActions : ICardGameActions
    {
        private readonly IPlayerEconomyService economy;
        private readonly Action onEconomyChanged;

        public EventCardGameActions(IPlayerEconomyService economy, Action onEconomyChanged)
        {
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            this.onEconomyChanged = onEconomyChanged;
        }

        public void MovePlayer(string playerId, int spaces)
        {
            Debug.LogWarning($"[Events] Moving {playerId} by {spaces} spaces is not supported by event cards yet.");
        }

        public void AddFunds(string playerId, int amount)
        {
            if (amount <= 0 || !TryGetPawnSlot(playerId, out int pawnSlot))
            {
                return;
            }

            economy.AddBalance(pawnSlot, amount);
            onEconomyChanged?.Invoke();
        }

        public void DeductFunds(string playerId, int amount)
        {
            if (amount <= 0 || !TryGetPawnSlot(playerId, out int pawnSlot))
            {
                return;
            }

            int deduction = Mathf.Min(amount, economy.GetBalance(pawnSlot));

            if (deduction <= 0)
            {
                return;
            }

            economy.DeductBalance(pawnSlot, deduction);
            onEconomyChanged?.Invoke();
        }

        public void TransferFunds(string fromPlayerId, string toPlayerId, int amount)
        {
            if (amount <= 0 ||
                !TryGetPawnSlot(fromPlayerId, out int fromSlot) ||
                !TryGetPawnSlot(toPlayerId, out int toSlot))
            {
                return;
            }

            int transfer = Mathf.Min(amount, economy.GetBalance(fromSlot));

            if (transfer <= 0)
            {
                return;
            }

            economy.DeductBalance(fromSlot, transfer);
            economy.AddBalance(toSlot, transfer);
            onEconomyChanged?.Invoke();
        }

        public void RedirectNextPayment(string payerPlayerId, string recipientPlayerId)
        {
            Debug.LogWarning("[Events] Payment redirection is not supported by event cards yet.");
        }

        private bool TryGetPawnSlot(string playerId, out int pawnSlot)
        {
            pawnSlot = -1;

            if (string.IsNullOrEmpty(playerId))
            {
                return false;
            }

            var states = economy.GetAllEconomyStates();

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].PlayerId.ToString() == playerId)
                {
                    pawnSlot = states[i].PawnSlot;
                    return true;
                }
            }

            return false;
        }
    }
}
