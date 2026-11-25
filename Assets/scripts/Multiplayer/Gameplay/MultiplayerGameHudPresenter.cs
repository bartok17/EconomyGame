using System.Collections;
using System.Collections.Generic;
using MonopolyGame.Multiplayer.Camera;
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
        [SerializeField] private TextMeshProUGUI roomCodeText;

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
        [SerializeField] private Button leaveButton;

        private Button payButton;

        [Header("Overlays")]
        [SerializeField] private GameObject cardInspectionPanel;

        [Header("Action Cards")]
        [SerializeField] private ActionCardHudPresenter actionCardHud;
        [SerializeField] private ActionCardActivationPresenter actionCardActivation;
        [SerializeField] private BoardFocusCoordinator boardFocusCoordinator;

        private readonly List<PlayerHudEntry> playerEntries = new();
        private bool isSessionBound;

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
                session = FindAnyObjectByType<MultiplayerGameSessionController>();
                yield return null;
            }

            ResolvePlayerEntries();
            EnsurePayButton();
            EnsureLeaveButton();
            BindSession();
            RefreshFromSession();
        }

        public void BindSession(MultiplayerGameSessionController sessionController)
        {
            session = sessionController;
            actionCardHud?.BindSession(sessionController);
            actionCardActivation?.BindSession(sessionController, boardFocusCoordinator);

            if (actionCardHud != null)
            {
                actionCardHud.CardActivated -= HandleCardActivated;
                actionCardHud.CardActivated += HandleCardActivated;
            }
        }

        private void HandleCardActivated(ActionCardHandEntry entry)
        {
            if (actionCardActivation != null)
            {
                actionCardActivation.Open(entry);
            }
        }

        public void BindFocusCoordinator(BoardFocusCoordinator coordinator)
        {
            boardFocusCoordinator = coordinator;
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

        // Other players confirm their own payments, so the bar needs a Pay button.
        // Cloning Buy inherits its themed styling and layout without new UI assets.
        private void EnsurePayButton()
        {
            if (payButton != null || buyButton == null)
            {
                return;
            }

            GameObject clone = Instantiate(buyButton.gameObject, buyButton.transform.parent);
            clone.name = "PayButton";
            clone.transform.SetSiblingIndex(buyButton.transform.GetSiblingIndex() + 1);

            payButton = clone.GetComponent<Button>();

            if (payButton != null)
            {
                payButton.onClick.RemoveAllListeners();
            }

            TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>();

            if (label != null)
            {
                label.text = "Pay";
            }
        }
        
        private void EnsureLeaveButton()
        {
            if (leaveButton != null)
            {
                return;
            }

            Transform found = transform.Find("LeaveButton") ??
                              transform.Find("ActionBar/LeaveButton") ??
                              transform.Find("TopBar/LeaveButton");
            if (found != null)
            {
                leaveButton = found.GetComponent<Button>();
                return;
            }

            if (endTurnButton != null && endTurnButton.transform.parent != null)
            {
                GameObject clone = Instantiate(endTurnButton.gameObject, endTurnButton.transform.parent);
                clone.name = "LeaveButton";
                leaveButton = clone.GetComponent<Button>();

                if (leaveButton != null)
                {
                    leaveButton.onClick.RemoveAllListeners();
                    leaveButton.interactable = true;
                }

                TextMeshProUGUI label = clone.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = "Leave";
                }
            }
        }

        private void BindSession()
        {
            if (session == null || isSessionBound)
            {
                return;
            }

            isSessionBound = true;

            session.PhaseChanged += HandlePhaseChanged;
            session.TurnChanged += HandleTurnChanged;
            session.DiceRolled += HandleDiceRolled;
            session.EconomyChanged += HandleEconomyChanged;
            session.CardResolutionChanged += HandleCardResolutionChanged;
            session.PaymentsChanged += HandlePaymentsChanged;
            session.ParticipantsChanged += HandleParticipantsChanged;

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

            if (payButton != null)
            {
                payButton.onClick.AddListener(HandlePayClicked);
            }
            
            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(HandleLeaveClicked);
            }
        }

        private void UnbindSession()
        {
            isSessionBound = false;

            if (session != null)
            {
                session.PhaseChanged -= HandlePhaseChanged;
                session.TurnChanged -= HandleTurnChanged;
                session.DiceRolled -= HandleDiceRolled;
                session.EconomyChanged -= HandleEconomyChanged;
                session.CardResolutionChanged -= HandleCardResolutionChanged;
                session.PaymentsChanged -= HandlePaymentsChanged;
                session.ParticipantsChanged -= HandleParticipantsChanged;
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

            if (payButton != null)
            {
                payButton.onClick.RemoveListener(HandlePayClicked);
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.RemoveListener(HandleLeaveClicked);
            }

            actionCardHud?.BindSession(null);

            if (actionCardHud != null)
            {
                actionCardHud.CardActivated -= HandleCardActivated;
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

        private void HandlePayClicked()
        {
            if (session != null)
            {
                session.RequestPayPendingPayment();
            }
        }
        
        private void HandleLeaveClicked()
        {
            if (session != null)
            {
                session.RequestLeaveGame();
            }
        }

        private void HandleParticipantsChanged()
        {
            RefreshDashboard();
            UpdateControls();
        }

        private void HandlePaymentsChanged()
        {
            UpdateControls();
            UpdatePhaseText();
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

        private void HandleCardResolutionChanged()
        {
            UpdateControls();
            UpdatePhaseText();
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

            if (roomCodeText != null)
            {
                string code = session.GetRoomCode();
                roomCodeText.text = string.IsNullOrEmpty(code) ? string.Empty : $"Room: {code}";
            }
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

                ParticipantSessionStatus status = session.GetParticipantStatus(state.PawnSlot);
                playerEntries[entryIndex].Apply(displayName, state.Balance, isActivePlayer, status);
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
            string roomCode = session != null ? session.GetRoomCode() : string.Empty;
            string roomSuffix = string.IsNullOrWhiteSpace(roomCode) ? string.Empty : $"  ·  Room: {roomCode}";
            turnText.text = $"Turn {turnIndex + 1} — {playerName}{roomSuffix}";
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
            bool resolvingCard = session.IsResolvingCardAction;
            bool paying = session.HasPendingPayment;
            bool canRoll = isMyTurn && !resolvingCard && !paying && session.CurrentPhase == TurnPhase.AwaitingRoll;
            bool canEnd = isMyTurn && !resolvingCard && !paying && session.CurrentPhase == TurnPhase.WaitingForEndTurn;

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

            if (payButton != null)
            {
                payButton.interactable = session.CanPayPendingPayment;

                TextMeshProUGUI payLabel = payButton.GetComponentInChildren<TextMeshProUGUI>();
                if (payLabel != null)
                {
                    payLabel.text = session.GetPayButtonLabel();
                }
            }
        }
    }
}
