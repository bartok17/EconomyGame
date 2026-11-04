using System.Collections;
using MonopolyGame.Multiplayer.Gameplay;
using TMPro;
using UnityEngine;

namespace MonopolyGame.Board
{
    /// <summary>
    /// Replaces the red action-deck placeholder on the table with two physical
    /// card piles and live count labels. The blue event deck is untouched.
    /// </summary>
    public sealed class ActionCardBoardView : MonoBehaviour
    {
        [Header("Board Anchor")]
        [SerializeField] private Transform actionDeckAnchor;
        [SerializeField] private BoardGenerator boardGenerator;

        [Header("Pile Styling")]
        [SerializeField] private float cardWidth = 0.85f;
        [SerializeField] private float cardDepth = 1.0f;
        [SerializeField] private float pileSpacing = 1.1f;
        [SerializeField] private float tableY = 0.05f;
        [SerializeField] private Color drawPileColor = new Color(0.65f, 0.06f, 0.10f);
        [SerializeField] private Color discardPileColor = new Color(0.88f, 0.84f, 0.70f);

        private BoardCardPileView drawPile;
        private BoardCardPileView discardPile;
        private Transform pilesRoot;
        private MultiplayerGameSessionController session;
        private Coroutine bindRoutine;

        private void Awake()
        {
            if (boardGenerator == null)
            {
                boardGenerator = GetComponent<BoardGenerator>();
            }

            if (actionDeckAnchor == null)
            {
                actionDeckAnchor = transform.Find("Decor/ActionDeck");
            }
        }

        private void Start()
        {
            BuildPiles();
            bindRoutine = StartCoroutine(BindWhenReady());
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.ActionCardsChanged -= Refresh;
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

        private void BuildPiles()
        {
            if (actionDeckAnchor == null)
            {
                Debug.LogWarning("[ActionCardBoardView] Decor/ActionDeck was not found; card piles will not be shown.");
                return;
            }

            Renderer anchorRenderer = actionDeckAnchor.GetComponent<Renderer>();
            Material sourceMaterial = anchorRenderer != null
                ? anchorRenderer.sharedMaterial
                : ResolveSurfaceMaterial();

            if (anchorRenderer != null)
            {
                anchorRenderer.enabled = false;
            }

            Collider anchorCollider = actionDeckAnchor.GetComponent<Collider>();

            if (anchorCollider != null)
            {
                anchorCollider.enabled = false;
            }

            var pilesObject = new GameObject("ActionCardPiles");
            pilesRoot = pilesObject.transform;
            pilesRoot.SetParent(actionDeckAnchor.parent, false);
            pilesRoot.position = new Vector3(
                actionDeckAnchor.position.x,
                tableY,
                actionDeckAnchor.position.z);
            pilesRoot.rotation = actionDeckAnchor.rotation;

            TMP_FontAsset font = ResolveFont();
            drawPile = new BoardCardPileView(
                pilesRoot,
                new Vector3(-pileSpacing * 0.5f, 0f, 0f),
                "DRAW",
                -1f,
                cardWidth,
                cardDepth,
                font,
                sourceMaterial,
                drawPileColor);
            discardPile = new BoardCardPileView(
                pilesRoot,
                new Vector3(pileSpacing * 0.5f, 0f, 0f),
                "DISCARD",
                1f,
                cardWidth,
                cardDepth,
                font,
                sourceMaterial,
                discardPileColor);
        }

        private IEnumerator BindWhenReady()
        {
            while (session == null)
            {
                session = FindAnyObjectByType<MultiplayerGameSessionController>();
                yield return null;
            }

            session.ActionCardsChanged += Refresh;
            bindRoutine = null;
            Refresh();
        }

        private void Refresh()
        {
            drawPile?.SetCount(session != null ? session.ActionCardDrawPileCount : 0);
            discardPile?.SetCount(session != null ? session.ActionCardDiscardPileCount : 0);
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
