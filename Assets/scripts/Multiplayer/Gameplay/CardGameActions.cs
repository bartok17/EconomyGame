using System;
using MonopolyGame.Cards;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Server-side implementation of the card effect surface, shared by action
    /// and event cards. Movement is delegated to the session controller so the
    /// pawn animates for every client and resolves the landing normally.
    /// Payment redirection is reserved for a later card.
    /// </summary>
    public sealed class CardGameActions : ICardGameActions
    {
        private readonly IPlayerEconomyService economy;
        private readonly Action onEconomyChanged;
        private readonly Action<string, int> movePlayer;
        private readonly Action<int, int, int, string> queuePayment;

        public CardGameActions(
            IPlayerEconomyService economy,
            Action onEconomyChanged,
            Action<string, int> movePlayer,
            Action<int, int, int, string> queuePayment = null)
        {
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            this.onEconomyChanged = onEconomyChanged;
            this.movePlayer = movePlayer;
            this.queuePayment = queuePayment;
        }

        public void MovePlayer(string playerId, int spaces)
        {
            if (spaces == 0)
            {
                return;
            }

            if (movePlayer == null)
            {
                Debug.LogWarning("[Cards] No pawn mover is bound; movement effects are ignored.");
                return;
            }

            movePlayer(playerId, spaces);
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

            if (queuePayment != null)
            {
                queuePayment(fromSlot, toSlot, transfer, "Card payment");
                return;
            }

            economy.DeductBalance(fromSlot, transfer);
            economy.AddBalance(toSlot, transfer);
            onEconomyChanged?.Invoke();
        }

        public void RedirectNextPayment(string payerPlayerId, string recipientPlayerId)
        {
            Debug.LogWarning("[Cards] Payment redirection is not supported yet.");
        }

        public bool TryGetPawnSlot(string playerId, out int pawnSlot)
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
