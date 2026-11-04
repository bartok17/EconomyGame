using MonopolyGame.Cards;
using MonopolyGame.Cards.Definitions;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class ActionCardEffectTests
    {
        [Test]
        public void Sprinter_MovesActingPlayerForward()
        {
            var actions = new RecordingActions();
            var card = new SprinterActionCard(1);
            var context = new ActionCardPlayContext("player-1", actions);

            card.Play(context);

            Assert.AreEqual("player-1", actions.MovedPlayerId);
            Assert.AreEqual(SprinterActionCard.MoveSpaces, actions.MovedSpaces);
        }

        [Test]
        public void Windfall_AddsFundsToActingPlayer()
        {
            var actions = new RecordingActions();
            var card = new WindfallActionCard(1);
            var context = new ActionCardPlayContext("player-1", actions);

            card.Play(context);

            Assert.AreEqual("player-1", actions.FundedPlayerId);
            Assert.AreEqual(WindfallActionCard.Amount, actions.FundsAdded);
        }

        private sealed class RecordingActions : IActionCardGameActions
        {
            public string MovedPlayerId { get; private set; }
            public int MovedSpaces { get; private set; }
            public string FundedPlayerId { get; private set; }
            public int FundsAdded { get; private set; }

            public void MovePlayer(string playerId, int spaces)
            {
                MovedPlayerId = playerId;
                MovedSpaces = spaces;
            }

            public void AddFunds(string playerId, int amount)
            {
                FundedPlayerId = playerId;
                FundsAdded = amount;
            }

            public void TransferFunds(string fromPlayerId, string toPlayerId, int amount)
            {
            }

            public void RedirectNextPayment(string payerPlayerId, string recipientPlayerId)
            {
            }
        }
    }
}
