using System.Collections.Generic;
using MonopolyGame.Multiplayer.Gameplay;
using NUnit.Framework;

namespace MonopolyGame.Core.Tests
{
    public sealed class TurnStateMachineReconnectTests
    {
        [Test]
        public void EliminateMiddlePlayer_PreservesTurnIndicesAndSkipsEliminatedInAdvanceTurn()
        {
            var machine = new TurnStateMachine();
            machine.SetParticipants(new[]
            {
                new TurnParticipant(0, "Host", 0, "auth-0"),
                new TurnParticipant(1, "Alice", 1, "auth-1"),
                new TurnParticipant(2, "Bob", 2, "auth-2")
            });

            machine.StartGame(0);
            machine.MarkParticipantEliminated(1);

            Assert.AreEqual(3, machine.ParticipantCount);
            Assert.AreEqual(2, machine.ActiveParticipantCount);
            Assert.AreEqual(2, machine.GetParticipant(2).TurnIndex);

            machine.AdvanceTurn();

            Assert.AreEqual(2, machine.State.TurnIndex);
            Assert.AreEqual("Bob", machine.State.ActivePlayerName);

            machine.AdvanceTurn();

            Assert.AreEqual(0, machine.State.TurnIndex);
            Assert.AreEqual("Host", machine.State.ActivePlayerName);
        }

        [Test]
        public void Reconnect_MatchesExactAuthPlayerIdAndUpdatesActiveClientId()
        {
            var machine = new TurnStateMachine();
            machine.SetParticipants(new[]
            {
                new TurnParticipant(0, "Player", 0, "auth-host"),
                new TurnParticipant(1, "Player", 1, "auth-guest")
            });

            machine.StartGame(1);
            machine.SetParticipantConnected(1, false);

            Assert.IsFalse(machine.IsAuthorizedTurnRequest(1));
            Assert.IsFalse(machine.IsAuthorizedTurnRequest(0));

            TurnParticipant candidate = machine.FindReconnectCandidate("auth-guest", "Player");
            Assert.IsNotNull(candidate);
            Assert.AreEqual(1, candidate.TurnIndex);

            bool updated = machine.UpdateParticipantClientId(candidate.TurnIndex, 7);
            Assert.IsTrue(updated);
            Assert.IsTrue(machine.IsParticipantConnected(1));
            Assert.AreEqual(7UL, machine.State.ActiveClientId);
            Assert.IsTrue(machine.IsAuthorizedTurnRequest(7));
            Assert.IsNull(machine.FindConnectedParticipantByClientId(1));
        }

        [Test]
        public void Reconnect_RejectsUnknownAuthPlayerIdEvenWithMatchingName()
        {
            var machine = new TurnStateMachine();
            machine.SetParticipants(new[]
            {
                new TurnParticipant(0, "Host", 0, "auth-host"),
                new TurnParticipant(1, "Anna", 1, "auth-anna")
            });

            machine.StartGame(0);
            machine.SetParticipantConnected(1, false);

            TurnParticipant intruder = machine.FindReconnectCandidate("auth-stranger", "Anna");
            Assert.IsNull(intruder);
        }

        [Test]
        public void EliminatedPlayer_CannotReconnect()
        {
            var machine = new TurnStateMachine();
            machine.SetParticipants(new[]
            {
                new TurnParticipant(0, "Host", 0, "auth-host"),
                new TurnParticipant(1, "Anna", 1, "auth-anna")
            });

            machine.StartGame(0);
            machine.MarkParticipantEliminated(1);

            Assert.IsNull(machine.FindReconnectCandidate("auth-anna", "Anna"));
            Assert.IsFalse(machine.UpdateParticipantClientId(1, 5));
        }
    }
}