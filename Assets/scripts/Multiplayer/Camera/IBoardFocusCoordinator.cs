using MonopolyGame.Board;

namespace MonopolyGame.Multiplayer.Camera
{
    /// <summary>
    /// Single entry point for driving the board camera: tile lookup, focus
    /// priority and mode switching live in one place, so input clicks, game
    /// events and future features never resolve tiles or touch the camera
    /// directly. Implemented by <see cref="BoardFocusCoordinator"/>.
    /// </summary>
    public interface IBoardFocusCoordinator
    {
        /// <summary>
        /// When enabled, the camera automatically focuses the tile a pawn lands on.
        /// Runtime-toggleable (planned: keyboard shortcut / UI button).
        /// </summary>
        bool FocusOnAction { get; set; }

        void FocusSpace(int boardIndex);

        void FocusSpace(BoardSpaceView space);

        /// <summary>
        /// Drop the current focus override. Restores the active base mode
        /// (player lock / trade view) or returns to the overview pivot.
        /// </summary>
        void ClearFocus();

        void ShowOverview();

        /// <summary>
        /// Lock the camera to the given player's table side (0–3), framing that
        /// player's stash zone when the stash system is available.
        /// </summary>
        void ShowPlayerView(int playerIndex);

        void ShowTradeView(int playerA, int playerB);

        void EndTradeView();
    }
}
