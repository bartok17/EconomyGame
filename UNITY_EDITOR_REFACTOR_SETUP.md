# Unity Editor Setup Guide for the Multiplayer Refactor

This guide describes the manual Unity Editor steps needed to finish wiring the refactor in this project. It is intended as a checklist for scene setup, inspector binding, and asset creation after the code-level cleanup.

## What changed in the refactor

The refactor split the major gameplay loop into clearer layers:

- The Game scene now uses a scene installer as the composition root.
- The session controller keeps authoritative network state, but rules are separated into pure gameplay types.
- Pawn visuals are driven by a ScriptableObject configuration instead of hardcoded values.
- Pawn instantiation is centralized in a factory path.
- The game HUD is designed to bind to an existing session instead of searching the scene.
- The multiplayer UI presenters now expect explicit inspector wiring.

Because of that, a few scene and asset references must be assigned manually in the Unity Editor.

## Before you start

1. Open the project in Unity.
2. Let the editor finish importing scripts and assets.
3. Open the Console and make sure there are no script compile errors before editing scenes.
4. Save your current scene changes before making wiring updates.

## Game scene setup

Open the Game scene and verify the following objects and components.

### 1. GameSessionController object

Find the GameObject that owns the multiplayer game session components. It should already have:

- `MultiplayerGameSessionController`
- `MultiplayerGameHudPresenter`
- The new `GameSceneInstaller` component

Assign or verify these serialized fields on `GameSceneInstaller`:

- `Session Controller` should point to the same `MultiplayerGameSessionController` GameObject.
- `Board Manager` should point to the board object that owns `BoardManager`.
- `Pawn Spawner` should point to the object that owns `PlayerPawnSpawner`.
- `Hud Presenter` should point to the object that owns `MultiplayerGameHudPresenter`.

If any of these are missing, drag the correct scene object into the field.

### 2. Board object

Select the board GameObject and verify:

- It has `BoardManager`.
- The board spaces are still assigned and visible in the hierarchy or prefab instance.
- The board view objects still expose the space metadata needed by the rules layer.

If the board is built from child objects or prefabs, make sure the board data is still reflected in the inspector the same way it was before the refactor.

### 3. Pawn spawner object

Select the pawn spawner GameObject and verify:

- It has `PlayerPawnSpawner`.
- `Board Manager` is assigned.
- `Pawn Prefab` is assigned.
- `Pawn Visual Config` is assigned.
- `Pawn Root` is assigned if the scene already has a dedicated transform for spawned pawns.

If `Pawn Root` is left empty, the spawner will create one at runtime, but it is better to assign a dedicated root in the scene if you want cleaner hierarchy organization.

### 4. HUD presenter object

Select the HUD presenter GameObject and verify:

- It has `MultiplayerGameHudPresenter`.
- The title, turn, phase, dice, and message text fields are assigned.
- The roll and end-turn buttons are assigned.
- If you want the presenter to build the layout automatically, keep `Build Default Layout` enabled.
- If the scene already has a canvas and event system, assign them in the optional scene UI fields.

If the HUD is intended to be created automatically, confirm the presenter has enough references to build itself without relying on scene lookup.

## Create the pawn visual config asset

The refactor expects a `PawnVisualConfig` ScriptableObject asset.

### Steps

1. In the Project window, create a new asset using the menu:
   - `Create > Monopoly Game > Pawn Visual Config`
2. Name the asset something clear, such as `PawnVisualConfig_Default`.
3. Select the asset and configure:
   - Pawn height
   - Pawn radius
   - Pawn colors

### Suggested setup

- Use the default height and radius if the pawn prefab already looks correct.
- Choose pawn colors that are easy to distinguish in the board view.
- Keep the color list length equal to or greater than the maximum expected number of players.

### Assign the asset

Drag the new `PawnVisualConfig` asset into the `Pawn Visual Config` field on `PlayerPawnSpawner`.

## Verify the pawn prefab

Open the pawn prefab used by `PlayerPawnSpawner` and confirm:

- It still contains the rendered geometry or visual children needed for coloring.
- It includes `NetworkObject` if the pawn is spawned over the network.
- It has `PlayerPawn` and `PlayerPawnNetworkSync` either already present or created by the spawn code at runtime.
- Any visual child objects still use the expected materials and renderers.

The factory will create or add the pawn logic components at runtime, but the prefab must still be a valid networked object.

## Multiplayer bootstrapper setup

Open the object that owns `MultiplayerBootstrapper` and confirm:

- The `NetworkManager` reference is assigned if the script expects one.
- The flow coordinator is assigned if that field is present.
- The bootstrapper no longer needs to find the pawn spawner automatically.

If the scene has a dedicated bootstrap object, keep it in the hub or startup scene, not the gameplay scene, unless your scene flow explicitly requires it.

## Multiplayer UI setup

The multiplayer UI layer now expects explicit inspector wiring instead of searching the scene.

### 1. Hub menu presenter

Find the GameObject with `HubMenuPresenter` and assign:

- `Hub` to the `HubMenuCoordinator` in the same UI flow.
- `Ui Commands` to the object with `MultiplayerUiCommands`.
- `Join By Code Presenter` to the code-entry UI presenter.

If any of these are not assigned, the presenter will disable itself instead of searching the scene.

### 2. Hub menu coordinator

Find the GameObject with `HubMenuCoordinator` and assign:

- All relevant `CanvasGroup` fields:
  - `Auth Panel Group`
  - `Hub Menu Panel Group`
  - `Lobby Browser Panel Group`
  - `Lobby Creator Panel Group`
  - `Join By Code Panel Group`
  - `Lobby Waiting Panel Group`
- `Transition Duration` if you want a different fade speed.
- `Coordinator` to the `MultiplayerFlowCoordinator` instance.

