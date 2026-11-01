using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class MultiplayerGameHudPresenter : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MultiplayerGameSessionController session;

        [Header("Top Bar")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI diceText;
        [SerializeField] private TextMeshProUGUI turnHintText;

        [Header("Local Player")]
        [SerializeField] private TextMeshProUGUI localPlayerNameText;
        [SerializeField] private TextMeshProUGUI balanceText;

        [Header("Current Field")]
        [SerializeField] private TextMeshProUGUI propertyText;
        [SerializeField] private TextMeshProUGUI propertyDetailsText;
        [SerializeField] private TextMeshProUGUI ownerText;

        [Header("Economy Message")]
        [SerializeField] private TextMeshProUGUI economyMessageText;
        [SerializeField] private GameObject economyMessagePanel;

        [Header("Player Rail")]
        [SerializeField] private Transform playerRailRoot;

        [Header("Actions")]
        [SerializeField] private Button rollButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button buyButton;

        [Header("Overlays")]
        [SerializeField] private GameObject cardInspectionPanel;

        private readonly List<PlayerHudEntry> playerEntries = new();

        private void OnEnable()
        {
            StartCoroutine(BindWhenReady());
        }

        private void OnDisable()
        {
            UnbindSession();
            playerEntries.Clear();
        }

        private IEnumerator BindWhenReady()
        {
            while (session == null)
            {
                yield return null;
            }

            ResolvePlayerEntries();
            BindSession();
            RefreshFromSession();
        }

        public void BindSession(MultiplayerGameSessionController sessionController)
        {
            session = sessionController;
        }

        public void ShowCardInspection()
        {
            if (cardInspectionPanel != null)
            {
                cardInspectionPanel.SetActive(true);
            }
        }

        public void HideCardInspection()
        {
            if (cardInspectionPanel != null)
            {
                cardInspectionPanel.SetActive(false);
            }
        }

        private void ResolvePlayerEntries()
        {
            playerEntries.Clear();

            if (playerRailRoot == null)
            {
                return;
            }

            playerEntries.AddRange(playerRailRoot.GetComponentsInChildren<PlayerHudEntry>(includeInactive: true));
        }

        private void BindSession()
        {
            if (session == null)
            {
                return;
            }

            session.PhaseChanged += HandlePhaseChanged;
            session.TurnChanged += HandleTurnChanged;
            session.DiceRolled += HandleDiceRolled;
            session.EconomyChanged += HandleEconomyChanged;

            if (rollButton != null)
            {
                rollButton.onClick.AddListener(HandleRollClicked);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(HandleEndTurnClicked);
            }

            if (buyButton != null)
            {
                buyButton.onClick.AddListener(HandleBuyClicked);
            }
        }

        private void UnbindSession()
        {
            if (session != null)
            {
                session.PhaseChanged -= HandlePhaseChanged;
                session.TurnChanged -= HandleTurnChanged;
                session.DiceRolled -= HandleDiceRolled;
                session.EconomyChanged -= HandleEconomyChanged;
            }

            if (rollButton != null)
            {
                rollButton.onClick.RemoveListener(HandleRollClicked);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(HandleEndTurnClicked);
            }

            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(HandleBuyClicked);
            }
        }

        private void HandleRollClicked()
        {
            if (session != null)
            {
                session.RequestRoll();
            }
        }

        private void HandleEndTurnClicked()
        {
            if (session != null)
            {
                session.RequestEndTurn();
            }
        }

        private void HandleBuyClicked()
        {
            if (session != null)
            {
                session.RequestBuyCurrentProperty();
            }
        }

        private void HandlePhaseChanged(TurnPhase phase)
        {
            UpdatePhaseText();
            RefreshDashboard();
            UpdateControls();
        }

        private void HandleTurnChanged(int turnIndex, string activePlayerName)
        {
            UpdateTurnText(turnIndex, activePlayerName);
            RefreshDashboard();
            UpdateControls();
        }

        private void HandleDiceRolled(int diceValue)
        {
            UpdateDiceText(diceValue);
        }

        private void HandleEconomyChanged()
        {
            RefreshDashboard();
            UpdateControls();
        }

        private void RefreshFromSession()
        {
            if (session == null)
            {
                return;
            }

            UpdateTurnText(session.CurrentTurnIndex, session.ActivePlayerName);
            UpdatePhaseText();
            UpdateDiceText(session.LastDiceRoll);
            RefreshDashboard();
            UpdateControls();
        }

        private void RefreshDashboard()
        {
            if (session == null)
            {
                return;
            }

            if (balanceText != null)
            {
                balanceText.text = session.GetLocalPlayerBalance().ToString("N0");
            }

            if (propertyText != null)
            {
                string propertyName = session.GetCurrentPropertyName();
                propertyText.text = string.IsNullOrEmpty(propertyName) ? "No field resolved" : propertyName;
            }

            if (propertyDetailsText != null)
            {
                propertyDetailsText.text = session.GetCurrentPropertyDetails();
            }

            if (ownerText != null)
            {
                ownerText.text = $"Owner: {session.GetCurrentPropertyOwnerName()}";
            }

            RefreshEconomyMessage();
            RefreshPlayerRail();
        }

        private void RefreshEconomyMessage()
        {
            bool hasMessage = session.HasEconomyMessage();

            if (economyMessageText != null)
            {
                economyMessageText.text = hasMessage ? session.GetLastEconomyMessage() : string.Empty;
            }

            if (economyMessagePanel != null)
            {
                economyMessagePanel.SetActive(hasMessage);
            }
        }

        private void RefreshPlayerRail()
        {
            int localSlot = session.GetLocalPawnSlot();
            IReadOnlyList<PlayerEconomyState> states = session.GetPlayerEconomyStates();
            int entryIndex = 0;

            for (int i = 0; i < states.Count; i++)
            {
                PlayerEconomyState state = states[i];
                string displayName = state.DisplayName.ToString();

                if (state.PawnSlot == localSlot)
                {
                    if (localPlayerNameText != null)
                    {
                        localPlayerNameText.text = string.IsNullOrEmpty(displayName) ? "Player" : displayName;
                    }

                    continue;
                }

                if (entryIndex >= playerEntries.Count)
                {
                    continue;
                }

                bool isActivePlayer = !string.IsNullOrEmpty(session.ActivePlayerName) &&
                                      session.ActivePlayerName == displayName;

                playerEntries[entryIndex].Apply(displayName, state.Balance, isActivePlayer);
                playerEntries[entryIndex].SetVisible(true);
                entryIndex++;
            }

            if (localPlayerNameText != null && string.IsNullOrEmpty(localPlayerNameText.text))
            {
                localPlayerNameText.text = "Player";
            }

            for (; entryIndex < playerEntries.Count; entryIndex++)
            {
                playerEntries[entryIndex].SetVisible(false);
            }
        }

        private void UpdateTurnText(int turnIndex, string activePlayerName)
        {
            if (turnText == null)
            {
                return;
            }

            string playerName = string.IsNullOrWhiteSpace(activePlayerName) ? "Waiting" : activePlayerName;
            turnText.text = $"Turn {turnIndex + 1} — {playerName}";
        }

        private void UpdatePhaseText()
        {
            if (phaseText != null)
            {
                phaseText.text = session != null ? session.GetPhaseLabel() : string.Empty;
            }
        }

        private void UpdateDiceText(int diceValue)
        {
            if (diceText != null)
            {
                diceText.text = diceValue > 0 ? diceValue.ToString() : "–";
            }
        }

        private void UpdateControls()
        {
            if (session == null)
            {
                if (rollButton != null) rollButton.interactable = false;
                if (endTurnButton != null) endTurnButton.interactable = false;
                if (buyButton != null) buyButton.interactable = false;
                return;
            }

            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            bool isMyTurn = session.IsInitialized && session.ActiveClientId == localClientId;
            bool canRoll = isMyTurn && session.CurrentPhase == TurnPhase.AwaitingRoll;
            bool canEnd = isMyTurn && session.CurrentPhase == TurnPhase.WaitingForEndTurn;

            if (rollButton != null)
            {
                rollButton.interactable = canRoll;
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = canEnd;
            }

            if (turnHintText != null)
            {
                turnHintText.text = isMyTurn
                    ? "Your turn"
                    : string.IsNullOrWhiteSpace(session.ActivePlayerName)
                        ? "Waiting for players…"
                        : $"Waiting for {session.ActivePlayerName}…";
            }

            if (buyButton != null)
            {
                buyButton.interactable = session.CanBuyCurrentProperty();

                TextMeshProUGUI buyLabel = buyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buyLabel != null)
                {
                    buyLabel.text = session.GetBuyButtonLabel();
                }
            }
        }
    }
}
