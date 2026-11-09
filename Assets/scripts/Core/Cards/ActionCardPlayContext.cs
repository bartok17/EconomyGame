using System;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// State handed to an action card when it is played: who played it and the
    /// operations it may perform.
    /// </summary>
    public sealed class ActionCardPlayContext
    {
        public ActionCardPlayContext(string actingPlayerId, ICardGameActions actions)
        {
            ActingPlayerId = actingPlayerId ?? string.Empty;
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public string ActingPlayerId { get; }
        public ICardGameActions Actions { get; }
    }
}
