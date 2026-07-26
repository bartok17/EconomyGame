namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Public contract for the game HUD presenter.
    /// Scene-management code depends on this instead of the concrete MonoBehaviour.
    /// </summary>
    public interface IGameHudPresenter
    {
        void BindSession(IGameSessionController sessionController);
    }
}
