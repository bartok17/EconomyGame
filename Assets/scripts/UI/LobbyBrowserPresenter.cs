using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Displays list of available lobbies and handles lobby selection.
    /// Refreshes from MultiplayerFlowCoordinator LobbyListUpdated event.
    /// </summary>
    public class LobbyBrowserPresenter : MonoBehaviour
    {
        [SerializeField] private Transform lobbyListContainer;
        [SerializeField] private GameObject lobbyListItemPrefab;
        [SerializeField] private TextMeshProUGUI emptyStateText;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TMPro.TMP_Dropdown sortDropdown;

        [SerializeField] private MultiplayerUiCommands uiCommands;
        [SerializeField] private MultiplayerFlowCoordinator coordinator;

        private List<LobbyListItemController> _currentItems = new();
        private List<LobbySummary> _currentLobbies = new();

        private void OnEnable()
        {
            if (coordinator != null)
            {
                coordinator.LobbyListUpdated += OnLobbyListUpdated;
            }

            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshLobbies);

            if (sortDropdown != null)
                sortDropdown.onValueChanged.AddListener(OnSortChanged);
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.LobbyListUpdated -= OnLobbyListUpdated;
            }

            if (refreshButton != null)
                refreshButton.onClick.RemoveListener(RefreshLobbies);

            if (sortDropdown != null)
                sortDropdown.onValueChanged.RemoveListener(OnSortChanged);
        }

        private void Start()
        {
            RefreshLobbies();
        }

        [ContextMenu("Populate Mock Lobbies")]
        private void PopulateMockLobbies()
        {
            var mockLobbies = new List<LobbySummary>(12);

            for (var i = 1; i <= 12; i++)
            {
                var lobbyId = $"mock-lobby-{i:00}";
                var lobbyCode = $"MOCK{i:00}";
                var playerCount = 1 + (i % 4);
                var maxPlayers = 4 + (i % 3) * 2;
                var isPrivate = i % 3 == 0;

                var data = new Dictionary<string, string>
                {
                    { "gameVariant", i % 2 == 0 ? "Classic" : "Turbo" }
                };

                mockLobbies.Add(new LobbySummary(
                    lobbyId,
                    lobbyCode,
                    $"Test Lobby {i:00}",
                    maxPlayers,
                    playerCount,
                    isPrivate,
                    data));
            }

            _currentLobbies = mockLobbies;
            DisplayLobbies(_currentLobbies);
        }

        private void RefreshLobbies()
        {
            Debug.Log("[LobbyBrowser] Refreshing lobby list");
            if (uiCommands != null)
                uiCommands.RefreshLobbies();
        }

        private void OnLobbyListUpdated(IReadOnlyList<LobbySummary> lobbies)
        {
            Debug.Log($"[LobbyBrowser] Updated with {lobbies.Count} lobbies");
            _currentLobbies = new List<LobbySummary>(lobbies);
            DisplayLobbies(_currentLobbies);
        }

        private void DisplayLobbies(List<LobbySummary> lobbies)
        {
            foreach (var item in _currentItems)
            {
                SafeDestroy(item != null ? item.gameObject : null);
            }
            _currentItems.Clear();

            if (lobbies == null || lobbies.Count == 0)
            {
                if (emptyStateText != null)
                {
                    emptyStateText.gameObject.SetActive(true);
                    emptyStateText.text = "No games available.\nCreate one to get started!";
                }
                return;
            }

            if (emptyStateText != null)
                emptyStateText.gameObject.SetActive(false);

            foreach (var lobbySummary in lobbies)
            {
                GameObject itemGO = Instantiate(lobbyListItemPrefab, lobbyListContainer);
                LobbyListItemController itemController = itemGO.GetComponent<LobbyListItemController>();

                if (itemController != null)
                {
                    itemController.SetLobbyData(lobbySummary);
                    itemController.OnJoinClicked += OnLobbyJoinClicked;
                    _currentItems.Add(itemController);
                }
            }
        }

        private void OnLobbyJoinClicked(string lobbyId)
        {
            Debug.Log($"[LobbyBrowser] Join clicked for lobby target: {lobbyId}");
            if (uiCommands != null)
                uiCommands.Join(lobbyId);
        }

        private void OnSortChanged(int sortIndex)
        {
            if (_currentLobbies == null || _currentLobbies.Count == 0)
                return;

            List<LobbySummary> sorted = new(_currentLobbies);

            switch (sortIndex)
            {
                case 0:
                    sorted.Sort((a, b) => b.PlayerCount.CompareTo(a.PlayerCount));
                    break;
                case 1:
                    sorted.Sort((a, b) => a.Name.CompareTo(b.Name));
                    break;
                case 2:
                    sorted.Sort((a, b) => 
                        (b.MaxPlayers - b.PlayerCount).CompareTo(a.MaxPlayers - a.PlayerCount));
                    break;
            }

            DisplayLobbies(sorted);
        }

        private static void SafeDestroy(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
