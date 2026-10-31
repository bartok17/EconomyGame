using MonopolyGame.DesignSystem;
using MonopolyGame.UI.DesignSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.UI
{
    public class PlayerSlotItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI readyStatusText;
        [SerializeField] private GameObject emptySlotPlaceholder;

        private ThemedImage _avatarThemed;

        private void Awake()
        {
            if (avatarImage == null)
            {
                Transform avatar = transform.Find("Image");
                if (avatar != null)
                    avatarImage = avatar.GetComponent<Image>();
            }

            if (avatarImage != null)
                _avatarThemed = avatarImage.GetComponent<ThemedImage>();
        }

        public void SetPlayer(string playerName, bool isReady, int colorIndex = -1)
        {
            if (emptySlotPlaceholder != null)
                emptySlotPlaceholder.SetActive(false);

            if (playerNameText != null)
                playerNameText.text = playerName;

            if (readyStatusText != null)
                readyStatusText.text = isReady ? "Ready" : "...";

            if (avatarImage != null)
                avatarImage.enabled = true;

            if (_avatarThemed != null && colorIndex >= 0)
            {
                _avatarThemed.SetPlayerColor((PlayerColorRole)(colorIndex % 4));
            }
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