The coordinator now assumes the flow coordinator is assigned in the inspector.

### 3. Multiplayer UI commands

Find the GameObject with `MultiplayerUiCommands` and assign:

- `Coordinator` to the `MultiplayerFlowCoordinator` instance.
- `Busy Root` if you want a container to show and hide during network operations.
- Any UI controls that should be disabled while busy in `Disable While Busy`.

Also verify that any UI events calling these methods are still connected:

- `Initialize`
- `SignIn`
- `SignUp`
- `SetName`
- `RefreshLobbies`
- `Host`
- `Join`
- `StartGame`
- `LeaveLobby`

### 4. Multiplayer Unity event bridge

Find the GameObject with `MultiplayerUnityEventBridge` and assign:

- `Coordinator` to the `MultiplayerFlowCoordinator` instance.

Then verify that any listeners in the inspector are still hooked up to the bridge events:

- `StatusChanged`
- `SignedIn`
- `LobbyListUpdated`
- `LobbyJoined`
- `LobbyLeft`
- `RelayReady`
- `NetworkStarted`
- `ReadyToEnterGame`
- `ErrorOccurred`
- `ErrorMessage`

## Game scene installer wiring flow

The intended wiring sequence in the Game scene is:

1. The scene loads.
2. `MultiplayerSceneManager` detects the game-scene installer registration.
3. `GameSceneInstaller` receives the coordinator and binds the gameplay dependencies.
4. `MultiplayerGameSessionController` receives the board and pawn spawner.
5. `PlayerPawnSpawner` receives the board manager and flow coordinator.
6. `MultiplayerGameHudPresenter` binds to the session controller.
7. The pawn prefab is registered on the network manager if needed.

If any of those steps fail, check the inspector assignments first before debugging code.

## Checklist for the first editor pass

Use this as a quick verification pass after the refactor is opened in Unity.

- [ ] `GameSceneInstaller` exists in the Game scene.
- [ ] `GameSceneInstaller.sessionController` is assigned.
- [ ] `GameSceneInstaller.boardManager` is assigned.
- [ ] `GameSceneInstaller.pawnSpawner` is assigned.
- [ ] `GameSceneInstaller.hudPresenter` is assigned.
- [ ] `PlayerPawnSpawner.boardManager` is assigned.
- [ ] `PlayerPawnSpawner.pawnPrefab` is assigned.
- [ ] `PlayerPawnSpawner.pawnVisualConfig` is assigned.
- [ ] `MultiplayerGameHudPresenter` has all required UI references.
- [ ] `HubMenuPresenter` has all three references assigned.
- [ ] `HubMenuCoordinator.coordinator` is assigned.
- [ ] `MultiplayerUiCommands.coordinator` is assigned.
- [ ] `MultiplayerUnityEventBridge.coordinator` is assigned.
- [ ] The pawn prefab still has the correct network and visual components.
- [ ] The `PawnVisualConfig` asset exists and is referenced by the spawner.

## What to test in play mode

After wiring everything, enter Play Mode and verify the following behavior.

### Lobby / hub flow

- Sign-in still completes.
- The hub panels transition correctly.
- Opening browser, create, and join-by-code panels still works.
- The waiting room appears when a lobby is joined.

### Gameplay flow

- The game scene loads without warnings about missing session wiring.
- Pawns spawn on the board.
- Pawn colors and sizes reflect the visual config asset.
- The HUD displays the current turn and phase.
- Rolling the dice advances the pawn.
- Landing on a board space logs the landing message from the rules layer.
- End-turn progression still works for the host.

### Network flow

- The network manager remains initialized through scene transitions.
- The pawn prefab is registered before network spawn is required.
- Host and client entries still follow the same lobby-to-game transition.

## Common mistakes to look for

- Forgetting to assign one of the serialized fields after removing runtime lookup.
- Leaving an old prefab or scene instance with stale component references.
- Creating the pawn visual config asset but not assigning it to the spawner.
- Using a pawn prefab without the correct renderers for color application.
- Forgetting to keep the `MultiplayerFlowCoordinator` in the scene that owns the UI presenters.
- Assuming the Game scene installer will find references automatically. It will not.

## Notes for future cleanup

- The project is now more explicit, so scene objects should be wired through the inspector instead of discovered at runtime.
- If additional presenters or helpers are added later, prefer serialized references or a scene installer over more `Find*` calls.
- If the editor ever stops resolving a new script cleanly, try keeping the type in an already-indexed file or existing namespace structure before adding another layer of indirection.

## Suggested file references

- [GameSceneInstaller.cs](Assets/scripts/SceneManagement/GameSceneInstaller.cs)
- [MultiplayerSceneManager.cs](Assets/scripts/SceneManagement/MultiplayerSceneManager.cs)
- [PlayerPawnSpawner.cs](Assets/scripts/Pawns/PlayerPawnSpawner.cs)
- [PlayerPawn.cs](Assets/scripts/Pawns/PlayerPawn.cs)
- [MultiplayerGameSessionController.cs](Assets/scripts/Multiplayer/Gameplay/MultiplayerGameSessionController.cs)
- [MultiplayerGameHudPresenter.cs](Assets/scripts/Multiplayer/Gameplay/MultiplayerGameHudPresenter.cs)
- [HubMenuPresenter.cs](Assets/scripts/Multiplayer/UI/HubMenuPresenter.cs)
- [HubMenuCoordinator.cs](Assets/scripts/Multiplayer/UI/HubMenuCoordinator.cs)
- [MultiplayerUiCommands.cs](Assets/scripts/Multiplayer/UI/MultiplayerUiCommands.cs)
- [MultiplayerUnityEventBridge.cs](Assets/scripts/Multiplayer/MultiplayerUnityEventBridge.cs)
