using System;
using System.Collections.Generic;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// State handed to an action card when it resolves: who played it, the
    /// choices the player made, the current players and the operations it may
    /// perform.
    /// </summary>
    public sealed class ActionCardPlayContext
    {
        private static readonly CardPlayerState[] NoPlayers = new CardPlayerState[0];
        private readonly List<string> messages = new List<string>();

        public ActionCardPlayContext(
            int actingPawnSlot,
            string actingPlayerId,
            ICardGameActions actions,
            ActionCardParameters parameters = default,
            IReadOnlyList<CardPlayerState> players = null)
        {
            ActingPawnSlot = actingPawnSlot;
            ActingPlayerId = actingPlayerId ?? string.Empty;
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            Parameters = parameters;
            Players = players ?? NoPlayers;
        }

        public int ActingPawnSlot { get; }
        public string ActingPlayerId { get; }
        public ActionCardParameters Parameters { get; }
        public IReadOnlyList<CardPlayerState> Players { get; }
        public ICardGameActions Actions { get; }

        /// <summary>Human-readable lines produced while the card resolves.</summary>
        public IReadOnlyList<string> Messages => messages;

        /// <summary>
        /// The player chosen through the card's parameters. Cards without a
        /// player parameter resolve against the acting player.
        /// </summary>
        public bool TryGetTarget(out CardPlayerState player)
        {
            int slot = Parameters.HasTarget ? Parameters.TargetPawnSlot : ActingPawnSlot;
            return ActionCardDefinition.TryFindPlayer(Players, slot, out player);
        }

        public void Report(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }
    }
}
