using System;
using System.Collections.Generic;
using MonopolyGame.Cards;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Owns the authoritative action-card deck and hands on the server. Clients
    /// only receive the public pile state and their own hand through the session
    /// controller.
    /// </summary>
    public sealed class ActionCardSessionService
    {
        public const int MaxPlayers = 4;

        private readonly NetworkVariable<CardPileState> pileStateNet;
        private readonly Action<int, IReadOnlyList<ActionCardHandEntry>> handChanged;
        private readonly ActionCardHand[] hands = new ActionCardHand[MaxPlayers];

        private ActionCardDeck deck;
        private bool initialized;

        public ActionCardSessionService(
            NetworkVariable<CardPileState> pileStateNet,
            Action<int, IReadOnlyList<ActionCardHandEntry>> handChanged)
        {
            this.pileStateNet = pileStateNet ?? throw new ArgumentNullException(nameof(pileStateNet));
            this.handChanged = handChanged;
        }

        public bool IsInitialized => initialized;

        public void Initialize(int playerCount)
        {
            if (initialized)
            {
                return;
            }

            int handCount = Math.Min(MaxPlayers, Math.Max(0, playerCount));

            for (int i = 0; i < hands.Length; i++)
            {
                hands[i] = i < handCount ? new ActionCardHand() : null;
            }

            deck = ActionCardCatalog.CreateDefaultDeck();
            initialized = true;
            SyncPileState();
        }

        /// <summary>
        /// Draws a card for the player. Public so future systems (passing Start,
        /// events, rewards) can grant cards through the same path.
        /// </summary>
        public ActionCardGrantResult GrantCard(int pawnSlot)
        {
            if (!initialized || pawnSlot < 0 || pawnSlot >= hands.Length || hands[pawnSlot] == null)
            {
                return new ActionCardGrantResult(ActionCardGrantOutcome.InvalidPlayer);
            }

            ActionCardInstance card = deck.Draw();
            bool reshuffled = deck.LastDrawReshuffled;

            if (card == null)
            {
                SyncPileState();
                return new ActionCardGrantResult(ActionCardGrantOutcome.DeckEmpty, null, reshuffled);
            }

            ActionCardHand hand = hands[pawnSlot];

            if (!hand.TryAdd(card))
            {
                deck.Discard(card);
                SyncPileState();
                return new ActionCardGrantResult(ActionCardGrantOutcome.HandFullDiscarded, card, reshuffled);
            }

            SyncPileState();
            handChanged?.Invoke(pawnSlot, BuildHandEntries(pawnSlot));
            return new ActionCardGrantResult(ActionCardGrantOutcome.Added, card, reshuffled);
        }

        public IReadOnlyList<ActionCardHandEntry> GetHandEntries(int pawnSlot)
        {
            if (!initialized || pawnSlot < 0 || pawnSlot >= hands.Length || hands[pawnSlot] == null)
            {
                return Array.Empty<ActionCardHandEntry>();
            }

            return BuildHandEntries(pawnSlot);
        }

        private ActionCardHandEntry[] BuildHandEntries(int pawnSlot)
        {
            ActionCardHand hand = hands[pawnSlot];
            var entries = new ActionCardHandEntry[hand.Count];

            for (int i = 0; i < hand.Count; i++)
            {
                ActionCardInstance card = hand.Cards[i];
                ActionCardDefinition definition = card.Definition;
                entries[i] = new ActionCardHandEntry(
                    pawnSlot,
                    card.InstanceId,
                    definition.CardId,
                    definition.DisplayName,
                    definition.ShortLabel,
                    definition.Description);
            }

            return entries;
        }

        private void SyncPileState()
        {
            ActionCardInstance topDiscard = deck.TopDiscard;

            pileStateNet.Value = topDiscard == null
                ? new CardPileState(deck.DrawPileCount, deck.DiscardPileCount)
                : new CardPileState(
                    deck.DrawPileCount,
                    deck.DiscardPileCount,
                    topDiscard.Definition.CardId,
                    topDiscard.Definition.DisplayName,
                    topDiscard.Definition.ShortLabel);
        }
    }
}
