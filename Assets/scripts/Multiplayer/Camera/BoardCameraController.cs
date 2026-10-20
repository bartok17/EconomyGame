using System;
using MonopolyGame.Board;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MonopolyGame.Multiplayer.Camera
{
    /// <summary>
    /// Orbit-style board camera with support for multiple modes: free orbit,
    /// locked player views, trade framing, and a public API ready for future modes.
    /// Controls: right-drag = orbit, scroll = zoom, click a tile = focus it,
    /// click the table = return to overview.
    /// Attach this to the Main Camera in the Game scene.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class BoardCameraController : MonoBehaviour
    {
        // ── Inspector configuration ────────────────────────────────────────────

        [Header("Orbit")]
        [Tooltip("Point the camera orbits around (world space).")]
        [SerializeField] private Vector3 pivot = Vector3.zero;

        [Tooltip("Optional transform override — when assigned, pivot follows this transform.")]
        [SerializeField] private Transform pivotTransform;

        [Header("Elevation (degrees above horizontal)")]
        [SerializeField, Range(0f, 90f)] private float minElevation = 10f;
        [SerializeField, Range(0f, 90f)] private float maxElevation = 70f;

        [Header("Zoom (distance from pivot)")]
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 30f;

        [Header("Input sensitivity")]
        [SerializeField] private float orbitSensitivity = 2f;
        [SerializeField] private float zoomSensitivity = 2.5f;

        [Header("Transitions")]
        [Tooltip("How fast the camera blends between modes (higher = snappier).")]
        [SerializeField] private float transitionSpeed = 5f;

        [Header("Player View Presets")]
        [Tooltip("Preset angles for each player side. Index 0 = south, 1 = east, 2 = north, 3 = west.")]
        [SerializeField] private PlayerViewPreset[] playerViewPresets = new PlayerViewPreset[]
        {
            new PlayerViewPreset { azimuth = 0f,   elevation = 45f, distance = 12f },
            new PlayerViewPreset { azimuth = 90f,  elevation = 45f, distance = 12f },
            new PlayerViewPreset { azimuth = 180f, elevation = 45f, distance = 12f },
            new PlayerViewPreset { azimuth = 270f, elevation = 45f, distance = 12f }
        };

        [Header("Trade View")]
        [SerializeField, Range(0f, 90f)] private float tradeViewElevation = 30f;
        [SerializeField] private float tradeViewDistance = 8f;

        [Header("Brim Focus")]
        [Tooltip("Left-click a tile to glide the camera onto it; click the table to return to overview.")]
        [SerializeField] private bool enableClickToFocus = true;

        [SerializeField, Range(0f, 90f)] private float focusElevation = 35f;
        [SerializeField] private float focusDistance = 7f;
        [SerializeField] private float focusHeightOffset = 0.15f;

        // ── Runtime state ──────────────────────────────────────────────────────

        private float currentAzimuth;
        private float currentElevation;
        private float currentDistance;
        private Vector3 currentPivot;

        private float targetAzimuth;
        private float targetElevation;
        private float targetDistance;
        private Vector3 targetPivot;

        // Base mode: explicit user/system state (free orbit, player lock, trade).
        // A focused tile is an override on top of the base mode and never
        // replaces it — clearing the focus restores the base view.
        private CameraMode baseMode = CameraMode.FreeOrbit;
        private int basePlayerIndex = -1;
        private int baseTradePlayerA = -1;
        private int baseTradePlayerB = -1;

        private BoardSpaceView focusedSpace;
        private UnityEngine.Camera cachedCamera;

        // Input tracking
        private Vector2 lastMousePosition;
        private bool isOrbiting;

        // ── Public properties ──────────────────────────────────────────────────

        /// <summary>The base mode (focus overrides do not change this).</summary>
        public CameraMode CurrentMode => baseMode;

        /// <summary>The tile currently focused, or null when no override is active.</summary>
        public BoardSpaceView FocusedSpace => focusedSpace;

        public bool IsFocusOverrideActive => focusedSpace != null;

        /// <summary>
        /// Raised when the user clicks the board: the clicked tile, or null when
        /// the click landed on the table/empty space. Consumed by
        /// <see cref="BoardFocusCoordinator"/>; when nobody listens the camera
        /// handles the click itself.
        /// </summary>
        public event Action<BoardSpaceView> SpaceClicked;

        /// <summary>
        /// The world-space point the camera orbits around.
        /// </summary>
        public Vector3 Pivot
        {
            get => targetPivot;
            set => SetPivot(value);
        }

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            cachedCamera = GetComponent<UnityEngine.Camera>();
            ApplyDefaultOrbitState();
            currentPivot = pivot;
            targetPivot = pivot;
        }

        private void LateUpdate()
        {
            UpdatePivotFromTransform();
            HandleInput();
            UpdateTransition();
            ApplyCameraTransform();
        }

        // ── Public API — mode switching ────────────────────────────────────────

        /// <summary>
        /// Switch to free-orbit mode, keeping the current camera position.
        /// Clears any focus override.
        /// </summary>
        public void SetModeFreeOrbit()
        {
            baseMode = CameraMode.FreeOrbit;
            focusedSpace = null;
            targetAzimuth = currentAzimuth;
            targetElevation = currentElevation;
            targetDistance = currentDistance;
            targetPivot = currentPivot;
        }

        /// <summary>
        /// Lock the camera to a preset view for the given player index
        /// (0 = south, 1 = east, 2 = north, 3 = west).
        /// Clears any focus override.
        /// </summary>
        public void SetModePlayerView(int playerIndex)
        {
            if (playerViewPresets == null || playerIndex < 0 || playerIndex >= playerViewPresets.Length)
            {
                Debug.LogWarning($"[BoardCamera] Invalid player index {playerIndex}. Must be 0–{playerViewPresets?.Length - 1 ?? -1}.");
                return;
            }

            baseMode = CameraMode.PlayerView;
            basePlayerIndex = playerIndex;
            focusedSpace = null;
            ApplyPlayerViewTargets(playerIndex);
        }

        private void ApplyPlayerViewTargets(int playerIndex)
        {
            var preset = playerViewPresets[playerIndex];
            targetAzimuth = preset.azimuth;
            targetElevation = Mathf.Clamp(preset.elevation, minElevation, maxElevation);
            targetDistance = Mathf.Clamp(preset.distance, minDistance, maxDistance);
            targetPivot = pivot;
        }

        /// <summary>
        /// Frame the camera for a trade between two players.
        /// Clears any focus override.
        /// </summary>
        /// <param name="playerA">Index of the first trading player.</param>
        /// <param name="playerB">Index of the second trading player.</param>
        public void SetModeTrade(int playerA, int playerB)
        {
            baseMode = CameraMode.Trade;
            baseTradePlayerA = playerA;
            baseTradePlayerB = playerB;
            focusedSpace = null;
            ApplyTradeTargets(playerA, playerB);
        }

        private void ApplyTradeTargets(int playerA, int playerB)
        {
            // Average azimuth between the two players' sides
            float azimuthA = GetPlayerAzimuth(playerA);
            float azimuthB = GetPlayerAzimuth(playerB);

            // Handle wrap-around for averaging (e.g. 350° and 10° should average to 0°)
            float avgAzimuth = AverageAngles(azimuthA, azimuthB);

            targetAzimuth = avgAzimuth;
            targetElevation = Mathf.Clamp(tradeViewElevation, minElevation, maxElevation);
            targetDistance = Mathf.Clamp(tradeViewDistance, minDistance, maxDistance);
            targetPivot = pivot;
        }

        /// <summary>
        /// Move the orbit pivot to a new world position (animates smoothly).
        /// </summary>
        public void SetPivot(Vector3 newPivot)
        {
            targetPivot = newPivot;
        }

        /// <summary>
        /// Instantly jump to the given spherical coordinates (no transition).
        /// Useful for initialisation or teleport-style cuts.
        /// </summary>
        public void SnapToPreset(float azimuth, float elevation, float distance)
        {
            targetAzimuth = azimuth;
            targetElevation = Mathf.Clamp(elevation, minElevation, maxElevation);
            targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            currentAzimuth = targetAzimuth;
            currentElevation = targetElevation;
            currentDistance = targetDistance;
            ApplyCameraTransform();
        }

        /// <summary>
        /// Glide the orbit pivot onto a board tile (the brim) and frame it
        /// from outside the board. Free orbit and zoom keep working around the tile.
        /// This is an override: it does not change the base mode, and
        /// <see cref="ClearFocus"/> restores whatever the base mode was.
        /// Tile lookup lives in <see cref="BoardFocusCoordinator"/>.
        /// </summary>
        /// <param name="space">The tile to focus (already resolved).</param>
        /// <param name="faceFromOutside">When true, the camera swings to the outer edge looking in.</param>
        public void FocusSpace(BoardSpaceView space, bool faceFromOutside = true)
        {
            if (space == null)
            {
                return;
            }

            focusedSpace = space;
            targetPivot = space.transform.position + Vector3.up * focusHeightOffset;

            if (faceFromOutside)
            {
                Vector3 outward = space.transform.position - pivot;
                outward.y = 0f;

                if (outward.sqrMagnitude > 0.0001f)
                {
                    targetAzimuth = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
                }

                targetElevation = Mathf.Clamp(focusElevation, minElevation, maxElevation);
                targetDistance = Mathf.Clamp(focusDistance, minDistance, maxDistance);
            }
        }

        /// <summary>
        /// Drop the focus override and restore the base view: the active player
        /// lock or trade framing when one is set, otherwise the overview pivot.
        /// </summary>
        public void ClearFocus()
        {
            focusedSpace = null;

            if (baseMode == CameraMode.PlayerView && basePlayerIndex >= 0)
            {
                ApplyPlayerViewTargets(basePlayerIndex);
                return;
            }

            if (baseMode == CameraMode.Trade)
            {
                ApplyTradeTargets(baseTradePlayerA, baseTradePlayerB);
                return;
            }

            targetPivot = pivot;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void ApplyDefaultOrbitState()
        {
            // Default: 45° elevation, looking from south (azimuth 0°), medium distance
            float defaultAzimuth = 0f;
            float defaultElevation = 45f;
            float defaultDistance = 18f;

            currentAzimuth = defaultAzimuth;
            currentElevation = Mathf.Clamp(defaultElevation, minElevation, maxElevation);
            currentDistance = Mathf.Clamp(defaultDistance, minDistance, maxDistance);
            targetAzimuth = currentAzimuth;
            targetElevation = currentElevation;
            targetDistance = currentDistance;

            baseMode = CameraMode.FreeOrbit;
            basePlayerIndex = -1;
            baseTradePlayerA = -1;
            baseTradePlayerB = -1;
            focusedSpace = null;
        }

        private void UpdatePivotFromTransform()
        {
            if (pivotTransform != null && focusedSpace == null)
            {
                targetPivot = pivotTransform.position;
            }
        }

        private void HandleInput()
        {
            // User input is allowed in free orbit, and around a focused tile
            // even when a lock (player view / trade) is the base mode beneath it.
            bool canUserOrbit = baseMode == CameraMode.FreeOrbit || focusedSpace != null;

            if (!canUserOrbit)
            {
                isOrbiting = false;
                return;
            }

            // ── Right-click drag orbit ─────────────────────────────────────
            bool orbitHeld = GetOrbitButtonHeld();

            if (orbitHeld && !isOrbiting)
            {
                isOrbiting = true;
                lastMousePosition = GetPointerPosition();
            }
            else if (!orbitHeld)
            {
                isOrbiting = false;
            }

            if (isOrbiting)
            {
                Vector2 pointer = GetPointerPosition();
                Vector2 delta = pointer - lastMousePosition;
                lastMousePosition = pointer;

                targetAzimuth -= delta.x * orbitSensitivity * 0.1f;
                targetElevation += delta.y * orbitSensitivity * 0.1f;
                targetElevation = Mathf.Clamp(targetElevation, minElevation, maxElevation);
            }

            // ── Scroll-wheel zoom ──────────────────────────────────────────
            float scroll = GetScrollDelta();
            if (!Mathf.Approximately(scroll, 0f))
            {
                targetDistance -= scroll * zoomSensitivity;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            }

            // ── Click a tile to focus it, click the table for overview ─────
            HandleClickToFocus();
        }

        private void HandleClickToFocus()
        {
            if (!enableClickToFocus || !GetLeftClickPressedThisFrame())
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            UnityEngine.Camera cam = cachedCamera != null ? cachedCamera : UnityEngine.Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector2 pointer = GetPointerPosition();
            Ray ray = cam.ScreenPointToRay(new Vector3(pointer.x, pointer.y, 0f));

            BoardSpaceView space = null;
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                space = hit.collider.GetComponentInParent<BoardSpaceView>();
            }

            if (SpaceClicked != null)
            {
                // Routed through BoardFocusCoordinator (single focus pipeline).
                SpaceClicked.Invoke(space);
            }
            else if (space != null)
            {
                FocusSpace(space);
            }
            else
            {
                ClearFocus();
            }
        }

        // ── Input abstraction (new Input System with legacy fallback) ─────────

        private static bool GetOrbitButtonHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        private static bool GetLeftClickPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        private static Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private static float GetScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null)
            {
                return 0f;
            }

            // Normalize the raw ~120-per-notch scroll value to roughly ±1.
            return Mouse.current.scroll.ReadValue().y / 120f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        private void UpdateTransition()
        {
            float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

            currentAzimuth = Mathf.LerpAngle(currentAzimuth, targetAzimuth, t);
            currentElevation = Mathf.Lerp(currentElevation, targetElevation, t);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, t);
            currentPivot = Vector3.Lerp(currentPivot, targetPivot, t);
        }

        private void ApplyCameraTransform()
        {
            Vector3 offset = SphericalToCartesian(currentAzimuth, currentElevation, currentDistance);
            transform.position = currentPivot + offset;
            transform.LookAt(currentPivot);
        }

        private static Vector3 SphericalToCartesian(float azimuthDeg, float elevationDeg, float radius)
        {
            float azimuthRad = azimuthDeg * Mathf.Deg2Rad;
            float elevationRad = elevationDeg * Mathf.Deg2Rad;

            float cosElevation = Mathf.Cos(elevationRad);
            float sinElevation = Mathf.Sin(elevationRad);

            float x = radius * cosElevation * Mathf.Sin(azimuthRad);
            float y = radius * sinElevation;
            float z = radius * cosElevation * Mathf.Cos(azimuthRad);

            return new Vector3(x, y, z);
        }

        private float GetPlayerAzimuth(int playerIndex)
        {
            if (playerViewPresets != null && playerIndex >= 0 && playerIndex < playerViewPresets.Length)
            {
                return playerViewPresets[playerIndex].azimuth;
            }

            // Fallback: 90° increments starting from south
            return playerIndex * 90f;
        }

        private static float AverageAngles(float a, float b)
        {
            // Convert to vectors on the unit circle and average
            float aRad = a * Mathf.Deg2Rad;
            float bRad = b * Mathf.Deg2Rad;

            float avgX = (Mathf.Cos(aRad) + Mathf.Cos(bRad)) * 0.5f;
            float avgY = (Mathf.Sin(aRad) + Mathf.Sin(bRad)) * 0.5f;

            return Mathf.Atan2(avgY, avgX) * Mathf.Rad2Deg;
        }

        // ── Context-menu shortcuts for editor testing ──────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Mode / Free Orbit")]
        private void EditorSetFreeOrbit() => SetModeFreeOrbit();

        [ContextMenu("Mode / Player 0 (South)")]
        private void EditorSetPlayer0() => SetModePlayerView(0);

        [ContextMenu("Mode / Player 1 (East)")]
        private void EditorSetPlayer1() => SetModePlayerView(1);

        [ContextMenu("Mode / Player 2 (North)")]
        private void EditorSetPlayer2() => SetModePlayerView(2);

        [ContextMenu("Mode / Player 3 (West)")]
        private void EditorSetPlayer3() => SetModePlayerView(3);

        [ContextMenu("Mode / Trade (P0 ↔ P2)")]
        private void EditorSetTrade() => SetModeTrade(0, 2);

        [ContextMenu("Focus / Space 0 (Start)")]
        private void EditorFocusSpace0() => FocusSpace(FindSpaceInEditor(0));

        [ContextMenu("Focus / Space 13 (opposite side)")]
        private void EditorFocusSpace13() => FocusSpace(FindSpaceInEditor(13));

        [ContextMenu("Focus / Clear (restore base view)")]
        private void EditorClearFocus() => ClearFocus();

        private static BoardSpaceView FindSpaceInEditor(int index)
        {
            BoardManager manager = FindAnyObjectByType<BoardManager>();
            if (manager == null)
            {
                return null;
            }

            try
            {
                return manager.GetSpace(index);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
#endif
    }

    /// <summary>
    /// Preset spherical coordinates for a locked camera view.
    /// </summary>
    [Serializable]
    public struct PlayerViewPreset
    {
        [Tooltip("Horizontal angle in degrees (0 = south/+Z, 90 = east/+X, etc.).")]
        public float azimuth;

        [Tooltip("Angle above the horizontal plane in degrees.")]
        public float elevation;

        [Tooltip("Distance from the orbit pivot.")]
        public float distance;
    }
}
