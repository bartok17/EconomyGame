using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Individual lobby list item displayed in the lobby browser.
    /// Shows game info and handles join button click.
    /// </summary>
    public class LobbyListItemController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI gameNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI hostNameText;
        [SerializeField] private Image privateIcon;
        [SerializeField] private TextMeshProUGUI variantTag;
        [SerializeField] private Button joinButton;

        private string _lobbyId;
        public event Action<string> OnJoinClicked;

        private void OnEnable()
        {
            if (joinButton != null)
                joinButton.onClick.AddListener(HandleJoinClick);
        }

        private void OnDisable()
        {
            if (joinButton != null)
                joinButton.onClick.RemoveListener(HandleJoinClick);
        }

        public void SetLobbyData(LobbySummary lobbySummary)
        {
            _lobbyId = string.IsNullOrWhiteSpace(lobbySummary.LobbyCode) ? lobbySummary.LobbyId : lobbySummary.LobbyCode;

            if (gameNameText != null)
                gameNameText.text = lobbySummary.Name;

            if (playerCountText != null)
                playerCountText.text = $"{lobbySummary.PlayerCount}/{lobbySummary.MaxPlayers}";

            if (hostNameText != null)
            {
                hostNameText.text = "Host";
            }

            if (privateIcon != null)
                privateIcon.gameObject.SetActive(lobbySummary.IsPrivate);

            if (variantTag != null)
            {
                string variant = "Classic";
                if (lobbySummary.Data != null && lobbySummary.Data.ContainsKey("gameVariant"))
                    variant = lobbySummary.Data["gameVariant"];

                variantTag.text = variant;
            }
        }

        private void HandleJoinClick()
        {
            Debug.Log($"[LobbyListItem] Joining lobby: {_lobbyId}");
            OnJoinClicked?.Invoke(_lobbyId);
        }

        public string GetLobbyId() => _lobbyId;
    }
}
