using System.Collections.Generic;
using System.Linq;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class TurnStateMachine
    {
        private readonly List<TurnParticipant> participants = new List<TurnParticipant>();

        public TurnStateMachine()
        {
            State = new TurnState(0, TurnPhase.WaitingForSetup, 0, string.Empty, 0, false);
        }

        public TurnState State { get; private set; }

        public void SetParticipants(IEnumerable<TurnParticipant> turnParticipants)
        {
            participants.Clear();

            if (turnParticipants != null)
            {
                participants.AddRange(turnParticipants.Where(p => p != null));
            }
        }

        public void SetState(TurnState state)
        {
            if (state != null)
            {
                State = state;
            }
        }

        public void StartGame(int startingTurnIndex = 0)
        {
            State = new TurnState(
                startingTurnIndex,
                TurnPhase.AwaitingRoll,
                0,
                GetParticipantName(startingTurnIndex),
                GetParticipantClientId(startingTurnIndex),
                true);
        }

        public void BeginRoll(int diceRoll)
        {
            State = new TurnState(State.TurnIndex, TurnPhase.MovingPawn, diceRoll, State.ActivePlayerName, State.ActiveClientId, true);
        }

        public void BeginResolve()
        {
            State = new TurnState(State.TurnIndex, TurnPhase.ResolvingSpace, State.DiceRoll, State.ActivePlayerName, State.ActiveClientId, true);
        }

        public void BeginWaitingForEndTurn()
        {
            State = new TurnState(State.TurnIndex, TurnPhase.WaitingForEndTurn, State.DiceRoll, State.ActivePlayerName, State.ActiveClientId, true);
        }

        public void AdvanceTurn()
        {
            if (participants.Count == 0)
            {
                StartGame(State.TurnIndex);
                return;
            }

            int nextIndex = (State.TurnIndex + 1) % participants.Count;
            State = new TurnState(
                nextIndex,
                TurnPhase.AwaitingRoll,
                0,
                GetParticipantName(nextIndex),
                GetParticipantClientId(nextIndex),
                true);
        }

        public bool IsAuthorizedTurnRequest(ulong senderClientId)
        {
            if (participants.Count == 0)
            {
                return true;
            }

            return senderClientId == GetParticipantClientId(State.TurnIndex);
        }

        public string GetParticipantName(int turnIndex)
        {
            if (participants.Count == 0)
            {
                return $"Player {turnIndex + 1}";
            }

            int index = Normalize(turnIndex);
            TurnParticipant p = participants[index];
            return p != null && !string.IsNullOrWhiteSpace(p.DisplayName) ? p.DisplayName : $"Player {index + 1}";
        }

        public ulong GetParticipantClientId(int turnIndex)
        {
            if (participants.Count == 0)
            {
                return 0;
            }

            TurnParticipant p = participants[Normalize(turnIndex)];
            return p != null ? p.ClientId : 0;
        }

        private int Normalize(int turnIndex)
        {
            int n = turnIndex % participants.Count;
            return n < 0 ? n + participants.Count : n;
        }
    }
}
