using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// One row in the remembered-accounts list. Follows the
    /// LobbyListItemController conventions (SetData + events, AddListener in
    /// OnEnable / RemoveListener in OnDisable).
    /// </summary>
    public sealed class RememberedAccountItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI displayNameText;
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private Button useButton;
        [SerializeField] private Button forgetButton;

        private RememberedAccount _account;

        public event Action<RememberedAccount> UseClicked;
        public event Action<RememberedAccount> ForgetClicked;

        private void OnEnable()
        {
            if (useButton != null)
            {
                useButton.onClick.AddListener(HandleUseClick);
            }

            if (forgetButton != null)
            {
                forgetButton.onClick.AddListener(HandleForgetClick);
            }
        }

        private void OnDisable()
        {
            if (useButton != null)
            {
                useButton.onClick.RemoveListener(HandleUseClick);
            }

            if (forgetButton != null)
            {
                forgetButton.onClick.RemoveListener(HandleForgetClick);
            }
        }

        public void SetAccount(RememberedAccount account)
        {
            _account = account;

            if (displayNameText != null)
            {
                displayNameText.text = account.displayName;
            }

            if (usernameText != null)
            {
                usernameText.text = account.username;
            }
        }

        public void SetInteractable(bool interactable)
        {
            if (useButton != null)
            {
                useButton.interactable = interactable;
            }

            if (forgetButton != null)
            {
                forgetButton.interactable = interactable;
            }
        }

        private void HandleUseClick()
        {
            if (_account != null)
            {
                UseClicked?.Invoke(_account);
            }
        }

        private void HandleForgetClick()
        {
            if (_account != null)
            {
                ForgetClicked?.Invoke(_account);
            }
        }
    }
}
