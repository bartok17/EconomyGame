using UnityEngine;

namespace MonopolyGame.Pawns
{
    [CreateAssetMenu(menuName = "Monopoly Game/Pawn Visual Config", fileName = "PawnVisualConfig")]
    public sealed class PawnVisualConfig : ScriptableObject
    {
        [SerializeField] private float pawnHeight = 0.8f;
        [SerializeField] private float pawnRadius = 0.28f;
        [SerializeField] private Color[] pawnColors =
        {
            new Color(0.10f, 0.75f, 0.25f),
            new Color(0.85f, 0.12f, 0.12f),
            new Color(0.12f, 0.35f, 0.90f),
            new Color(0.95f, 0.82f, 0.15f)
        };

        public float PawnHeight => pawnHeight;
        public float PawnRadius => pawnRadius;

        public Color GetPawnColor(int pawnSlot)
        {
            if (pawnColors == null || pawnColors.Length == 0)
            {
                return Color.white;
            }

            int index = Mathf.Abs(pawnSlot) % pawnColors.Length;
            return pawnColors[index];
        }
    }
}
