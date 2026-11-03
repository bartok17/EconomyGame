using System.Collections.Generic;
using UnityEngine;

namespace MonopolyGame.Stash
{
    /// <summary>
    /// One denomination stack inside a player stash zone. Notes are flat
    /// placeholder rectangles stacked into a pile that grows and shrinks with
    /// the player's balance. The pile-root collider is the future hover target
    /// for the info-card system.
    /// </summary>
    public sealed class MoneyPileView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private const float NoteBaseY = 0.02f;

        private readonly List<GameObject> notes = new List<GameObject>();

        private Transform notesRoot;
        private BoxCollider hitCollider;
        private MaterialPropertyBlock noteBlock;
        private MaterialPropertyBlock padBlock;
        private Material sourceMaterial;
        private Material padMaterial;
        private bool usePlaceholderColor;

        private int pawnSlot;
        private int denomination;
        private int noteCount;
        private int visibleNoteCount;
        private float noteWidth;
        private float noteDepth;
        private float noteThickness;
        private float noteGap;
        private int maxVisibleNotes;

        private float currentHeightScale = 1f;
        private float targetHeight;

        public int PawnSlot => pawnSlot;
        public int Denomination => denomination;
        public int NoteCount => noteCount;
        public int VisibleNoteCount => visibleNoteCount;
        public long TotalValue => (long)denomination * noteCount;

        public void Setup(
            int pawnSlot,
            int denomination,
            Color noteColor,
            PlayerStashVisualConfig config,
            Material surfaceMaterial,
            Material noteMaterialOverride)
        {
            this.pawnSlot = pawnSlot;
            this.denomination = denomination;

            noteWidth = config.banknoteWidth;
            noteDepth = config.banknoteDepth;
            noteThickness = config.banknoteThickness;
            noteGap = config.banknoteGap;
            maxVisibleNotes = Mathf.Max(1, config.maxVisibleNotes);

            padMaterial = surfaceMaterial;
            usePlaceholderColor = noteMaterialOverride == null;
            sourceMaterial = usePlaceholderColor ? surfaceMaterial : noteMaterialOverride;

            notesRoot = new GameObject("Notes").transform;
            notesRoot.SetParent(transform, false);

            noteBlock = new MaterialPropertyBlock();
            padBlock = new MaterialPropertyBlock();
            padBlock.SetColor(BaseColorId, config.padColor);
            padBlock.SetColor(ColorId, config.padColor);

            if (usePlaceholderColor)
            {
                noteBlock.SetColor(BaseColorId, noteColor);
                noteBlock.SetColor(ColorId, noteColor);
            }

            CreatePad(config);

            hitCollider = gameObject.AddComponent<BoxCollider>();
            hitCollider.isTrigger = false;

            SetNoteCount(0);
        }

        public void SetNoteCount(int count)
        {
            noteCount = Mathf.Max(0, count);
            visibleNoteCount = Mathf.Min(noteCount, maxVisibleNotes);

            EnsureNotes(visibleNoteCount);

            float step = noteThickness + noteGap;
            float previousHeight = targetHeight;

            for (int i = 0; i < notes.Count; i++)
            {
                bool visible = i < visibleNoteCount;
                notes[i].SetActive(visible);

                if (!visible)
                {
                    continue;
                }

                float jitterX = (GetJitter(i, 1) - 0.5f) * 0.05f;
                float jitterZ = (GetJitter(i, 2) - 0.5f) * 0.05f;
                float jitterYaw = (GetJitter(i, 3) - 0.5f) * 8f;

                notes[i].transform.localPosition = new Vector3(
                    jitterX,
                    NoteBaseY + noteThickness * 0.5f + i * step,
                    jitterZ);
                notes[i].transform.localRotation = Quaternion.Euler(0f, jitterYaw, 0f);
            }

            targetHeight = visibleNoteCount > 0 ? visibleNoteCount * step : 0f;

            if (targetHeight > 0.001f && previousHeight > 0.001f)
            {
                currentHeightScale = Mathf.Clamp(previousHeight / targetHeight, 0.15f, 3f);
            }
            else
            {
                currentHeightScale = targetHeight > 0.001f ? 0.05f : 1f;
            }

            notesRoot.localScale = new Vector3(1f, currentHeightScale, 1f);

            UpdateCollider();
        }

        private void Update()
        {
            if (notesRoot == null || Mathf.Approximately(currentHeightScale, 1f))
            {
                return;
            }

            currentHeightScale = Mathf.MoveTowards(currentHeightScale, 1f, Time.deltaTime * 3.5f);
            notesRoot.localScale = new Vector3(1f, currentHeightScale, 1f);
        }

        private void CreatePad(PlayerStashVisualConfig config)
        {
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Pad";
            pad.transform.SetParent(transform, false);
            pad.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            pad.transform.localScale = new Vector3(config.pilePadWidth, 0.02f, config.pilePadDepth);

            RemoveCollider(pad);

            Renderer padRenderer = pad.GetComponent<Renderer>();

            if (padRenderer != null)
            {
                ApplyMaterial(padRenderer, padMaterial);
                padRenderer.SetPropertyBlock(padBlock);
            }
        }

        private void EnsureNotes(int count)
        {
            while (notes.Count < count)
            {
                int index = notes.Count;
                GameObject note = GameObject.CreatePrimitive(PrimitiveType.Cube);
                note.name = $"Note_{index:00}";
                note.transform.SetParent(notesRoot, false);
                note.transform.localScale = new Vector3(noteWidth, noteThickness, noteDepth);

                RemoveCollider(note);

                Renderer noteRenderer = note.GetComponent<Renderer>();

                if (noteRenderer != null)
                {
                    ApplyMaterial(noteRenderer, sourceMaterial);

                    if (usePlaceholderColor)
                    {
                        noteRenderer.SetPropertyBlock(noteBlock);
                    }
                }

                notes.Add(note);
            }
        }

        private static void ApplyMaterial(Renderer renderer, Material material)
        {
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private void UpdateCollider()
        {
            if (hitCollider == null)
            {
                return;
            }

            float height = Mathf.Max(NoteBaseY + targetHeight, 0.04f);
            const float paddingX = 0.12f;
            const float paddingZ = 0.01f;

            hitCollider.size = new Vector3(noteWidth + paddingX, height + 0.04f, noteDepth + paddingZ);
            hitCollider.center = new Vector3(0f, height * 0.5f + 0.01f, 0f);
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();

            if (collider == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static float GetJitter(int index, int salt)
        {
            float value = Mathf.Sin((index + 1) * 12.9898f + salt * 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
