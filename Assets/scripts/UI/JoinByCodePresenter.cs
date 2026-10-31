using MonopolyGame.DesignSystem;
using MonopolyGame.UI.DesignSystem;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.UI
{
    public class JoinByCodePresenter : MonoBehaviour
    {
        [SerializeField] private TMP_InputField codeInputField;
        [SerializeField] private Button joinButton;
        [SerializeField] private TextMeshProUGUI instructionText;

        [SerializeField] private MultiplayerUiCommands uiCommands;

        private ThemedText _instructionThemed;

        private const int CodeLength = 6;

        private void OnEnable()
        {
            _instructionThemed = instructionText != null ? instructionText.GetComponent<ThemedText>() : null;

            if (codeInputField != null)
            {
                codeInputField.onValueChanged.AddListener(OnCodeInputChanged);
            }

            if (joinButton != null)
            {
                joinButton.onClick.AddListener(HandleJoinByCode);
            }

            if (codeInputField != null)
            {
                codeInputField.ActivateInputField();
            }

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
            string formatted = newValue.ToUpper();
            formatted = System.Text.RegularExpressions.Regex.Replace(formatted, "[^A-Z0-9]", "");

            if (formatted.Length > CodeLength)
                formatted = formatted.Substring(0, CodeLength);

            if (formatted != codeInputField.text)
            {
                codeInputField.text = formatted;
            }

            bool isValid = formatted.Length == CodeLength;
            if (joinButton != null)
                joinButton.interactable = isValid;

            if (instructionText != null)
            {
                instructionText.text = isValid ? "✓ Code ready" : "Enter lobby code";
            }

            _instructionThemed?.SetTextStyle(isValid ? TextStyle.Success : TextStyle.Caption);
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
