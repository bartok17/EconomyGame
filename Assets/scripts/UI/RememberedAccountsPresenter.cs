using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MonopolyGame.Multiplayer.UI
{
    /// <summary>
    /// Renders the "Remembered accounts" section of the auth panel:
    /// one row per account with Use / Forget buttons.
    /// Follows the LobbyBrowserPresenter conventions (serialized view refs,
    /// OnEnable/OnDisable subscriptions, code-built item rows).
    /// </summary>
    public sealed class RememberedAccountsPresenter : MonoBehaviour
    {
        [SerializeField] private MultiplayerFlowCoordinator coordinator;
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private Transform itemContainer;
        [SerializeField] private GameObject sectionRoot;

        private readonly List<RememberedAccountItem> _activeItems = new();

        private void OnEnable()
        {
            if (coordinator == null)
            {
                Debug.LogError("[RememberedAccountsPresenter] MultiplayerFlowCoordinator not assigned. Wire it in the inspector.");
                enabled = false;
                return;
            }

            coordinator.StatusChanged += HandleStatusChanged;
            coordinator.SignedIn += HandleSignedIn;
            coordinator.RememberedAccountsChanged += HandleAccountsChanged;

            Render(coordinator.RememberedAccounts);
        }

        private void OnDisable()
        {
            if (coordinator == null)
            {
                return;
            }

            coordinator.StatusChanged -= HandleStatusChanged;
            coordinator.SignedIn -= HandleSignedIn;
            coordinator.RememberedAccountsChanged -= HandleAccountsChanged;
        }

        private void HandleStatusChanged(MultiplayerStatus status)
        {
            SetItemsInteractable(status != MultiplayerStatus.SigningIn);
            UpdateVisibility();
        }

        private void HandleSignedIn(string playerId, string displayName)
        {
            UpdateVisibility();
        }

        private void HandleAccountsChanged(IReadOnlyList<RememberedAccount> accounts)
        {
            Render(accounts);
        }

        private void Render(IReadOnlyList<RememberedAccount> accounts)
        {
            DestroyActiveItems();

            if (accounts == null || accounts.Count == 0 || itemPrefab == null || itemContainer == null)
            {
                UpdateVisibility();
                return;
            }

            foreach (var account in accounts)
            {
                var itemController = Instantiate(itemPrefab, itemContainer).GetComponent<RememberedAccountItem>();
                if (itemController == null)
                {
                    continue;
                }

                itemController.SetAccount(account);
                itemController.UseClicked += HandleUseClicked;
                itemController.ForgetClicked += HandleForgetClicked;
                _activeItems.Add(itemController);
            }

            UpdateVisibility();
        }

        private void DestroyActiveItems()
        {
            foreach (var item in _activeItems)
            {
                if (item == null)
                {
                    continue;
                }

                item.UseClicked -= HandleUseClicked;
                item.ForgetClicked -= HandleForgetClicked;
                Destroy(item.gameObject);
            }

            _activeItems.Clear();
        }

        private void UpdateVisibility()
        {
            if (sectionRoot == null)
            {
                return;
            }

            var isVisible = coordinator != null
                            && coordinator.Status == MultiplayerStatus.SignedOut
                            && _activeItems.Count > 0;

            sectionRoot.SetActive(isVisible);
        }

        private void SetItemsInteractable(bool interactable)
        {
            foreach (var item in _activeItems)
            {
                if (item != null)
                {
                    item.SetInteractable(interactable);
                }
            }
        }

        private void HandleUseClicked(RememberedAccount account)
        {
            Debug.Log($"[RememberedAccounts] Use clicked for: {account.username}");
            Run(async () => await coordinator.TrySignInRememberedAsync(account.id));
        }

        private void HandleForgetClicked(RememberedAccount account)
        {
            Debug.Log($"[RememberedAccounts] Forget clicked for: {account.username}");
            coordinator.ForgetRememberedAccount(account.id);
        }

        private static void Run(Func<Task> action)
        {
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
