using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    public sealed class AuthScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject lobbyPanel;

        public void OnStatusChanged(MultiplayerStatus status)
        {
            var isSignedIn = status == MultiplayerStatus.SignedIn;

            if (loginPanel != null) loginPanel.SetActive(!isSignedIn);
            if (lobbyPanel != null) lobbyPanel.SetActive(isSignedIn);
        }

        public void OnSignedIn(string playerId, string displayName)
        {
            if (loginPanel != null) loginPanel.SetActive(false);
        }
    }
}