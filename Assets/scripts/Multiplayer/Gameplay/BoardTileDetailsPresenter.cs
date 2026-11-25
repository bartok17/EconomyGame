using MonopolyGame.Board;
using MonopolyGame.DesignSystem;
using MonopolyGame.Multiplayer.Camera;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class BoardTileDetailsPresenter : MonoBehaviour
    {
        private const float PanelWidth = 380f;
        private const float PanelHeight = 330f;

        private MultiplayerGameSessionController session;
        private BoardFocusCoordinator boardFocus;
        private RectTransform panelRoot;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI ownerText;
        private TextMeshProUGUI statsText;
        private TextMeshProUGUI buildingsText;
        private TextMeshProUGUI statusText;
        private Button buildButton;
        private Button sellButton;
        private TextMeshProUGUI buildLabel;
        private TextMeshProUGUI sellLabel;
        private BoardSpaceView currentSpace;
        private bool isOpen;

        public bool IsOpen => isOpen;

        public void Bind(MultiplayerGameSessionController sessionController, BoardFocusCoordinator focusCoordinator)
        {
            Unbind();

            session = sessionController;
            boardFocus = focusCoordinator;

            if (session != null)
            {
                session.EconomyChanged += Refresh;
                session.PhaseChanged += HandlePhaseChanged;
                session.TurnChanged += HandleTurnChanged;
                session.PaymentsChanged += Refresh;
                session.CardResolutionChanged += Refresh;
            }

            if (boardFocus != null)
            {
                boardFocus.SpaceClicked += HandleSpaceClicked;
            }
        }

        private void OnEnable()
        {
            if (session != null && boardFocus != null)
            {
                Bind(session, boardFocus);
            }
        }

        private void OnDisable()
        {
            Unbind();
            Close();
        }

        private void Update()
        {
            if (!isOpen || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        private void Unbind()
        {
            if (session != null)
            {
                session.EconomyChanged -= Refresh;
                session.PhaseChanged -= HandlePhaseChanged;
                session.TurnChanged -= HandleTurnChanged;
                session.PaymentsChanged -= Refresh;
                session.CardResolutionChanged -= Refresh;
            }

            if (boardFocus != null)
            {
                boardFocus.SpaceClicked -= HandleSpaceClicked;
            }
        }

        private void HandlePhaseChanged(TurnPhase phase)
        {
            Refresh();
        }

        private void HandleTurnChanged(int turnIndex, string activePlayerName)
        {
            Refresh();
        }

        private void HandleSpaceClicked(BoardSpaceView space)
        {
            if (space == null)
            {
                Close();
                return;
            }

            currentSpace = space;
            EnsureUi();

            if (panelRoot == null)
            {
                return;
            }

            isOpen = true;
            panelRoot.gameObject.SetActive(true);
            Refresh();
        }

        private void Close()
        {
            isOpen = false;
            currentSpace = null;

            if (panelRoot != null)
            {
                panelRoot.gameObject.SetActive(false);
            }
        }

        private void EnsureUi()
        {
            if (panelRoot != null || session == null)
            {
                return;
            }

            Canvas canvas = ResolveCanvas();

            if (canvas == null)
            {
                Debug.LogWarning($"[{nameof(BoardTileDetailsPresenter)}] No screen canvas found; the tile panel stays hidden.");
                return;
            }

            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            var panelObject = new GameObject("TileDetailsPanel", typeof(RectTransform));
            panelRoot = (RectTransform)panelObject.transform;
            panelRoot.SetParent(canvas.transform, false);
            panelRoot.anchorMin = new Vector2(1f, 0f);
            panelRoot.anchorMax = new Vector2(1f, 0f);
            panelRoot.pivot = new Vector2(1f, 0f);
            panelRoot.anchoredPosition = new Vector2(-24f, 24f);
            panelRoot.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var background = panelObject.AddComponent<Image>();
            background.color = sheet != null ? sheet.GetImageColor(ImageRole.PanelBackground) : new Color(0.08f, 0.10f, 0.13f, 0.92f);

            var vertical = panelObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(16, 16, 14, 14);
            vertical.spacing = 6f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            RectTransform header = CreateRow("Header", 34f);
            ConfigureHorizontal(header, 8f, expandWidth: true);

            titleText = CreateText(header, "Title", "Tile", TextStyle.Title);
            SetFlexibleWidth(titleText.gameObject, 1f);
            CreateButton(header, "Close", "X", ButtonRole.Neutral, new Vector2(32f, 32f), Close);

            ownerText = CreateText(panelRoot, "Owner", string.Empty, TextStyle.Body);
            statsText = CreateText(panelRoot, "Stats", string.Empty, TextStyle.Caption);
            buildingsText = CreateText(panelRoot, "Buildings", string.Empty, TextStyle.Property);

            statusText = CreateText(panelRoot, "Status", string.Empty, TextStyle.Warning);
            statusText.textWrappingMode = TextWrappingModes.Normal;
            statusText.gameObject.AddComponent<LayoutElement>().minHeight = 40f;

            RectTransform buttons = CreateRow("Buttons", 44f);
            ConfigureHorizontal(buttons, 12f, expandWidth: true);

            buildButton = CreateButton(buttons, "Build", "Build", ButtonRole.Primary, new Vector2(160f, 40f), HandleBuildClicked);
            buildLabel = buildButton.GetComponentInChildren<TextMeshProUGUI>();
            sellButton = CreateButton(buttons, "Sell", "Sell", ButtonRole.Danger, new Vector2(160f, 40f), HandleSellClicked);
            sellLabel = sellButton.GetComponentInChildren<TextMeshProUGUI>();

            panelObject.SetActive(false);
        }

        private void Refresh()
        {
            if (!isOpen || currentSpace == null || session == null)
            {
                return;
            }

            UIStyleSheet sheet = UIStyleSheetLocator.Instance;
            titleText.text = currentSpace.displayName;

            if (currentSpace.type != BoardSpaceType.Property)
            {
                ownerText.text = currentSpace.type.ToString();
                statsText.text = string.Empty;
                buildingsText.text = string.Empty;
                SetStatus("Only properties can be built on.", allowed: false, sheet);
                buildButton.gameObject.SetActive(false);
                sellButton.gameObject.SetActive(false);
                return;
            }

            int spaceIndex = currentSpace.index;
            int localSlot = session.GetLocalPawnSlot();
            int ownerSlot = session.GetPropertyOwnerSlot(spaceIndex);
            int level = session.GetPropertyHouses(spaceIndex);
            bool hotel = PropertyBuildingRules.IsHotel(level);
            int rent = session.GetPropertyRent(spaceIndex);

            ownerText.text = ownerSlot < 0
                ? "Unowned"
                : $"Owner: {ResolvePlayerName(ownerSlot)}";

            bool ownsProperty = ownerSlot >= 0 && ownerSlot == localSlot;
            string levelText = hotel
                ? "Hotel"
                : level > 0
                    ? $"{level} house{(level == 1 ? string.Empty : "s")}"
                    : "No buildings";
            string lapLimit = session.HousesPerLapLimit > 0 ? session.HousesPerLapLimit.ToString() : "∞";
            string lapText = ownsProperty
                ? $" · lap {session.GetHousesBuiltThisLap(localSlot)}/{lapLimit}"
                : string.Empty;

            statsText.text = $"Price {currentSpace.price} · House {currentSpace.houseCost}\nRent {rent}";
            buildingsText.text = $"{levelText}{lapText}";

            buildButton.gameObject.SetActive(ownsProperty);
            sellButton.gameObject.SetActive(ownsProperty);

            if (!ownsProperty)
            {
                SetStatus(ownerSlot < 0 ? "Buy this property to develop it." : "Only the owner can build or sell.", allowed: false, sheet);
                return;
            }

            PropertyBuildCheck buildCheck = session.GetBuildStatus(spaceIndex);
            PropertyBuildCheck sellCheck = session.GetSellStatus(spaceIndex);

            if (buildLabel != null)
            {
                buildLabel.text = buildCheck.Allowed
                    ? PropertyBuildingRules.IsHotel(buildCheck.NextLevel)
                        ? $"Hotel ({buildCheck.Amount})"
                        : $"Build ({buildCheck.Amount})"
                    : "Build";
            }

            if (sellLabel != null)
            {
                sellLabel.text = sellCheck.Allowed ? $"Sell (+{sellCheck.Amount})" : "Sell";
            }

            bool turnReady = session.CanManageBuildings;
            buildButton.interactable = turnReady && buildCheck.Allowed;
            sellButton.interactable = turnReady && sellCheck.Allowed;

            if (!turnReady)
            {
                string reason = !session.IsLocalPlayersTurn
                    ? "Not your turn."
                    : "Resolve the pending action first.";
                SetStatus(reason, allowed: false, sheet);
                return;
            }

            if (buildCheck.Allowed && sellCheck.Allowed)
            {
                SetStatus("Ready to build or sell.", allowed: true, sheet);
            }
            else if (buildCheck.Allowed)
            {
                SetStatus("Ready to build.", allowed: true, sheet);
            }
            else if (sellCheck.Allowed)
            {
                SetStatus("Ready to sell.", allowed: true, sheet);
            }
            else
            {
                SetStatus(buildCheck.Reason, allowed: false, sheet);
            }
        }

        private void SetStatus(string message, bool allowed, UIStyleSheet sheet)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.text = message ?? string.Empty;

            if (sheet != null)
            {
                statusText.color = sheet.GetTextColor(allowed ? TextStyle.Success : TextStyle.Warning);
            }
        }

        private string ResolvePlayerName(int pawnSlot)
        {
            System.Collections.Generic.IReadOnlyList<PlayerEconomyState> states = session.GetPlayerEconomyStates();

            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].PawnSlot != pawnSlot)
                {
                    continue;
                }

                string name = states[i].DisplayName.ToString();
                return string.IsNullOrWhiteSpace(name) ? $"Player {pawnSlot + 1}" : name;
            }

            return $"Player {pawnSlot + 1}";
        }

        private void HandleBuildClicked()
        {
            if (currentSpace != null)
            {
                session.RequestBuildHouse(currentSpace.index);
            }
        }

        private void HandleSellClicked()
        {
            if (currentSpace != null)
            {
                session.RequestSellHouse(currentSpace.index);
            }
        }

        private static Canvas ResolveCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>();

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].isRootCanvas && canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return canvases[i];
                }
            }

            return null;
        }

        private RectTransform CreateRow(string name, float height)
        {
            RectTransform rect = CreateChild(panelRoot, name);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return rect;
        }

        private static void ConfigureHorizontal(RectTransform rect, float spacing, bool expandWidth)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = false;
        }

        private static void SetFlexibleWidth(GameObject target, float flexibleWidth)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();

            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }

            element.flexibleWidth = flexibleWidth;
        }

        private static RectTransform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string content, TextStyle style)
        {
            RectTransform row = CreateChild(parent, name);
            var text = row.gameObject.AddComponent<TextMeshProUGUI>();
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            if (sheet != null)
            {
                sheet.ApplyTextStyle(text, style);
            }

            text.text = content;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.margin = new Vector4(2f, 2f, 2f, 2f);
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            ButtonRole role,
            Vector2 size,
            UnityAction onClick)
        {
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            RectTransform rect = CreateChild(parent, name);

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = size.x;
            element.preferredWidth = size.x;
            element.minHeight = size.y;
            element.preferredHeight = size.y;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = sheet != null ? sheet.GetButtonColor(role) : Color.gray;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            RectTransform textRect = CreateChild(rect, "Text");
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.color = sheet != null ? sheet.ButtonTextColor : Color.white;
            text.fontSize = sheet != null ? sheet.ButtonFontSize : 16f;

            if (sheet != null && sheet.DefaultFont != null)
            {
                text.font = sheet.DefaultFont;
                sheet.ApplyOutline(text);
            }

            return button;
        }
    }
}
