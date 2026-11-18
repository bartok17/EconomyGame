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
            if (!IsValidHand(pawnSlot))
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

            return AddToHand(pawnSlot, card, reshuffled);
        }

        /// <summary>Pulls one named card out of the draw pile; an empty id falls back to a normal draw.</summary>
        public ActionCardGrantResult GrantSpecificCard(int pawnSlot, string cardId)
        {
            if (!IsValidHand(pawnSlot))
            {
                return new ActionCardGrantResult(ActionCardGrantOutcome.InvalidPlayer);
            }

            if (string.IsNullOrWhiteSpace(cardId))
            {
                return GrantCard(pawnSlot);
            }

            bool taken = deck.TryTake(cardId, out ActionCardInstance card);
            bool reshuffled = deck.LastDrawReshuffled;

            if (!taken || card == null)
            {
                SyncPileState();
                return new ActionCardGrantResult(ActionCardGrantOutcome.DeckEmpty, null, reshuffled);
            }

            return AddToHand(pawnSlot, card, reshuffled);
        }

        /// <summary>Sends every card in a hand to the discard pile.</summary>
        public bool ClearHand(int pawnSlot)
        {
            if (!IsValidHand(pawnSlot))
            {
                return false;
            }

            ActionCardHand hand = hands[pawnSlot];

            for (int i = 0; i < hand.Cards.Count; i++)
            {
                deck.Discard(hand.Cards[i]);
            }

            hand.Clear();
            SyncPileState();
            handChanged?.Invoke(pawnSlot, BuildHandEntries(pawnSlot));
            return true;
        }

        /// <summary>Rebuilds the deck and empties every seated hand (debug console only).</summary>
        public void ResetDeck()
        {
            deck = ActionCardCatalog.CreateDefaultDeck();
            initialized = true;

            for (int slot = 0; slot < hands.Length; slot++)
            {
                if (hands[slot] == null)
                {
                    continue;
                }

                hands[slot].Clear();
            }

            SyncPileState();

            for (int slot = 0; slot < hands.Length; slot++)
            {
                if (hands[slot] != null)
                {
                    handChanged?.Invoke(slot, BuildHandEntries(slot));
                }
            }
        }

        public IReadOnlyList<ActionCardHandEntry> GetHandEntries(int pawnSlot)
        {
            if (!initialized || pawnSlot < 0 || pawnSlot >= hands.Length || hands[pawnSlot] == null)
            {
                return Array.Empty<ActionCardHandEntry>();
            }

            return BuildHandEntries(pawnSlot);
        }

        /// <summary>
        /// Resolves an activation request on the server: validates the choices,
        /// lets a passive card of the target react, plays the card and discards
        /// it. The card is only consumed once validation succeeds.
        /// </summary>
        public ActionCardPlayResult TryPlayCard(
            int actingPawnSlot,
            int instanceId,
            ActionCardParameters parameters,
            IReadOnlyList<CardPlayerState> players,
            ICardGameActions actions,
            bool beforeRoll = true,
            bool afterRoll = true)
        {
            if (!initialized || actingPawnSlot < 0 || actingPawnSlot >= hands.Length || hands[actingPawnSlot] == null)
            {
                return new ActionCardPlayResult(ActionCardPlayOutcome.InvalidPlayer);
            }

            ActionCardHand hand = hands[actingPawnSlot];

            if (!TryFindCard(hand, instanceId, out ActionCardInstance card))
            {
                return new ActionCardPlayResult(ActionCardPlayOutcome.CardNotInHand);
            }

            ActionCardDefinition definition = card.Definition;

            if (definition.Timing != CardTiming.Active)
            {
                return new ActionCardPlayResult(ActionCardPlayOutcome.NotActivatable, card, "This card activates by itself.");
            }

            if (!CardActivationWindows.IsOpen(definition.ActivationWindow, beforeRoll, afterRoll))
            {
                return new ActionCardPlayResult(ActionCardPlayOutcome.NotAllowedNow, card, "This card cannot be played right now.");
            }

            if (!ActionCardDefinition.TryFindPlayer(players, actingPawnSlot, out CardPlayerState actingPlayer))
            {
                return new ActionCardPlayResult(ActionCardPlayOutcome.InvalidPlayer, card);
            }

            if (!definition.TryValidate(actingPawnSlot, parameters, players, out string error))
            {
                return new ActionCardPlayResult(ActionCardPlayOutcome.InvalidParameters, card, error);
            }

            int targetSlot = parameters.HasTarget ? parameters.TargetPawnSlot : actingPawnSlot;
            bool cancelled = TryRunReaction(actingPawnSlot, targetSlot, definition, parameters, players,
                out string reactionCardId, out string reactionMessage);

            if (!cancelled)
            {
                var context = new ActionCardPlayContext(
                    actingPawnSlot,
                    actingPlayer.PlayerId,
                    actions,
                    parameters,
                    players);
                definition.Play(context);

                hand.TryTake(instanceId, out _);
                deck.Discard(card);
                SyncPileState();
                handChanged?.Invoke(actingPawnSlot, BuildHandEntries(actingPawnSlot));

                return new ActionCardPlayResult(
                    ActionCardPlayOutcome.Played,
                    card,
                    string.Empty,
                    context.Messages);
            }

            hand.TryTake(instanceId, out _);
            deck.Discard(card);
            SyncPileState();
            handChanged?.Invoke(actingPawnSlot, BuildHandEntries(actingPawnSlot));

            var messages = string.IsNullOrWhiteSpace(reactionMessage)
                ? Array.Empty<string>()
                : new[] { reactionMessage };

            return new ActionCardPlayResult(
                ActionCardPlayOutcome.Played,
                card,
                string.Empty,
                messages,
                cancelled: true,
                cancelledByCardId: reactionCardId);
        }

        private bool TryRunReaction(
            int actingPawnSlot,
            int targetSlot,
            ActionCardDefinition incomingCard,
            ActionCardParameters parameters,
            IReadOnlyList<CardPlayerState> players,
            out string reactionCardId,
            out string reactionMessage)
        {
            reactionCardId = string.Empty;
            reactionMessage = string.Empty;

            if (targetSlot == actingPawnSlot ||
                targetSlot < 0 ||
                targetSlot >= hands.Length ||
                hands[targetSlot] == null ||
                !ActionCardDefinition.TryFindPlayer(players, targetSlot, out CardPlayerState targetPlayer) ||
                !ActionCardDefinition.TryFindPlayer(players, actingPawnSlot, out CardPlayerState actingPlayer))
            {
                return false;
            }

            ActionCardHand targetHand = hands[targetSlot];

            for (int i = 0; i < targetHand.Cards.Count; i++)
            {
                if (!(targetHand.Cards[i].Definition is PassiveActionCardDefinition passive))
                {
                    continue;
                }

                var reaction = new ActionCardReactionContext(
                    targetPlayer,
                    actingPlayer,
                    incomingCard,
                    parameters,
                    targetsReactingPlayer: true);

                if (!passive.TryReact(reaction))
                {
                    continue;
                }

                ActionCardInstance consumed = targetHand.Cards[i];
                targetHand.TryTake(consumed.InstanceId, out _);
                deck.Discard(consumed);
                SyncPileState();
                handChanged?.Invoke(targetSlot, BuildHandEntries(targetSlot));

                reactionCardId = consumed.Definition.CardId;
                reactionMessage = reaction.Message;
                return reaction.IsCancelled;
            }

            return false;
        }

        private bool IsValidHand(int pawnSlot)
        {
            return initialized &&
                   pawnSlot >= 0 &&
                   pawnSlot < hands.Length &&
                   hands[pawnSlot] != null &&
                   deck != null;
        }

        private ActionCardGrantResult AddToHand(int pawnSlot, ActionCardInstance card, bool reshuffled)
        {
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

        private static bool TryFindCard(ActionCardHand hand, int instanceId, out ActionCardInstance card)
        {
            card = null;

            for (int i = 0; i < hand.Cards.Count; i++)
            {
                if (hand.Cards[i].InstanceId == instanceId)
                {
                    card = hand.Cards[i];
                    return true;
                }
            }

            return false;
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
