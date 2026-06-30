using MonopolyGame.Multiplayer.Gameplay;
using UnityEngine;

namespace MonopolyGame.Board
{
    public sealed class BoardSpaceView : MonoBehaviour
    {
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
        
        public Vector3 GetPawnWorldPosition(int pawnSlot)
        {
            Vector3[] offsets =
            {
                new Vector3(-0.35f, 0f, -0.35f),
                new Vector3(0.35f, 0f, -0.35f),
                new Vector3(-0.35f, 0f, 0.35f),
                new Vector3(0.35f, 0f, 0.35f)
            };

            int slot = Mathf.Abs(pawnSlot) % offsets.Length;
            return transform.position + offsets[slot] + Vector3.up * 0.45f;
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