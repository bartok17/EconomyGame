using System;
using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>Per-type visual defaults used by <see cref="BoardTileView"/>.</summary>
    [Serializable]
    public sealed class BoardTypeStyle
    {
        [SerializeField] private BoardSpaceType type;
        [SerializeField] private Color baseTint = Color.white;
        [SerializeField] private Color accentTint = Color.white;
        [SerializeField] private bool showName = true;
        [SerializeField] private bool showPrice;
        [SerializeField] private bool showIcon;

        public BoardTypeStyle()
        {
        }

        public BoardTypeStyle(
            BoardSpaceType type,
            Color baseTint,
            Color accentTint,
            bool showName = true,
            bool showPrice = false,
            bool showIcon = false)
        {
            this.type = type;
            this.baseTint = baseTint;
            this.accentTint = accentTint;
            this.showName = showName;
            this.showPrice = showPrice;
            this.showIcon = showIcon;
        }

        public BoardSpaceType Type => type;
        public Color BaseTint => baseTint;
        public Color AccentTint => accentTint;
        public bool ShowName => showName;
        public bool ShowPrice => showPrice;
        public bool ShowIcon => showIcon;
    }

    /// <summary>Per-side yaw for flat tile labels. Corners use one yaw per corner.</summary>
    [Serializable]
    public sealed class TileOrientationSettings
    {
        [SerializeField] private float bottomYaw;
        [SerializeField] private float leftYaw = 90f;
        [SerializeField] private float topYaw = 180f;
        [SerializeField] private float rightYaw = 270f;
        [SerializeField] private float[] cornerYaws = { 0f, 90f, 180f, 270f };

        public float GetYaw(TileSide side, int cornerIndex)
        {
            switch (side)
            {
                case TileSide.Bottom:
                    return bottomYaw;
                case TileSide.Left:
                    return leftYaw;
                case TileSide.Top:
                    return topYaw;
                case TileSide.Right:
                    return rightYaw;
                case TileSide.Corner:
                    if (cornerYaws == null || cornerYaws.Length == 0)
                    {
                        return 0f;
                    }

                    int index = ((cornerIndex % cornerYaws.Length) + cornerYaws.Length) % cornerYaws.Length;
                    return cornerYaws[index];
                default:
                    return 0f;
            }
        }
    }

    /// <summary>
    /// The tile template: one prefab plus shared materials, fonts and placement
    /// rules. Every board tile is generated from this asset; per-tile data comes
    /// from <see cref="BoardSpaceDefinition"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Monopoly Game/Board/Tile Template", fileName = "BoardTileTemplate")]
    public sealed class BoardTileTemplate : ScriptableObject
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private GameObject cornerPrefabOverride;
        [SerializeField] private string tilePrefabResourcePath = "Board/BoardTile";
        [SerializeField] private string cornerPrefabResourcePath = "Board/BoardTileCorner";

        [Header("Footprint")]
        [SerializeField] private int edgeSpacesPerSide = 9;
        [SerializeField] private float cornerSize = 2f;
        [SerializeField] private float edgeLength = 1.3f;
        [SerializeField] private float laneDepth = 1.8f;
        [SerializeField] private float tileHeight = 0.12f;
        [SerializeField, Range(0.5f, 1f)] private float tileGapFactor = 0.92f;

        [Header("Pawn Placement")]
        [SerializeField] private Vector3[] pawnSlotOffsets =
        {
            new Vector3(-0.35f, 0f, -0.35f),
            new Vector3(0.35f, 0f, -0.35f),
            new Vector3(-0.35f, 0f, 0.35f),
            new Vector3(0.35f, 0f, 0.35f)
        };
        [SerializeField] private Vector3[] cornerPawnSlotOffsets =
        {
            new Vector3(-0.45f, 0f, -0.45f),
            new Vector3(0.45f, 0f, -0.45f),
            new Vector3(-0.45f, 0f, 0.45f),
            new Vector3(0.45f, 0f, 0.45f)
        };
        [SerializeField] private float pawnHeightOffset = 0.45f;

        [Header("Text")]
        [SerializeField] private TMPro.TMP_FontAsset font;
        [SerializeField] private float nameFontSize = 0.35f;
        [SerializeField] private float priceFontSize = 0.28f;
        [SerializeField] private Color textColor = Color.white;

        [Header("Materials")]
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material accentMaterial;
        [SerializeField] private Material iconMaterial;
        [SerializeField] private Material ownerMarkerMaterial;

        [Header("Palette")]
        [SerializeField] private Color[] propertyGroupColors =
        {
            new Color(0.54f, 0.35f, 0.22f),
            new Color(0.48f, 0.74f, 0.90f),
            new Color(0.85f, 0.42f, 0.68f),
            new Color(0.92f, 0.58f, 0.22f),
            new Color(0.84f, 0.18f, 0.20f),
            new Color(0.94f, 0.80f, 0.18f),
            new Color(0.22f, 0.68f, 0.32f),
            new Color(0.14f, 0.24f, 0.62f)
        };
        [SerializeField] private Color[] ownerColors =
        {
            new Color(0.10f, 0.75f, 0.25f),
            new Color(0.85f, 0.12f, 0.12f),
            new Color(0.12f, 0.35f, 0.90f),
            new Color(0.95f, 0.82f, 0.15f)
        };
        [SerializeField] private BoardTypeStyle[] typeStyles = CreateDefaultTypeStyles();

        [Header("Orientation")]
        [SerializeField] private TileOrientationSettings orientations = new TileOrientationSettings();

        public GameObject TilePrefab => tilePrefab;
        public GameObject CornerPrefabOverride => cornerPrefabOverride;

        public int EdgeSpacesPerSide => edgeSpacesPerSide;
        public float CornerSize => cornerSize;
        public float EdgeLength => edgeLength;
        public float LaneDepth => laneDepth;
        public float TileHeight => tileHeight;
        public float TileGapFactor => tileGapFactor;

        public Vector3[] PawnSlotOffsets => pawnSlotOffsets;
        public Vector3[] CornerPawnSlotOffsets => cornerPawnSlotOffsets;
        public float PawnHeightOffset => pawnHeightOffset;

        public TMPro.TMP_FontAsset Font => font;
        public float NameFontSize => nameFontSize;
        public float PriceFontSize => priceFontSize;
        public Color TextColor => textColor;

        public Material BaseMaterial => baseMaterial;
        public Material AccentMaterial => accentMaterial;
        public Material IconMaterial => iconMaterial;
        public Material OwnerMarkerMaterial => ownerMarkerMaterial;

        public TileOrientationSettings Orientations => orientations;

        public GameObject GetPrefab(bool isCorner)
        {
            return isCorner && cornerPrefabOverride != null ? cornerPrefabOverride : tilePrefab;
        }

        /// <summary>
        /// Resolves the tile prefab, falling back to Resources when the serialized
        /// reference was not wired (tooling / runtime generation).
        /// </summary>
        public GameObject ResolvePrefab(bool isCorner)
        {
            GameObject prefab = GetPrefab(isCorner);

            if (prefab != null)
            {
                return prefab;
            }

            string resourcePath = isCorner && !string.IsNullOrEmpty(cornerPrefabResourcePath)
                ? cornerPrefabResourcePath
                : tilePrefabResourcePath;

            return string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<GameObject>(resourcePath);
        }

        public Vector3[] GetPawnSlotOffsets(bool isCorner)
        {
            Vector3[] offsets = isCorner ? cornerPawnSlotOffsets : pawnSlotOffsets;

            if (offsets == null || offsets.Length == 0)
            {
                offsets = pawnSlotOffsets;
            }

            if (offsets == null || offsets.Length == 0)
            {
                offsets = new[]
                {
                    new Vector3(-0.35f, 0f, -0.35f),
                    new Vector3(0.35f, 0f, -0.35f),
                    new Vector3(-0.35f, 0f, 0.35f),
                    new Vector3(0.35f, 0f, 0.35f)
                };
            }

            return offsets;
        }

        public float GetSideYaw(TileSide side, int cornerIndex)
        {
            return orientations != null ? orientations.GetYaw(side, cornerIndex) : 0f;
        }

        public Color GetGroupColor(int colorGroup)
        {
            if (propertyGroupColors == null || propertyGroupColors.Length == 0)
            {
                return Color.white;
            }

            if (colorGroup <= 0)
            {
                return Color.white;
            }

            int index = (colorGroup - 1) % propertyGroupColors.Length;
            return propertyGroupColors[index];
        }

        public Color GetOwnerColor(int ownerSlot)
        {
            if (ownerColors == null || ownerColors.Length == 0)
            {
                return Color.white;
            }

            int index = Mathf.Abs(ownerSlot) % ownerColors.Length;
            return ownerColors[index];
        }

        public BoardTypeStyle GetTypeStyle(BoardSpaceType type)
        {
            if (typeStyles == null)
            {
                return null;
            }

            for (int i = 0; i < typeStyles.Length; i++)
            {
                if (typeStyles[i] != null && typeStyles[i].Type == type)
                {
                    return typeStyles[i];
                }
            }

            return null;
        }

        private static BoardTypeStyle[] CreateDefaultTypeStyles()
        {
            return new[]
            {
                new BoardTypeStyle(BoardSpaceType.Start, new Color(0.80f, 0.80f, 0.78f), new Color(0.95f, 0.90f, 0.65f), true, false, true),
                new BoardTypeStyle(BoardSpaceType.Property, new Color(0.34f, 0.36f, 0.36f), Color.white, true, true, false),
                new BoardTypeStyle(BoardSpaceType.ActionCard, new Color(0.55f, 0.12f, 0.18f), new Color(0.92f, 0.28f, 0.32f), true, false, true),
                new BoardTypeStyle(BoardSpaceType.EventCard, new Color(0.10f, 0.36f, 0.52f), new Color(0.24f, 0.62f, 0.82f), true, false, true),
                new BoardTypeStyle(BoardSpaceType.Tax, new Color(0.55f, 0.42f, 0.18f), new Color(0.92f, 0.76f, 0.32f), true, true, true),
                new BoardTypeStyle(BoardSpaceType.Parking, new Color(0.40f, 0.48f, 0.42f), new Color(0.64f, 0.78f, 0.66f), true, false, true),
                new BoardTypeStyle(BoardSpaceType.Jail, new Color(0.45f, 0.45f, 0.48f), new Color(0.70f, 0.70f, 0.76f), true, false, true),
                new BoardTypeStyle(BoardSpaceType.GoToJail, new Color(0.42f, 0.32f, 0.48f), new Color(0.72f, 0.56f, 0.82f), true, false, true),
                new BoardTypeStyle(BoardSpaceType.Special, new Color(0.50f, 0.50f, 0.50f), new Color(0.72f, 0.72f, 0.72f), true, false, false)
            };
        }
    }
}
