using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer
{
    public sealed class MultiplayerUiCommands : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MultiplayerFlowCoordinator coordinator;

        [Header("Form State (set via UI events)")]
        [SerializeField] private string username;
        [SerializeField] private string password;
        [SerializeField] private string displayName;
        [SerializeField] private string lobbyName;
        [SerializeField] private string lobbyCode;
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private bool isPrivate;

        [Header("Optional UX")]
        [SerializeField] private bool autoInitializeOnEnable = true;
        [SerializeField] private GameObject busyRoot;
        [SerializeField] private List<Selectable> disableWhileBusy = new List<Selectable>();

        private static readonly HashSet<MultiplayerStatus> BusyStatuses = new()
        {
            MultiplayerStatus.Initializing,
            MultiplayerStatus.SigningIn,
            MultiplayerStatus.LobbyQuerying,
            MultiplayerStatus.LobbyJoining,
            MultiplayerStatus.RelayAllocating,
            MultiplayerStatus.RelayJoining,
            MultiplayerStatus.NetworkStarting
        };

        private void OnEnable()
        {
            if (coordinator == null)
            {
                coordinator = FindObjectOfType<MultiplayerFlowCoordinator>();
            }

            if (coordinator == null)
            {
                Debug.LogError("MultiplayerFlowCoordinator not found. Assign it in the inspector or add it to the scene.");
                enabled = false;
                return;
            }

            coordinator.StatusChanged += HandleStatusChanged;
            HandleStatusChanged(coordinator.Status);

            if (autoInitializeOnEnable)
            {
                Initialize();
            }
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.StatusChanged -= HandleStatusChanged;
            }
        }

        // --- UI Setters (wire these to InputField/TMP_InputField onValueChanged)
        public void SetUsername(string value) => username = value;
        public void SetPassword(string value) => password = value;
        public void SetDisplayName(string value) => displayName = value;
        public void SetLobbyName(string value) => lobbyName = value;
        public void SetLobbyCode(string value) => lobbyCode = value;

        public void SetMaxPlayers(string value)
        {
            if (int.TryParse(value, out var parsed))
            {
                maxPlayers = Mathf.Clamp(parsed, 2, 16);
            }
        }

        public void SetIsPrivate(bool value) => isPrivate = value;

        // --- UI Commands (wire these to Buttons)
        public void Initialize() => Run(coordinator.InitializeAsync);

        public void SignIn() => Run(() => coordinator.SignInAsync(username, password));

        public void SignUp() => Run(() => coordinator.SignUpAsync(username, password, displayName));

        public void SetName() => Run(() => coordinator.SetDisplayNameAsync(displayName));

        public void SignOut() => coordinator.SignOut();

        public void RefreshLobbies() => Run(() => coordinator.QueryLobbiesAsync());

        public void Host() => Run(() => coordinator.StartHostFlowAsync(lobbyName, maxPlayers, isPrivate));

        public void Join() => Run(() => coordinator.StartClientFlowAsync(lobbyCode));

        public void LeaveLobby() => Run(coordinator.LeaveLobbyAsync);

        private void HandleStatusChanged(MultiplayerStatus status)
        {
            var isBusy = BusyStatuses.Contains(status);

            if (busyRoot != null)
            {
                busyRoot.SetActive(isBusy);
            }

            if (disableWhileBusy != null)
            {
                for (var i = 0; i < disableWhileBusy.Count; i++)
                {
                    var selectable = disableWhileBusy[i];
                    if (selectable != null)
                    {
                        selectable.interactable = !isBusy;
                    }
                }
            }
        }

        private static void Run(Func<Task> action)
        {
            // Intentionally async void: this is the Unity UI callback boundary.
            _ = RunAsync(action);
        }

        private static async Task RunAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
