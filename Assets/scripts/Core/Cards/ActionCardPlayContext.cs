using System;

namespace MonopolyGame.Cards
{
    /// <summary>
    /// Operations a card may use when it is played. Extend this interface when a
    /// new mechanic needs a game operation that does not exist yet; cards can
    /// also compose several of these operations.
    /// </summary>
    public interface IActionCardGameActions
    {
        void MovePlayer(string playerId, int spaces);
        void AddFunds(string playerId, int amount);
        void TransferFunds(string fromPlayerId, string toPlayerId, int amount);
        void RedirectNextPayment(string payerPlayerId, string recipientPlayerId);
    }

    public sealed class ActionCardPlayContext
    {
        public ActionCardPlayContext(string actingPlayerId, IActionCardGameActions actions)
        {
            ActingPlayerId = actingPlayerId ?? string.Empty;
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public string ActingPlayerId { get; }
        public IActionCardGameActions Actions { get; }
    }
}
