using System;
using UnityEngine;

namespace MonopolyGame.Stash
{
    /// <summary>
    /// Inspector-tunable placeholder styling for the player stash zones. Kept as
    /// a serializable class so no extra asset is required; the defaults already
    /// produce the intended look.
    /// </summary>
    [Serializable]
    public sealed class PlayerStashVisualConfig
    {
        [Header("Banknotes")]
        public float banknoteWidth = 1.05f;
        public float banknoteDepth = 0.34f;
        public float banknoteThickness = 0.04f;
        public float banknoteGap = 0.01f;
        public int maxVisibleNotes = 20;

        [Header("Pile Column (right side of the panel)")]
        public float pileColumnSpacing = 0.4f;
        public float pileColumnMargin = 0.1f;
        public float pileColumnRightMargin = 0.25f;
        public float pilePadWidth = 1.18f;
        public float pilePadDepth = 0.36f;

        [Header("Zones")]
        public float zoneDepth = 2.4f;
        public float zoneGap = 0.8f;
        public float zoneWidthMargin = 0.785f;
        public float zonePlateHeight = 0.06f;
        public float zoneY = 0.03f;

        [Header("Zone Colours")]
        public Color neutralBase = new(0.10f, 0.11f, 0.13f);
        public Color emptyAccent = new(0.32f, 0.33f, 0.36f);
        public Color padColor = new(0.06f, 0.07f, 0.08f);
        [Range(0f, 1f)] public float occupiedTint = 0.18f;

        [Header("Money Colours (5, 10, 20, 50, 100, 500)")]
        public Color[] noteColors =
        {
            new(0.72f, 0.74f, 0.76f),
            new(0.20f, 0.45f, 0.85f),
            new(0.20f, 0.65f, 0.35f),
            new(0.92f, 0.52f, 0.18f),
            new(0.82f, 0.24f, 0.24f),
            new(0.55f, 0.30f, 0.78f)
        };

        [Header("Money Materials (optional, one per denomination)")]
        [Tooltip(
            "Optional banknote-textured material per denomination (5, 10, 20, 50, 100, 500). " +
            "Empty slots use the shared surface material and the placeholder colour. " +
            "Notes are thin cubes with default UVs, so the texture maps to the top face.")]
        public Material[] noteMaterials = new Material[MoneyNotes.Count];

        public Color GetNoteColor(int index)
        {
            if (noteColors == null || noteColors.Length == 0)
            {
                return Color.white;
            }

            int safeIndex = Mathf.Abs(index) % noteColors.Length;
            return noteColors[safeIndex];
        }

        public Material GetNoteMaterial(int index)
        {
            if (noteMaterials == null || index < 0 || index >= noteMaterials.Length)
            {
                return null;
            }

            return noteMaterials[index];
        }
    }
}
