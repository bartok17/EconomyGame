using MonopolyGame.Cards;
using MonopolyGame.Cards.Definitions;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class ActionCardParameterTests
    {
        private static readonly CardPlayerState[] Players =
        {
            new CardPlayerState(0, 0, "player-0", "Alice", 1500),
            new CardPlayerState(1, 1, "player-1", "Bob", 900)
        };

        [Test]
        public void Sprinter_AcceptsATargetAndSpacesInRange()
        {
            var card = new SprinterActionCard(1);

            Assert.IsTrue(card.TryValidate(0, new ActionCardParameters(1, 4), Players, out string error));
            Assert.IsNull(error);
        }

        [Test]
        public void Sprinter_RejectsSpacesOutsideTheRange()
        {
            var card = new SprinterActionCard(1);

            Assert.IsFalse(card.TryValidate(0, new ActionCardParameters(1, 0), Players, out _));
            Assert.IsFalse(card.TryValidate(0, new ActionCardParameters(1, 7), Players, out _));
        }

        [Test]
        public void Sprinter_RejectsAMissingOrUnknownTarget()
        {
            var card = new SprinterActionCard(1);

            Assert.IsFalse(card.TryValidate(0, new ActionCardParameters(ActionCardParameters.NoTarget, 3), Players, out _));
            Assert.IsFalse(card.TryValidate(0, new ActionCardParameters(9, 3), Players, out _));
        }

        [Test]
        public void Windfall_RejectsAmountsOffTheStepGrid()
        {
            var card = new WindfallActionCard(1);

            Assert.IsTrue(card.TryValidate(0, new ActionCardParameters(1, 150), Players, out _));
            Assert.IsFalse(card.TryValidate(0, new ActionCardParameters(1, 125), Players, out _));
            Assert.IsFalse(card.TryValidate(0, new ActionCardParameters(1, 550), Players, out _));
        }

        [Test]
        public void Spec_ClampAmount_SnapsToTheGridAndRange()
        {
            ActionCardParameterSpec spec = ActionCardParameterSpec.Amount("Amount", 50, 500, 50, 200);

            Assert.AreEqual(50, spec.ClampAmount(0));
            Assert.AreEqual(50, spec.ClampAmount(74));
            Assert.AreEqual(100, spec.ClampAmount(76));
            Assert.AreEqual(500, spec.ClampAmount(9999));
        }

        [Test]
        public void Spec_AllowSelfFalse_RejectsTheOwnerAsTarget()
        {
            var card = new SelflessTestCard();

            Assert.IsFalse(card.TryValidate(0, new ActionCardParameters(0, 1), Players, out _));
            Assert.IsTrue(card.TryValidate(0, new ActionCardParameters(1, 1), Players, out _));
        }

        private sealed class SelflessTestCard : ActionCardDefinition
        {
            private static readonly ActionCardParameterSpec[] CardParameters =
            {
                ActionCardParameterSpec.PlayerTarget("Target", allowSelf: false),
                ActionCardParameterSpec.Amount("Amount", 1, 3, 1, 1)
            };

            public SelflessTestCard()
                : base("selfless", "Selfless", string.Empty, "SELF", 1)
            {
            }

            public override System.Collections.Generic.IReadOnlyList<ActionCardParameterSpec> Parameters => CardParameters;

            public override void Play(ActionCardPlayContext context)
            {
            }
        }
    }
}
