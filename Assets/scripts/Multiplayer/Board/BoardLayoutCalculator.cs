using System;
using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>Which side of the board a tile belongs to.</summary>
    public enum TileSide
    {
        Bottom,
        Left,
        Top,
        Right,
        Corner
    }

    /// <summary>Computed world placement for one tile.</summary>
    public readonly struct BoardTilePlacement
    {
        public BoardTilePlacement(Vector3 position, Vector3 size, float yRotation, TileSide side, int cornerIndex)
        {
            Position = position;
            Size = size;
            YRotation = yRotation;
            Side = side;
            CornerIndex = cornerIndex;
        }

        public Vector3 Position { get; }
        public Vector3 Size { get; }
        public float YRotation { get; }
        public TileSide Side { get; }
        public int CornerIndex { get; }
        public bool IsCorner => Side == TileSide.Corner;
    }

    /// <summary>
    /// Pure layout math for the 40-space square board. Extracted from the old
    /// editor-only builder so the runtime generator and editor preview share it.
    /// </summary>
    public static class BoardLayoutCalculator
    {
        public static int GetSpaceCount(int edgeSpacesPerSide)
        {
            return edgeSpacesPerSide > 0 ? edgeSpacesPerSide * 4 + 4 : 0;
        }

        public static TileSide GetSide(int index, int edgeSpacesPerSide)
        {
            int spaceCount = GetSpaceCount(edgeSpacesPerSide);

            if (spaceCount <= 0)
            {
                return TileSide.Corner;
            }

            int normalized = Normalize(index, spaceCount);
            int edge = edgeSpacesPerSide;

            if (normalized == 0 || normalized == edge + 1 || normalized == edge * 2 + 2 || normalized == edge * 3 + 3)
            {
                return TileSide.Corner;
            }

            if (normalized >= 1 && normalized <= edge)
            {
                return TileSide.Bottom;
            }

            if (normalized >= edge + 2 && normalized <= edge * 2 + 1)
            {
                return TileSide.Left;
            }

            if (normalized >= edge * 2 + 3 && normalized <= edge * 3 + 2)
            {
                return TileSide.Top;
            }

            return TileSide.Right;
        }

        public static BoardTilePlacement GetPlacement(int index, int edgeSpacesPerSide, BoardTileTemplate template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            int spaceCount = GetSpaceCount(edgeSpacesPerSide);

            if (spaceCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(edgeSpacesPerSide), "Edge spaces per side must be positive.");
            }

            int normalized = Normalize(index, spaceCount);
            int edge = edgeSpacesPerSide;

            float cornerSize = template.CornerSize;
            float edgeLength = template.EdgeLength;
            float laneDepth = template.LaneDepth;
            float tileHeight = template.TileHeight;
            float longSide = edgeLength * template.TileGapFactor;

            float cornerOffset = edge * edgeLength / 2f + cornerSize / 2f;

            TileSide side = GetSide(normalized, edge);
            int cornerIndex = GetCornerIndex(normalized, edge);
            float yRotation = template.GetSideYaw(side, cornerIndex);

            switch (side)
            {
                case TileSide.Corner:
                    return CreateCornerPlacement(cornerIndex, cornerOffset, cornerSize, tileHeight, yRotation);

                case TileSide.Bottom:
                {
                    int step = normalized - 1;
                    float x = cornerOffset - cornerSize / 2f - edgeLength / 2f - step * edgeLength;
                    return new BoardTilePlacement(
                        new Vector3(x, 0f, -cornerOffset),
                        new Vector3(longSide, tileHeight, laneDepth),
                        yRotation,
                        side,
                        -1);
                }

                case TileSide.Left:
                {
                    int step = normalized - (edge + 2);
                    float z = -cornerOffset + cornerSize / 2f + edgeLength / 2f + step * edgeLength;
                    return new BoardTilePlacement(
                        new Vector3(-cornerOffset, 0f, z),
                        new Vector3(longSide, tileHeight, laneDepth),
                        yRotation,
                        side,
                        -1);
                }

                case TileSide.Top:
                {
                    int step = normalized - (edge * 2 + 3);
                    float x = -cornerOffset + cornerSize / 2f + edgeLength / 2f + step * edgeLength;
                    return new BoardTilePlacement(
                        new Vector3(x, 0f, cornerOffset),
                        new Vector3(longSide, tileHeight, laneDepth),
                        yRotation,
                        side,
                        -1);
                }

                default:
                {
                    int step = normalized - (edge * 3 + 4);
                    float z = cornerOffset - cornerSize / 2f - edgeLength / 2f - step * edgeLength;
                    return new BoardTilePlacement(
                        new Vector3(cornerOffset, 0f, z),
                        new Vector3(longSide, tileHeight, laneDepth),
                        yRotation,
                        TileSide.Right,
                        -1);
                }
            }
        }

        private static BoardTilePlacement CreateCornerPlacement(
            int cornerIndex,
            float cornerOffset,
            float cornerSize,
            float tileHeight,
            float yRotation)
        {
            Vector3 position;

            switch (cornerIndex)
            {
                case 0:
                    position = new Vector3(cornerOffset, 0f, -cornerOffset);
                    break;
                case 1:
                    position = new Vector3(-cornerOffset, 0f, -cornerOffset);
                    break;
                case 2:
                    position = new Vector3(-cornerOffset, 0f, cornerOffset);
                    break;
                default:
                    position = new Vector3(cornerOffset, 0f, cornerOffset);
                    break;
            }

            return new BoardTilePlacement(
                position,
                new Vector3(cornerSize, tileHeight, cornerSize),
                yRotation,
                TileSide.Corner,
                cornerIndex);
        }

        private static int GetCornerIndex(int normalizedIndex, int edgeSpacesPerSide)
        {
            int edge = edgeSpacesPerSide;

            if (normalizedIndex == 0)
            {
                return 0;
            }

            if (normalizedIndex == edge + 1)
            {
                return 1;
            }

            if (normalizedIndex == edge * 2 + 2)
            {
                return 2;
            }

            if (normalizedIndex == edge * 3 + 3)
            {
                return 3;
            }

            return -1;
        }

        private static int Normalize(int index, int spaceCount)
        {
            int normalized = index % spaceCount;
            return normalized < 0 ? normalized + spaceCount : normalized;
        }
    }
}
