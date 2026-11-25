using System;
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
        public int ActiveParticipantCount => participants.Count(p => !p.IsEliminated);
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
            int resolvedIndex = startingTurnIndex;
            
            if (participants.Count > 0)
            {
                TurnParticipant requested = GetParticipant(startingTurnIndex);
                if (requested == null || requested.IsEliminated)
                {
                    TurnParticipant firstActive = GetFirstActiveParticipant();
                    if (firstActive != null)
                    {
                        resolvedIndex = firstActive.TurnIndex;
                    }
                }
            }

            State = new TurnState(
                resolvedIndex,
                TurnPhase.AwaitingRoll,
                0,
                GetParticipantName(resolvedIndex),
                GetParticipantClientId(resolvedIndex),
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
            
            int currentListIndex = participants.FindIndex(p => p.TurnIndex == State.TurnIndex);
            if (currentListIndex < 0)
            {
                currentListIndex = Normalize(State.TurnIndex);
            }

            TurnParticipant nextParticipant = null;

            for (int step = 1; step <= participants.Count; step++)
            {
                TurnParticipant candidate = participants[(currentListIndex + step) % participants.Count];
                if (!candidate.IsEliminated)
                {
                    nextParticipant = candidate;
                    break;
                }
            }

            if (nextParticipant == null)
            {
                return;
            }
            
            State = new TurnState(
                nextParticipant.TurnIndex,
                TurnPhase.AwaitingRoll,
                0,
                GetParticipantName(nextParticipant.TurnIndex),
                nextParticipant.ClientId,
                true);
        }
        
        public TurnParticipant GetParticipant(int slotIndex)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].TurnIndex == slotIndex)
                {
                    return participants[i];
                }
            }

            return null;
        }

        public TurnParticipant GetFirstActiveParticipant()
        {
            for (int i = 0; i < participants.Count; i++)
            {
                if (!participants[i].IsEliminated)
                {
                    return participants[i];
                }
            }

            return null;
        }

        public void SetParticipantConnected(int slotIndex, bool isConnected)
        {
            TurnParticipant participant = GetParticipant(slotIndex);
            if (participant != null && !participant.IsEliminated)
            {
                participant.IsConnected = isConnected;
            }
        }

        public bool IsParticipantConnected(int slotIndex)
        {
            if (participants.Count == 0)
            {
                return true;
            }

            TurnParticipant participant = GetParticipant(slotIndex);
            return participant != null && !participant.IsEliminated && participant.IsConnected;
        }

        public bool IsParticipantEliminated(int slotIndex)
        {
            TurnParticipant participant = GetParticipant(slotIndex);
            return participant != null && participant.IsEliminated;
        }

        public void MarkParticipantEliminated(int slotIndex)
        {
            TurnParticipant participant = GetParticipant(slotIndex);
            if (participant == null)
            {
                return;
            }

            participant.IsEliminated = true;
            participant.IsConnected = false;
        }

        public bool UpdateParticipantClientId(int slotIndex, ulong newClientId)
        {
            TurnParticipant participant = GetParticipant(slotIndex);
            if (participant == null || participant.IsEliminated)
            {
                return false;
            }

            participant.ClientId = newClientId;
            participant.IsConnected = true;

            if (State != null && State.TurnIndex == slotIndex)
            {
                State = new TurnState(
                    State.TurnIndex,
                    State.Phase,
                    State.DiceRoll,
                    State.ActivePlayerName,
                    newClientId,
                    State.IsInitialized);
            }

            return true;
        }

        public TurnParticipant FindConnectedParticipantByClientId(ulong clientId)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                TurnParticipant p = participants[i];
                if (!p.IsEliminated && p.IsConnected && p.ClientId == clientId)
                {
                    return p;
                }
            }

            return null;
        }

        public TurnParticipant FindReconnectCandidate(string authPlayerId, string displayName)
        {
            bool anyAuthIds = participants.Any(p => !string.IsNullOrWhiteSpace(p.AuthPlayerId));

            if (anyAuthIds)
            {
                if (string.IsNullOrWhiteSpace(authPlayerId))
                {
                    return null;
                }

                for (int i = 0; i < participants.Count; i++)
                {
                    TurnParticipant p = participants[i];
                    if (!p.IsEliminated && string.Equals(p.AuthPlayerId, authPlayerId, StringComparison.Ordinal))
                    {
                        return p;
                    }
                }

                return null;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }

            for (int i = 0; i < participants.Count; i++)
            {
                TurnParticipant p = participants[i];
                if (!p.IsEliminated &&
                    !p.IsConnected &&
                    string.Equals(p.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            return null;
        }

        public bool IsAuthorizedTurnRequest(ulong senderClientId)
        {
            if (participants.Count == 0)
            {
                return true;
            }
            
            TurnParticipant active = GetParticipant(State.TurnIndex);
            return active != null && !active.IsEliminated && active.IsConnected && active.ClientId == senderClientId;
        }

        public string GetParticipantName(int turnIndex)
        {
            if (participants.Count == 0)
            {
                return $"Player {turnIndex + 1}";
            }

            TurnParticipant p = GetParticipant(turnIndex) ?? participants[Normalize(turnIndex)];
            return p != null && !string.IsNullOrWhiteSpace(p.DisplayName) ? p.DisplayName : $"Player {turnIndex + 1}";
        }

        public ulong GetParticipantClientId(int turnIndex)
        {
            if (participants.Count == 0)
            {
                return 0;
            }

            TurnParticipant p = GetParticipant(turnIndex) ?? participants[Normalize(turnIndex)];
            return p != null ? p.ClientId : 0;
        }

        private int Normalize(int turnIndex)
        {
            if (participants.Count == 0)
            {
                return 0;
            }
            
            int n = turnIndex % participants.Count;
            return n < 0 ? n + participants.Count : n;
        }
    }
}
