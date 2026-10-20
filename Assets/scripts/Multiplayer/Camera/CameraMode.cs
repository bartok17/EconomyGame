namespace MonopolyGame.Multiplayer.Camera
{
    /// <summary>
    /// Available camera behaviour modes for the board camera.
    /// Extend this enum when adding new views (e.g. CardInspection, Auction).
    /// </summary>
    public enum CameraMode
    {
        /// <summary>Player freely orbits and zooms around the board.</summary>
        FreeOrbit,

        /// <summary>Camera locked to a specific player's table side.</summary>
        PlayerView,

        /// <summary>Special framing for a trade between two players.</summary>
        Trade
    }
}
