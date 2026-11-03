using System.Collections;
using System.Collections.Generic;
using MonopolyGame.Board;
using MonopolyGame.Multiplayer.Gameplay;
using MonopolyGame.Pawns;
using TMPro;
using UnityEngine;

namespace MonopolyGame.Stash
{
    /// <summary>
    /// Builds the four per-player stash zones around the board and keeps their
    /// money piles in sync with the authoritative economy state. The zones are
    /// public table information; the numeric HUD remains the exact source.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStashZoneSystem : MonoBehaviour
    {
        private static readonly Color[] DefaultPlayerColors =
        {
            new(0.10f, 0.75f, 0.25f),
            new(0.85f, 0.12f, 0.12f),
            new(0.12f, 0.35f, 0.90f),
            new(0.95f, 0.82f, 0.15f)
        };

        [Header("Dependencies")]
        [SerializeField] private BoardGenerator boardGenerator;
        [SerializeField] private MultiplayerGameSessionController session;

        [Header("Visuals")]
        [SerializeField] private PlayerStashVisualConfig visuals = new PlayerStashVisualConfig();

        [Header("Offline Preview (dev only)")]
        [Tooltip("Shows sample piles when the Game scene is played without a networked session.")]
        [SerializeField] private bool previewWhenNoSession;
        [SerializeField] private string[] previewNames = { "Player 1", "Player 2", "Player 3", "Player 4" };
        [SerializeField] private int[] previewBalances = { 1500, 1480, 940, 2500 };

        private readonly PlayerStashZoneView[] zones = new PlayerStashZoneView[PlayerStashLayout.PlayerCount];

        private Transform zonesRoot;
        private Material fallbackMaterial;
        private Coroutine bindRoutine;

        public int ZoneCount => zones.Length;

        public bool TryGetZone(int pawnSlot, out PlayerStashZoneView zone)
        {
            if (pawnSlot >= 0 && pawnSlot < zones.Length && zones[pawnSlot] != null)
            {
                zone = zones[pawnSlot];
                return true;
            }

            zone = null;
            return false;
        }

        public bool TryGetZoneCenter(int pawnSlot, out Vector3 center)
        {
            if (TryGetZone(pawnSlot, out PlayerStashZoneView zone))
            {
                center = zone.ZoneCenter;
                return true;
            }

            center = Vector3.zero;
            return false;
        }

        private void Awake()
        {
            if (boardGenerator == null)
            {
                boardGenerator = GetComponent<BoardGenerator>();
            }
        }

        private void Start()
        {
            BuildZones();
            bindRoutine = StartCoroutine(BindWhenReady());
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.EconomyChanged -= Refresh;
                session.TurnChanged -= HandleTurnChanged;
            }

            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
            }

            if (zonesRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(zonesRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(zonesRoot.gameObject);
                }
            }

