namespace MonopolyGame.Multiplayer.Camera
{
    /// <summary>
    /// Available camera behaviour modes for the board camera.
    /// Extend this enum when adding new views (e.g. CardInspection, Auction).
    /// </summary>
    public enum CameraMode
    {
        FreeOrbit,

        PlayerView,

        Trade
    }
}
