using MonopolyGame.Board;
using UnityEngine;

namespace MonopolyGame.Stash
{
    /// <summary>Computed placement for the four outer player stash zones.</summary>
    public readonly struct PlayerStashMetrics
    {
        public PlayerStashMetrics(float outerHalfSize, float zoneWidth, float zoneDepth, float zoneGap)
        {
            OuterHalfSize = outerHalfSize;
            ZoneWidth = zoneWidth;
            ZoneDepth = zoneDepth;
            ZoneGap = zoneGap;
        }

        public float OuterHalfSize { get; }
        public float ZoneWidth { get; }
        public float ZoneDepth { get; }
        public float ZoneGap { get; }

        public float ZoneCenterDistance => OuterHalfSize + ZoneGap + ZoneDepth * 0.5f;

        public Vector3 GetZoneCenter(int pawnSlot, float y)
        {
            float azimuth = PlayerStashLayout.GetCameraAzimuth(pawnSlot) * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Sin(azimuth) * ZoneCenterDistance,
                y,
                Mathf.Cos(azimuth) * ZoneCenterDistance);
        }

        public float GetPileSpacing(float maxSpacing, float margin, int pileCount)
        {
            if (pileCount <= 0)
            {
                return 0f;
            }

            float available = Mathf.Max(0f, ZoneDepth - margin * 2f);
            return Mathf.Min(maxSpacing, available / pileCount);
        }

        public float GetPileColumnX(float noteWidth, float rightMargin)
        {
            return ZoneWidth * 0.5f - rightMargin - noteWidth * 0.5f;
        }

        /// <summary>
        /// Column order is top-to-bottom from the player's view: 5 at the far
        /// (board) edge, 500 nearest the player.
        /// </summary>
        public float GetPileLocalZ(int index, int pileCount, float spacing)
        {
            if (pileCount <= 0)
            {
                return 0f;
            }

            float start = (pileCount - 1) * 0.5f * spacing;
            return start - index * spacing;
        }
    }

    /// <summary>
    /// Pure layout math shared by the runtime stash view and the editor setup.
    /// Slot order matches the camera player-view presets: 0/+Z, 1/+X, 2/-Z, 3/-X.
    /// </summary>
    public static class PlayerStashLayout
    {
        public const int PlayerCount = 4;

        private const float DefaultOuterHalfSize = 7.85f;

        public static float GetCameraAzimuth(int pawnSlot)
        {
            return Mathf.Repeat(pawnSlot * 90f, 360f);
        }

        /// <summary>
        /// Zone root yaw makes local +Z point inward (toward the board), so local
        /// -Z is the player-facing front edge and local +X is the player's right.
        /// </summary>
        public static float GetZoneYaw(int pawnSlot)
        {
            return Mathf.Repeat(180f + pawnSlot * 90f, 360f);
        }

        public static PlayerStashMetrics FromTemplate(
            BoardTileTemplate template,
            float zoneGap,
            float zoneDepth,
            float widthMargin)
        {
            float outerHalfSize = DefaultOuterHalfSize;

            if (template != null)
            {
                float cornerOffset =
                    template.EdgeSpacesPerSide * template.EdgeLength * 0.5f +
                    template.CornerSize * 0.5f;
                outerHalfSize = cornerOffset + template.CornerSize * 0.5f;
            }

            float outerSize = outerHalfSize * 2f;
            float zoneWidth = Mathf.Max(6f, outerSize - widthMargin * 2f);

            return new PlayerStashMetrics(outerHalfSize, zoneWidth, zoneDepth, zoneGap);
        }
    }
}
