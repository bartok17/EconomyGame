using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Handles lobby creation form: name, max players, variant, privacy.
    /// Validates input and passes config to MultiplayerUiCommands.Host().
    /// </summary>
    public class LobbyCreatorPresenter : MonoBehaviour
    {
        private static readonly string[] MaxPlayerOptions = { "2 Players", "3 Players", "4 Players" };
        private static readonly string[] VariantOptions = { "Classic", "SpeedRules" };
        private static readonly string[] PrivacyOptions = { "Public", "Private" };

        [SerializeField] private TMP_InputField gameNameInput;
        [SerializeField] private TMPro.TMP_Dropdown maxPlayersDropdown;
        [SerializeField] private TMPro.TMP_Dropdown variantDropdown;
        [SerializeField] private TMPro.TMP_Dropdown privacyDropdown;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button createButton;
        [SerializeField] private TextMeshProUGUI errorText;

        [SerializeField] private MultiplayerUiCommands uiCommands;

        private void OnEnable()
        {
            InitializeDropdownOptions();

            if (gameNameInput != null)
                gameNameInput.onValueChanged.AddListener(OnFormValueChanged);

            if (maxPlayersDropdown != null)
                maxPlayersDropdown.onValueChanged.AddListener(OnFormValueChanged);

            if (privacyDropdown != null)
                privacyDropdown.onValueChanged.AddListener(OnPrivacyToggled);

            if (createButton != null)
                createButton.onClick.AddListener(HandleCreateLobby);

            if (gameNameInput != null)
                gameNameInput.ActivateInputField();

            ValidateForm();
        }

        private void InitializeDropdownOptions()
        {
            if (maxPlayersDropdown != null)
            {
                maxPlayersDropdown.ClearOptions();
                maxPlayersDropdown.AddOptions(new System.Collections.Generic.List<string>(MaxPlayerOptions));
                maxPlayersDropdown.value = Mathf.Clamp(maxPlayersDropdown.value, 0, MaxPlayerOptions.Length - 1);
            }

            if (variantDropdown != null)
            {
                variantDropdown.ClearOptions();
                variantDropdown.AddOptions(new System.Collections.Generic.List<string>(VariantOptions));
                variantDropdown.value = Mathf.Clamp(variantDropdown.value, 0, VariantOptions.Length - 1);
            }

            if (privacyDropdown != null)
            {
                privacyDropdown.ClearOptions();
                privacyDropdown.AddOptions(new System.Collections.Generic.List<string>(PrivacyOptions));
                privacyDropdown.value = Mathf.Clamp(privacyDropdown.value, 0, PrivacyOptions.Length - 1);
            }
        }

        private void OnDisable()
        {
            if (gameNameInput != null)
                gameNameInput.onValueChanged.RemoveListener(OnFormValueChanged);

            if (maxPlayersDropdown != null)
                maxPlayersDropdown.onValueChanged.RemoveListener(OnFormValueChanged);

            if (privacyDropdown != null)
                privacyDropdown.onValueChanged.RemoveListener(OnPrivacyToggled);

            if (createButton != null)
                createButton.onClick.RemoveListener(HandleCreateLobby);
        }

        private void OnFormValueChanged(string _)
        {
            ValidateForm();
        }

        private void OnFormValueChanged(int _)
        {
            ValidateForm();
        }

        private void OnPrivacyToggled(int privacyIndex)
        {
            bool isPrivate = IsPrivateSelection(privacyIndex);

            if (passwordInput != null)
                passwordInput.gameObject.SetActive(isPrivate);

            ValidateForm();
        }

        private bool IsPrivateSelection(int privacyIndex)
        {
            return privacyIndex == 1;
        }

        private void ValidateForm()
        {
            string gameName = gameNameInput != null ? gameNameInput.text.Trim() : "";
            bool isPrivate = privacyDropdown != null ? IsPrivateSelection(privacyDropdown.value) : false;
            string password = passwordInput != null ? passwordInput.text.Trim() : "";

            bool isValid = gameName.Length >= 1 && gameName.Length <= 32;

            if (isPrivate && password.Length < 1)
                isValid = false;

            if (createButton != null)
                createButton.interactable = isValid;

            if (errorText != null)
            {
                if (!isValid)
                {
                    if (gameName.Length == 0)
                        errorText.text = "Game name required";
                    else if (gameName.Length > 32)
                        errorText.text = "Game name too long (max 32 chars)";
                    else if (isPrivate && password.Length == 0)
                        errorText.text = "Password required for private games";
                    else
                        errorText.text = "Invalid form";
                }
                else
                {
                    errorText.text = "";
                }
            }
        }

        private void HandleCreateLobby()
        {
            string gameName = gameNameInput != null ? gameNameInput.text.Trim() : "My Game";
            int maxPlayersIndex = maxPlayersDropdown != null ? maxPlayersDropdown.value : 3;
            int maxPlayers = new int[] { 2, 3, 4 }[maxPlayersIndex];

            int variantIndex = variantDropdown != null ? variantDropdown.value : 0;
            string variant = VariantOptions[variantIndex];

            bool isPrivate = privacyDropdown != null ? IsPrivateSelection(privacyDropdown.value) : false;
            string password = passwordInput != null ? passwordInput.text.Trim() : "";

            var config = new LobbyConfig(gameName, maxPlayers, variant, isPrivate, password);

            if (!config.IsValid())
            {
                Debug.LogError($"[LobbyCreator] Invalid config: {config}");
                return;
            }

            Debug.Log($"[LobbyCreator] Creating lobby: {config}");

            if (uiCommands != null)
            {
                uiCommands.SetLobbyName(gameName);
                uiCommands.SetMaxPlayers(maxPlayers);
                uiCommands.SetIsPrivate(isPrivate);
                uiCommands.Host();
            }
        }

        /// <summary>
        /// Reset form when panel is shown.
        /// </summary>
        public void ResetForm()
        {
            if (gameNameInput != null)
            {
                gameNameInput.text = "";
                gameNameInput.ActivateInputField();
            }

            if (maxPlayersDropdown != null)
                maxPlayersDropdown.value = 2;

            if (variantDropdown != null)
                variantDropdown.value = 0;

            if (privacyDropdown != null)
                privacyDropdown.value = 0;

            if (passwordInput != null)
            {
                passwordInput.text = "";
                passwordInput.gameObject.SetActive(false);
            }

            ValidateForm();
        }
    }
}
