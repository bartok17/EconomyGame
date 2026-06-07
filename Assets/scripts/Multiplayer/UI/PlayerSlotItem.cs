using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Displays one lobby player slot, including empty-slot state and ready status.
    /// </summary>
    public class PlayerSlotItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI readyStatusText;
        [SerializeField] private GameObject emptySlotPlaceholder;

        public void SetPlayer(string playerName, bool isReady)
        {
            if (emptySlotPlaceholder != null)
                emptySlotPlaceholder.SetActive(false);

            if (playerNameText != null)
                playerNameText.text = playerName;

            if (readyStatusText != null)
                readyStatusText.text = isReady ? "Ready" : "...";

            if (avatarImage != null)
                avatarImage.enabled = true;
        }

        public void SetEmpty()
        {
            if (emptySlotPlaceholder != null)
                emptySlotPlaceholder.SetActive(true);

            if (playerNameText != null)
                playerNameText.text = string.Empty;

            if (readyStatusText != null)
                readyStatusText.text = string.Empty;

            if (avatarImage != null)
                avatarImage.enabled = false;
        }
    }
}