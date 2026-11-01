using UnityEngine;

namespace MonopolyGame.Board
{
    public sealed class BoardSpaceView : MonoBehaviour
    {
        private static readonly Vector3[] DefaultPawnSlotOffsets =
        {
            new Vector3(-0.35f, 0f, -0.35f),
            new Vector3(0.35f, 0f, -0.35f),
            new Vector3(-0.35f, 0f, 0.35f),
            new Vector3(0.35f, 0f, 0.35f)
        };

        [Header("Basic Info")]
        public int index;
        public string displayName;
        public BoardSpaceType type;
        
        [Header("Financial Data")]
        public int price; 
        public int baseRent; 
        public int houseCost;
        
        [Header("Current State")]
        public string ownerId = ""; 
        public int housesBuilt = 0;

        [Header("Pawn Placement")]
        [SerializeField] private Vector3[] pawnSlotOffsets = DefaultPawnSlotOffsets;
        [SerializeField] private float pawnHeightOffset = 0.45f;

        private BoardTileView tileView;
        private BoardTileTemplate template;

        public void Configure(BoardSpaceDefinition definition, BoardTileTemplate tileTemplate, bool isCorner)
        {
            template = tileTemplate;
            tileView = GetComponent<BoardTileView>();

            if (definition != null)
            {
                index = definition.Index;
                displayName = definition.DisplayName;
                type = definition.Type;
                price = definition.Price;
                baseRent = definition.BaseRent;
                houseCost = definition.HouseCost;
            }

            ownerId = string.Empty;
            housesBuilt = 0;

            if (tileTemplate != null)
            {
                pawnSlotOffsets = tileTemplate.GetPawnSlotOffsets(isCorner);
                pawnHeightOffset = tileTemplate.PawnHeightOffset;
            }
        }

        public Vector3 GetPawnWorldPosition(int pawnSlot)
        {
            Vector3[] offsets = pawnSlotOffsets != null && pawnSlotOffsets.Length > 0
                ? pawnSlotOffsets
                : DefaultPawnSlotOffsets;

            int slot = Mathf.Abs(pawnSlot) % offsets.Length;
            return transform.position + transform.TransformVector(offsets[slot]) + Vector3.up * pawnHeightOffset;
        }

        public void ApplyOwnerVisual(int ownerSlot)
        {
            if (tileView == null)
            {
                tileView = GetComponent<BoardTileView>();
            }

            if (tileView == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(ownerId))
            {
                tileView.SetOwner(null);
                return;
            }

            Color ownerColor = template != null ? template.GetOwnerColor(ownerSlot) : Color.white;
            tileView.SetOwner(ownerColor);
        }

        public void OnPlayerLanded(BoardLandingResult landingResult)
        {
            if (landingResult == null)
            {
                return;
            }

            Debug.Log(landingResult.Message);
        }
    }
}