            if (fallbackMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(fallbackMaterial);
                }
                else
                {
                    DestroyImmediate(fallbackMaterial);
                }
            }
        }

        [ContextMenu("Rebuild Stash Zones")]
        private void RebuildZones()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[{nameof(PlayerStashZoneSystem)}] Rebuild is only available in play mode.");
                return;
            }

            if (zonesRoot != null)
            {
                Destroy(zonesRoot.gameObject);
            }

            BuildZones();
            Refresh();
        }

        [ContextMenu("Validate Stash Layout")]
        private void ValidateLayoutFromMenu()
        {
            ValidateLayout();
        }

        public bool ValidateLayout()
        {
            BoardTileTemplate template = GetTemplate();
            PlayerStashVisualConfig config = visuals ?? new PlayerStashVisualConfig();
            PlayerStashMetrics metrics = PlayerStashLayout.FromTemplate(
                template,
                config.zoneGap,
                config.zoneDepth,
                config.zoneWidthMargin);

            float spacing = metrics.GetPileSpacing(
                config.pileColumnSpacing,
                config.pileColumnMargin,
                MoneyNotes.Count);

            bool valid = true;

            if (metrics.ZoneWidth <= 4f || metrics.ZoneDepth <= 0f)
            {
                Debug.LogError($"[{nameof(PlayerStashZoneSystem)}] Invalid zone size {metrics.ZoneWidth} x {metrics.ZoneDepth}.");
                valid = false;
            }

            if (spacing <= config.banknoteDepth + 0.02f)
            {
                Debug.LogError($"[{nameof(PlayerStashZoneSystem)}] Pile spacing {spacing:0.00} does not fit note depth {config.banknoteDepth:0.00}.");
                valid = false;
            }

            float columnHalfDepth = (MoneyNotes.Count - 1) * 0.5f * spacing + config.banknoteDepth * 0.5f;
            float allowedHalfDepth = metrics.ZoneDepth * 0.5f - config.pileColumnMargin;

            if (columnHalfDepth > allowedHalfDepth + 0.001f)
            {
                Debug.LogError($"[{nameof(PlayerStashZoneSystem)}] Pile column {columnHalfDepth:0.00} exceeds zone half-depth {allowedHalfDepth:0.00}.");
                valid = false;
            }

            float columnRightEdge =
                metrics.GetPileColumnX(config.banknoteWidth, config.pileColumnRightMargin) +
                config.banknoteWidth * 0.5f;

            if (columnRightEdge > metrics.ZoneWidth * 0.5f)
            {
                Debug.LogError($"[{nameof(PlayerStashZoneSystem)}] Pile column right edge {columnRightEdge:0.00} exceeds the zone.");
                valid = false;
            }

            if (valid)
            {
                Debug.Log(
                    $"[{nameof(PlayerStashZoneSystem)}] Layout valid: zones {metrics.ZoneWidth:0.00} x {metrics.ZoneDepth:0.00} " +
                    $"at {metrics.ZoneCenterDistance:0.00} from centre, pile column spacing {spacing:0.00}.");
            }

            return valid;
        }

        private IEnumerator BindWhenReady()
        {
            float elapsed = 0f;
            float nextSearch = 0f;

            while (session == null)
            {
                elapsed += Time.deltaTime;

                if (elapsed >= nextSearch)
                {
                    nextSearch = elapsed + 0.25f;
                    session = FindAnyObjectByType<MultiplayerGameSessionController>();

                    if (session != null)
                    {
                        break;
                    }

                    if (elapsed >= 1f && previewWhenNoSession)
                    {
                        ApplyPreview();
                        yield break;
                    }

                    if (elapsed >= 30f)
                    {
                        yield break;
                    }
                }

                yield return null;
            }

            session.EconomyChanged += Refresh;
            session.TurnChanged += HandleTurnChanged;

            float wait = 0f;
            float nextCheck = 0f;

            while (true)
            {
                wait += Time.deltaTime;

                if (wait >= nextCheck)
                {
                    nextCheck = wait + 0.25f;

                    if (session.GetSpawnedPawns().Count > 0 || session.GetPlayerEconomyStates().Count > 0)
                    {
                        break;
                    }

                    if (wait >= 1f && previewWhenNoSession && !session.IsInitialized)
                    {
                        ApplyPreview();
                        yield break;
                    }

                    if (wait >= 10f)
                    {
                        break;
                    }
                }

                yield return null;
            }

            Refresh();
        }

        private void HandleTurnChanged(int turnIndex, string activePlayerName)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (session == null)
            {
                return;
            }

            BoardTileTemplate template = GetTemplate();
            IReadOnlyList<PlayerEconomyState> states = session.GetPlayerEconomyStates();
            IReadOnlyList<PlayerPawn> pawns = session.GetSpawnedPawns();

            for (int slot = 0; slot < zones.Length; slot++)
            {
                PlayerStashZoneView zone = zones[slot];

                if (zone == null)
                {
                    continue;
                }

                string pawnName = FindPawnName(pawns, slot);
                bool hasState = TryFindState(states, slot, out PlayerEconomyState state);
                string stateName = hasState ? state.DisplayName.ToString() : string.Empty;
                string displayName = string.IsNullOrEmpty(stateName) ? pawnName : stateName;
                int balance = hasState ? state.Balance : 0;
                bool occupied = hasState || !string.IsNullOrEmpty(pawnName);

                zone.Apply(balance, displayName, GetPlayerColor(template, slot), occupied);
            }
        }

        private void ApplyPreview()
        {
            BoardTileTemplate template = GetTemplate();

            for (int slot = 0; slot < zones.Length; slot++)
            {
                if (zones[slot] == null)
                {
                    continue;
                }

                string displayName = previewNames != null && slot < previewNames.Length
                    ? previewNames[slot]
                    : $"Player {slot + 1}";
                int balance = previewBalances != null && slot < previewBalances.Length
                    ? previewBalances[slot]
                    : 0;

                zones[slot].Apply(balance, displayName, GetPlayerColor(template, slot), true);
            }

            Debug.Log($"[{nameof(PlayerStashZoneSystem)}] Applied offline preview balances.");
        }

        private void BuildZones()
        {
            BoardTileTemplate template = GetTemplate();
            PlayerStashVisualConfig config = visuals ?? new PlayerStashVisualConfig();
            PlayerStashMetrics metrics = PlayerStashLayout.FromTemplate(
                template,
                config.zoneGap,
                config.zoneDepth,
                config.zoneWidthMargin);
            Material surfaceMaterial = ResolveSurfaceMaterial(template);
            TMP_FontAsset font = ResolveFont(template);

            var rootObject = new GameObject("PlayerStashes");
            rootObject.transform.SetParent(transform, false);
            zonesRoot = rootObject.transform;

            for (int slot = 0; slot < zones.Length; slot++)
            {
                var zoneObject = new GameObject($"PlayerStash_{slot + 1}");
                zoneObject.transform.SetParent(zonesRoot, false);
                zoneObject.transform.localPosition = metrics.GetZoneCenter(slot, config.zoneY);

                PlayerStashZoneView zone = zoneObject.AddComponent<PlayerStashZoneView>();
                zone.Setup(slot, metrics, config, font, surfaceMaterial);
                zones[slot] = zone;
            }

            Debug.Log($"[{nameof(PlayerStashZoneSystem)}] Built {zones.Length} player stash zones.");
        }

        private BoardTileTemplate GetTemplate()
        {
            if (boardGenerator == null)
            {
                boardGenerator = GetComponent<BoardGenerator>();
            }

            return boardGenerator != null && boardGenerator.Definition != null
                ? boardGenerator.Definition.Template
                : null;
        }

        private Material ResolveSurfaceMaterial(BoardTileTemplate template)
        {
            if (template != null && template.BaseMaterial != null)
            {
                return template.BaseMaterial;
            }

            if (fallbackMaterial != null)
            {
                return fallbackMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogWarning($"[{nameof(PlayerStashZoneSystem)}] No compatible surface shader found.");
                return null;
            }

            fallbackMaterial = new Material(shader)
            {
                name = "PlayerStashFallback"
            };
            fallbackMaterial.SetColor("_BaseColor", Color.white);
            fallbackMaterial.SetColor("_Color", Color.white);

            return fallbackMaterial;
        }

        private static TMP_FontAsset ResolveFont(BoardTileTemplate template)
        {
            if (template != null && template.Font != null)
            {
                return template.Font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static string FindPawnName(IReadOnlyList<PlayerPawn> pawns, int slot)
        {
            if (pawns == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] != null && pawns[i].PawnSlot == slot)
                {
                    return pawns[i].DisplayName;
                }
            }

            return string.Empty;
        }

        private static bool TryFindState(
            IReadOnlyList<PlayerEconomyState> states,
            int slot,
            out PlayerEconomyState state)
        {
            if (states != null)
            {
                for (int i = 0; i < states.Count; i++)
                {
                    if (states[i].PawnSlot == slot)
                    {
                        state = states[i];
                        return true;
                    }
                }
            }

            state = default;
            return false;
        }

        private static Color GetPlayerColor(BoardTileTemplate template, int slot)
        {
            int index = Mathf.Abs(slot) % DefaultPlayerColors.Length;

            if (template != null)
            {
                return template.GetOwnerColor(slot);
            }

            return DefaultPlayerColors[index];
        }
    }
}
