using System.Collections;
using MonopolyGame.Multiplayer.Gameplay;
using TMPro;
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

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI diceText;
        [SerializeField] private TextMeshProUGUI hostHintText;
        [SerializeField] private Button rollButton;
        [SerializeField] private Button endTurnButton;

        private Canvas rootCanvas;

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
                session = FindAnyObjectByType<MultiplayerGameSessionController>();
                yield return null;
            }

            if (buildDefaultLayout)
            {
                EnsureLayout();
            }

            BindSession();
            RefreshFromSession();
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

            if (rollButton != null)
            {
                rollButton.onClick.AddListener(HandleRollClicked);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(HandleEndTurnClicked);
            }
        }

        private void UnbindSession()
        {
            if (session != null)
            {
                session.PhaseChanged -= HandlePhaseChanged;
                session.TurnChanged -= HandleTurnChanged;
                session.DiceRolled -= HandleDiceRolled;
            }

            if (rollButton != null)
            {
                rollButton.onClick.RemoveListener(HandleRollClicked);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(HandleEndTurnClicked);
            }
        }

        private void EnsureLayout()
        {
            if (rootCanvas == null)
            {
                rootCanvas = FindAnyObjectByType<Canvas>();
            }

            if (rootCanvas == null)
            {
                GameObject canvasObject = new GameObject("GameHudCanvas");
                rootCanvas = canvasObject.AddComponent<Canvas>();
                rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                rootCanvas.sortingOrder = 100;
                canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }

            if (titleText == null || turnText == null || phaseText == null || diceText == null || hostHintText == null || rollButton == null || endTurnButton == null)
            {
                BuildDefaultHud(rootCanvas.transform);
            }
        }

        private void BuildDefaultHud(Transform parent)
        {
            GameObject panel = new GameObject("GameHudPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(parent, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.offsetMin = new Vector2(24f, -220f);
            panelRect.offsetMax = new Vector2(-24f, -24f);

            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.08f, 0.10f, 0.13f, 0.92f);

            VerticalLayoutGroup vertical = panel.GetComponent<VerticalLayoutGroup>();
            vertical.spacing = 10f;
            vertical.padding = new RectOffset(16, 16, 16, 16);
            vertical.childAlignment = TextAnchor.UpperLeft;
            vertical.childForceExpandHeight = false;
            vertical.childForceExpandWidth = true;

            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = CreateText(panel.transform, "Multiplayer Turn HUD", 22, new Color(0.96f, 0.90f, 0.62f));
            turnText = CreateText(panel.transform, "Turn: -", 20, Color.white);
            phaseText = CreateText(panel.transform, "Phase: -", 18, new Color(0.80f, 0.88f, 1f));
            diceText = CreateText(panel.transform, "Dice: -", 18, new Color(0.90f, 0.92f, 0.78f));
            hostHintText = CreateText(panel.transform, "Host controls turn flow until client ownership is wired.", 16, new Color(0.82f, 0.82f, 0.82f));

            GameObject actionsRow = new GameObject("ActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            actionsRow.transform.SetParent(panel.transform, false);

            HorizontalLayoutGroup rowLayout = actionsRow.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 12f;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            rollButton = CreateButton(actionsRow.transform, "Roll", new Color(0.22f, 0.52f, 0.92f));
            endTurnButton = CreateButton(actionsRow.transform, "End Turn", new Color(0.88f, 0.46f, 0.15f));
        }

        private TextMeshProUGUI CreateText(Transform parent, string value, int size, Color color)
        {
            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private Button CreateButton(Transform parent, string label, Color color)
        {
            GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement element = buttonObject.GetComponent<LayoutElement>();
            element.minWidth = 160f;
            element.minHeight = 48f;

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            return button;
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

        private void HandlePhaseChanged(MultiplayerGameSessionController.TurnPhase phase)
        {
            if (phaseText != null)
            {
                phaseText.text = $"Phase: {session.GetPhaseLabel()}";
            }

            UpdateControls();
        }

        private void HandleTurnChanged(int turnIndex, string activePlayerName)
        {
            if (turnText != null)
            {
                turnText.text = $"Turn: {turnIndex + 1} - {activePlayerName}";
            }

            UpdateControls();
        }

        private void HandleDiceRolled(int diceValue)
        {
            if (diceText != null)
            {
                diceText.text = $"Dice: {diceValue}";
            }
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

            if (hostHintText != null)
            {
                hostHintText.gameObject.SetActive(!session.IsHostAuthority);
            }

            UpdateControls();
        }

        private void UpdateControls()
        {
            bool canInteract = session != null && session.IsHostAuthority && session.IsInitialized;
            bool canRoll = canInteract && session.CurrentPhase == MultiplayerGameSessionController.TurnPhase.AwaitingRoll;
            bool canEnd = canInteract && session.CurrentPhase == MultiplayerGameSessionController.TurnPhase.WaitingForEndTurn;

            if (rollButton != null)
            {
                rollButton.gameObject.SetActive(session != null);
                rollButton.interactable = canRoll;
            }

            if (endTurnButton != null)
            {
                endTurnButton.gameObject.SetActive(session != null);
                endTurnButton.interactable = canEnd;
            }

            if (hostHintText != null)
            {
                hostHintText.text = session != null && session.IsHostAuthority
                    ? "Host controls turn flow on this session."
                    : "Client view only. Waiting for host turn updates.";
            }
        }
    }
}
