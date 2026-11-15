using System;
using MonopolyGame.Cards;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Owns the authoritative event-card deck on the server. Event cards resolve
    /// immediately, so there are no hands: a draw triggers the effect and the
    /// card goes straight to the discard pile.
    /// </summary>
    public sealed class EventCardSessionService
    {
        private readonly NetworkVariable<CardPileState> pileStateNet;

        private EventCardDeck deck;
        private bool initialized;

        public EventCardSessionService(NetworkVariable<CardPileState> pileStateNet)
        {
            this.pileStateNet = pileStateNet ?? throw new ArgumentNullException(nameof(pileStateNet));
        }

        public bool IsInitialized => initialized;

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            deck = EventCardCatalog.CreateDefaultDeck();
            initialized = true;
            SyncPileState();
        }

        /// <summary>
        /// Draws the next event card, triggers it against the supplied context and
        /// discards it. Public so future systems (other board spaces, rewards, a
        /// dev console) can trigger events through the same path.
        /// </summary>
        public EventCardDrawResult TriggerCard(EventCardTriggerContext context)
        {
            if (!initialized || context == null)
            {
                return new EventCardDrawResult(EventCardOutcome.DeckEmpty);
            }

            EventCardInstance card = deck.Draw();
            bool reshuffled = deck.LastDrawReshuffled;

            if (card == null)
            {
                SyncPileState();
                return new EventCardDrawResult(EventCardOutcome.DeckEmpty, null, reshuffled);
            }

            try
            {
                card.Definition.Trigger(context);
            }
            finally
            {
                deck.Discard(card);
                SyncPileState();
            }

            return new EventCardDrawResult(EventCardOutcome.Triggered, card, reshuffled, context.Messages);
        }

        private void SyncPileState()
        {
            EventCardInstance topDiscard = deck.TopDiscard;

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
