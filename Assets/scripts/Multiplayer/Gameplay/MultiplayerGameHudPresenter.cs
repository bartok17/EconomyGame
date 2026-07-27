using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class MultiplayerGameHudPresenter : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MultiplayerGameSessionController session;

        [Header("Auto Layout")]
        [SerializeField] private bool buildDefaultLayout = true;

        [Header("Optional Scene UI")]
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private EventSystem eventSystem;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI diceText;
        [SerializeField] private TextMeshProUGUI balanceText;
        [SerializeField] private TextMeshProUGUI propertyText;
        [SerializeField] private TextMeshProUGUI ownerText;
        [SerializeField] private TextMeshProUGUI economyMessageText;
        [SerializeField] private TextMeshProUGUI hostHintText;
        [SerializeField] private Button rollButton;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button buyButton;

        private void OnEnable()
        {
            StartCoroutine(BindWhenReady());
        }

        private void OnDisable()
        {
            UnbindSession();
        }

        private IEnumerator BindWhenReady()
        {
            while (session == null)
            {
                yield return null;
            }

            if (buildDefaultLayout)
            {
                EnsureLayout();
            }

            BindSession();
            RefreshFromSession();
        }

        public void BindSession(MultiplayerGameSessionController sessionController)
        {
            session = sessionController;
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

        private void EnsureLayout()
        {
            if (titleText != null && turnText != null && phaseText != null &&
                diceText != null && balanceText != null && propertyText != null &&
                ownerText != null && economyMessageText != null && hostHintText != null &&
                rollButton != null && endTurnButton != null && buyButton != null)
            {
                return;
            }

            GameHudLayoutBuilder builder = new GameHudLayoutBuilder();
            GameHudLayoutBuilder.BuildResult result = builder.Build(styleSheet: null);

            rootCanvas = result.RootCanvas;
            eventSystem = result.EventSystem;
            titleText = result.TitleText;
            turnText = result.TurnText;
            phaseText = result.PhaseText;
            diceText = result.DiceText;
            balanceText = result.BalanceText;
            propertyText = result.PropertyText;
            ownerText = result.OwnerText;
            economyMessageText = result.EconomyMessageText;
            hostHintText = result.HostHintText;
            rollButton = result.RollButton;
            endTurnButton = result.EndTurnButton;
            buyButton = result.BuyButton;
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

        private void HandlePhaseChanged(TurnPhase phase)
        {
            if (phaseText != null)
            {
                phaseText.text = $"Phase: {session.GetPhaseLabel()}";
            }

            RefreshEconomyTexts();
            UpdateControls();
        }

        private void HandleTurnChanged(int turnIndex, string activePlayerName)
        {
            if (turnText != null)
            {
                turnText.text = $"Turn: {turnIndex + 1} - {activePlayerName}";
            }

            RefreshEconomyTexts();
            UpdateControls();
        }

        private void HandleDiceRolled(int diceValue)
        {
            if (diceText != null)
            {
                diceText.text = $"Dice: {diceValue}";
            }
        }
        
        private void HandleEconomyChanged()
        {
            RefreshEconomyTexts();
            UpdateControls();
        }

        private void RefreshFromSession()
        {
            if (session == null)
            {
                return;
            }

            HandleTurnChanged(session.CurrentTurnIndex, string.IsNullOrWhiteSpace(session.ActivePlayerName) ? "-" : session.ActivePlayerName);
            HandlePhaseChanged(session.CurrentPhase);
            HandleDiceRolled(session.LastDiceRoll);
            RefreshEconomyTexts();

            if (hostHintText != null)
            {
                hostHintText.gameObject.SetActive(!session.IsHostAuthority);
            }

            UpdateControls();
        }

        private void UpdateControls()
        {
            if (session == null)
            {
                if (rollButton != null) rollButton.interactable = false;
                if (endTurnButton != null) endTurnButton.interactable = false;
                return;
            }

            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            bool isMyTurn = session.IsInitialized && session.ActiveClientId == localClientId;
            bool canRoll = isMyTurn && session.CurrentPhase == TurnPhase.AwaitingRoll;
            bool canEnd = isMyTurn && session.CurrentPhase == TurnPhase.WaitingForEndTurn;

            if (rollButton != null)
            {
                rollButton.gameObject.SetActive(true);
                rollButton.interactable = canRoll;
            }

            if (endTurnButton != null)
            {
                endTurnButton.gameObject.SetActive(true);
                endTurnButton.interactable = canEnd;
            }

            if (hostHintText != null)
            {
                hostHintText.text = isMyTurn
                    ? "Your turn!"
                    : $"Waiting for {session.ActivePlayerName}...";
            }
            
            if (buyButton != null)
            {
                buyButton.gameObject.SetActive(true);
                buyButton.interactable = session.CanBuyCurrentProperty();
                
                TextMeshProUGUI buyLabel = buyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buyLabel != null)
                {
                    buyLabel.text = session.GetBuyButtonLabel();
                }
            }
        }
        
        private void HandleBuyClicked()
        {
            if (session != null)
            {
                session.RequestBuyCurrentProperty();
            }
        }
        
        private void RefreshEconomyTexts()
        {
            if (session == null)
            {
                return;
            }

            if (balanceText != null)
            {
                balanceText.text = $"Balance: {session.GetLocalPlayerBalance()}";
            }

            if (propertyText != null)
            {
                propertyText.text = $"Property: {session.GetCurrentPropertyLabel()}";
            }

            if (ownerText != null)
            {
                ownerText.text = session.GetCurrentPropertyOwnerLabel();
            }
            
            if (economyMessageText != null)
            {
                economyMessageText.text = session.GetLastEconomyMessage();
            }
        }
    }
}
