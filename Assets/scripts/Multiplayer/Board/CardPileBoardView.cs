using System.Collections;
using MonopolyGame.Multiplayer.Gameplay;
using TMPro;
using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>
    /// Replaces a deck placeholder on the table with two physical card piles and
    /// live count labels. Concrete views choose the anchor and expose the counts,
    /// so every card deck is presented the same way.
    /// </summary>
    public abstract class CardPileBoardView : MonoBehaviour
    {
        [Header("Board Anchor")]
        [SerializeField] protected Transform deckAnchor;
        [SerializeField] protected BoardGenerator boardGenerator;

        [Header("Pile Styling")]
        [SerializeField] protected float cardWidth = 0.85f;
        [SerializeField] protected float cardDepth = 1.0f;
        [SerializeField] protected float pileSpacing = 1.1f;
        [SerializeField] protected float tableY = 0.05f;
        [SerializeField] protected Color drawPileColor = new Color(0.65f, 0.06f, 0.10f);
        [SerializeField] protected Color discardPileColor = new Color(0.88f, 0.84f, 0.70f);

        private BoardCardPileView drawPile;
        private BoardCardPileView discardPile;
        private Transform pilesRoot;
        private Coroutine bindRoutine;

        protected MultiplayerGameSessionController Session { get; private set; }

        /// <summary>Hierarchy path (relative to the Board object) of the deck placeholder.</summary>
        protected abstract string DeckAnchorPath { get; }

        /// <summary>Caption shown above each pile count, e.g. "ACTION" or "EVENT".</summary>
        protected abstract string PileCaptionPrefix { get; }

        protected abstract int DrawCardCount { get; }
        protected abstract int DiscardCardCount { get; }

        protected abstract void BindSessionEvents(MultiplayerGameSessionController session);
        protected abstract void UnbindSessionEvents(MultiplayerGameSessionController session);

        protected virtual void Awake()
        {
            if (boardGenerator == null)
            {
                boardGenerator = GetComponent<BoardGenerator>();
            }

            if (deckAnchor == null)
            {
                deckAnchor = transform.Find(DeckAnchorPath);
            }
        }

        protected virtual void Start()
        {
            BuildPiles();
            bindRoutine = StartCoroutine(BindWhenReady());
        }

        protected virtual void OnDestroy()
        {
            if (Session != null)
            {
                UnbindSessionEvents(Session);
            }

            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
            }

            if (pilesRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(pilesRoot.gameObject);
                }
                else
                {
                    DestroyImmediate(pilesRoot.gameObject);
                }
            }
        }

        protected void Refresh()
        {
            drawPile?.SetCount(Session != null ? DrawCardCount : 0);
            discardPile?.SetCount(Session != null ? DiscardCardCount : 0);
        }

        private void BuildPiles()
        {
            if (deckAnchor == null)
            {
                Debug.LogWarning($"[{GetType().Name}] {DeckAnchorPath} was not found; card piles will not be shown.");
                return;
            }

            Renderer anchorRenderer = deckAnchor.GetComponent<Renderer>();
            Material sourceMaterial = anchorRenderer != null
                ? anchorRenderer.sharedMaterial
                : ResolveSurfaceMaterial();

            if (anchorRenderer != null)
            {
                anchorRenderer.enabled = false;
            }

            Collider anchorCollider = deckAnchor.GetComponent<Collider>();

            if (anchorCollider != null)
            {
                anchorCollider.enabled = false;
            }

            var pilesObject = new GameObject($"{PileCaptionPrefix}CardPiles");
            pilesRoot = pilesObject.transform;
            pilesRoot.SetParent(deckAnchor.parent, false);
            pilesRoot.position = new Vector3(
                deckAnchor.position.x,
                tableY,
                deckAnchor.position.z);
            pilesRoot.rotation = deckAnchor.rotation;

            TMP_FontAsset font = ResolveFont();
            drawPile = new BoardCardPileView(
                pilesRoot,
                new Vector3(-pileSpacing * 0.5f, 0f, 0f),
                $"{PileCaptionPrefix} DRAW",
                -1f,
                cardWidth,
                cardDepth,
                font,
                sourceMaterial,
                drawPileColor);
            discardPile = new BoardCardPileView(
                pilesRoot,
                new Vector3(pileSpacing * 0.5f, 0f, 0f),
                $"{PileCaptionPrefix} DISCARD",
                1f,
                cardWidth,
                cardDepth,
                font,
                sourceMaterial,
                discardPileColor);
        }

        private IEnumerator BindWhenReady()
        {
            while (Session == null)
            {
                Session = FindAnyObjectByType<MultiplayerGameSessionController>();
                yield return null;
            }

            BindSessionEvents(Session);
            bindRoutine = null;
            Refresh();
        }

        private TMP_FontAsset ResolveFont()
        {
            BoardTileTemplate template = boardGenerator != null && boardGenerator.Definition != null
                ? boardGenerator.Definition.Template
                : null;

            return template != null && template.Font != null
                ? template.Font
                : TMP_Settings.defaultFontAsset;
        }

        private Material ResolveSurfaceMaterial()
        {
            BoardTileTemplate template = boardGenerator != null && boardGenerator.Definition != null
                ? boardGenerator.Definition.Template
                : null;

            return template != null ? template.BaseMaterial : null;
        }
    }
}
