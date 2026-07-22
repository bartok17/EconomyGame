using UnityEngine;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Thin UI bridge for the Hub panel buttons.
    /// Prefer wiring Buttons to these methods (strongly named) instead of passing strings.
    /// </summary>
    public sealed class HubMenuPresenter : MonoBehaviour
    {
        [SerializeField] private bool allowAutoDisableWhenUnwired = true;
        [SerializeField] private HubMenuCoordinator hub;
        [SerializeField] private MultiplayerUiCommands uiCommands;
        [SerializeField] private JoinByCodePresenter joinByCodePresenter;

        private void OnEnable()
        {
            if (hub == null || uiCommands == null || joinByCodePresenter == null)
            {
                Debug.LogWarning("[HubMenuPresenter] Unwired references detected. Assign HubMenuCoordinator, MultiplayerUiCommands, and JoinByCodePresenter in the inspector.");

                if (allowAutoDisableWhenUnwired)
                {
                    enabled = false;
                }

                return;
            }
        }

        public void OpenBrowser()
        {
            if (hub != null)
                hub.ShowPanel("browser");

            // Optional: auto-refresh when opening the browser
            if (uiCommands != null)
                uiCommands.RefreshLobbies();
        }

        public void OpenCreateLobby()
        {
            if (hub != null)
                hub.ShowPanel("creator");
        }

        public void OpenJoinByCode()
        {
            if (hub != null)
                hub.ShowPanel("code");

            if (joinByCodePresenter != null)
                joinByCodePresenter.ResetInput();
        }

        public void BackToHub()
        {
            if (hub != null)
                hub.ShowPanel("hub");
        }
    }
}
