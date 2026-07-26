using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Handles join-by-code UI input and validation.
    /// Converts code input to lobby join request.
    /// </summary>
    public class JoinByCodePresenter : MonoBehaviour
    {
        [SerializeField] private TMP_InputField codeInputField;
        [SerializeField] private Button joinButton;
        [SerializeField] private TextMeshProUGUI instructionText;

        [SerializeField] private MultiplayerUiCommands uiCommands;

        private const int CodeLength = 6;

        private void OnEnable()
        {
            if (codeInputField != null)
            {
                codeInputField.onValueChanged.AddListener(OnCodeInputChanged);
            }

            if (joinButton != null)
            {
                joinButton.onClick.AddListener(HandleJoinByCode);
            }

            // Focus input field when panel shows
            if (codeInputField != null)
            {
                codeInputField.ActivateInputField();
            }

            // Ensure UI state is consistent when the panel becomes active
            OnCodeInputChanged(codeInputField != null ? codeInputField.text : string.Empty);
        }

        private void OnDisable()
        {
            if (codeInputField != null)
                codeInputField.onValueChanged.RemoveListener(OnCodeInputChanged);

            if (joinButton != null)
                joinButton.onClick.RemoveListener(HandleJoinByCode);
        }

        private void OnCodeInputChanged(string newValue)
        {
            // Auto-format to uppercase
            string formatted = newValue.ToUpper();

            // Remove non-alphanumeric characters
            formatted = System.Text.RegularExpressions.Regex.Replace(formatted, "[^A-Z0-9]", "");

            // Limit to code length
            if (formatted.Length > CodeLength)
                formatted = formatted.Substring(0, CodeLength);

            // Update field if changed
            if (formatted != codeInputField.text)
            {
                codeInputField.text = formatted;
            }

            // Update join button state
            bool isValid = formatted.Length == CodeLength;
            if (joinButton != null)
                joinButton.interactable = isValid;

            // Update instruction text
            if (instructionText != null)
            {
                if (isValid)
                    instructionText.text = $"✓ Code ready";
                else
                    instructionText.text = $"Enter lobby code";
            }
        }

        private void HandleJoinByCode()
        {
            string code = codeInputField.text.Trim();

            if (code.Length != CodeLength)
            {
                Debug.LogWarning($"[JoinByCode] Invalid code length: {code.Length}");
                return;
            }

            if (uiCommands != null)
            {
                uiCommands.Join(code);
            }
        }

        /// <summary>
        /// Reset input field when panel is shown.
        /// </summary>
        public void ResetInput()
        {
            if (codeInputField == null)
                return;

            codeInputField.text = "";
            codeInputField.ActivateInputField();
            OnCodeInputChanged(string.Empty);
        }
    }
}
