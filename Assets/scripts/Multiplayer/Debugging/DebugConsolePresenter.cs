using System.Collections;
using System.Collections.Generic;
using System.Text;
using MonopolyGame.Debugging;
using MonopolyGame.DesignSystem;
using MonopolyGame.Multiplayer.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MonopolyGame.Multiplayer.Debugging
{
    public sealed class DebugConsolePresenter : MonoBehaviour
    {
        private const int MaxOutputLines = 200;
        private const float WindowWidth = 780f;
        private const float WindowHeight = 420f;

        [Header("Dependencies")]
        [SerializeField] private MultiplayerGameSessionController session;
        [SerializeField] private ActionCardActivationPresenter actionCardActivation;

        [Header("Input")]
        [SerializeField] private Key toggleKey = Key.Backquote;
        [SerializeField] private Key alternateToggleKey = Key.F10;

        private static readonly Color CommandColor = new Color(0.80f, 0.88f, 1.00f);
        private static readonly Color SuccessColor = new Color(0.76f, 1.00f, 0.76f);
        private static readonly Color FailureColor = new Color(0.96f, 0.45f, 0.45f);
        private static readonly Color InfoColor = new Color(0.78f, 0.78f, 0.80f);

        private readonly List<string> outputLines = new List<string>();
        private readonly DebugConsoleHistory history = new DebugConsoleHistory();

        private DebugConsoleBridge bridge;
        private RectTransform canvasRect;
        private GameObject window;
        private ScrollRect scrollRect;
        private TextMeshProUGUI outputText;
        private TMP_InputField inputField;
        private Coroutine bindRoutine;
        private bool isOpen;
        private bool scrollToBottom;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }

            canvasRect = transform as RectTransform;

            if (toggleKey == Key.None)
            {
                toggleKey = Key.Backquote;
            }

            BuildWindow();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (!enabled)
            {
                return;
            }

            if (bindRoutine == null)
            {
                bindRoutine = StartCoroutine(BindWhenReady());
            }
        }

        private void OnDisable()
        {
            if (bridge != null)
            {
                bridge.ResponseReceived -= HandleResponse;
                bridge = null;
            }

            bindRoutine = null;
        }

        private IEnumerator BindWhenReady()
        {
            while (session == null)
            {
                session = FindAnyObjectByType<MultiplayerGameSessionController>();

                if (session == null)
                {
                    yield return null;
                }
            }

            if (actionCardActivation == null)
            {
                actionCardActivation = FindAnyObjectByType<ActionCardActivationPresenter>();
            }

            bridge = session.DebugConsole;
            bridge.ResponseReceived += HandleResponse;
            AppendLines(new[] { "Debug console ready. Type 'help' for the command list." }, InfoColor);
            bindRoutine = null;
        }

        private void OnDestroy()
        {
            if (bridge != null)
            {
                bridge.ResponseReceived -= HandleResponse;
                bridge = null;
            }
        }

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (IsTogglePressed())
            {
                Toggle();
                return;
            }

            if (!isOpen)
            {
                return;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (Keyboard.current.tabKey.wasPressedThisFrame)
            {
                CompleteInput();
                return;
            }

            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                NavigateHistory(-1);
                return;
            }

            if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                NavigateHistory(1);
            }
        }

        private void LateUpdate()
        {
            if (!isOpen || !scrollToBottom || scrollRect == null)
            {
                return;
            }

            scrollToBottom = false;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private bool IsTogglePressed()
        {
            if (Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                return true;
            }

            return alternateToggleKey != Key.None &&
                   alternateToggleKey != toggleKey &&
                   Keyboard.current[alternateToggleKey].wasPressedThisFrame;
        }

        public void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (bridge == null)
            {
                return;
            }

            if (actionCardActivation != null && actionCardActivation.IsOpen)
            {
                actionCardActivation.Close();
            }

            SetVisible(true);
            history.ResetCursor();
            FocusInput();
        }

        public void Close()
        {
            SetVisible(false);

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void SetVisible(bool visible)
        {
            isOpen = visible;

            if (window != null)
            {
                window.SetActive(visible);
            }
        }

        private void FocusInput()
        {
            if (inputField == null)
            {
                return;
            }

            inputField.SetTextWithoutNotify(string.Empty);
            inputField.ActivateInputField();

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(inputField.gameObject);
            }
        }

        private void HandleSubmit(string text)
        {
            string line = (text ?? string.Empty).Trim();

            if (line.Length == 0)
            {
                FocusInput();
                return;
            }

            history.Add(line);
            inputField.SetTextWithoutNotify(string.Empty);
            FocusInput();

            AppendLines(new[] { $"> {line}" }, CommandColor);

            if (bridge == null)
            {
                AppendLines(new[] { "The console is not connected to a game session yet." }, FailureColor);
                return;
            }

            DebugCommandResult result = bridge.Execute(line);
            ApplyResult(result);
        }

        private void HandleResponse(string line, DebugCommandResult result)
        {
            ApplyResult(result);
        }

        private void ApplyResult(DebugCommandResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.ClearsConsole)
            {
                outputLines.Clear();
                RefreshOutput();
                return;
            }

            Color color = result.IsSuccess
                ? (result.IsPending ? InfoColor : SuccessColor)
                : FailureColor;

            AppendLines(result.Lines, color);
        }

        private void CompleteInput()
        {
            if (bridge == null || inputField == null)
            {
                return;
            }

            string current = inputField.text ?? string.Empty;
            int spaceIndex = current.IndexOf(' ');
            string prefix = spaceIndex < 0 ? current : current.Substring(0, spaceIndex);
            string suffix = spaceIndex < 0 ? string.Empty : current.Substring(spaceIndex);

            IReadOnlyList<string> matches = bridge.Complete(prefix);

            if (matches.Count == 0)
            {
                return;
            }

            if (matches.Count == 1)
            {
                SetInput(matches[0] + suffix);
                return;
            }

            string common = LongestCommonPrefix(matches);

            if (common.Length > prefix.Length)
            {
                SetInput(common + suffix);
            }

            AppendLines(matches, InfoColor);
        }

        private void NavigateHistory(int direction)
        {
            string line;

            if (direction < 0)
            {
                if (history.TryMovePrevious(out line))
                {
                    SetInput(line);
                }

                return;
            }

            if (history.TryMoveNext(out line))
            {
                SetInput(line ?? string.Empty);
            }
        }

        private void SetInput(string text)
        {
            if (inputField == null)
            {
                return;
            }

            inputField.SetTextWithoutNotify(text ?? string.Empty);
            inputField.ActivateInputField();
            inputField.caretPosition = inputField.text.Length;
        }

        private static string LongestCommonPrefix(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            string prefix = values[0];

            for (int i = 1; i < values.Count; i++)
            {
                int length = 0;

                while (length < prefix.Length &&
                       length < values[i].Length &&
                       char.ToLowerInvariant(prefix[length]) == char.ToLowerInvariant(values[i][length]))
                {
                    length++;
                }

                prefix = prefix.Substring(0, length);

                if (prefix.Length == 0)
                {
                    break;
                }
            }

            return prefix;
        }

        private void AppendLines(IReadOnlyList<string> lines, Color color)
        {
            if (lines == null)
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                foreach (string part in line.Split('\n'))
                {
                    outputLines.Add(Colorize(part, color));
                }
            }

            while (outputLines.Count > MaxOutputLines)
            {
                outputLines.RemoveAt(0);
            }

            RefreshOutput();
        }

        private void RefreshOutput()
        {
            if (outputText == null)
            {
                return;
            }

            var builder = new StringBuilder();

            for (int i = 0; i < outputLines.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(outputLines[i]);
            }

            outputText.text = builder.ToString();
            scrollToBottom = true;
        }

        private static string Colorize(string line, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{line}</color>";
        }

        private void BuildWindow()
        {
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            if (canvasRect != null)
            {
                canvasRect.anchorMin = Vector2.zero;
                canvasRect.anchorMax = Vector2.one;
                canvasRect.offsetMin = Vector2.zero;
                canvasRect.offsetMax = Vector2.zero;
            }

            RectTransform windowRect = CreateChild(transform, "Window");
            window = windowRect.gameObject;
            windowRect.anchorMin = new Vector2(0f, 0f);
            windowRect.anchorMax = new Vector2(0f, 0f);
            windowRect.pivot = new Vector2(0f, 0f);
            windowRect.anchoredPosition = new Vector2(24f, 24f);
            windowRect.sizeDelta = new Vector2(WindowWidth, WindowHeight);

            var background = window.AddComponent<Image>();
            background.color = sheet != null
                ? sheet.GetImageColor(ImageRole.PanelBackground)
                : new Color(0.05f, 0.06f, 0.08f, 0.94f);
            background.raycastTarget = true;

            RectTransform titleBar = CreateChild(windowRect, "TitleBar");
            titleBar.anchorMin = new Vector2(0f, 1f);
            titleBar.anchorMax = new Vector2(1f, 1f);
            titleBar.pivot = new Vector2(0.5f, 1f);
            titleBar.anchoredPosition = Vector2.zero;
            titleBar.sizeDelta = new Vector2(0f, 32f);

            var titleBackground = titleBar.gameObject.AddComponent<Image>();
            titleBackground.color = sheet != null
                ? sheet.GetImageColor(ImageRole.CardSurface)
                : new Color(0.12f, 0.14f, 0.18f, 0.95f);

            var dragHandle = titleBar.gameObject.AddComponent<DebugConsoleDragHandle>();
            dragHandle.Configure(windowRect, canvasRect);

            TextMeshProUGUI title = CreateText(titleBar, "Title", "DEBUG CONSOLE", TextStyle.Accent);
            title.alignment = TextAlignmentOptions.Left;
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(10f, 0f);
            title.rectTransform.offsetMax = new Vector2(-90f, 0f);

            Button closeButton = CreateButton(titleBar, "CloseButton", "x", 32f, 24f, Close);
            RectTransform closeRect = closeButton.transform as RectTransform;

            if (closeRect != null)
            {
                closeRect.anchorMin = new Vector2(1f, 0.5f);
                closeRect.anchorMax = new Vector2(1f, 0.5f);
                closeRect.pivot = new Vector2(1f, 0.5f);
                closeRect.anchoredPosition = new Vector2(-6f, 0f);
            }

            BuildScrollback(windowRect, sheet);
            BuildInput(windowRect, sheet);
        }

        private void BuildScrollback(RectTransform windowRect, UIStyleSheet sheet)
        {
            RectTransform scrollRoot = CreateChild(windowRect, "Scrollback");
            scrollRoot.anchorMin = Vector2.zero;
            scrollRoot.anchorMax = Vector2.one;
            scrollRoot.offsetMin = new Vector2(8f, 44f);
            scrollRoot.offsetMax = new Vector2(-8f, -40f);

            scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 25f;

            RectTransform viewport = CreateChild(scrollRoot, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateChild(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            outputText = content.gameObject.AddComponent<TextMeshProUGUI>();
            outputText.text = string.Empty;
            outputText.alignment = TextAlignmentOptions.TopLeft;
            outputText.textWrappingMode = TextWrappingModes.Normal;
            outputText.richText = true;
            outputText.raycastTarget = false;
            outputText.margin = new Vector4(2f, 2f, 2f, 2f);
            outputText.fontSize = sheet != null ? sheet.GetTextSize(TextStyle.Muted) : 14f;

            if (sheet != null)
            {
                if (sheet.DefaultFont != null)
                {
                    outputText.font = sheet.DefaultFont;
                }

                sheet.ApplyOutline(outputText);
            }

            scrollRect.viewport = viewport;
            scrollRect.content = content;
        }

        private void BuildInput(RectTransform windowRect, UIStyleSheet sheet)
        {
            RectTransform inputRoot = CreateChild(windowRect, "Input");
            inputRoot.anchorMin = new Vector2(0f, 0f);
            inputRoot.anchorMax = new Vector2(1f, 0f);
            inputRoot.pivot = new Vector2(0.5f, 0f);
            inputRoot.anchoredPosition = new Vector2(0f, 8f);
            inputRoot.sizeDelta = new Vector2(-16f, 28f);

            var background = inputRoot.gameObject.AddComponent<Image>();
            background.color = sheet != null
                ? sheet.GetImageColor(ImageRole.InputBackground)
                : new Color(0.10f, 0.12f, 0.15f, 0.98f);

            inputRoot.gameObject.SetActive(false);

            RectTransform textArea = CreateChild(inputRoot, "Text Area");
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.offsetMin = new Vector2(8f, 4f);
            textArea.offsetMax = new Vector2(-8f, -4f);
            textArea.gameObject.AddComponent<RectMask2D>();

            RectTransform inputTextRect = CreateChild(textArea, "Text");
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;

            var inputText = inputTextRect.gameObject.AddComponent<TextMeshProUGUI>();
            inputText.text = string.Empty;
            inputText.richText = false;
            inputText.raycastTarget = false;
            inputText.alignment = TextAlignmentOptions.Left;
            inputText.textWrappingMode = TextWrappingModes.NoWrap;
            inputText.fontSize = sheet != null ? sheet.GetTextSize(TextStyle.Caption) : 16f;
            inputText.color = sheet != null ? sheet.InputTextColor : Color.white;

            RectTransform placeholderRect = CreateChild(inputRoot, "Placeholder");
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(8f, 4f);
            placeholderRect.offsetMax = new Vector2(-8f, -4f);

            var placeholder = placeholderRect.gameObject.AddComponent<TextMeshProUGUI>();
            placeholder.text = "Type a command. 'help' lists them.";
            placeholder.richText = false;
            placeholder.raycastTarget = false;
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            placeholder.fontSize = sheet != null ? sheet.GetTextSize(TextStyle.Caption) : 16f;
            placeholder.color = sheet != null ? sheet.InputPlaceholderColor : new Color(0.65f, 0.65f, 0.65f, 0.8f);

            if (sheet != null && sheet.DefaultFont != null)
            {
                inputText.font = sheet.DefaultFont;
                placeholder.font = sheet.DefaultFont;
            }

            var input = inputRoot.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.restoreOriginalTextOnEscape = false;
            input.customCaretColor = true;
            input.caretColor = sheet != null ? sheet.CaretColor : Color.white;
            input.caretWidth = 2;
            input.selectionColor = sheet != null ? sheet.SelectionColor : new Color(0.22f, 0.52f, 0.92f, 0.35f);
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            input.onSubmit.AddListener(HandleSubmit);

            inputField = input;
            inputRoot.gameObject.SetActive(true);
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
            RectTransform rect = CreateChild(parent, name);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;

            if (sheet != null)
            {
                sheet.ApplyTextStyle(text, style);
            }

            text.text = content;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            float width,
            float height,
            UnityEngine.Events.UnityAction onClick)
        {
            UIStyleSheet sheet = UIStyleSheetLocator.Instance;
            RectTransform rect = CreateChild(parent, name);
            rect.sizeDelta = new Vector2(width, height);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = sheet != null ? sheet.GetButtonColor(ButtonRole.Neutral) : Color.gray;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            RectTransform labelRect = CreateChild(rect, "Text");
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
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
            }

            if (sheet != null)
            {
                sheet.ApplyOutline(text);
            }

            return button;
        }
    }
}
