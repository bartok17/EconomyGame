using System;

namespace MonopolyGame.Multiplayer
{
    public sealed class MultiplayerStatusStateMachine
    {
        public MultiplayerStatus Status { get; private set; } = MultiplayerStatus.Idle;
        public MultiplayerError LastError { get; private set; }

        public event Action<MultiplayerStatus> StatusChanged;

        public void TransitionTo(MultiplayerStatus nextStatus)
        {
            Status = nextStatus;
            StatusChanged?.Invoke(nextStatus);
        }

        public void BeginWorkflowStep(MultiplayerStatus nextStatus)
        {
            ClearLastError();
            TransitionTo(nextStatus);
        }

        public void RaiseError(string code, string message, Exception exception)
        {
            LastError = new MultiplayerError(code, message, exception);
            RecoverStatusAfterError();
            StatusChanged?.Invoke(Status);
        }

        public void ClearLastError()
        {
            LastError = null;
        }

        private void RecoverStatusAfterError()
        {
            switch (Status)
            {
                case MultiplayerStatus.Initializing:
                    TransitionTo(MultiplayerStatus.Idle);
                    break;
                case MultiplayerStatus.SigningIn:
                    TransitionTo(MultiplayerStatus.SignedOut);
                    break;
                case MultiplayerStatus.LobbyQuerying:
                    TransitionTo(MultiplayerStatus.SignedIn);
                    break;
                case MultiplayerStatus.LobbyJoining:
                    TransitionTo(MultiplayerStatus.SignedIn);
                    break;
                case MultiplayerStatus.RelayAllocating:
                    TransitionTo(MultiplayerStatus.LobbyJoined);
                    break;
                case MultiplayerStatus.RelayJoining:
                    TransitionTo(MultiplayerStatus.LobbyJoined);
                    break;
                case MultiplayerStatus.NetworkStarting:
                    TransitionTo(MultiplayerStatus.LobbyJoined);
                    break;
            }
        }
    }
}
