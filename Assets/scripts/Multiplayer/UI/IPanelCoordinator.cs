namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Public contract for coordinating hub-menu panel visibility.
    /// Presenters depend on this instead of the concrete <see cref="HubMenuCoordinator"/>.
    /// </summary>
    public interface IPanelCoordinator
    {
        void ShowPanel(string panelName);
        void BackToHub();
    }
}
