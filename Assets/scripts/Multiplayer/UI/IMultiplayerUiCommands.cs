namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Public contract for UI command dispatching.
    /// Presenters depend on this instead of the concrete <see cref="MultiplayerUiCommands"/>.
    /// </summary>
    public interface IMultiplayerUiCommands
    {
        void SetUsername(string value);
        void SetPassword(string value);
        void SetDisplayName(string value);
        void SetLobbyName(string value);
        void SetLobbyCode(string value);
        void SetMaxPlayers(int value);
        void SetIsPrivate(bool value);

        void Initialize();
        void SignIn();
        void SignUp();
        void SetName();
        void SignOut();
        void RefreshLobbies();
        void Host();
        void Join();
        void Join(string joinCodeOrId);
        void StartGame();
        void LeaveLobby();
    }
}
