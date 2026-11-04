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
        public int ParticipantCount => participants.Count;
        public IReadOnlyList<TurnParticipant> Participants => participants;

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
        
        public void SetParticipantConnected(int turnIndex, bool isConnected)
        {
            if (participants.Count == 0) return;
            int idx = Normalize(turnIndex);
            if (idx >= 0 && idx < participants.Count)
            {
                participants[idx].IsConnected = isConnected;
            }
        }

        public bool IsParticipantConnected(int turnIndex)
        {
            if (participants.Count == 0) return true;
            int idx = Normalize(turnIndex);
            return idx >= 0 && idx < participants.Count && participants[idx].IsConnected;
        }

        public void UpdateParticipantClientId(int turnIndex, ulong newClientId)
        {
            if (participants.Count == 0) return;
            int idx = Normalize(turnIndex);
            if (idx >= 0 && idx < participants.Count)
            {
                participants[idx].ClientId = newClientId;
                participants[idx].IsConnected = true;
                
                if (State != null && State.TurnIndex == idx)
                {
                    State = new TurnState(State.TurnIndex, State.Phase, State.DiceRoll, State.ActivePlayerName, newClientId, State.IsInitialized);
                }
            }
        }

        public TurnParticipant FindParticipantByName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return null;
            return participants.FirstOrDefault(p => string.Equals(p.DisplayName, displayName, System.StringComparison.OrdinalIgnoreCase));
        }

        public void RemoveParticipant(int turnIndex)
        {
            int idx = Normalize(turnIndex);
            if (idx >= 0 && idx < participants.Count)
            {
                participants.RemoveAt(idx);
                for (int i = 0; i < participants.Count; i++)
                {
                    participants[i].TurnIndex = i;
                }

                if (participants.Count > 0 && State.TurnIndex >= participants.Count)
                {
                    State = new TurnState(0, State.Phase, State.DiceRoll, GetParticipantName(0), GetParticipantClientId(0), State.IsInitialized);
                }
            }
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
            if (participants.Count == 0) return 0;
            int n = turnIndex % participants.Count;
            return n < 0 ? n + participants.Count : n;
        }
    }
}
