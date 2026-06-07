using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Displays lobby waiting room: player list, game settings, ready/start controls.
    /// Manages ready state toggling and game launch coordination.
    /// </summary>
    public class LobbyWaitingRoomPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI lobbyNameText;
        [SerializeField] private TextMeshProUGUI roleBadgeText;
        [SerializeField] private TextMeshProUGUI variantText;
        [SerializeField] private TextMeshProUGUI maxPlayersText;
        [SerializeField] private TextMeshProUGUI privateStatusText;

        [SerializeField] private Transform playerSlotContainer;
        [SerializeField] private GameObject playerSlotPrefab;
        [SerializeField] private int maxDisplaySlots = 4;

        [SerializeField] private Button readyButton;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveLobbyButton;

        [SerializeField] private MultiplayerUiCommands uiCommands;
        [SerializeField] private MultiplayerFlowCoordinator coordinator;

        private LobbySnapshot _currentLobby;
        private MultiplayerRole _playerRole;
        private bool _isPlayerReady = false;
        private Dictionary<int, GameObject> _playerSlots = new();

        private void OnEnable()
        {
            if (coordinator != null)
            {
                coordinator.LobbyJoined += OnLobbyUpdated;
                coordinator.ErrorOccurred += OnError;
            }

            if (readyButton != null)
                readyButton.onClick.AddListener(HandleReadyToggle);

            if (startGameButton != null)
                startGameButton.onClick.AddListener(HandleStartGame);

            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.AddListener(HandleLeaveLobby);
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.LobbyJoined -= OnLobbyUpdated;
                coordinator.ErrorOccurred -= OnError;
            }

            if (readyButton != null)
                readyButton.onClick.RemoveListener(HandleReadyToggle);

            if (startGameButton != null)
                startGameButton.onClick.RemoveListener(HandleStartGame);

            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.RemoveListener(HandleLeaveLobby);
        }

        private void OnLobbyUpdated(LobbySnapshot lobbySnapshot)
        {
            _currentLobby = lobbySnapshot;
            DisplayLobbyInfo(lobbySnapshot);
            UpdatePlayerList(lobbySnapshot);
        }

        private void OnError(MultiplayerError error)
        {
            Debug.LogError($"[WaitingRoom] Error: {error.Message}");
        }

        public void SetPlayerRole(MultiplayerRole role)
        {
            _playerRole = role;
            UpdateRoleBadge();
            UpdateControlsForRole();
        }

        private void DisplayLobbyInfo(LobbySnapshot lobby)
        {
            if (lobbyNameText != null)
                lobbyNameText.text = lobby.Name;

            if (variantText != null)
            {
                string variant = "Classic";
                if (lobby.Data != null && lobby.Data.ContainsKey("gameVariant"))
                    variant = lobby.Data["gameVariant"];
                variantText.text = $"Variant: {variant}";
            }

            if (maxPlayersText != null)
                maxPlayersText.text = $"Max Players: {lobby.MaxPlayers}";

            if (privateStatusText != null)
                privateStatusText.text = lobby.IsPrivate ? "Private" : "Public";
        }

        private void UpdatePlayerList(LobbySnapshot lobby)
        {
            foreach (var slot in _playerSlots.Values)
            {
                SafeDestroy(slot);
            }
            _playerSlots.Clear();

            int totalSlots = lobby.MaxPlayers;

            for (int i = 0; i < totalSlots; i++)
            {
                GameObject slotGO = Instantiate(playerSlotPrefab, playerSlotContainer);
                _playerSlots[i] = slotGO;

                var slotUI = slotGO.GetComponent<PlayerSlotItem>();
                if (slotUI != null)
                {
                    if (i < lobby.PlayerDisplayNames.Count)
                    {
                        string playerName = lobby.PlayerDisplayNames[i];
                        bool isReady = IsPlayerReady(i, lobby);
                        slotUI.SetPlayer(playerName, isReady);
                    }
                    else
                    {
                        slotUI.SetEmpty();
                    }
                }
            }
        }

        private bool IsPlayerReady(int playerIndex, LobbySnapshot lobby)
        {
            // Ready-state is not synced yet, so we treat joined players as ready placeholders.
            return true;
        }

        private void UpdateRoleBadge()
        {
            if (roleBadgeText != null)
            {
                roleBadgeText.text = _playerRole == MultiplayerRole.Host ? "HOST" : "PLAYER";
                roleBadgeText.color = _playerRole == MultiplayerRole.Host ? Color.green : Color.cyan;
            }
        }

        private void UpdateControlsForRole()
        {
            if (readyButton != null)
                readyButton.gameObject.SetActive(_playerRole == MultiplayerRole.Client);

            if (startGameButton != null)
                startGameButton.gameObject.SetActive(_playerRole == MultiplayerRole.Host);

            if (startGameButton != null && _currentLobby != null)
            {
                startGameButton.interactable = _currentLobby.PlayerCount >= 2;
            }
        }

        private void HandleReadyToggle()
        {
            _isPlayerReady = !_isPlayerReady;

            if (readyButton != null)
            {
                readyButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                    _isPlayerReady ? "Ready" : "Not Ready";
                readyButton.colors = _isPlayerReady ? 
                    GetReadyButtonColors() : ColorBlock.defaultColorBlock;
            }

            Debug.Log($"[WaitingRoom] Player ready state: {_isPlayerReady}");
            // This currently updates local UI only; lobby-backed ready state can be wired in later.
        }

        private ColorBlock GetReadyButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.green;
            colors.highlightedColor = new Color(0.7f, 1f, 0.7f);
            return colors;
        }

        private void HandleStartGame()
        {
            if (_playerRole != MultiplayerRole.Host)
            {
                Debug.LogWarning("[WaitingRoom] Only host can start game");
                return;
            }

            if (_currentLobby == null || _currentLobby.PlayerCount < 2)
            {
                Debug.LogWarning("[WaitingRoom] Not enough players to start");
                return;
            }

            if (uiCommands != null)
            {
                // Reuse the host flow to ensure relay/network startup is triggered from one path.
                uiCommands.Host();
            }
        }

        private void HandleLeaveLobby()
        {
            if (uiCommands != null)
                uiCommands.LeaveLobby();
        }

        private static void SafeDestroy(Object target)
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
