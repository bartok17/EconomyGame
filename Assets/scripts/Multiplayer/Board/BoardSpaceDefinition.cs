using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>
    /// Authoring data for a single board space. The board is generated from
    /// these entries at runtime, so this is the single source of truth for
    /// what each tile is and what it does.
    /// </summary>
    [Serializable]
    public sealed class BoardSpaceDefinition
    {
        [SerializeField] private int index;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private BoardSpaceType type;
        [SerializeField] private int price;
        [SerializeField] private int baseRent;
        [SerializeField] private int houseCost;
        [SerializeField] private int colorGroup;
        [SerializeField] private string iconKey = string.Empty;
        [SerializeField, TextArea] private string description = string.Empty;
        [SerializeField] private Color accentOverride = Color.white;
        [SerializeField] private bool useAccentOverride;
        [SerializeField] private GameObject prefabOverride;

        public BoardSpaceDefinition()
        {
        }

        public BoardSpaceDefinition(
            int index,
            string displayName,
            BoardSpaceType type,
            int price = 0,
            int baseRent = 0,
            int houseCost = 0,
            int colorGroup = 0,
            string iconKey = "",
            string description = "")
        {
            this.index = index;
            this.displayName = displayName ?? string.Empty;
            this.type = type;
            this.price = price;
            this.baseRent = baseRent;
            this.houseCost = houseCost;
            this.colorGroup = colorGroup;
            this.iconKey = iconKey ?? string.Empty;
            this.description = description ?? string.Empty;
        }

        public int Index => index;
        public string DisplayName => displayName;
        public BoardSpaceType Type => type;
        public int Price => price;
        public int BaseRent => baseRent;
        public int HouseCost => houseCost;
        public int ColorGroup => colorGroup;
        public string IconKey => iconKey;
        public string Description => description;
        public Color AccentOverride => accentOverride;
        public bool UseAccentOverride => useAccentOverride;
        public GameObject PrefabOverride => prefabOverride;

        public bool IsProperty => type == BoardSpaceType.Property;

        public void SetColorGroup(int group)
        {
            colorGroup = group;
        }

        public void SetAccentOverride(Color color, bool enabled = true)
        {
            accentOverride = color;
            useAccentOverride = enabled;
        }
    }
}
