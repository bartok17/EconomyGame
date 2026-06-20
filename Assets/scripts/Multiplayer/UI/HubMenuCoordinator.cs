using UnityEngine;
using System.Collections.Generic;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Manages hub menu panel visibility and state transitions.
    /// Coordinates between hub menu, browser, creator, and waiting room screens.
    /// </summary>
    public class HubMenuCoordinator : MonoBehaviour
    {
        [SerializeField] private CanvasGroup authPanelGroup;
        [SerializeField] private CanvasGroup hubMenuPanelGroup;
        [SerializeField] private CanvasGroup lobbyBrowserPanelGroup;
        [SerializeField] private CanvasGroup lobbyCreatorPanelGroup;
        [SerializeField] private CanvasGroup joinByCodePanelGroup;
        [SerializeField] private CanvasGroup lobbyWaitingPanelGroup;

        [SerializeField] private float transitionDuration = 0.3f;
        [SerializeField] private MultiplayerFlowCoordinator coordinator;

        private CanvasGroup _currentActivePanel;
        private Dictionary<string, CanvasGroup> _panelMap;

        private void OnEnable()
        {
            if (coordinator == null)
            {
                coordinator = FindAnyObjectByType<MultiplayerFlowCoordinator>();
            }

            if (coordinator == null)
            {
                Debug.LogWarning("[HubMenuCoordinator] MultiplayerFlowCoordinator not found. Panel transitions will not react to lobby events.");
                return;
            }

            if (coordinator != null)
            {
                coordinator.StatusChanged += OnStatusChanged;
                coordinator.LobbyJoined += OnLobbyJoined;
                coordinator.LobbyLeft += OnLobbyLeft;
            }
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.StatusChanged -= OnStatusChanged;
                coordinator.LobbyJoined -= OnLobbyJoined;
                coordinator.LobbyLeft -= OnLobbyLeft;
            }
        }

        private void Awake()
        {
            InitializePanelMap();
            SetInitialPanelState();
        }

        private void InitializePanelMap()
        {
            _panelMap = new Dictionary<string, CanvasGroup>
            {
                { "auth", authPanelGroup },
                { "hub", hubMenuPanelGroup },
                { "browser", lobbyBrowserPanelGroup },
                { "creator", lobbyCreatorPanelGroup },
                { "code", joinByCodePanelGroup },
                { "waiting", lobbyWaitingPanelGroup }
            };
        }

        private void SetInitialPanelState()
        {
            HideAllPanelsImmediate();

            if (_panelMap.TryGetValue("auth", out CanvasGroup authPanel) && authPanel != null)
            {
                authPanel.gameObject.SetActive(true);
                authPanel.alpha = 1f;
                authPanel.interactable = true;
                authPanel.blocksRaycasts = true;
                _currentActivePanel = authPanel;
            }
        }

        private void HideAllPanelsImmediate()
        {
            foreach (CanvasGroup panel in _panelMap.Values)
            {
                if (panel == null)
                {
                    continue;
                }

                panel.alpha = 0f;
                panel.interactable = false;
                panel.blocksRaycasts = false;
                panel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Handle status changes from multiplayer coordinator.
        /// Show/hide panels based on login state and current flow.
        /// </summary>
        private void OnStatusChanged(MultiplayerStatus status)
        {
            if (status == MultiplayerStatus.SignedOut || status == MultiplayerStatus.SigningIn)
            {
                ShowPanel("auth");
            }
            else if (status == MultiplayerStatus.SignedIn)
            {
                if (_currentActivePanel == null || _currentActivePanel == authPanelGroup)
                {
                    ShowPanel("hub");
                }
            }
            else if (status == MultiplayerStatus.LobbyJoining || 
                     status == MultiplayerStatus.LobbyQuerying ||
                     status == MultiplayerStatus.RelayAllocating ||
                     status == MultiplayerStatus.RelayJoining ||
                     status == MultiplayerStatus.NetworkStarting)
            {
                // Keep the visible panel stable while child presenters disable their own actions.
            }
        }

        /// <summary>
        /// When successfully joined a lobby, transition to waiting room.
        /// </summary>
        private void OnLobbyJoined(LobbySnapshot lobbySnapshot)
        {
            ShowPanel("waiting");
        }

        /// <summary>
        /// When left a lobby, return to hub menu.
        /// </summary>
        private void OnLobbyLeft()
        {
            Debug.Log("[HubMenuCoordinator] Left lobby, returning to hub");
            ShowPanel("hub");
        }

        /// <summary>
        /// Display a specific panel and fade out others.
        /// </summary>
        public void ShowPanel(string panelName)
        {
            if (!_panelMap.ContainsKey(panelName))
            {
                Debug.LogWarning($"[HubMenuCoordinator] Panel '{panelName}' not found in map");
                return;
            }

            CanvasGroup targetPanel = _panelMap[panelName];

            if (_currentActivePanel != null && _currentActivePanel != targetPanel)
            {
                StopCoroutine(nameof(FadePanel));
                StartCoroutine(FadePanel(_currentActivePanel, 0, transitionDuration));
            }

            StopCoroutine(nameof(FadePanel));
            StartCoroutine(FadePanel(targetPanel, 1, transitionDuration));

            targetPanel.interactable = true;
            targetPanel.blocksRaycasts = true;
            _currentActivePanel = targetPanel;
        }

        /// <summary>
        /// Return to hub menu from any sub-panel.
        /// </summary>
        public void BackToHub()
        {
            ShowPanel("hub");
        }

        private System.Collections.IEnumerator FadePanel(CanvasGroup panelGroup, float targetAlpha, float duration)
        {
            if (panelGroup == null)
                yield break;

            panelGroup.gameObject.SetActive(true);
            panelGroup.interactable = targetAlpha > 0f;
            panelGroup.blocksRaycasts = targetAlpha > 0f;
            float startAlpha = panelGroup.alpha;
            float elapsed = 0;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                panelGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            panelGroup.alpha = targetAlpha;

            if (targetAlpha == 0)
            {
                panelGroup.interactable = false;
                panelGroup.blocksRaycasts = false;
                panelGroup.gameObject.SetActive(false);
            }
            else
            {
                panelGroup.interactable = true;
                panelGroup.blocksRaycasts = true;
            }
        }
    }
}
