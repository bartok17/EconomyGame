using TMPro;
using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    public sealed class MultiplayerErrorPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject errorRoot;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private bool clearOnEnable = true;

        private void OnEnable()
        {
            if (clearOnEnable)
            {
                ClearError();
            }
        }

        public void ShowErrorMessage(string message)
        {
            if (errorRoot != null)
            {
                errorRoot.SetActive(true);
            }

            if (errorText != null)
            {
                errorText.text = string.IsNullOrWhiteSpace(message)
                    ? "Something went wrong. Please try again."
                    : message;
            }
        }

        public void ShowError(MultiplayerError error)
        {
            if (error == null)
            {
                ShowErrorMessage("Unknown multiplayer error.");
                return;
            }

            ShowErrorMessage(error.Message);
        }

        public void ClearError()
        {
            if (errorText != null)
            {
                errorText.text = string.Empty;
            }

            if (errorRoot != null)
            {
                errorRoot.SetActive(false);
            }
        }
    }
}
