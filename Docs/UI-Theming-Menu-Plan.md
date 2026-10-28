# Menu UI Theming & Centralization — Step-by-Step Plan

**Goal:** Make the `Menu` scene the first fully themed UI, driven by the existing central design
system (`UIStyleSheet` + `ThemedText` / `ThemedButton` / `ThemedImage`).

**Current state (why this plan):**
- `UIStyleSheet` (design-token store) exists in `MonopolyGame.DesignSystem` but **no asset instance exists** anywhere in the project.
- The themed components (`ThemedText`, `ThemedButton`, `ThemedImage` in `Assets/scripts/UI/DesignSystem/`) are **used nowhere** in scenes or prefabs.
- `GameHudLayoutBuilder` supports the sheet but is called with `styleSheet: null` and duplicates every token as fallback constants.
- Presenters hardcode colors (`Color.green`, `Color.cyan`, `new Color(0.7f, 1f, 0.7f)`) that would fight the theme.

**Work split:** Steps marked **(code)** are C# edits Copilot can do. Steps marked **(editor)** are
manual Unity Editor work. Steps marked **(verify)** are play-mode checks.

---

## Step 1 — Code: auto-resolution of the style sheet (code)

The themed components currently require the `UIStyleSheet` asset to be assigned on **every single
component instance** in the Inspector. Add a central lookup so the field can stay empty:

1. **New file** `Assets/scripts/DesignSystem/UIStyleSheetLocator.cs`
   - Static cached loader: `Resources.Load<UIStyleSheet>("UIStyleSheet")` (lazy, cached, logs one warning if missing).
   - Public static `UIStyleSheet Instance` property.

2. **Edit** `ThemedText.cs`, `ThemedButton.cs`, `ThemedImage.cs` (all in `Assets/scripts/UI/DesignSystem/`)
   - Replace `if (styleSheet == null) return;` with:
     `var sheet = styleSheet != null ? styleSheet : UIStyleSheetLocator.Instance;`
     `if (sheet == null) return;`
   - Use `sheet` in the rest of `ApplyStyle()`.
   - Bonus: since `OnValidate()` runs in the editor, the Inspector will now live-preview themed values
     without any manual assignment.

3. **Edit** `Assets/scripts/UI/LobbyWaitingRoomPresenter.cs` — remove hardcoded colors:
   - `UpdateRoleBadge()`: HOST → `GetTextColor(TextStyle.Success)`, PLAYER → `GetTextColor(TextStyle.Accent)`.
   - `GetReadyButtonColors()`: base = `GetButtonColor(ButtonRole.Success)`, highlighted = `base * ButtonHighlightMultiplier`.
   - `HandleReadyToggle()` "Not Ready" branch: use `ButtonRole.Neutral` colors instead of `ColorBlock.defaultColorBlock`.
   - Add `using MonopolyGame.DesignSystem;`.

4. **(Optional, later phase)** `Assets/scripts/Multiplayer/Gameplay/MultiplayerGameHudPresenter.cs`
   — pass `UIStyleSheetLocator.Instance` instead of `null` to `GameHudLayoutBuilder.Build(...)`.
   Leave the builder's fallback constants in place as a safety net for now.

> The scene object `HubMenuCorrdinator` (typo in the object name) is harmless — only the script class matters. Do not rename during this pass.

---

## Step 2 — Create the style sheet asset (editor)

1. In the Project window create the folder **`Assets/Resources`** (does not exist yet).
   - The asset must live under `Resources/` for the runtime `Resources.Load` lookup from Step 1.
2. Right-click inside it → **Create → Monopoly Game → UI Style Sheet** → name it exactly **`UIStyleSheet`**.
3. Select the asset and assign **Default Font**:
   `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF` (drag into the field).
