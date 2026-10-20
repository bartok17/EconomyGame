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

        /// <summary>
        /// Resolve the tile by index and glide the camera onto it.
        /// </summary>
        void FocusSpace(int boardIndex);

        /// <summary>
        /// Glide the camera onto an already-resolved tile.
        /// </summary>
        void FocusSpace(BoardSpaceView space);

        /// <summary>
        /// Drop the current focus override. Restores the active base mode
        /// (player lock / trade view) or returns to the overview pivot.
        /// </summary>
        void ClearFocus();

        /// <summary>
        /// Switch to free orbit centred on the board.
        /// </summary>
        void ShowOverview();

        /// <summary>
        /// Lock the camera to the given player's table side (0–3).
        /// </summary>
        void ShowPlayerView(int playerIndex);

        /// <summary>
        /// Frame a trade between two players.
        /// </summary>
        void ShowTradeView(int playerA, int playerB);

        /// <summary>
        /// Leave the trade framing and return to free orbit.
        /// </summary>
        void EndTradeView();
    }
}
