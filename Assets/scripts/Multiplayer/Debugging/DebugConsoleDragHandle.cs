using UnityEngine;
using UnityEngine.EventSystems;

namespace MonopolyGame.Multiplayer.Debugging
{
    public sealed class DebugConsoleDragHandle : MonoBehaviour, IDragHandler
    {
        private RectTransform target;
        private RectTransform canvasRect;

        public void Configure(RectTransform window, RectTransform canvas)
        {
            target = window;
            canvasRect = canvas;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null)
            {
                return;
            }

            float scale = canvasRect != null ? canvasRect.localScale.x : 1f;

            if (scale <= 0f)
            {
                scale = 1f;
            }

            target.anchoredPosition += eventData.delta / scale;
        }
    }
}