4. Leave all colors/sizes/spacing at their defaults for the first pass — they already match the current
   hand-built look (the values were copied into `GameHudLayoutBuilder`'s fallbacks).
5. Quick sanity check: the Inspector should show all `Header` groups (Font, Text Colors, Text Sizes,
   Button Colors/Sizing, Image Colors, Player Colors, Spacing Scale, Layout — Panel).

---

## Step 3 — Theme the Menu scene (editor, main manual work)

Open `Assets/Scenes/Menu.unity`. Work **panel by panel**. General rules:

- Add `ThemedText` only to `TextMeshProUGUI` objects. The scene contains a few legacy `Text` objects —
  replace them first: delete the `Text` component, add **TextMeshPro - Text (UI)**, keep the same
  GameObject and text content.
- Leave `Style Sheet` field **empty** everywhere (the locator resolves it). If you want an instant
  preview in the Inspector, you may drag the asset in — but it also previews without it.
- Skip dropdown/input-field internals (Template, Viewport, Item Background, etc.) in this pass.
- `ThemedButton` styles the `Image` fill + color block + child label, and sizes via `LayoutElement`
  **only if one is present** — add a `LayoutElement` to buttons that need token sizing.
- If any single text needs a custom size/color that no token covers, **do not force it** — note it in
  a TODO and extend `UIStyleSheet` later rather than hardcoding.

### 3.1 HubPanel
| Object | Add | Setting |
|---|---|---|
| `HubPanel` background `Image` | `ThemedImage` | Image Role = `PanelBackground` |
| Hub title `Text (TMP)` | `ThemedText` | Text Style = `Title` |
| `Sign in (Button)` | `ThemedButton` | Button Role = `Primary`, Size = `Wide` |

### 3.2 Auth panel
| Object | Add | Setting |
|---|---|---|
| Auth panel background `Image` | `ThemedImage` | `PanelBackground` |
| Auth `Text (TMP)` (title/instructions) | `ThemedText` | `Heading` / `Caption` |

### 3.3 LobbyBrowserPanel
| Object | Add | Setting |
|---|---|---|
| Panel background `Image` | `ThemedImage` | `PanelBackground` |
| `LobbyList` background | `ThemedImage` | `CardSurface` |
| `RefreshButton` | `ThemedButton` | Role = `Neutral`, Size = `Standard` |
| Any static `Text (TMP)` in the panel | `ThemedText` | `Body` / `Caption` |

### 3.4 LobbyListItem prefab (open prefab in Prefab Mode)
| Object | Add | Setting |
|---|---|---|
| Item background `Image` | `ThemedImage` | `CardBackground` |
| `gameNameText` | `ThemedText` | `Heading` |
| `playerCountText`, `hostNameText` | `ThemedText` | `Caption` |
| `variantTag` | `ThemedText` | `Accent` |
| `privateIcon` `Image` | `ThemedImage` | `Overlay` (no dedicated icon role yet) |
| `joinButton` | `ThemedButton` | Role = `Primary`, Size = `Compact` |

### 3.5 CreateLobbyPanel
| Object | Add | Setting |
|---|---|---|
| Panel background `Image` | `ThemedImage` | `PanelBackground` |
| `MaxPlayersLabel` | `ThemedText` | `Body` |
| `CreateGameButton` | `ThemedButton` | Role = `Primary`, Size = `Wide` |
| `GameNameInput` / dropdowns | skip this pass | — |

### 3.6 Join-by-code panel
| Object | Add | Setting |
|---|---|---|
| Panel background `Image` | `ThemedImage` | `PanelBackground` |
| `Instructions` | `ThemedText` | `Caption` |
| `JoinByCodeButton` | `ThemedButton` | Role = `Primary`, Size = `Standard` |
| Code `Password (input field)` | skip this pass | — |

### 3.7 WaitingRoomPanel
| Object | Add | Setting |
|---|---|---|
| Panel background `Image` | `ThemedImage` | `PanelBackground` |
| Lobby name `Text (TMP)` | `ThemedText` | `Heading` |
| `roleBadgeText` | `ThemedText` | `Accent` (presenter sets color at runtime — see Step 1.3, must be done first) |
| `VariantLabel`, `MaxPlayersLabel`, private status | `ThemedText` | `Body` |
| `ReadyButton` | `ThemedButton` | Role = `Success`, Size = `Standard` |
| `StrartGameButton` (sic) | `ThemedButton` | Role = `Primary`, Size = `Wide` |
| `LeaveLobbyButton` | `ThemedButton` | Role = `Danger`, Size = `Standard` |
| `PlayerSlotContainer` background | `ThemedImage` | `CardSurface` |

### 3.8 PlayerSlotItem prefab (open prefab in Prefab Mode)
| Object | Add | Setting |
|---|---|---|
| Slot background `Image` | `ThemedImage` | `CardBackground` |
| `playerNameText` | `ThemedText` | `Body` |
| `readyStatusText` | `ThemedText` | `Caption` |
| `avatarImage` | `ThemedImage` | `Overlay` |
| `emptySlotPlaceholder` | skip | — |

### 3.9 Errors / InfoText
| Object | Add | Setting |
|---|---|---|
| `Errors` root `Image` | `ThemedImage` | `Overlay` |
| `errorText` | `ThemedText` | `Warning` |
| `InfoText` | `ThemedText` | `Muted` |

---

## Step 4 — Verify (verify)

1. Enter Play Mode in `Menu.unity`.
2. Walk the full flow: sign in → hub → browser → create → join-by-code → waiting room.
3. Check that every panel, text, and button picks up the sheet's values.
4. Confirm the `Ready` button toggle still works (green themed colors, not raw `Color.green`).
5. Confirm host/player role badge colors are themed.
6. If any object renders with a TMP default font or wrong color → a component was missed or its
   `TextMeshProUGUI` is inside a template (fix case-by-case).

---

## Step 5 — Follow-ups (later phases, not now)

- Game scene: reuse the same asset + component pass on the 3D-board HUD, then wire
  `MultiplayerGameHudPresenter` to `UIStyleSheetLocator` and remove the duplicated fallbacks in
  `GameHudLayoutBuilder` (keep them until the sheet is proven).
- Extend `UIStyleSheet` with tokens the design system currently lacks (dropdown/input-field colors,
  scrollbar, icon/private-badge role, hover tooltips) before touching dropdown internals.
- Consider a theme-swap test: duplicate the asset, change accent/player colors, confirm everything
  re-themes from one place — this is the payoff of the centralization.

## Definition of done
- One `UIStyleSheet` asset under `Assets/Resources/`.
- All Menu panels/prefabs use themed components; zero hardcoded colors in Menu-related presenters.
- Full menu flow verified in Play Mode with no visual regressions.
