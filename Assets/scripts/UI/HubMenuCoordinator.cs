using UnityEngine;
using System.Collections.Generic;

namespace MonopolyGame.Multiplayer.UI
{
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
            EnsureCoordinator();

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
            EnsureCoordinator();
            InitializePanelMap();
            SetInitialPanelState();
        }
        
        private void Start()
        {
            EnsureCoordinator();
            if (coordinator != null && coordinator.Status == MultiplayerStatus.SignedIn)
            {
                ShowPanel("hub");
            }
        }

        private void EnsureCoordinator()
        {
            if (MultiplayerFlowCoordinator.Instance != null)
            {
                coordinator = MultiplayerFlowCoordinator.Instance;
            }
            else if (coordinator == null)
            {
                coordinator = FindAnyObjectByType<MultiplayerFlowCoordinator>();
            }
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
            EnsureCoordinator();
            HideAllPanelsImmediate();
            string defaultPanel = (coordinator != null && coordinator.Status == MultiplayerStatus.SignedIn)
                ? "hub"
                : "auth";
            if (_panelMap.TryGetValue(defaultPanel, out CanvasGroup initialPanel) && initialPanel != null)
            {
                initialPanel.gameObject.SetActive(true);
                initialPanel.alpha = 1f;
                initialPanel.interactable = true;
                initialPanel.blocksRaycasts = true;
                _currentActivePanel = initialPanel;
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
            }
        }

        private void OnLobbyJoined(LobbySnapshot lobbySnapshot)
        {
            ShowPanel("waiting");
        }

        private void OnLobbyLeft()
        {
            Debug.Log("[HubMenuCoordinator] Left lobby, returning to hub");
            ShowPanel("hub");
        }

        public void ShowPanel(string panelName)
        {
            if (_panelMap == null || !_panelMap.ContainsKey(panelName))
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
