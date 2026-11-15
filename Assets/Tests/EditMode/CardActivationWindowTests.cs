using MonopolyGame.Cards;
using MonopolyGame.Cards.Definitions;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class CardActivationWindowTests
    {
        [Test]
        public void BeforeRoll_IsOpenOnlyBeforeTheRoll()
        {
            Assert.IsTrue(CardActivationWindows.IsOpen(CardActivationWindow.BeforeRoll, true, false));
            Assert.IsFalse(CardActivationWindows.IsOpen(CardActivationWindow.BeforeRoll, false, true));
            Assert.IsFalse(CardActivationWindows.IsOpen(CardActivationWindow.BeforeRoll, false, false));
        }

        [Test]
        public void AfterRoll_IsOpenOnlyAfterTheRoll()
        {
            Assert.IsFalse(CardActivationWindows.IsOpen(CardActivationWindow.AfterRoll, true, false));
            Assert.IsTrue(CardActivationWindows.IsOpen(CardActivationWindow.AfterRoll, false, true));
        }

        [Test]
        public void AnyTime_IsOpenInBothWindows()
        {
            Assert.IsTrue(CardActivationWindows.IsOpen(CardActivationWindow.AnyTime, true, false));
            Assert.IsTrue(CardActivationWindows.IsOpen(CardActivationWindow.AnyTime, false, true));
            Assert.IsFalse(CardActivationWindows.IsOpen(CardActivationWindow.AnyTime, false, false));
        }

        [Test]
        public void PlaceholderCards_CanBePlayedAnyTimeDuringTheTurn()
        {
            Assert.AreEqual(CardActivationWindow.AnyTime, new SprinterActionCard(1).ActivationWindow);
            Assert.AreEqual(CardActivationWindow.AnyTime, new WindfallActionCard(1).ActivationWindow);
        }

        [Test]
        public void Definitions_DefaultToAnyTime()
        {
            Assert.AreEqual(CardActivationWindow.AnyTime, new DefaultWindowTestCard().ActivationWindow);
        }

        private sealed class DefaultWindowTestCard : ActionCardDefinition
        {
            public DefaultWindowTestCard()
                : base("default-window", "Default", string.Empty, "DEF", 1)
            {
            }

            public override void Play(ActionCardPlayContext context)
            {
            }
        }
    }
}
